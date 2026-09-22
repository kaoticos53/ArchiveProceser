using Avalonia;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;

namespace FileFlow.App.Services;

/// <summary>
/// Serializador bidireccional entre la representación gráfica en memoria (EditorViewModel) y el modelo desacoplado DAG (WorkflowGraph).
/// </summary>
public static class WorkflowGraphSerializer
{
    public static WorkflowGraph Export(
        IEnumerable<NodeViewModel> nodes,
        IEnumerable<ConnectionViewModel> connections,
        string globalOutputDir,
        string name = "FileFlow Workflow",
        IEnumerable<AnnotationViewModel>? annotations = null,
        IEnumerable<GroupViewModel>? groups = null)
    {
        var graph = new WorkflowGraph
        {
            Name = name,
            GlobalOutputDir = globalOutputDir
        };

        if (annotations != null)
        {
            foreach (var a in annotations)
            {
                graph.Annotations.Add(new WorkflowAnnotation
                {
                    Id = a.Id,
                    Title = a.Title,
                    Content = a.Content,
                    X = a.Location.X,
                    Y = a.Location.Y,
                    Width = a.Width,
                    Height = a.Height,
                    Color = a.Color
                });
            }
        }

        if (groups != null)
        {
            foreach (var g in groups)
            {
                graph.Groups.Add(new WorkflowGroup
                {
                    Id = g.Id,
                    Title = g.Title,
                    X = g.Location.X,
                    Y = g.Location.Y,
                    Width = g.Width,
                    Height = g.Height,
                    Color = g.Color,
                    NodeIds = [.. g.NodeIds]
                });
            }
        }

        foreach (var n in nodes)
        {
            var nodeDto = new WorkflowNode
            {
                Id = n.Id,
                NodeTypeName = n.NodeTypeName,
                CustomTitle = n.CustomTitle,
                X = n.Location.X,
                Y = n.Location.Y,
                HasBreakpoint = n.HasBreakpoint,
                IsLoggingEnabled = n.IsLoggingEnabled,
                Parameters = EffectiveParameters(n)
            };
            graph.Nodes.Add(nodeDto);

            if (n.HasBreakpoint)
            {
                graph.BreakpointNodeIds.Add(n.Id);
            }

            if (!n.IsLoggingEnabled)
            {
                graph.DisabledLoggingNodeIds.Add(n.Id);
            }
        }

        foreach (var c in connections)
        {
            var edgeDto = new WorkflowEdge
            {
                SourceNodeId = c.Source.NodeOwner.Id,
                SourcePortName = c.Source.Name,
                TargetNodeId = c.Target.NodeOwner.Id,
                TargetPortName = c.Target.Name
            };
            graph.Edges.Add(edgeDto);
        }

        return graph;
    }

    /// <summary>
    /// Parámetros con los que se guarda un nodo. No son sólo las filas que muestra el inspector: el nodo
    /// guarda además **estado de diseño que no es un parámetro de usuario** —los casos de un switch, la
    /// definición incrustada de un subflujo, los puertos que expone un contenedor— y ese estado tiene que
    /// viajar en el archivo o el flujo reabierto pierde lo que el usuario configuró. La instancia es la
    /// autoridad para el motor, así que sus valores van al final; es el mismo criterio y el mismo orden que
    /// usa el portapapeles al copiar un nodo.
    /// </summary>
    private static Dictionary<string, object?> EffectiveParameters(NodeViewModel node)
    {
        var parameters = node.Parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Key))
            .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);

        lock (node.NodeInstance.Parameters)
        {
            foreach (var (key, value) in node.NodeInstance.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(key) && value != null)
                {
                    parameters[key] = value;
                }
            }
        }

        return parameters;
    }

    /// <summary>
    /// Reconstruye el grafo en el editor.
    ///
    /// El orden importa: primero se vuelcan los parámetros de cada nodo, después se materializan sus
    /// puertos dinámicos y sólo entonces se emparejan las aristas contra ellos. Las conexiones se
    /// reconstruyen por nombre de puerto, así que un nodo cuyos puertos todavía no existen —el script con
    /// puertos declarados, el contenedor de subflujo con su frontera— <b>no coincide con ninguna arista</b>
    /// y el cable desaparece al reabrir sin que nada avise.
    ///
    /// Las aristas se leen <b>ya migradas</b> cuando el archivo es anterior al formato actual: hay huecos
    /// —los puertos de un contenedor, por ejemplo— que ese archivo no guardaba y que se rellenan con lo que
    /// él sí conserva, antes de que la materialización los use.
    ///
    /// Devuelve lo que <b>no</b> se pudo reconstruir, que es poco para el flujo pero mucho para el usuario:
    /// un cable descartado en silencio convierte un flujo reabierto en uno incompleto que parece completo.
    /// </summary>
    public static ConnectionRebuildReport Import(
        WorkflowGraph graph,
        PluginLoader pluginLoader,
        EditorViewModel editor,
        Action<NodeViewModel> registerNodeCallback,
        Action<ConnectionViewModel> registerConnectionCallback,
        Action<AnnotationViewModel>? registerAnnotationCallback = null,
        Action<GroupViewModel>? registerGroupCallback = null)
    {
        Dictionary<string, NodeViewModel> nodeLookup = [];

        // El nombre de cada nodo tal y como lo declara el archivo. Hace falta tenerlo a mano porque un nodo
        // que no se pudo crear no está en `nodeLookup`, y sin él no habría con qué describir el extremo de un
        // cable que se descarta.
        Dictionary<string, string> uncreatedNodeNames = new(StringComparer.OrdinalIgnoreCase);

        List<DroppedConnection> droppedConnections = [];

        var migration = WorkflowFormat.Plan(graph);

        if (graph.Annotations != null)
        {
            foreach (var aDto in graph.Annotations)
            {
                var annotVm = new AnnotationViewModel(
                    aDto.Title,
                    aDto.Content,
                    new Point(aDto.X, aDto.Y),
                    aDto.Width > 0 ? aDto.Width : 250,
                    aDto.Height > 0 ? aDto.Height : 180,
                    !string.IsNullOrWhiteSpace(aDto.Color) ? aDto.Color : "#FEF08A"
                )
                {
                    Id = aDto.Id,
                    ParentEditor = editor
                };

                if (registerAnnotationCallback != null)
                {
                    registerAnnotationCallback(annotVm);
                }
                else
                {
                    editor.Annotations.Add(annotVm);
                }
            }
        }

        if (graph.Groups != null)
        {
            foreach (var gDto in graph.Groups)
            {
                var groupVm = new GroupViewModel(
                    gDto.Title,
                    new Point(gDto.X, gDto.Y),
                    gDto.Width > 0 ? gDto.Width : 450,
                    gDto.Height > 0 ? gDto.Height : 320,
                    !string.IsNullOrWhiteSpace(gDto.Color) ? gDto.Color : "#3B82F6",
                    gDto.NodeIds
                )
                {
                    Id = gDto.Id,
                    ParentEditor = editor
                };

                if (registerGroupCallback != null)
                {
                    registerGroupCallback(groupVm);
                }
                else
                {
                    editor.Groups.Add(groupVm);
                }
            }
        }

        foreach (var nodeDto in graph.Nodes)
        {
            uncreatedNodeNames[nodeDto.Id] = DescribeUncreatedNode(nodeDto);

            IFlowNode? instance = pluginLoader.CreateNodeInstance(nodeDto.NodeTypeName);
            if (instance == null) continue;

            instance.Id = nodeDto.Id;
            foreach (var (k, v) in nodeDto.Parameters)
            {
                instance.Parameters[k] = v;
            }

            // Lo que este archivo no guardaba y ya no está en ningún sitio —los puertos que exponía un
            // contenedor— se siembra desde las aristas que los nombran antes de materializar: si la
            // definición del subflujo resuelve, sus puertos la sustituyen; si no resuelve, es lo único que
            // queda de ellos y sin sembrarla el contenedor volvería a los genéricos y perdería sus cables.
            // El plan no devuelve nada que recuperar cuando el archivo no lo necesita, así que no hay aquí
            // ninguna decisión de versión que replicar.
            if (instance is ISubflowNode container)
            {
                var (recoveredInputs, recoveredOutputs) = migration.PortsNamedByEdges(nodeDto.Id);
                if (recoveredInputs.Count > 0 || recoveredOutputs.Count > 0)
                {
                    container.RememberedPorts = (recoveredInputs, recoveredOutputs);
                }
            }

            // Antes de construir la tarjeta —y por tanto antes de emparejar cualquier arista—, los puertos
            // que el nodo deriva de lo que se acaba de volcar en sus parámetros tienen que existir.
            DynamicPortMaterializer.Materialize(instance);

            var nodeVm = new NodeViewModel(instance, new Point(nodeDto.X, nodeDto.Y))
            {
                ParentEditor = editor,
                CustomTitle = nodeDto.CustomTitle,
                Title = !string.IsNullOrWhiteSpace(nodeDto.CustomTitle) ? nodeDto.CustomTitle : instance.Name,
                HasBreakpoint = nodeDto.HasBreakpoint || graph.BreakpointNodeIds.Contains(nodeDto.Id),
                IsLoggingEnabled = nodeDto.IsLoggingEnabled && !graph.DisabledLoggingNodeIds.Contains(nodeDto.Id)
            };

            registerNodeCallback(nodeVm);
            nodeLookup[nodeDto.Id] = nodeVm;
        }

        foreach (var edgeDto in graph.Edges)
        {
            // La arista se reconstruye con la misma regla con la que se reconstruye un cable pegado, y
            // cuando no puede, aquí no se calla: se deja dicho quién era y por qué, para que quien haya
            // pedido la reconstrucción pueda contárselo al usuario.
            var dropped = ConnectionReconstructor.TryRebuild(
                edgeDto.SourceNodeId, edgeDto.SourcePortName,
                edgeDto.TargetNodeId, edgeDto.TargetPortName,
                nodeLookup, uncreatedNodeNames,
                register: registerConnectionCallback);

            if (dropped != null)
            {
                droppedConnections.Add(dropped);
            }
        }

        return new ConnectionRebuildReport(droppedConnections);
    }

    /// <summary>
    /// Nombre con el que reconocer un nodo que no se pudo crear: lo que el archivo decía de él. El título que
    /// el usuario le puso, si lo tenía, y si no su tipo —que es lo que se busca para saber qué plugin falta—.
    /// Sin nada de eso queda su identificador, que lo resuelve <see cref="ConnectionReconstructor"/>.
    /// </summary>
    private static string DescribeUncreatedNode(WorkflowNode dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.CustomTitle))
        {
            return dto.CustomTitle;
        }

        return string.IsNullOrWhiteSpace(dto.NodeTypeName) ? dto.Id : dto.NodeTypeName;
    }
}
