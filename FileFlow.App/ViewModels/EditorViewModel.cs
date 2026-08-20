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
}
