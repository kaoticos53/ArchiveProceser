using System.Windows;
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
        string name = "FileFlow Workflow")
    {
        var graph = new WorkflowGraph
        {
            Name = name,
            GlobalOutputDir = globalOutputDir
        };

        foreach (var n in nodes)
        {
            var nodeDto = new WorkflowNode
            {
                Id = n.Id,
                NodeTypeName = n.NodeTypeName,
                X = n.Location.X,
                Y = n.Location.Y,
                HasBreakpoint = n.HasBreakpoint,
                IsLoggingEnabled = n.IsLoggingEnabled,
                Parameters = n.Parameters
                    .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                    .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase)
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

    public static void Import(
        WorkflowGraph graph,
        PluginLoader pluginLoader,
        EditorViewModel editor,
        Action<NodeViewModel> registerNodeCallback,
        Action<ConnectionViewModel> registerConnectionCallback)
    {
        Dictionary<string, NodeViewModel> nodeLookup = [];

        foreach (var nodeDto in graph.Nodes)
        {
            IFlowNode? instance = pluginLoader.CreateNodeInstance(nodeDto.NodeTypeName);
            if (instance == null) continue;

            instance.Id = nodeDto.Id;
            foreach (var (k, v) in nodeDto.Parameters)
            {
                instance.Parameters[k] = v;
            }

            var nodeVm = new NodeViewModel(instance, new Point(nodeDto.X, nodeDto.Y))
            {
                ParentEditor = editor,
                HasBreakpoint = nodeDto.HasBreakpoint || graph.BreakpointNodeIds.Contains(nodeDto.Id),
                IsLoggingEnabled = nodeDto.IsLoggingEnabled && !graph.DisabledLoggingNodeIds.Contains(nodeDto.Id)
            };

            registerNodeCallback(nodeVm);
            nodeLookup[nodeDto.Id] = nodeVm;
        }

        foreach (var edgeDto in graph.Edges)
        {
            if (nodeLookup.TryGetValue(edgeDto.SourceNodeId, out var srcNode) &&
                nodeLookup.TryGetValue(edgeDto.TargetNodeId, out var targetNode))
            {
                var srcPort = srcNode.OutputPorts.FirstOrDefault(p => p.Name.Equals(edgeDto.SourcePortName, StringComparison.OrdinalIgnoreCase));
                var targetPort = targetNode.InputPorts.FirstOrDefault(p => p.Name.Equals(edgeDto.TargetPortName, StringComparison.OrdinalIgnoreCase));

                if (srcPort != null && targetPort != null)
                {
                    registerConnectionCallback(new ConnectionViewModel(srcPort, targetPort));
                }
            }
        }
    }
}
