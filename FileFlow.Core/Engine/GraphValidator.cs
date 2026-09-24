using FileFlow.Core.Plugins;
using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

public record ValidationResult(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings,
    List<IFlowNode> TopologicalOrder
);

public class GraphValidator
{
    public ValidationResult Validate(WorkflowGraph graph, PluginLoader loader)
    {
        List<string> errors = [];
        List<string> warnings = [];
        Dictionary<string, IFlowNode> nodeInstances = [];

        // 1. Instantiate all nodes
        foreach (var nodeDto in graph.Nodes)
        {
            IFlowNode? instance = loader.CreateNodeInstance(nodeDto.NodeTypeName);
            if (instance == null)
            {
                errors.Add($"Node '{nodeDto.Id}' uses unknown node type '{nodeDto.NodeTypeName}'.");
                continue;
            }

            instance.Id = nodeDto.Id;
            instance.Parameters.Clear();
            foreach (var (k, v) in nodeDto.Parameters)
            {
                instance.Parameters[k] = v;
            }

            // Los puertos calculados no salen del constructor sino de la configuración que se acaba de volcar
            // (los casos de un switch, los nombres de un subflujo, la frontera de un contenedor). Sin este paso
            // el validador compara las aristas contra los puertos de fábrica y rechaza un flujo que el lienzo
            // dibujó correctamente: `Source node 'X' does not have output port 'Case 1'`. Es la misma pregunta
            // —«¿qué puertos expone esta instancia recién configurada?»— que resuelven el cargador de un flujo,
            // el portapapeles y el diagnóstico previo, y tiene que contestarse igual en los cuatro sitios.
            DynamicPortMaterializer.Materialize(instance);

            nodeInstances[nodeDto.Id] = instance;
        }

        if (errors.Count > 0)
        {
            return new ValidationResult(false, errors, warnings, []);
        }

        // 2. Validate Edge compatibility and Port Existence
        foreach (var edge in graph.Edges)
        {
            if (!nodeInstances.TryGetValue(edge.SourceNodeId, out var srcNode))
            {
                errors.Add($"Edge '{edge.Id}' references non-existent source node '{edge.SourceNodeId}'.");
                continue;
            }

            if (!nodeInstances.TryGetValue(edge.TargetNodeId, out var targetNode))
            {
                errors.Add($"Edge '{edge.Id}' references non-existent target node '{edge.TargetNodeId}'.");
                continue;
            }

            var srcPort = srcNode.Outputs.FirstOrDefault(p => p.Name.Equals(edge.SourcePortName, StringComparison.OrdinalIgnoreCase));
            if (srcPort == null)
            {
                errors.Add($"Source node '{srcNode.Name}' ({srcNode.Id}) does not have output port '{edge.SourcePortName}'.");
                continue;
            }

            var targetPort = targetNode.Inputs.FirstOrDefault(p => p.Name.Equals(edge.TargetPortName, StringComparison.OrdinalIgnoreCase));
            if (targetPort == null)
            {
                errors.Add($"Target node '{targetNode.Name}' ({targetNode.Id}) does not have input port '{edge.TargetPortName}'.");
                continue;
            }

            // Check Data Type Compatibility
            if (!targetPort.DataType.IsAssignableFrom(srcPort.DataType) &&
                !(targetPort.DataType == typeof(FileItemContext) && srcPort.DataType == typeof(FileItemContext)))
            {
                errors.Add($"Incompatible port types: Output '{srcPort.DisplayName}' ({srcPort.DataType.Name}) -> Input '{targetPort.DisplayName}' ({targetPort.DataType.Name}) between node {srcNode.Name} and {targetNode.Name}.");
            }
        }

        if (errors.Count > 0)
        {
            return new ValidationResult(false, errors, warnings, []);
        }

        // 3. Topological Sort (Kahn's Algorithm) & Cycle Detection (DAG check)
        //
        // Las aristas que entran por un puerto de retroalimentación (`IsFeedbackSignal`) NO son precedencia: el
        // nodo que las recibe es el que bifurcó la rama que las alimenta, así que contarlas dibujaría un ciclo
        // donde hay una barrera, y un fork/join no se podría escribir. Fuera del orden topológico, pero siguen
        // siendo aristas: el ítem de la rama llega por ellas igual que antes.
        List<WorkflowEdge> precedenceEdges = [.. graph.Edges.Where(edge => !IsFeedbackSignal(edge, nodeInstances))];

        ValidateFeedbackEdges(graph, nodeInstances, precedenceEdges, warnings);

        List<IFlowNode> sortedNodes = [];
        Dictionary<string, int> inDegree = nodeInstances.Keys.ToDictionary(id => id, _ => 0);
        Dictionary<string, List<string>> adjacency = nodeInstances.Keys.ToDictionary(id => id, _ => new List<string>());

        foreach (var edge in precedenceEdges)
        {
            if (nodeInstances.ContainsKey(edge.SourceNodeId) && nodeInstances.ContainsKey(edge.TargetNodeId))
            {
                adjacency[edge.SourceNodeId].Add(edge.TargetNodeId);
                inDegree[edge.TargetNodeId]++;
            }
        }

        Queue<string> zeroInDegreeQueue = new(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        while (zeroInDegreeQueue.Count > 0)
        {
            string currentId = zeroInDegreeQueue.Dequeue();
            sortedNodes.Add(nodeInstances[currentId]);

            foreach (string neighborId in adjacency[currentId])
            {
                inDegree[neighborId]--;
                if (inDegree[neighborId] == 0)
                {
                    zeroInDegreeQueue.Enqueue(neighborId);
                }
            }
        }

        if (sortedNodes.Count != nodeInstances.Count)
        {
            errors.Add("Graph contains a cycle (DAG violation). Dynamic workflow execution requires an acyclic graph.");
            return new ValidationResult(false, errors, warnings, []);
        }

        return new ValidationResult(true, errors, warnings, sortedNodes);
    }

    /// <summary>¿El elemento entra por un puerto que sólo avisa de que una rama terminó?</summary>
    private static bool IsFeedbackSignal(WorkflowEdge edge, Dictionary<string, IFlowNode> nodeInstances) =>
        nodeInstances.TryGetValue(edge.TargetNodeId, out var target) &&
        target.Inputs.Any(port => port.IsFeedbackSignal &&
                                  port.Name.Equals(edge.TargetPortName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Una arista de retroalimentación es la vuelta de una rama hacia la barrera que la bifurcó, y eso se puede
    /// comprobar: el nodo de destino tiene que poder <b>alcanzar</b> al de origen siguiendo las aristas de
    /// precedencia. Cuando no, la barrera no se cierra nunca —nadie le manda el aviso que espera— y el flujo
    /// termina en verde sin entregar nada: exactamente el silencio que este validador existe para romper.
    ///
    /// <para>Son dos avisos, no errores: el grafo es ejecutable (no hay ciclo) y el defecto es que una rama
    /// entera queda muerta, no que el motor no pueda correrlo. Se avisa también cuando <b>todo</b> lo que entra
    /// a un nodo es retroalimentación: sin una arista que le traiga el elemento original, nadie lo arranca.</para>
    /// </summary>
    private static void ValidateFeedbackEdges(
        WorkflowGraph graph,
        Dictionary<string, IFlowNode> nodeInstances,
        List<WorkflowEdge> precedenceEdges,
        List<string> warnings)
    {
        List<WorkflowEdge> feedbackEdges = [.. graph.Edges.Where(edge => IsFeedbackSignal(edge, nodeInstances))];
        if (feedbackEdges.Count == 0) return;

        Dictionary<string, List<string>> reach = nodeInstances.Keys.ToDictionary(id => id, _ => new List<string>());
        foreach (var edge in precedenceEdges)
        {
            if (reach.TryGetValue(edge.SourceNodeId, out var neighbours) && nodeInstances.ContainsKey(edge.TargetNodeId))
            {
                neighbours.Add(edge.TargetNodeId);
            }
        }

        foreach (var edge in feedbackEdges)
        {
            if (!CanReach(edge.TargetNodeId, edge.SourceNodeId, reach))
            {
                warnings.Add($"Feedback edge '{edge.Id}' feeds '{edge.TargetPortName}' of node '{edge.TargetNodeId}' " +
                             $"from node '{edge.SourceNodeId}', which is not downstream of it: the barrier will never see that branch finish.");
            }
        }

        foreach (string nodeId in nodeInstances.Keys)
        {
            List<WorkflowEdge> incoming = [.. graph.Edges.Where(e => e.TargetNodeId == nodeId)];
            if (incoming.Count > 0 && incoming.All(e => IsFeedbackSignal(e, nodeInstances)))
            {
                warnings.Add($"Node '{nodeId}' is only fed by feedback ports ({string.Join(", ", incoming.Select(e => e.TargetPortName))}): " +
                             "nothing ever starts it, so it will never fork the branches those ports are waiting for.");
            }
        }
    }

    /// <summary>
    /// ¿Hay camino de <paramref name="from"/> a <paramref name="to"/>? Se empieza por los vecinos, no por el
    /// propio origen, para que un nodo que se retroalimenta a sí mismo no cuente como camino de longitud cero.
    /// </summary>
    private static bool CanReach(string from, string to, Dictionary<string, List<string>> reach)
    {
        HashSet<string> visited = [from];
        Queue<string> pending = new(reach.TryGetValue(from, out var neighbours) ? neighbours : []);

        while (pending.Count > 0)
        {
            string current = pending.Dequeue();
            if (current == to) return true;
            if (!visited.Add(current) || !reach.TryGetValue(current, out var following)) continue;

            foreach (string next in following)
            {
                pending.Enqueue(next);
            }
        }

        return false;
    }
}
