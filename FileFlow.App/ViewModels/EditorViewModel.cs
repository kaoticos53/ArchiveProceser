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
                Parameters = n.Parameters
                    .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                    .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase)
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

        // 1. Built-in System & Environment Variables (Always available)
        var systemGroup = new FileFlow.App.Models.VariableGroupItem("🌐 System & Environment");
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("FileName", "{FileName}", "Full file name (e.g. photo.jpg)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("FileNameNoExt", "{FileNameNoExt}", "File name without extension (e.g. photo)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Extension", "{Extension}", "File extension (e.g. jpg)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("CurrentPath", "{CurrentPath}", "Current absolute item path"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("OriginalPath", "{OriginalPath}", "Original source item path"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("RelativePath", "{RelativePath}", "Relative subfolder path from source root"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("DateNow", "{DateNow}", "Current execution date (yyyy-MM-dd)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("TimeNow", "{TimeNow}", "Current execution time (HH-mm-ss)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("DateTimeNow", "{DateTimeNow}", "Combined timestamp (yyyy-MM-dd_HH-mm-ss)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Counter", "{Counter}", "Item sequence index in batch (e.g. 1, 2, 3)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("SizeMB", "{SizeMB}", "File size in Megabytes (e.g. 4.25MB)"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("SizeKB", "{SizeKB}", "File size in Kilobytes"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("UserName", "{UserName}", "Windows user name"));
        systemGroup.Variables.Add(new FileFlow.App.Models.VariableItem("MachineName", "{MachineName}", "Environment host computer name"));
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
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("ImageWidth", "{ImageWidth}", "Image width in pixels"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("ImageHeight", "{ImageHeight}", "Image height in pixels"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Orientation", "{Orientation}", "Landscape, Portrait, or Square"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("AspectRatio", "{AspectRatio}", "Calculated Aspect Ratio (e.g. 16:9)"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Megapixels", "{Megapixels}", "Image resolution in Megapixels"));
                    }
                    else if (typeName.Contains("VariableInjectorNode", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var param in upstreamNode.Parameters)
                        {
                            string keyName = param.Key;
                            if (!string.IsNullOrWhiteSpace(keyName))
                            {
                                upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem(keyName, $"{{{keyName}}}", $"Injected by {upstreamNode.Title}"));
                            }
                        }
                    }
                    else if (typeName.Contains("SmartUnpackNode", StringComparison.OrdinalIgnoreCase))
                    {
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("UnpackedFrom", "{UnpackedFrom}", "Original Archive Path"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("ArchiveFormat", "{ArchiveFormat}", "Archive format (ZIP/7Z/RAR)"));
                        upstreamGroup.Variables.Add(new FileFlow.App.Models.VariableItem("UnpackedFileCount", "{UnpackedFileCount}", "Total extracted file count"));
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
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Sanitize", "{Sanitize(CameraModel)}", "Clean illegal Windows path characters"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("PadLeft", "{PadLeft(Counter, 4, \"0\")}", "Pad number with leading characters"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Upper", "{Upper(FileNameNoExt)}", "Convert text to uppercase"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Lower", "{Lower(Extension)}", "Convert text to lowercase"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("FormatDate", "{FormatDate(DateTaken, \"yyyy-MM\")}", "Custom Date Format"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Substring", "{Substring(FileNameNoExt, 0, 8)}", "Extract text substring"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("RegexMatch", "{RegexMatch(FileNameNoExt, \"[0-9]+\")}", "Extract regular expression match"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("RegexReplace", "{RegexReplace(FileNameNoExt, \"[^a-zA-Z0-9]\", \"_\")}", "Replace regex pattern"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Coalesce", "{Coalesce(DateTaken, FileCreatedDate, DateNow)}", "First non-empty value in list"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("FileAgeDays", "{FileAgeDays(DateTaken)}", "Days elapsed since date"));
        fnGroup.Variables.Add(new FileFlow.App.Models.VariableItem("Default", "{Default(DateTaken, \"2026-01-01\")}", "Fallback value if empty"));
        result.Add(fnGroup);

        return result;
    }
}
