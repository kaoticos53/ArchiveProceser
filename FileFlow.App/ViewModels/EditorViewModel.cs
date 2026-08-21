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
    private readonly Services.IVariableDiscoveryService _variableDiscoveryService;

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
        ViewportZoom = Math.Min(2.5, Math.Round(ViewportZoom + 0.05, 2));
    }

    [RelayCommand]
    public void ZoomOut()
    {
        ViewportZoom = Math.Max(0.2, Math.Round(ViewportZoom - 0.05, 2));
    }

    [RelayCommand]
    public void ResetZoom()
    {
        FitToScreen();
    }

    [RelayCommand]
    public void FitToScreen()
    {
        if (Nodes.Count == 0)
        {
            ViewportZoom = 1.0;
            ViewportLocation = new Point(0, 0);
            return;
        }

        double minX = Nodes.Min(n => n.Location.X);
        double minY = Nodes.Min(n => n.Location.Y);
        double maxX = Nodes.Max(n => n.Location.X + (n.Width > 0 ? n.Width : 280));
        double maxY = Nodes.Max(n => n.Location.Y + 220);

        double graphWidth = Math.Max(maxX - minX, 100);
        double graphHeight = Math.Max(maxY - minY, 100);

        double viewWidth = 900;
        double viewHeight = 500;

        double paddingX = 120;
        double paddingY = 120;

        double scaleX = (viewWidth - paddingX) / graphWidth;
        double scaleY = (viewHeight - paddingY) / graphHeight;

        double targetZoom = Math.Clamp(Math.Min(scaleX, scaleY), 0.3, 1.8);
        ViewportZoom = Math.Round(targetZoom, 2);

        double visibleCanvasWidth = viewWidth / ViewportZoom;
        double visibleCanvasHeight = viewHeight / ViewportZoom;

        double extraCanvasX = Math.Max(50, (visibleCanvasWidth - graphWidth) / 2.0);
        double extraCanvasY = Math.Max(50, (visibleCanvasHeight - graphHeight) / 2.0);

        double locX = minX - extraCanvasX;
        double locY = minY - extraCanvasY;

        ViewportLocation = new Point(Math.Round(locX, 1), Math.Round(locY, 1));
    }

    public EditorViewModel(PluginLoader pluginLoader, Services.IVariableDiscoveryService? variableDiscoveryService = null)
    {
        _pluginLoader = pluginLoader;
        _variableDiscoveryService = variableDiscoveryService ?? new Services.VariableDiscoveryService();
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

        node.Cleanup();
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

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    [RelayCommand]
    public void ClearGraph()
    {
        Connections.Clear();
        foreach (var node in Nodes)
        {
            node.Cleanup();
        }
        Nodes.Clear();
        SelectedNode = null;
    }

    public NodeViewModel? AddNode(string nodeTypeName, Point position)
    {
        IFlowNode? nodeInstance = _pluginLoader.CreateNodeInstance(nodeTypeName);
        if (nodeInstance == null) return null;

        var nodeVm = new NodeViewModel(nodeInstance, position);
        nodeVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(NodeViewModel.IsSelected) && nodeVm.IsSelected)
            {
                SelectedNode = nodeVm;
            }
        };
        Nodes.Add(nodeVm);
        return nodeVm;
    }

    public void ClearDebugStates()
    {
        foreach (var node in Nodes)
        {
            node.ClearDebugData();
        }
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
                HasBreakpoint = n.HasBreakpoint,
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

            var nodeVm = new NodeViewModel(instance, new Point(nodeDto.X, nodeDto.Y))
            {
                HasBreakpoint = nodeDto.HasBreakpoint || graph.BreakpointNodeIds.Contains(nodeDto.Id)
            };

            nodeVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(NodeViewModel.IsSelected) && nodeVm.IsSelected)
                {
                    SelectedNode = nodeVm;
                }
            };

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
        return _variableDiscoveryService.GetAvailableVariables(targetNode, Connections);
    }
}
