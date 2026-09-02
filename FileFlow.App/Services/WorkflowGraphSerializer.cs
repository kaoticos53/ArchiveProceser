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
        Action<ConnectionViewModel> registerConnectionCallback,
        Action<AnnotationViewModel>? registerAnnotationCallback = null,
        Action<GroupViewModel>? registerGroupCallback = null)
    {
        Dictionary<string, NodeViewModel> nodeLookup = [];

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
