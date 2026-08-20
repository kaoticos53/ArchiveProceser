using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;

namespace FileFlow.App.ViewModels;

public partial class EditorViewModel : ObservableObject
{
    private readonly PluginLoader _pluginLoader;

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    [ObservableProperty]
    private PendingConnectionViewModel? _pendingConnection;

    [ObservableProperty]
    private Point _viewportLocation;

    [ObservableProperty]
    private double _viewportZoom = 1.0;

    [RelayCommand]
    public void ZoomIn()
    {
        ViewportZoom = Math.Min(2.5, Math.Round(ViewportZoom + 0.1, 2));
    }

    [RelayCommand]
    public void ZoomOut()
    {
        ViewportZoom = Math.Max(0.2, Math.Round(ViewportZoom - 0.1, 2));
    }

    [RelayCommand]
    public void ResetZoom()
    {
        ViewportZoom = 1.0;
    }

    public EditorViewModel(PluginLoader pluginLoader)
    {
        _pluginLoader = pluginLoader;
    }

    public void CreateConnection(PortViewModel source, PortViewModel target)
    {
        if (source == null || target == null || source == target) return;
        if (source.NodeOwner == target.NodeOwner) return;

        // Ensure Source is Output and Target is Input
        PortViewModel outputPort = source.Direction == PortDirection.Output ? source : target;
        PortViewModel inputPort = source.Direction == PortDirection.Output ? target : source;

        if (outputPort.Direction != PortDirection.Output || inputPort.Direction != PortDirection.Input)
            return;

        // Remove any existing connection to the same input port
        var existing = Connections.FirstOrDefault(c => c.Target == inputPort);
        if (existing != null)
        {
            Connections.Remove(existing);
        }

        Connections.Add(new ConnectionViewModel(outputPort, inputPort));
    }

    [RelayCommand]
    public void StartConnection(object? source)
    {
        if (source is PortViewModel port)
        {
            PendingConnection = new PendingConnectionViewModel(port);
        }
    }

    [RelayCommand]
    public void FinishConnection(object? target)
    {
        if (PendingConnection?.Source != null && target is PortViewModel targetPort)
        {
            CreateConnection(PendingConnection.Source, targetPort);
        }
        PendingConnection = null;
    }

    [RelayCommand]
    public void CancelConnection()
    {
        PendingConnection = null;
    }

    [RelayCommand]
    public void DisconnectConnector(object? connector)
    {
        if (connector is PortViewModel port)
        {
            var removeList = Connections.Where(c => c.Source == port || c.Target == port).ToList();
            foreach (var conn in removeList)
            {
                Connections.Remove(conn);
            }
        }
    }

    [RelayCommand]
    public void DeleteNode(object? nodeParam)
    {
        if (nodeParam is NodeViewModel node)
        {
            RemoveNodeWithConnections(node);
        }
    }

    [RelayCommand]
    public void DeleteConnection(object? connectionParam)
    {
        if (connectionParam is ConnectionViewModel conn)
        {
            Connections.Remove(conn);
        }
    }

    public void RemoveNodeWithConnections(NodeViewModel node)
    {
        var relatedConnections = Connections
            .Where(c => c.Source.NodeOwner == node || c.Target.NodeOwner == node)
            .ToList();

        foreach (var conn in relatedConnections)
        {
            Connections.Remove(conn);
        }

        Nodes.Remove(node);
    }

    [RelayCommand]
    public void DeleteSelectedNodes()
    {
        var selectedNodes = Nodes.Where(n => n.IsSelected).ToList();
        foreach (var node in selectedNodes)
        {
            RemoveNodeWithConnections(node);
        }
    }

    [RelayCommand]
    public void ClearGraph()
    {
        Connections.Clear();
        Nodes.Clear();
    }

    public NodeViewModel? AddNode(string nodeTypeName, Point position)
    {
        IFlowNode? nodeInstance = _pluginLoader.CreateNodeInstance(nodeTypeName);
        if (nodeInstance == null) return null;

        var nodeVm = new NodeViewModel(nodeInstance, position);
        Nodes.Add(nodeVm);
        return nodeVm;
    }

    public WorkflowGraph ExportToGraphModel(string name = "FileFlow Workflow")
    {
        var graph = new WorkflowGraph { Name = name };

        foreach (var n in Nodes)
        {
            var nodeDto = new WorkflowNode
            {
                Id = n.Id,
                NodeTypeName = n.NodeTypeName,
                X = n.Location.X,
                Y = n.Location.Y,
                Parameters = n.Parameters.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase)
            };
            graph.Nodes.Add(nodeDto);
        }

        foreach (var c in Connections)
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

    public void LoadFromGraphModel(WorkflowGraph graph)
    {
        ClearGraph();

        Dictionary<string, NodeViewModel> nodeLookup = [];

        foreach (var nodeDto in graph.Nodes)
        {
            IFlowNode? instance = _pluginLoader.CreateNodeInstance(nodeDto.NodeTypeName);
            if (instance == null) continue;

            instance.Id = nodeDto.Id;
            foreach (var (k, v) in nodeDto.Parameters)
            {
                instance.Parameters[k] = v;
            }

            var nodeVm = new NodeViewModel(instance, new Point(nodeDto.X, nodeDto.Y));
            Nodes.Add(nodeVm);
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
                    Connections.Add(new ConnectionViewModel(srcPort, targetPort));
                }
            }
        }
    }

    public List<FileFlow.App.Models.VariableGroupItem> GetUpstreamAvailableVariables(NodeViewModel targetNode)
    {
        var result = new List<FileFlow.App.Models.VariableGroupItem>();

        // 1. Built-in System Variables (Always available)
        var systemGroup = new FileFlow.App.Models.VariableGroupItem("🌐 System Variables");
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("FileName", "{FileName}", "Full file name (e.g. photo.jpg)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("FileNameNoExt", "{FileNameNoExt}", "File name without extension (e.g. photo)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Extension", "{Extension}", "File extension (e.g. jpg)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("CurrentPath", "{CurrentPath}", "Current absolute item path"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("OriginalPath", "{OriginalPath}", "Original source item path"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("RelativePath", "{RelativePath}", "Relative path from source root"));
        result.Add(systemGroup);

        // 2. Upstream Traversal
        var visitedNodes = new HashSet<NodeViewModel>();
        var queue = new Queue<NodeViewModel>();
        queue.Enqueue(targetNode);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var incomingConns = Connections.Where(c => c.Target.NodeOwner == current).ToList();

            foreach (var conn in incomingConns)
            {
                var upstreamNode = conn.Source.NodeOwner;
                if (visitedNodes.Add(upstreamNode))
                {
                    queue.Enqueue(upstreamNode);

                    string typeName = upstreamNode.NodeTypeName;
                    var upstreamGroup = new FileFlow.App.Models.VariableGroupItem($"🔗 {upstreamNode.Title}");

                    if (typeName.Contains("ExifMetadataNode", StringComparison.OrdinalIgnoreCase))
                    {
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("DateTaken", "{DateTaken}", "Date/Time Original EXIF"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Year", "{Year(DateTaken)}", "4-Digit Year"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Month", "{Month(DateTaken)}", "2-Digit Month"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Day", "{Day(DateTaken)}", "2-Digit Day"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("CameraModel", "{CameraModel}", "Camera Model EXIF"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("CameraMake", "{CameraMake}", "Camera Make EXIF"));
                    }
                    else if (typeName.Contains("VariableInjectorNode", StringComparison.OrdinalIgnoreCase))
                    {
                        var varNameParam = upstreamNode.Parameters.FirstOrDefault(p => p.Key.Equals("VariableName", StringComparison.OrdinalIgnoreCase));
                        string keyName = varNameParam?.Value?.ToString() ?? "CustomKey";
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem(keyName, $"{{{keyName}}}", $"Injected by {upstreamNode.Title}"));
                    }
                    else if (typeName.Contains("SmartUnpackNode", StringComparison.OrdinalIgnoreCase))
                    {
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("UnpackedFrom", "{UnpackedFrom}", "Original Archive Path"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("HasSingleWrapper", "{HasSingleWrapper}", "Is Single Folder Wrapper"));
                    }
                    else if (typeName.Contains("ImageOptimizerNode", StringComparison.OrdinalIgnoreCase))
                    {
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("OptimizedFormat", "{OptimizedFormat}", "Output Format (WebP/Jpeg/Png)"));
                    }

                    if (upstreamGroup.Variables.Count > 0)
                    {
                        result.Add(upstreamGroup);
                    }
                }
            }
        }

        // 3. Transformation Functions Group
        var fnGroup = new FileFlow.App.Models.VariableGroupItem("🔤 Expression Functions");
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Upper", "{Upper(FileNameNoExt)}", "Convert text to uppercase"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Lower", "{Lower(Extension)}", "Convert text to lowercase"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("FormatDate", "{FormatDate(DateTaken, \"yyyy-MM\")}", "Custom Date Format"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Replace", "{Replace(FileNameNoExt, \"old\", \"new\")}", "Replace string pattern"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Default", "{Default(DateTaken, \"2026-01-01\")}", "Fallback value if empty"));
        result.Add(fnGroup);

        return result;
    }
}
