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
        List<IFlowNode> sortedNodes = [];
        Dictionary<string, int> inDegree = nodeInstances.Keys.ToDictionary(id => id, _ => 0);
        Dictionary<string, List<string>> adjacency = nodeInstances.Keys.ToDictionary(id => id, _ => new List<string>());

        foreach (var edge in graph.Edges)
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
}
