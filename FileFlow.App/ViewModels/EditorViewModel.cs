using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.Services.UndoRedo;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Material.Icons;

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
    private readonly IUndoRedoService _undoRedoService;
    private readonly Action _preferencesChangedHandler;

    public Services.INodeClipboardService ClipboardService => _clipboardService;
    public Services.IVariableDiscoveryService VariableDiscoveryService => _variableDiscoveryService;
    public IUndoRedoService UndoRedoService => _undoRedoService;

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = [];
    public ObservableCollection<AnnotationViewModel> Annotations { get; } = [];
    public ObservableCollection<GroupViewModel> Groups { get; } = [];
    public ObservableCollection<object> CanvasDecorators { get; } = [];

    // --- Spotlight Quick-Add Search ---
    [ObservableProperty]
    private bool _isSpotlightOpen;

    [ObservableProperty]
    private string _spotlightSearchText = string.Empty;

    [ObservableProperty]
    private Point _spotlightScreenPosition = new(200, 200);

    [ObservableProperty]
    private Point _spotlightCanvasPosition = new(200, 200);

    [ObservableProperty]
    private NodeToolboxItem? _selectedSpotlightItem;

    public ObservableCollection<NodeToolboxItem> FilteredSpotlightItems { get; } = [];
    private readonly List<NodeToolboxItem> _allSpotlightItems = [];

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

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private int _selectedNodesCount;

    public int TotalNodesCount => Nodes.Count;
    public int ConnectionsCount => Connections.Count;
    public string FormattedLocation => $"{ViewportLocation.X:F1}, {ViewportLocation.Y:F1}";
    public string FormattedZoom => $"{ViewportZoom:F2}x";

    partial void OnViewportLocationChanged(Point value)
    {
        OnPropertyChanged(nameof(FormattedLocation));
    }

    partial void OnViewportZoomChanged(double value)
    {
        OnPropertyChanged(nameof(FormattedZoom));
    }

    public void UpdateSelectedCount()
    {
        SelectedNodesCount = Nodes.Count(n => n.IsSelected);
    }

    private readonly Dictionary<string, List<ConnectionViewModel>> _connectionLookup = new(StringComparer.OrdinalIgnoreCase);

    public EditorViewModel(
        PluginLoader pluginLoader,
        Services.IVariableDiscoveryService? variableDiscoveryService = null,
        Services.INodeClipboardService? clipboardService = null,
        IUserPreferencesService? userPreferencesService = null,
        ILocalizationService? localizationService = null,
        IDialogService? dialogService = null,
        IUndoRedoService? undoRedoService = null)
    {
        _pluginLoader = pluginLoader;
        _variableDiscoveryService = variableDiscoveryService ?? new Services.VariableDiscoveryService();
        _clipboardService = clipboardService ?? new Services.NodeClipboardService(_pluginLoader);
        _userPreferencesService = userPreferencesService ?? UserPreferencesService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;
        _dialogService = dialogService ?? AvaloniaDialogService.Instance;
        _undoRedoService = undoRedoService ?? new UndoRedoService();
        _globalOutputDir = _userPreferencesService.Preferences.DefaultGlobalOutputDir;
        _preferencesChangedHandler = () =>
        {
            GlobalOutputDir = _userPreferencesService.Preferences.DefaultGlobalOutputDir;
        };
        _userPreferencesService.PreferencesChanged += _preferencesChangedHandler;

        _undoRedoService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IUndoRedoService.CanUndo) || e.PropertyName == nameof(IUndoRedoService.CanRedo))
            {
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        };

        Connections.CollectionChanged += (s, e) =>
        {
            RebuildConnectionLookup();
            UpdatePortConnectionStates();
            RefreshAllNodeFileVersions();
            OnPropertyChanged(nameof(ConnectionsCount));
        };
        Nodes.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (NodeViewModel node in e.NewItems)
                {
                    node.PropertyChanged += (ns, ne) =>
                    {
                        if (ne.PropertyName == nameof(NodeViewModel.IsSelected))
                        {
                            UpdateSelectedCount();
                        }
                    };
                }
            }
            UpdatePortConnectionStates();
            RefreshAllNodeFileVersions();
            OnPropertyChanged(nameof(TotalNodesCount));
            UpdateSelectedCount();
        };
    }

    public bool CanUndo => _undoRedoService.CanUndo;
    public bool CanRedo => _undoRedoService.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo()
    {
        if (_undoRedoService.CanUndo)
        {
            _undoRedoService.Undo();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo()
    {
        if (_undoRedoService.CanRedo)
        {
            _undoRedoService.Redo();
        }
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

        if (Connections.Any(c => c.Source == outputPort && c.Target == inputPort))
            return;

        using var tx = _undoRedoService.BeginTransaction($"Conectar {outputPort.NodeOwner.Title} -> {inputPort.NodeOwner.Title}");

        // Remove any existing connection to the same input port
        var existing = Connections.FirstOrDefault(c => c.Target == inputPort);
        if (existing != null)
        {
            Connections.Remove(existing);
            _undoRedoService.Record(new DeleteConnectionAction(this, existing));
        }

        var newConn = new ConnectionViewModel(outputPort, inputPort);
        Connections.Add(newConn);
        _undoRedoService.Record(new AddConnectionAction(this, newConn));
    }

    private static (PortViewModel? Source, PortViewModel? Target) ExtractPortsFromParameter(object? param)
    {
        if (param is PortViewModel singlePort)
        {
            return (null, singlePort);
        }

        if (param is System.Runtime.CompilerServices.ITuple tuple && tuple.Length > 0)
        {
            PortViewModel? p1 = tuple[0] as PortViewModel;
            PortViewModel? p2 = tuple.Length > 1 ? tuple[1] as PortViewModel : null;
            return (p1, p2);
        }

        return (null, null);
    }

    [RelayCommand]
    public void StartConnection(object? source)
    {
        var (p1, p2) = ExtractPortsFromParameter(source);
        var port = p1 ?? p2;
        if (port != null)
        {
            PendingConnection = new PendingConnectionViewModel(port);
            ApplyPortCompatibilityHighlight(port);
        }
    }

    [RelayCommand]
    public void FinishConnection(object? target)
    {
        var (p1, p2) = ExtractPortsFromParameter(target);
        PortViewModel? sourcePort = p1 ?? PendingConnection?.Source;
        PortViewModel? targetPort = p2;

        if (p1 != null && p2 == null)
        {
            if (PendingConnection?.Source != null && PendingConnection.Source != p1)
            {
                sourcePort = PendingConnection.Source;
                targetPort = p1;
            }
            else
            {
                targetPort = p1;
            }
        }

        if (sourcePort != null && targetPort != null && sourcePort != targetPort)
        {
            CreateConnection(sourcePort, targetPort);
        }
        if (PendingConnection != null)
        {
            PendingConnection.IsVisible = false;
        }
        PendingConnection = null;
        ClearPortCompatibilityHighlight();
    }

    [RelayCommand]
    public void CancelConnection()
    {
        if (PendingConnection != null)
        {
            PendingConnection.IsVisible = false;
        }
        PendingConnection = null;
        ClearPortCompatibilityHighlight();
    }

    /// <summary>
    /// Marca cada puerto del lienzo con su compatibilidad respecto al puerto que se está arrastrando, para
    /// que la tarjeta pueda resaltar los destinos válidos y atenuar el resto mientras se dibuja el cable.
    /// </summary>
    private void ApplyPortCompatibilityHighlight(PortViewModel source)
    {
        foreach (var port in AllPorts())
        {
            port.IsDragActive = true;
            port.ApplyDragHighlight(source);
        }
    }

    /// <summary>Devuelve todos los puertos del lienzo al estado de reposo (fin o cancelación del arrastre).</summary>
    public void ClearPortCompatibilityHighlight()
    {
        foreach (var port in AllPorts())
        {
            port.ClearDragHighlight();
        }
    }

    private IEnumerable<PortViewModel> AllPorts()
        => Nodes.SelectMany(n => n.InputPorts.Concat(n.OutputPorts));

    [RelayCommand]
    public void DisconnectConnector(object? connector)
    {
        var (p1, p2) = ExtractPortsFromParameter(connector);
        var port = p1 ?? p2;

        if (p1 != null && p2 != null)
        {
            var specificConn = Connections.FirstOrDefault(c => (c.Source == p1 && c.Target == p2) || (c.Source == p2 && c.Target == p1));
            if (specificConn != null)
            {
                Connections.Remove(specificConn);
                _undoRedoService.Record(new DeleteConnectionAction(this, specificConn));
                return;
            }
        }

        if (port != null)
        {
            var removeList = Connections.Where(c => c.Source == port || c.Target == port).ToList();
            if (removeList.Count > 0)
            {
                using var tx = _undoRedoService.BeginTransaction("Desconectar puerto");
                foreach (var conn in removeList)
                {
                    Connections.Remove(conn);
                    _undoRedoService.Record(new DeleteConnectionAction(this, conn));
                }
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
            _undoRedoService.Record(new DeleteConnectionAction(this, conn));
        }
    }

    private int _maxZIndex = 0;

    public void BringToFront(NodeViewModel node)
    {
        if (node == null) return;
        if (node.ZIndex == _maxZIndex && _maxZIndex > 0) return;
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
        if (node == null) return;
        var relatedConnections = Connections
            .Where(c => c.Source.NodeOwner == node || c.Target.NodeOwner == node)
            .ToList();

        foreach (var conn in relatedConnections)
        {
            Connections.Remove(conn);
        }

        node.PropertyChanged -= OnNodePropertyChanged;
        Nodes.Remove(node);
        _undoRedoService.Record(new DeleteNodesAction(this, [node], relatedConnections));
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
        if (targets.Count == 0) return;

        var targetIds = new HashSet<string>(targets.Select(t => t.Id), StringComparer.OrdinalIgnoreCase);
        var relatedConnections = Connections
            .Where(c => targetIds.Contains(c.Source.NodeOwner.Id) || targetIds.Contains(c.Target.NodeOwner.Id))
            .ToList();

        foreach (var conn in relatedConnections)
        {
            Connections.Remove(conn);
        }

        foreach (var node in targets)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            Nodes.Remove(node);
        }

        _undoRedoService.Record(new DeleteNodesAction(this, targets, relatedConnections));
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
            DeleteSelectedNodes(targets.Count == 1 ? targets[0] : null);
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

        using var tx = _undoRedoService.BeginTransaction("Pegar Nodos");
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
            using var tx = _undoRedoService.BeginTransaction("Duplicar Nodos");
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
        _undoRedoService.Clear();
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
        _undoRedoService.Record(new AddAnnotationAction(this, annotation));
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
            _undoRedoService.Record(new DeleteAnnotationAction(this, annotation));
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
        _undoRedoService.Record(new AddGroupAction(this, group));
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
            _undoRedoService.Record(new DeleteGroupAction(this, group));
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
        _undoRedoService.Record(new AddNodesAction(this, [nodeVm]));
        return nodeVm;
    }

    public void ClearDebugStates()
    {
        foreach (var node in Nodes)
        {
            node.ClearDebugData();
        }
    }

    public void ResetAllNodeMetrics()
    {
        foreach (var node in Nodes)
        {
            node.UpdateTelemetryStats(FileFlow.Sdk.Telemetry.NodeTelemetryStats.Empty(node.Id));
        }
    }

    [RelayCommand]
    public async Task OpenWorkflowSettings()
    {
        try
        {
            var win = new Views.Components.WorkflowSettingsWindow(GlobalOutputDir);
            var result = App.MainWindow != null ? await win.ShowDialog<bool>(App.MainWindow) : false;
            if (result)
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
        var fileDialogService = new FileDialogService();
        var selectedFolder = fileDialogService.ShowFolderBrowserDialog("Seleccionar Ruta de Salida Global");
        if (!string.IsNullOrWhiteSpace(selectedFolder))
        {
            GlobalOutputDir = selectedFolder;
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

        RefreshAllNodeFileVersions();
        _undoRedoService.Clear();
    }

    public void RefreshAllNodeFileVersions()
    {
        foreach (var node in Nodes)
        {
            foreach (var param in node.Parameters)
            {
                if (param.IsFileVersionSelector)
                {
                    param.RefreshAvailableVersions();
                }
            }
        }
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
                PulseConnectionEnergy(conn);
            }
        }
    }

    /// <summary>
    /// Energiza un cable durante unos instantes (flujo de energía animado mientras los datos viajan) y lo
    /// devuelve a reposo. Reutiliza un único temporizador por cable para que ráfagas consecutivas no dejen
    /// animaciones colgadas.
    /// </summary>
    public void PulseConnectionEnergy(ConnectionViewModel connection, int durationMs = 900)
    {
        if (durationMs <= 0)
        {
            connection.IsExecuting = false;
            return;
        }

        connection.LastDispatchedCount++;
        connection.IsExecuting = true;

        int generation = connection.LastDispatchedCount;
        _ = Task.Delay(durationMs).ContinueWith(
            _ => RunOnUiThread(() => CompleteConnectionPulse(connection, generation)),
            TaskScheduler.Default);
    }

    /// <summary>
    /// Apaga la energía de un cable al vencer su pulso... salvo que ya haya empezado otro más reciente. La
    /// comparación de generación es lo que evita que una ráfaga de datos deje el cable apagado antes de
    /// tiempo (o encendido para siempre) cuando los pulsos se solapan.
    /// </summary>
    public static void CompleteConnectionPulse(ConnectionViewModel connection, int generation)
    {
        if (connection.LastDispatchedCount == generation)
        {
            connection.IsExecuting = false;
        }
    }

    /// <summary>Apaga el flujo de energía de todos los cables (fin de ejecución o parada).</summary>
    public void ClearConnectionEnergy()
    {
        foreach (var connection in Connections)
        {
            connection.IsExecuting = false;
        }
    }

    private static void RunOnUiThread(Action action)
    {
        try
        {
            if (Application.Current is null)
            {
                action();
                return;
            }

            Dispatcher.UIThread.Post(action);
        }
        catch
        {
            // Sin ciclo de vida de UI (pruebas, apagado): el cambio de estado no es crítico.
            action();
        }
    }

    public void PopulateSpotlightItems()
    {
        _allSpotlightItems.Clear();
        var types = _pluginLoader.UniqueNodeTypes.ToList();
        foreach (var type in types)
        {
            string typeName = type.FullName ?? type.Name;
            IFlowNode? sampleInstance = null;
            try
            {
                sampleInstance = _pluginLoader.CreateNodeInstance(typeName);
            }
            catch { }

            var defAttr = type.GetCustomAttribute<NodeDefinitionAttribute>();
            string name = _loc.GetString(type.Name + "_Name", sampleInstance?.Name ?? defAttr?.Name ?? type.Name);
            if (name.EndsWith("_Name", StringComparison.OrdinalIgnoreCase) && sampleInstance != null && !string.IsNullOrWhiteSpace(sampleInstance.Name))
            {
                name = sampleInstance.Name;
            }

            string category = sampleInstance?.Category ?? defAttr?.Category ?? "General";
            string locCategory = _loc.GetString($"Category_{category}", category);

            string description = _loc.GetString(type.Name + "_Desc", sampleInstance?.Description ?? defAttr?.Description ?? string.Empty);
            if (description.EndsWith("_Desc", StringComparison.OrdinalIgnoreCase) && sampleInstance != null && !string.IsNullOrWhiteSpace(sampleInstance.Description))
            {
                description = sampleInstance.Description;
            }

            MaterialIconKind icon = NodeIconResolver.GetIconForNodeType(typeName);
            var role = defAttr?.Role ?? PipelineRole.Transform;
            var tags = defAttr?.Tags ?? Array.Empty<string>();
            var subCategory = defAttr?.SubCategory ?? string.Empty;
            string localizedRole = _loc.GetString($"Role_{role}", role.ToString());

            var item = new NodeToolboxItem(
                name,
                locCategory,
                description,
                typeName,
                icon,
                false,
                0,
                role,
                tags,
                subCategory,
                localizedRole
            );
            _allSpotlightItems.Add(item);
        }
        UpdateFilteredSpotlightItems();
    }

    partial void OnSpotlightSearchTextChanged(string value)
    {
        UpdateFilteredSpotlightItems();
    }

    private void UpdateFilteredSpotlightItems()
    {
        FilteredSpotlightItems.Clear();
        var query = SpotlightSearchText?.Trim() ?? string.Empty;
        var matches = string.IsNullOrEmpty(query)
            ? _allSpotlightItems
            : _allSpotlightItems.Where(i =>
                i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (i.Tags != null && i.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))));

        foreach (var item in matches.Take(30))
        {
            FilteredSpotlightItems.Add(item);
        }

        SelectedSpotlightItem = FilteredSpotlightItems.FirstOrDefault();
    }

    [RelayCommand]
    public void OpenSpotlight(Point? canvasPosition = null)
    {
        PopulateSpotlightItems();
        SpotlightCanvasPosition = canvasPosition ?? new Point(
            ViewportLocation.X + (ViewportSize.Width > 0 ? (ViewportSize.Width / (2 * (ViewportZoom > 0 ? ViewportZoom : 1.0))) : 200),
            ViewportLocation.Y + (ViewportSize.Height > 0 ? (ViewportSize.Height / (2 * (ViewportZoom > 0 ? ViewportZoom : 1.0))) : 200)
        );
        SpotlightSearchText = string.Empty;
        IsSpotlightOpen = true;
    }

    [RelayCommand]
    public void CloseSpotlight()
    {
        IsSpotlightOpen = false;
        SpotlightSearchText = string.Empty;
    }

    public bool HasBreadcrumbs => Breadcrumbs.Count > 1;

    public void InitializeBreadcrumbs()
    {
        Breadcrumbs.Clear();
        var rootGraph = WorkflowGraphSerializer.Export(Nodes, Connections, GlobalOutputDir, CurrentWorkflowTitle, Annotations, Groups);
        Breadcrumbs.Add(new BreadcrumbItem(CurrentWorkflowTitle, null, rootGraph));
        OnPropertyChanged(nameof(HasBreadcrumbs));
    }

    [RelayCommand]
    public void OpenSubflow(NodeViewModel subflowNodeVm)
    {
        if (subflowNodeVm == null) return;

        // Si la lista de breadcrumbs está vacía, inicializar la raíz
        if (Breadcrumbs.Count == 0)
        {
            var rootGraph = WorkflowGraphSerializer.Export(Nodes, Connections, GlobalOutputDir, CurrentWorkflowTitle, Annotations, Groups);
            Breadcrumbs.Add(new BreadcrumbItem(CurrentWorkflowTitle, null, rootGraph));
        }
        else
        {
            // Guardar el estado actual en el breadcrumb superior
            var currentGraph = WorkflowGraphSerializer.Export(Nodes, Connections, GlobalOutputDir, CurrentWorkflowTitle, Annotations, Groups);
            var top = Breadcrumbs.Last();
            int topIndex = Breadcrumbs.Count - 1;
            Breadcrumbs[topIndex] = new BreadcrumbItem(top.Name, top.NodeId, currentGraph);
        }

        // Resolver el grafo del subflujo
        WorkflowGraph? innerGraph = null;
        if (subflowNodeVm.NodeInstance is ISubflowNode sn)
        {
            if (sn.EmbedDefinition && !string.IsNullOrWhiteSpace(sn.SubflowDefinitionJson))
            {
                innerGraph = WorkflowGraph.FromJson(sn.SubflowDefinitionJson);
            }
            else if (!string.IsNullOrWhiteSpace(sn.SubflowDefinitionJson))
            {
                try { innerGraph = WorkflowGraph.FromJson(sn.SubflowDefinitionJson); } catch { }
            }

            if (innerGraph == null && !string.IsNullOrWhiteSpace(sn.SubflowPath) && File.Exists(sn.SubflowPath))
            {
                try
                {
                    string json = File.ReadAllText(sn.SubflowPath);
                    innerGraph = WorkflowGraph.FromJson(json);
                }
                catch { }
            }
        }

        // Si no tiene grafo interno aún, crear uno predeterminado con SubflowInputNode y SubflowOutputNode
        if (innerGraph == null || innerGraph.Nodes.Count == 0)
        {
            innerGraph = new WorkflowGraph
            {
                Name = subflowNodeVm.Title,
                GlobalOutputDir = GlobalOutputDir
            };

            var inputNode = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                NodeTypeName = "FileFlow.Plugin.Subflows.SubflowInputNode",
                CustomTitle = "Entrada",
                X = 100,
                Y = 200,
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "In" }
            };

            var outputNode = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                NodeTypeName = "FileFlow.Plugin.Subflows.SubflowOutputNode",
                CustomTitle = "Salida",
                X = 600,
                Y = 200,
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "Out" }
            };

            innerGraph.Nodes.Add(inputNode);
            innerGraph.Nodes.Add(outputNode);
        }

        // Limpiar el lienzo actual e importar el grafo interno
        ClearCanvas();
        WorkflowGraphSerializer.Import(
            innerGraph,
            _pluginLoader,
            this,
            n => Nodes.Add(n),
            c => Connections.Add(c),
            a => Annotations.Add(a),
            g => Groups.Add(g));

        CurrentWorkflowTitle = subflowNodeVm.Title;
        Breadcrumbs.Add(new BreadcrumbItem(subflowNodeVm.Title, subflowNodeVm.Id, innerGraph));
        OnPropertyChanged(nameof(HasBreadcrumbs));
        FitToScreen();
    }

    [RelayCommand]
    public void NavigateToBreadcrumb(BreadcrumbItem targetItem)
    {
        if (targetItem == null || Breadcrumbs.Count == 0) return;
        if (Breadcrumbs.LastOrDefault() == targetItem) return;

        int targetIndex = Breadcrumbs.IndexOf(targetItem);
        if (targetIndex < 0) return;

        // Guardar el grafo actual del nivel que abandonamos
        var currentLevelGraph = WorkflowGraphSerializer.Export(Nodes, Connections, GlobalOutputDir, CurrentWorkflowTitle, Annotations, Groups);
        var currentTop = Breadcrumbs.Last();

        // Si el nivel que abandonamos correspondía a un nodo de subflujo, actualizar su SubflowDefinitionJson en el grafo padre
        if (!string.IsNullOrWhiteSpace(currentTop.NodeId) && Breadcrumbs.Count >= 2)
        {
            var parentBreadcrumb = Breadcrumbs[Breadcrumbs.Count - 2];
            var targetNodeDto = parentBreadcrumb.Graph.Nodes.FirstOrDefault(n => n.Id == currentTop.NodeId);
            if (targetNodeDto != null)
            {
                targetNodeDto.Parameters["SubflowDefinitionJson"] = currentLevelGraph.ToJson();
                targetNodeDto.Parameters["EmbedDefinition"] = true;
            }
        }

        // Eliminar todos los breadcrumbs posteriores al targetIndex
        while (Breadcrumbs.Count > targetIndex + 1)
        {
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }

        // Cargar el grafo del target
        ClearCanvas();
        WorkflowGraphSerializer.Import(
            targetItem.Graph,
            _pluginLoader,
            this,
            n => Nodes.Add(n),
            c => Connections.Add(c),
            a => Annotations.Add(a),
            g => Groups.Add(g));

        CurrentWorkflowTitle = targetItem.Name;
        OnPropertyChanged(nameof(HasBreadcrumbs));
        FitToScreen();
    }

    private void ClearCanvas()
    {
        Connections.Clear();
        Nodes.Clear();
        Annotations.Clear();
        Groups.Clear();
        CanvasDecorators.Clear();
    }

    [RelayCommand]
    public void CollapseSelectionToSubflow()
    {
        var selectedNodes = Nodes.Where(n => n.IsSelected).ToList();
        if (selectedNodes.Count == 0) return;

        using var tx = _undoRedoService.BeginTransaction("Colapsar a Subflujo");

        var selectedSet = selectedNodes.ToHashSet();

        // Identificar conexiones entrantes (desde nodos externos hacia la selección)
        var incomingConns = Connections
            .Where(c => selectedSet.Contains(c.Target.NodeOwner) && !selectedSet.Contains(c.Source.NodeOwner))
            .ToList();

        // Identificar conexiones salientes (desde la selección hacia nodos externos)
        var outgoingConns = Connections
            .Where(c => selectedSet.Contains(c.Source.NodeOwner) && !selectedSet.Contains(c.Target.NodeOwner))
            .ToList();

        // Identificar conexiones internas
        var internalConns = Connections
            .Where(c => selectedSet.Contains(c.Source.NodeOwner) && selectedSet.Contains(c.Target.NodeOwner))
            .ToList();

        var inPortNames = incomingConns.Select(c => c.Target.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (inPortNames.Count == 0) inPortNames.Add("In");

        var outPortNames = outgoingConns.Select(c => c.Source.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (outPortNames.Count == 0) outPortNames.Add("Out");

        // Coordenadas para calcular el centro
        double minX = selectedNodes.Min(n => n.Location.X);
        double minY = selectedNodes.Min(n => n.Location.Y);
        double maxX = selectedNodes.Max(n => n.Location.X);
        double maxY = selectedNodes.Max(n => n.Location.Y);
        double centerX = (minX + maxX) / 2.0;
        double centerY = (minY + maxY) / 2.0;

        // Construir el subgrafo interno
        var subflowGraph = new WorkflowGraph
        {
            Name = "Subflujo Compuesto",
            GlobalOutputDir = GlobalOutputDir
        };

        var inputBoundary = new WorkflowNode
        {
            Id = Guid.NewGuid().ToString(),
            NodeTypeName = "FileFlow.Plugin.Subflows.SubflowInputNode",
            CustomTitle = "Entrada",
            X = minX - 300,
            Y = minY,
            Parameters = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PortNames"] = string.Join(";", inPortNames)
            }
        };
        subflowGraph.Nodes.Add(inputBoundary);

        var outputBoundary = new WorkflowNode
        {
            Id = Guid.NewGuid().ToString(),
            NodeTypeName = "FileFlow.Plugin.Subflows.SubflowOutputNode",
            CustomTitle = "Salida",
            X = maxX + 300,
            Y = minY,
            Parameters = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PortNames"] = string.Join(";", outPortNames)
            }
        };
        subflowGraph.Nodes.Add(outputBoundary);

        // Añadir nodos seleccionados al subgrafo
        foreach (var node in selectedNodes)
        {
            var nodeDto = new WorkflowNode
            {
                Id = node.Id,
                NodeTypeName = node.NodeTypeName,
                CustomTitle = node.CustomTitle,
                X = node.Location.X,
                Y = node.Location.Y,
                HasBreakpoint = node.HasBreakpoint,
                IsLoggingEnabled = node.IsLoggingEnabled,
                Parameters = node.Parameters
                    .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                    .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase)
            };
            subflowGraph.Nodes.Add(nodeDto);
        }

        // Añadir aristas internas
        foreach (var c in internalConns)
        {
            subflowGraph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = c.Source.NodeOwner.Id,
                SourcePortName = c.Source.Name,
                TargetNodeId = c.Target.NodeOwner.Id,
                TargetPortName = c.Target.Name
            });
        }

        // Conectar SubflowInputNode a los nodos internos destino
        foreach (var inConn in incomingConns)
        {
            subflowGraph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = inputBoundary.Id,
                SourcePortName = inConn.Target.Name,
                TargetNodeId = inConn.Target.NodeOwner.Id,
                TargetPortName = inConn.Target.Name
            });
        }

        // Conectar los nodos internos origen a SubflowOutputNode
        foreach (var outConn in outgoingConns)
        {
            subflowGraph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = outConn.Source.NodeOwner.Id,
                SourcePortName = outConn.Source.Name,
                TargetNodeId = outputBoundary.Id,
                TargetPortName = outConn.Source.Name
            });
        }

        string subflowJson = subflowGraph.ToJson();

        // Crear la instancia del nodo SubflowNode en el lienzo padre
        IFlowNode? subflowInstance = _pluginLoader.CreateNodeInstance("FileFlow.Plugin.Subflows.SubflowNode")
                                  ?? _pluginLoader.CreateNodeInstance("SubflowNode");
        if (subflowInstance == null) return;

        subflowInstance.Parameters["EmbedDefinition"] = true;
        subflowInstance.Parameters["SubflowDefinitionJson"] = subflowJson;
        subflowInstance.Parameters["SubflowName"] = "Subflujo Compuesto";

        if (subflowInstance is ISubflowNode snNode)
        {
            snNode.RefreshDynamicPorts(inPortNames, outPortNames);
        }

        var subflowNodeVm = new NodeViewModel(subflowInstance, new Point(centerX, centerY))
        {
            ParentEditor = this,
            Title = "Subflujo Compuesto"
        };
        subflowNodeVm.SyncSubflowPorts();

        // 1. Eliminar conexiones incidentes de los nodos seleccionados
        var allIncidentConns = Connections
            .Where(c => selectedSet.Contains(c.Source.NodeOwner) || selectedSet.Contains(c.Target.NodeOwner))
            .ToList();

        foreach (var c in allIncidentConns)
        {
            _undoRedoService.Record(new DeleteConnectionAction(this, c));
            Connections.Remove(c);
        }

        // 2. Eliminar nodos seleccionados
        _undoRedoService.Record(new DeleteNodesAction(this, selectedNodes, allIncidentConns));
        foreach (var n in selectedNodes)
        {
            Nodes.Remove(n);
        }

        // 3. Añadir el nuevo nodo subflujo
        _undoRedoService.Record(new AddNodesAction(this, [subflowNodeVm]));
        Nodes.Add(subflowNodeVm);

        // 4. Reconectar aristas externas al nuevo nodo subflujo
        foreach (var inConn in incomingConns)
        {
            var targetPort = subflowNodeVm.InputPorts.FirstOrDefault(p => p.Name.Equals(inConn.Target.Name, StringComparison.OrdinalIgnoreCase))
                             ?? subflowNodeVm.InputPorts.FirstOrDefault();
            if (targetPort != null)
            {
                var newConn = new ConnectionViewModel(inConn.Source, targetPort);
                _undoRedoService.Record(new AddConnectionAction(this, newConn));
                Connections.Add(newConn);
            }
        }

        foreach (var outConn in outgoingConns)
        {
            var sourcePort = subflowNodeVm.OutputPorts.FirstOrDefault(p => p.Name.Equals(outConn.Source.Name, StringComparison.OrdinalIgnoreCase))
                             ?? subflowNodeVm.OutputPorts.FirstOrDefault();
            if (sourcePort != null)
            {
                var newConn = new ConnectionViewModel(sourcePort, outConn.Target);
                _undoRedoService.Record(new AddConnectionAction(this, newConn));
                Connections.Add(newConn);
            }
        }

        subflowNodeVm.IsSelected = true;
    }

    [RelayCommand]
    public void ConfirmSpotlightSelection()
    {
        if (SelectedSpotlightItem != null)
        {
            AddNode(SelectedSpotlightItem.TypeName, SpotlightCanvasPosition);
            CloseSpotlight();
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
