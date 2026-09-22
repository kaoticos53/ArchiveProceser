using System.Text.Json;
using Avalonia;
using Avalonia.Input.Platform;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;

namespace FileFlow.App.Services;

/// <summary>
/// Modelo de datos serializable para almacenar nodos en el portapapeles.
/// </summary>
public sealed class NodeClipboardItem
{
    public string OriginalId { get; set; } = string.Empty;
    public string NodeTypeName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? CustomTitle { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 200;
    public bool IsExpanded { get; set; }
    public bool HasBreakpoint { get; set; }
    public bool IsLoggingEnabled { get; set; } = true;
    public string? AccentColor { get; set; }
    public string? HeaderColor { get; set; }
    public string? InnerGraphJson { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Modelo de datos serializable para almacenar conexiones internas entre los nodos copiados.
/// </summary>
public sealed class EdgeClipboardItem
{
    public string SourceNodeOriginalId { get; set; } = string.Empty;
    public string SourcePortName { get; set; } = string.Empty;
    public string TargetNodeOriginalId { get; set; } = string.Empty;
    public string TargetPortName { get; set; } = string.Empty;
}

/// <summary>
/// Paquete contenedor del portapapeles con metadatos de versión y compatibilidad.
/// </summary>
public sealed class NodeClipboardPackage
{
    public string Schema { get; set; } = "FileFlow.NodeClipboard.v1";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<NodeClipboardItem> Nodes { get; set; } = [];
    public List<EdgeClipboardItem> Edges { get; set; } = [];
}

/// <summary>
/// Lo que dejó un pegado o una duplicación: los nodos nuevos y lo que no se pudo reconectar entre ellos.
///
/// Los nodos y el informe van juntos, y no por comodidad: quien pega recibe <b>siempre</b> la cuenta de lo
/// que se perdió, sin poder pedirla por separado ni olvidarse de mirarla. Un pegado que descarta un cable en
/// silencio es exactamente el fallo que este informe cierra.
/// </summary>
public sealed class ClipboardPasteResult
{
    /// <summary>Un pegado que no dejó nada: ni nodos ni cables perdidos.</summary>
    public static ClipboardPasteResult Empty { get; } = new([], ConnectionRebuildReport.Complete);

    internal ClipboardPasteResult(IReadOnlyList<NodeViewModel> nodes, ConnectionRebuildReport report)
    {
        Nodes = nodes;
        Report = report;
    }

    /// <summary>Nodos nuevos, en el orden en que se pegaron. El último es el que queda seleccionado.</summary>
    public IReadOnlyList<NodeViewModel> Nodes { get; }

    /// <summary>Conexiones internas que el paquete declaraba y no se pudieron reconstruir.</summary>
    public ConnectionRebuildReport Report { get; }
}

/// <summary>
/// Implementación thread-safe y tolerante a fallos del servicio de portapapeles para el lienzo DAG.
/// </summary>
public sealed class NodeClipboardService : INodeClipboardService
{
    private const string ClipboardHeaderPrefix = "<!-- FileFlow.NodeClipboard.v1 -->";
    private readonly PluginLoader _pluginLoader;
    private readonly System.Threading.Lock _lock = new();
    private NodeClipboardPackage? _inMemoryFallback;
    private int _pasteCounter = 0;

    public NodeClipboardService(PluginLoader? pluginLoader = null)
    {
        _pluginLoader = pluginLoader ?? new PluginLoader();
    }

    public void Copy(IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var selectedNodes = nodes.Where(n => n != null).ToList();
        if (selectedNodes.Count == 0) return;

        var selectedIds = new HashSet<string>(selectedNodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);
        var package = new NodeClipboardPackage();

        foreach (var node in selectedNodes)
        {
            var item = new NodeClipboardItem
            {
                OriginalId = node.Id,
                NodeTypeName = node.NodeTypeName,
                Title = node.Title,
                CustomTitle = node.CustomTitle,
                X = node.Location.X,
                Y = node.Location.Y,
                Width = node.Width,
                IsExpanded = node.IsExpanded,
                HasBreakpoint = node.HasBreakpoint,
                IsLoggingEnabled = node.IsLoggingEnabled,
                AccentColor = node.AccentColor,
                HeaderColor = node.HeaderColor,
                InnerGraphJson = node.InnerGraphJson
            };

            // 1. Extraer parámetros configurados en los ViewModels (interfaz gráfica)
            foreach (var param in node.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(param.Key))
                {
                    item.Parameters[param.Key] = param.Value;
                }
            }

            // 2. Extraer parámetros de la instancia de nodo (para claves internas como CasesJson/MethodSteps o cambios directos)
            lock (node.NodeInstance.Parameters)
            {
                foreach (var (k, v) in node.NodeInstance.Parameters)
                {
                    if (!string.IsNullOrWhiteSpace(k) && v != null)
                    {
                        item.Parameters[k] = v;
                    }
                }
            }

            package.Nodes.Add(item);
        }

        // 3. Capturar conexiones internas entre los nodos seleccionados
        if (connections != null)
        {
            foreach (var conn in connections)
            {
                if (conn.Source?.NodeOwner != null && conn.Target?.NodeOwner != null)
                {
                    string srcId = conn.Source.NodeOwner.Id;
                    string tgtId = conn.Target.NodeOwner.Id;

                    if (selectedIds.Contains(srcId) && selectedIds.Contains(tgtId))
                    {
                        package.Edges.Add(new EdgeClipboardItem
                        {
                            SourceNodeOriginalId = srcId,
                            SourcePortName = conn.Source.Name,
                            TargetNodeOriginalId = tgtId,
                            TargetPortName = conn.Target.Name
                        });
                    }
                }
            }
        }

        lock (_lock)
        {
            _inMemoryFallback = package;
            _pasteCounter = 0;
        }

        // 4. Escribir de forma segura en el portapapeles del sistema
        try
        {
            string json = JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = false });
            string clipboardPayload = $"{ClipboardHeaderPrefix}\n{json}";

            var topLevel = App.MainWindow != null ? Avalonia.Controls.TopLevel.GetTopLevel(App.MainWindow) : null;
            if (topLevel?.Clipboard != null)
            {
                _ = topLevel.Clipboard.SetTextAsync(clipboardPayload);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NodeClipboardService] Warning: Could not write to OS Clipboard: {ex.Message}. Using in-memory fallback.");
        }
    }

    public bool CanPaste()
    {
        lock (_lock)
        {
            if (_inMemoryFallback != null && _inMemoryFallback.Nodes.Count > 0)
            {
                return true;
            }
        }

        try
        {
            var topLevel = App.MainWindow != null ? Avalonia.Controls.TopLevel.GetTopLevel(App.MainWindow) : null;
            if (topLevel?.Clipboard != null)
            {
                string? text = topLevel.Clipboard.TryGetTextAsync().GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(text) && text.Contains(ClipboardHeaderPrefix))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignorar bloqueo temporal del portapapeles
        }

        return false;
    }

    public ClipboardPasteResult Paste(EditorViewModel editor, Point? targetPosition = null)
    {
        ArgumentNullException.ThrowIfNull(editor);

        NodeClipboardPackage? package = GetCurrentPackage();
        if (package == null || package.Nodes.Count == 0)
        {
            return ClipboardPasteResult.Empty;
        }

        lock (_lock)
        {
            _pasteCounter++;
        }

        double minX = package.Nodes.Min(n => n.X);
        double minY = package.Nodes.Min(n => n.Y);

        Dictionary<string, NodeViewModel> idMapping = new(StringComparer.OrdinalIgnoreCase);
        List<NodeViewModel> createdNodes = [];

        // El nombre de cada nodo tal y como lo llevaba el paquete: un nodo que no llegue a crearse —su tipo
        // no está registrado en esta máquina— se describe con él al contar el cable que se pierda.
        Dictionary<string, string> uncreatedNodeNames = new(StringComparer.OrdinalIgnoreCase);
        List<DroppedConnection> droppedConnections = [];

        // Deseleccionar nodos previamente existentes
        foreach (var existingNode in editor.Nodes)
        {
            existingNode.IsSelected = false;
        }

        // 1. Instanciar y configurar los nodos
        foreach (var item in package.Nodes)
        {
            uncreatedNodeNames[item.OriginalId] = DescribeCopiedNode(item);

            IFlowNode? instance = _pluginLoader.CreateNodeInstance(item.NodeTypeName);
            if (instance == null) continue;

            string newId = Guid.NewGuid().ToString();
            instance.Id = newId;

            // Transferir parámetros antes de construir el ViewModel para que la inicialización los tome directamente
            lock (instance.Parameters)
            {
                foreach (var (k, v) in item.Parameters)
                {
                    instance.Parameters[k] = UnwrapJsonValue(v);
                }
            }

            // Los puertos declarados (script, subflujo contenedor) salen de esos parámetros: si no se
            // materializan aquí, el emparejamiento por nombre de más abajo no encuentra el puerto y el
            // cable pegado se pierde en silencio.
            DynamicPortMaterializer.Materialize(instance);

            // Calcular posición de pegado
            Point newLocation;
            if (targetPosition.HasValue)
            {
                double offsetX = item.X - minX;
                double offsetY = item.Y - minY;
                newLocation = new Point(targetPosition.Value.X + offsetX, targetPosition.Value.Y + offsetY);
            }
            else
            {
                int currentCounter;
                lock (_lock)
                {
                    currentCounter = _pasteCounter;
                }
                double offset = 40.0 * currentCounter;
                newLocation = new Point(item.X + offset, item.Y + offset);
            }

            string effectiveTitle = !string.IsNullOrWhiteSpace(item.CustomTitle)
                ? item.CustomTitle
                : (!string.IsNullOrWhiteSpace(item.Title) ? item.Title : instance.Name);

            var nodeVm = new NodeViewModel(instance, newLocation)
            {
                ParentEditor = editor,
                Title = effectiveTitle,
                CustomTitle = !string.IsNullOrWhiteSpace(item.CustomTitle) ? item.CustomTitle : (effectiveTitle != instance.Name ? effectiveTitle : null),
                HasBreakpoint = item.HasBreakpoint,
                IsLoggingEnabled = item.IsLoggingEnabled,
                Width = item.Width > 0 ? item.Width : 200,
                IsExpanded = item.IsExpanded,
                InnerGraphJson = item.InnerGraphJson ?? string.Empty,
                IsSelected = true
            };

            if (!string.IsNullOrWhiteSpace(item.AccentColor))
            {
                nodeVm.AccentColor = item.AccentColor;
            }
            if (!string.IsNullOrWhiteSpace(item.HeaderColor))
            {
                nodeVm.HeaderColor = item.HeaderColor;
            }

            // Sincronizar explícitamente los valores de los parámetros en los ViewModels
            foreach (var p in nodeVm.Parameters)
            {
                if (item.Parameters.TryGetValue(p.Key, out var rawVal))
                {
                    p.Value = UnwrapJsonValue(rawVal);
                }
            }

            // Si el nodo contiene parámetros dinámicos (ej. VariableInjectorNode) que no están en descriptores, asegurarse de que se reflejen
            if (nodeVm.IsVariableInjectorNode)
            {
                foreach (var (k, v) in item.Parameters)
                {
                    if (k.StartsWith("Var_", StringComparison.OrdinalIgnoreCase) && !nodeVm.Parameters.Any(p => p.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        var unwrapped = UnwrapJsonValue(v)?.ToString() ?? "";
                        nodeVm.Parameters.Add(new NodeParameterViewModel(new NodeParameterDescriptor(k, ParameterEditorType.Text, DefaultValue: unwrapped), unwrapped, nodeOwner: nodeVm));
                    }
                }
            }

            idMapping[item.OriginalId] = nodeVm;
            createdNodes.Add(nodeVm);
        }

        // 2. Registrar nodos en el editor
        foreach (var node in createdNodes)
        {
            editor.Nodes.Add(node);
            editor.BringToFront(node);
        }

        // 3. Recrear las aristas/conexiones internas con los nuevos IDs, con la misma regla con la que se
        // reconstruye un flujo abierto: emparejar los puertos por nombre. Un cable que no empareja no se
        // descarta en silencio —se deja dicho quién era y por qué— porque el usuario acaba de pegar y es
        // quien puede volver a conectarlo.
        foreach (var edge in package.Edges)
        {
            var dropped = ConnectionReconstructor.TryRebuild(
                edge.SourceNodeOriginalId, edge.SourcePortName,
                edge.TargetNodeOriginalId, edge.TargetPortName,
                idMapping, uncreatedNodeNames,
                register: connection => editor.CreateConnection(connection.Source, connection.Target));

            if (dropped != null)
            {
                droppedConnections.Add(dropped);
            }
        }

        return new ClipboardPasteResult(createdNodes, new ConnectionRebuildReport(droppedConnections));
    }

    public ClipboardPasteResult Duplicate(IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections, EditorViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        Copy(nodes, connections);
        return Paste(editor, targetPosition: null);
    }

    /// <summary>
    /// Nombre con el que reconocer un nodo del paquete: el título que el usuario le puso y, si no lo tenía,
    /// su tipo —que es lo que se busca para saber qué plugin falta—. Es el mismo criterio que al abrir un
    /// archivo, para que el mismo nodo ausente se cuente igual por los dos caminos.
    /// </summary>
    private static string DescribeCopiedNode(NodeClipboardItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.CustomTitle))
        {
            return item.CustomTitle;
        }

        return string.IsNullOrWhiteSpace(item.NodeTypeName) ? item.OriginalId : item.NodeTypeName;
    }

    private NodeClipboardPackage? GetCurrentPackage()
    {
        try
        {
            var topLevel = App.MainWindow != null ? Avalonia.Controls.TopLevel.GetTopLevel(App.MainWindow) : null;
            if (topLevel?.Clipboard != null)
            {
                string? text = topLevel.Clipboard.TryGetTextAsync().GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(text) && text.Contains(ClipboardHeaderPrefix))
                {
                    int index = text.IndexOf(ClipboardHeaderPrefix, StringComparison.Ordinal);
                    string json = text[(index + ClipboardHeaderPrefix.Length)..].Trim();
                    var package = JsonSerializer.Deserialize<NodeClipboardPackage>(json);
                    if (package != null && package.Nodes.Count > 0)
                    {
                        return package;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NodeClipboardService] Could not read OS Clipboard: {ex.Message}. Falling back to memory.");
        }

        lock (_lock)
        {
            return _inMemoryFallback;
        }
    }

    private static object? UnwrapJsonValue(object? val)
    {
        if (val is JsonElement elem)
        {
            return elem.ValueKind switch
            {
                JsonValueKind.String => elem.GetString(),
                JsonValueKind.Number => elem.TryGetInt64(out long l) ? l : (elem.TryGetDouble(out double d) ? d : elem.GetRawText()),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => elem.GetRawText()
            };
        }
        return val;
    }
}
