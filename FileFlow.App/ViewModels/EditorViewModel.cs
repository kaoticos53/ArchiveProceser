using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public sealed record BreadcrumbItem(string Name, string? NodeId, WorkflowGraph Graph);

public partial class EditorViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly PluginLoader _pluginLoader;
    private readonly Services.IVariableDiscoveryService _variableDiscoveryService;
    private readonly Services.INodeClipboardService _clipboardService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ILocalizationService _loc;
    private readonly IDialogService _dialogService;
    private readonly Action _preferencesChangedHandler;

    public Services.INodeClipboardService ClipboardService => _clipboardService;

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = [];
    public ObservableCollection<AnnotationViewModel> Annotations { get; } = [];
    public ObservableCollection<GroupViewModel> Groups { get; } = [];
    public ObservableCollection<object> CanvasDecorators { get; } = [];

    [ObservableProperty]
    private string _currentWorkflowTitle = "Root Workflow";

    [ObservableProperty]
    private string _globalOutputDir = @"C:\FileFlowOutput";

    [ObservableProperty]
    private PendingConnectionViewModel? _pendingConnection;

    [ObservableProperty]
    private Point _viewportLocation;

    [ObservableProperty]
    private Size _viewportSize;

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
        var (zoom, location) = EditorViewportCalculator.CalculateFitToScreen(Nodes);
        ViewportZoom = zoom;
        ViewportLocation = location;
    }

    private readonly Dictionary<string, List<ConnectionViewModel>> _connectionLookup = new(StringComparer.OrdinalIgnoreCase);

    public EditorViewModel(
        PluginLoader pluginLoader,
        Services.IVariableDiscoveryService? variableDiscoveryService = null,
        Services.INodeClipboardService? clipboardService = null,
        IUserPreferencesService? userPreferencesService = null,
        ILocalizationService? localizationService = null,
        IDialogService? dialogService = null)
    {
        _pluginLoader = pluginLoader;
        _variableDiscoveryService = variableDiscoveryService ?? new Services.VariableDiscoveryService();
        _clipboardService = clipboardService ?? new Services.NodeClipboardService(_pluginLoader);
        _userPreferencesService = userPreferencesService ?? UserPreferencesService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;
        _dialogService = dialogService ?? WpfDialogService.Instance;
        _globalOutputDir = _userPreferencesService.Preferences.DefaultGlobalOutputDir;
        _preferencesChangedHandler = () =>
        {
            GlobalOutputDir = _userPreferencesService.Preferences.DefaultGlobalOutputDir;
        };
        _userPreferencesService.PreferencesChanged += _preferencesChangedHandler;
        Connections.CollectionChanged += (s, e) =>
        {
            RebuildConnectionLookup();
            UpdatePortConnectionStates();
        };
        Nodes.CollectionChanged += (s, e) => UpdatePortConnectionStates();
    }

    private void RebuildConnectionLookup()
    {
        _connectionLookup.Clear();
        foreach (var conn in Connections)
        {
            string key = $"{conn.Source.NodeOwner.Id}:{conn.Source.Name}";
            if (!_connectionLookup.TryGetValue(key, out var list))
            {
                list = [];
                _connectionLookup[key] = list;
            }
            list.Add(conn);
        }
    }

    public void UpdatePortConnectionStates()
    {
        foreach (var node in Nodes)
        {
            foreach (var inPort in node.InputPorts)
            {
                var connectedSources = Connections
                    .Where(c => c.Target == inPort)
                    .Select(c => $"{c.Source.NodeOwner.Title} (\"{c.Source.DisplayName}\")")
                    .ToList();
                inPort.UpdateConnectionState(connectedSources.Count > 0, string.Join(", ", connectedSources));
            }

            foreach (var outPort in node.OutputPorts)
            {
                var connectedTargets = Connections
                    .Where(c => c.Source == outPort)
                    .Select(c => $"{c.Target.NodeOwner.Title} (\"{c.Target.DisplayName}\")")
                    .ToList();
                outPort.UpdateConnectionState(connectedTargets.Count > 0, string.Join(", ", connectedTargets));
            }
        }
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

    private int _maxZIndex = 0;

    public void BringToFront(NodeViewModel node)
    {
        if (node == null) return;
        node.ZIndex = ++_maxZIndex;
    }

    private void OnNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is NodeViewModel nodeVm && e.PropertyName == nameof(NodeViewModel.IsSelected) && nodeVm.IsSelected)
        {
            SelectedNode = nodeVm;
            BringToFront(nodeVm);
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

        node.PropertyChanged -= OnNodePropertyChanged;
        node.Dispose();
        Nodes.Remove(node);
    }

    private List<NodeViewModel> ResolveTargetNodes(object? parameter)
    {
        if (parameter is NodeViewModel singleNode)
        {
            if (singleNode.IsSelected)
            {
                var selected = Nodes.Where(n => n.IsSelected).ToList();
                if (selected.Count > 1 && selected.Contains(singleNode))
                {
                    return selected;
                }
            }
            return [singleNode];
        }

        var targets = Nodes.Where(n => n.IsSelected).ToList();
        if (targets.Count == 0 && SelectedNode != null)
        {
            targets.Add(SelectedNode);
        }
        return targets;
    }

    [RelayCommand]
    public void DeleteSelectedNodes(object? parameter = null)
    {
        var targets = ResolveTargetNodes(parameter);
        foreach (var node in targets)
        {
            RemoveNodeWithConnections(node);
        }
    }

    [RelayCommand]
    public void CopySelectedNodes(object? parameter = null)
    {
        var targets = ResolveTargetNodes(parameter);
        if (targets.Count > 0)
        {
            _clipboardService.Copy(targets, Connections);
        }
    }

    [RelayCommand]
    public void CutSelectedNodes(object? parameter = null)
    {
        var targets = ResolveTargetNodes(parameter);
        if (targets.Count > 0)
        {
            _clipboardService.Copy(targets, Connections);
            foreach (var node in targets)
            {
                RemoveNodeWithConnections(node);
            }
        }
    }

    [RelayCommand]
    public void PasteNodes(object? positionParam = null)
    {
        Point? targetPoint = null;
        if (positionParam is Point pt)
        {
            targetPoint = pt;
        }

        var newNodes = _clipboardService.Paste(this, targetPoint);
        if (newNodes.Count > 0)
        {
            SelectedNode = newNodes.Last();
        }
    }

    [RelayCommand]
    public void DuplicateSelectedNodes(object? parameter = null)
    {
        var targets = ResolveTargetNodes(parameter);
        if (targets.Count > 0)
        {
            var newNodes = _clipboardService.Duplicate(targets, Connections, this);
            if (newNodes.Count > 0)
            {
                SelectedNode = newNodes.Last();
            }
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
            node.PropertyChanged -= OnNodePropertyChanged;
            node.Dispose();
        }
        Nodes.Clear();
        Annotations.Clear();
        Groups.Clear();
        CanvasDecorators.Clear();
        SelectedNode = null;
    }

    public AnnotationViewModel AddAnnotation(Point? position = null, string title = "Nota", string content = "", string color = "#FEF08A")
    {
        var loc = position ?? new Point(Math.Max(50, -ViewportLocation.X + 150), Math.Max(50, -ViewportLocation.Y + 150));
        var annotation = new AnnotationViewModel(title, content, loc, color: color)
        {
            ParentEditor = this
        };
        Annotations.Add(annotation);
        CanvasDecorators.Add(annotation);
        return annotation;
    }

    [RelayCommand]
    public void AddNewAnnotation()
    {
        AddAnnotation();
    }

    [RelayCommand]
    public void DeleteAnnotation(AnnotationViewModel? annotation)
    {
        if (annotation != null)
        {
            Annotations.Remove(annotation);
            CanvasDecorators.Remove(annotation);
        }
    }

    public GroupViewModel AddGroup(Point? position = null, string title = "Grupo de Nodos", double width = 450, double height = 320, string color = "#3B82F6", IEnumerable<string>? nodeIds = null)
    {
        var loc = position ?? new Point(Math.Max(50, -ViewportLocation.X + 100), Math.Max(50, -ViewportLocation.Y + 100));
        var group = new GroupViewModel(title, loc, width, height, color, nodeIds)
        {
            ParentEditor = this
        };
        Groups.Add(group);
        CanvasDecorators.Insert(0, group);
        return group;
    }

    [RelayCommand]
    public void AddNewGroup()
    {
        AddGroup();
    }

    [RelayCommand]
    public void GroupSelectedNodes()
    {
        var selected = Nodes.Where(n => n.IsSelected).ToList();
        if (selected.Count == 0)
        {
            AddNewGroup();
            return;
        }

        double minX = selected.Min(n => n.Location.X) - 30;
        double minY = selected.Min(n => n.Location.Y) - 50;
        double maxX = selected.Max(n => n.Location.X + n.Width) + 30;
        double maxY = selected.Max(n => n.Location.Y + 250) + 30;

        AddGroup(new Point(minX, minY), "Grupo", Math.Max(300, maxX - minX), Math.Max(200, maxY - minY), "#3B82F6", selected.Select(n => n.Id));
    }

    [RelayCommand]
    public void DeleteGroup(GroupViewModel? group)
    {
        if (group != null)
        {
            Groups.Remove(group);
            CanvasDecorators.Remove(group);
        }
    }

    public NodeViewModel? AddNode(string nodeTypeName, Point position)
    {
        IFlowNode? nodeInstance = _pluginLoader.CreateNodeInstance(nodeTypeName);
        if (nodeInstance == null) return null;

        var nodeVm = new NodeViewModel(nodeInstance, position)
        {
            ParentEditor = this
        };
        nodeVm.PropertyChanged += OnNodePropertyChanged;
        Nodes.Add(nodeVm);
        UserPreferencesService.Instance.IncrementNodeUsage(nodeTypeName);
        return nodeVm;
    }

    public void ClearDebugStates()
    {
        foreach (var node in Nodes)
        {
            node.ClearDebugData();
        }
    }

    [RelayCommand]
    public void OpenWorkflowSettings()
    {
        try
        {
            var win = new Views.Components.WorkflowSettingsWindow(GlobalOutputDir);
            if (Application.Current?.MainWindow != null)
            {
                win.Owner = Application.Current.MainWindow;
            }
            if (win.ShowDialog() == true)
            {
                GlobalOutputDir = win.GlobalOutputDir;
            }
        }
        catch (Exception ex)
        {
            string msg = string.Format(_loc.GetString("Msg_OpenSettingsError", "Error al abrir la Configuración del Flujo: {0}"), ex.Message);
            string title = _loc.GetString("Error", "Error");
            _dialogService.ShowError(msg, title);
        }
    }

    [RelayCommand]
    public void BrowseGlobalOutputDir()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Seleccionar Ruta de Salida Global",
            InitialDirectory = GlobalOutputDir
        };

        if (dialog.ShowDialog() == true)
        {
            GlobalOutputDir = dialog.FolderName;
        }
    }

    public WorkflowGraph ExportToGraphModel(string name = "FileFlow Workflow")
    {
        return WorkflowGraphSerializer.Export(Nodes, Connections, GlobalOutputDir, name, Annotations, Groups);
    }

    public void LoadFromGraphModel(WorkflowGraph graph)
    {
        ClearGraph();

        if (!string.IsNullOrWhiteSpace(graph.GlobalOutputDir))
        {
            GlobalOutputDir = graph.GlobalOutputDir;
        }

        WorkflowGraphSerializer.Import(
            graph,
            _pluginLoader,
            this,
            registerNodeCallback: nodeVm =>
            {
                nodeVm.PropertyChanged += OnNodePropertyChanged;
                Nodes.Add(nodeVm);
            },
            registerConnectionCallback: conn =>
            {
                Connections.Add(conn);
            },
            registerAnnotationCallback: annotVm =>
            {
                Annotations.Add(annotVm);
                CanvasDecorators.Add(annotVm);
            },
            registerGroupCallback: groupVm =>
            {
                Groups.Add(groupVm);
                CanvasDecorators.Insert(0, groupVm);
            }
        );
    }

    public List<FileFlow.App.Models.VariableGroupItem> GetUpstreamAvailableVariables(NodeViewModel targetNode)
    {
        return _variableDiscoveryService.GetAvailableVariables(targetNode, Connections);
    }

    [RelayCommand]
    public void OpenSubWorkflow(NodeViewModel node)
    {
        if (node == null) return;

        // Save current graph state into breadcrumb
        var currentGraph = ExportToGraphModel();
        Breadcrumbs.Add(new BreadcrumbItem(CurrentWorkflowTitle, node.Id, currentGraph));

        CurrentWorkflowTitle = node.Title;

        // Load inner graph if exists, or start fresh sub-graph
        if (!string.IsNullOrWhiteSpace(node.InnerGraphJson))
        {
            try
            {
                var innerGraph = System.Text.Json.JsonSerializer.Deserialize<WorkflowGraph>(node.InnerGraphJson);
                if (innerGraph != null)
                {
                    LoadFromGraphModel(innerGraph);
                    return;
                }
            }
            catch
            {
                // Fallback to clear
            }
        }

        ClearGraph();
    }

    [RelayCommand]
    public void NavigateBreadcrumb(BreadcrumbItem target)
    {
        if (target == null) return;

        int index = Breadcrumbs.IndexOf(target);
        if (index < 0) return;

        // Restore target graph
        LoadFromGraphModel(target.Graph);
        CurrentWorkflowTitle = target.Name;

        // Remove all subsequent breadcrumbs
        while (Breadcrumbs.Count > index)
        {
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }
    }

    public void UpdateEdgeDispatched(string sourceNodeId, string portName, int count)
    {
        string key = $"{sourceNodeId}:{portName}";
        if (_connectionLookup.TryGetValue(key, out var list))
        {
            foreach (var conn in list)
            {
                conn.UpdateCount(count);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _userPreferencesService.PreferencesChanged -= _preferencesChangedHandler;
        GC.SuppressFinalize(this);
    }
}
