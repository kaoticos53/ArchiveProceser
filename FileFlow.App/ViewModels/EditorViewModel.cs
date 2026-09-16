using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
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
    private readonly Action _preferencesChangedHandler;

    public Services.INodeClipboardService ClipboardService => _clipboardService;
    public Services.IVariableDiscoveryService VariableDiscoveryService => _variableDiscoveryService;

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
        IDialogService? dialogService = null)
    {
        _pluginLoader = pluginLoader;
        _variableDiscoveryService = variableDiscoveryService ?? new Services.VariableDiscoveryService();
        _clipboardService = clipboardService ?? new Services.NodeClipboardService(_pluginLoader);
        _userPreferencesService = userPreferencesService ?? UserPreferencesService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;
        _dialogService = dialogService ?? AvaloniaDialogService.Instance;
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
        PendingConnection = null;
        ClearPortCompatibilityHighlight();
    }

    [RelayCommand]
    public void CancelConnection()
    {
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
                return;
            }
        }

        if (port != null)
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
        var types = _pluginLoader.DiscoveredNodeTypes.Values.Distinct().ToList();
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
