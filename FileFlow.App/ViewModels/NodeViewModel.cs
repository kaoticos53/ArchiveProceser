using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FileFlow.App.Messages;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public partial class NodeViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IFlowNode _nodeInstance;
    private readonly NodeParameterManager _parameterManager;
    private readonly IPortTopologyNode? _portTopologyNode;

    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    public EditorViewModel? ParentEditor { get; set; }

    [ObservableProperty]
    private string _title = "Node";

    partial void OnTitleChanged(string value)
    {
        if (_nodeInstance != null)
        {
            if (string.Equals(value, _nodeInstance.Name, StringComparison.OrdinalIgnoreCase))
            {
                CustomTitle = null;
            }
            else
            {
                CustomTitle = value;
            }
        }
    }

    [ObservableProperty]
    private string? _customTitle;

    [ObservableProperty]
    private bool _isEditingTitle;

    [ObservableProperty]
    private string _editingTitleText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icon))]
    private string _category = "General";

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icon))]
    private string _nodeTypeName = string.Empty;

    [ObservableProperty]
    private Point _location;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _zIndex = 0;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value && ParentEditor != null)
        {
            ParentEditor.BringToFront(this);
        }
    }

    [RelayCommand]
    public void InspectNode()
    {
        IsSelected = true;
        WeakReferenceMessenger.Default.Send(new NodeSelectedMessage(this, autoOpenInspector: true));
    }

    [RelayCommand]
    public void StartRenaming()
    {
        EditingTitleText = Title;
        IsEditingTitle = true;
    }

    [RelayCommand]
    public void CommitTitleRename()
    {
        if (!IsEditingTitle) return;

        string trimmed = EditingTitleText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) || string.Equals(trimmed, _nodeInstance.Name, StringComparison.OrdinalIgnoreCase))
        {
            CustomTitle = null;
            Title = _nodeInstance.Name;
        }
        else
        {
            CustomTitle = trimmed;
            Title = trimmed;
        }

        IsEditingTitle = false;
    }

    [RelayCommand]
    public void CancelTitleRename()
    {
        EditingTitleText = Title;
        IsEditingTitle = false;
    }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private double _collapsedWidth = 200;

    [ObservableProperty]
    private double _expandedWidth = 340;

    [ObservableProperty]
    private double _width = 200;

    [ObservableProperty]
    private double _maxWidth = 600;

    [ObservableProperty]
    private string _headerColor = "#202430";

    [ObservableProperty]
    private string _accentColor = "#818CF8";

    [ObservableProperty]
    private bool _hasBreakpoint;

    [ObservableProperty]
    private bool _isLoggingEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExecuting))]
    [NotifyPropertyChangedFor(nameof(IsFaulted))]
    [NotifyPropertyChangedFor(nameof(ExecutionStatusText))]
    private NodeExecutionStatus _executionStatus = NodeExecutionStatus.Idle;

    [ObservableProperty]
    private bool _isLedOn;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _progressMessage = string.Empty;

    [ObservableProperty]
    private bool _isProgressActive;

    [ObservableProperty]
    private bool _isGpuAccelerated;

    [ObservableProperty]
    private string _detailedMetricsToolTip = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTelemetry))]
    [NotifyPropertyChangedFor(nameof(HasRamTelemetry))]
    [NotifyPropertyChangedFor(nameof(RollingLatencyMs))]
    private FileFlow.Sdk.Telemetry.NodeTelemetryStats _currentStats;

    [ObservableProperty]
    private bool _isBottleneck;

    [ObservableProperty]
    private FileFlow.Sdk.Telemetry.LatencyHeatLevel _heatLevel = FileFlow.Sdk.Telemetry.LatencyHeatLevel.None;

    [ObservableProperty]
    private string _bottleneckRatioText = string.Empty;

    // AI Model Lifecycle Support
    public bool IsModelManaged => _nodeInstance is IModelLifecycleNode;

    [ObservableProperty]
    private bool _isModelLoaded;

    [ObservableProperty]
    private bool _isModelLoading;

    [ObservableProperty]
    private string? _modelIdentifier;

    [ObservableProperty]
    private string _modelStatusToolTip = string.Empty;

    [RelayCommand]
    public async Task ToggleModelLoadAsync()
    {
        if (_nodeInstance is not IModelLifecycleNode lifecycleNode || IsModelLoading) return;

        if (lifecycleNode.IsModelLoaded)
        {
            lifecycleNode.UnloadModel();
            FileFlow.Core.Utils.MemoryReclamationHelper.ReclaimMemory(trimWorkingSet: true);
            UpdateModelStatus();
        }
        else
        {
            try
            {
                IsModelLoading = true;
                ModelStatusToolTip = LocalizationManager.Instance.GetString("Node_ModelLoading_ToolTip", "Cargando modelo de IA...");
                await lifecycleNode.PreloadModelAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NodeViewModel] Error preloading model: {ex.Message}");
            }
            finally
            {
                IsModelLoading = false;
                UpdateModelStatus();
            }
        }
    }

    public void UnloadModel()
    {
        if (_nodeInstance is IModelLifecycleNode lifecycleNode && lifecycleNode.IsModelLoaded)
        {
            lifecycleNode.UnloadModel();
            UpdateModelStatus();
        }
    }

    private void OnModelStatusChanged()
    {
        UpdateModelStatus();
    }

    public void UpdateModelStatus()
    {
        if (_nodeInstance is not IModelLifecycleNode lifecycleNode) return;

        IsModelLoaded = lifecycleNode.IsModelLoaded;
        ModelIdentifier = lifecycleNode.ModelIdentifier;
        ModelStatusToolTip = IsModelLoaded
            ? LocalizationManager.Instance.GetString("Node_ModelLoaded_ToolTip", "El modelo de IA está cargado en memoria (RAM/VRAM). Haz clic para descargarlo y liberar memoria.")
            : LocalizationManager.Instance.GetString("Node_ModelUnloaded_ToolTip", "El modelo de IA no está cargado en memoria. Haz clic para precargarlo en memoria.");
    }

    /// <summary>
    /// Vuelca una instantánea de telemetría en el nodo.
    ///
    /// El pie de la tarjeta NO guarda texto formateado: expone los valores numéricos (a través de
    /// <see cref="CurrentStats"/>) y deja el formato a los convertidores de la vista. Así no hay emojis ni
    /// cadenas con unidades dentro del view model, y el mismo dato se puede reutilizar en cualquier idioma.
    /// </summary>
    public void UpdateTelemetryStats(FileFlow.Sdk.Telemetry.NodeTelemetryStats stats)
    {
        CurrentStats = stats;

        if (stats.ProcessedCount > 0)
        {
            IsGpuAccelerated = stats.IsGpuAccelerated;
            IsBottleneck = stats.IsBottleneck;
            HeatLevel = stats.HeatLevel;
            BottleneckRatioText = stats.RelativeBottleneckRatio > 0.05
                ? LocalizationManager.Instance.GetFormattedString(
                    "Node_BottleneckPercent", "{0}%", $"{stats.RelativeBottleneckRatio * 100:F0}")
                : string.Empty;

            DetailedMetricsToolTip = BuildDetailedMetricsToolTip(stats);
        }
        else
        {
            IsGpuAccelerated = false;
            IsBottleneck = false;
            HeatLevel = FileFlow.Sdk.Telemetry.LatencyHeatLevel.None;
            BottleneckRatioText = string.Empty;
            DetailedMetricsToolTip = string.Empty;
        }

        OnPropertyChanged(nameof(HasTelemetry));
        OnPropertyChanged(nameof(HasRamTelemetry));
        OnPropertyChanged(nameof(RollingLatencyMs));
    }

    private string BuildDetailedMetricsToolTip(FileFlow.Sdk.Telemetry.NodeTelemetryStats stats)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📊 {Title} ({Category})");
        sb.AppendLine($"──────────────────────────────");
        sb.AppendLine($"Procesados: {stats.ProcessedCount} items");
        sb.AppendLine($"Latencia media total: {FormatLatencyHelper(stats.AverageTimeMs)}");
        if (stats.RollingAvgDurationMs > 0)
        {
            sb.AppendLine($"Latencia rodante (últimas 8 ops): {FormatLatencyHelper(stats.RollingAvgDurationMs)}");
        }
        if (stats.RecentSamples != null && stats.RecentSamples.Count > 0)
        {
            var min = stats.RecentSamples.Min(s => s.DurationMs);
            var max = stats.RecentSamples.Max(s => s.DurationMs);
            sb.AppendLine($"Rango latencia reciente: Min {FormatLatencyHelper(min)} | Max {FormatLatencyHelper(max)}");
        }
        
        if (stats.RollingAvgAllocatedBytes > 0 || stats.PeakAllocatedBytes > 0)
        {
            sb.AppendLine($"RAM media por item: {FormatBytesHelper(stats.RollingAvgAllocatedBytes)} (Pico: {FormatBytesHelper(stats.PeakAllocatedBytes)})");
        }
        if (stats.AvgCpuPercentage > 0)
        {
            sb.AppendLine($"Carga CPU estimada: {stats.AvgCpuPercentage:F1}%");
        }
        if (stats.IsGpuAccelerated)
        {
            sb.AppendLine($"Aceleración por Hardware: GPU / DirectML 🎮");
        }
        if (stats.IsBottleneck)
        {
            sb.AppendLine($"⚠️ Cuello de botella detectado: {stats.RelativeBottleneckRatio * 100:F0}% del tiempo total");
        }
        if (stats.RecentSamples != null && stats.RecentSamples.Count > 0)
        {
            sb.AppendLine($"──────────────────────────────");
            sb.AppendLine($"Historial reciente ({stats.RecentSamples.Count} ops):");
            for (int i = 0; i < stats.RecentSamples.Count; i++)
            {
                var sample = stats.RecentSamples[i];
                var gpuTag = sample.GpuAccelerated ? " [GPU]" : "";
                sb.AppendLine($"  #{i + 1}: {FormatLatencyHelper(sample.DurationMs)} | {FormatBytesHelper(sample.AllocatedBytes)}{gpuTag} ({sample.Timestamp:HH:mm:ss})");
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string FormatLatencyHelper(double ms)
    {
        if (ms < 1.0) return $"{ms * 1000:F0} µs";
        if (ms < 1000.0) return $"{ms:F1} ms";
        return $"{ms / 1000.0:F2} s";
    }

    private static string FormatBytesHelper(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F0} KB";
        return $"{bytes} B";
    }

    partial void OnExecutionStatusChanged(NodeExecutionStatus value)
    {
        IsLedOn = value == NodeExecutionStatus.Running || value == NodeExecutionStatus.Completed;
        OnPropertyChanged(nameof(IsExecuting));
        OnPropertyChanged(nameof(IsFaulted));
        OnPropertyChanged(nameof(ExecutionStatusText));
        if (value == NodeExecutionStatus.Idle)
        {
            IsProgressActive = false;
            ProgressPercentage = 0;
            ProgressMessage = string.Empty;
        }
        else if (value == NodeExecutionStatus.Completed)
        {
            IsProgressActive = false;
        }
    }

    public void UpdateProgress(double percentage, string message)
    {
        ProgressPercentage = percentage;
        ProgressMessage = message;
        IsProgressActive = percentage > 0 && percentage < 100;
    }

    [ObservableProperty]
    private string? _lastErrorDetails;

    [ObservableProperty]
    private bool _isSubWorkflow;

    [ObservableProperty]
    private string _innerGraphJson = string.Empty;

    public IFlowNode NodeInstance => _nodeInstance;

    public ObservableCollection<PortViewModel> InputPorts { get; } = [];
    public ObservableCollection<PortViewModel> OutputPorts { get; } = [];
    public ObservableCollection<NodeParameterViewModel> Parameters => _parameterManager.Parameters;
    public ObservableCollection<NodeDataSnapshot> InputSnapshots { get; } = [];
    public ObservableCollection<NodeDataSnapshot> OutputSnapshots { get; } = [];
    public ObservableCollection<SwitchCaseItemViewModel> SwitchCases { get; } = [];

    public NodeViewModel(IFlowNode node, Point location)
    {
        _nodeInstance = node;
        _id = node.Id;
        _title = node.Name;
        _category = node.Category;
        _description = node.Description;
        _nodeTypeName = node.GetType().FullName ?? node.GetType().Name;
        _location = location;

        SetDefaultColorsForCategory(_category);

        foreach (var inPort in node.Inputs)
        {
            InputPorts.Add(new PortViewModel(this, inPort.Name, inPort.DisplayName, inPort.Direction, inPort.DataType, inPort.Description));
        }

        bool isSwitch = node.GetType().Name.Contains("SwitchCaseNode", StringComparison.OrdinalIgnoreCase);

        if (isSwitch)
        {
            NodeSwitchCaseCoordinator.InitializeSwitchCases(node, this, OutputPorts, SwitchCases);
        }
        else
        {
            foreach (var outPort in node.Outputs)
            {
                OutputPorts.Add(new PortViewModel(this, outPort.Name, outPort.DisplayName, outPort.Direction, outPort.DataType, outPort.Description));
            }
        }

        _parameterManager = new NodeParameterManager(node, this);

        if (isSwitch)
        {
            SyncSwitchCasesToNodeInstance();
        }

        foreach (var action in node.CustomActions)
        {
            CustomActions.Add(new NodeActionViewModel(action, this));
        }

        if (node is IModelLifecycleNode lifecycleNode)
        {
            lifecycleNode.ModelStatusChanged += OnModelStatusChanged;
            UpdateModelStatus();
        }

        // Los puertos del lienzo son una proyección de los del nodo: se reconstruyen cuando el nodo anuncia
        // que su topología cambió, en lugar de esperar a que alguien llame a un método de sincronización.
        if (node is IPortTopologyNode portTopology)
        {
            _portTopologyNode = portTopology;
            portTopology.PortsChanged += OnPortsChanged;
        }

        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<NodeActionViewModel> CustomActions { get; } = [];

    public bool IsVariableInjectorNode => NodeTypeName.Contains("VariableInjectorNode", StringComparison.OrdinalIgnoreCase);
    public bool IsSwitchCaseNode => NodeTypeName.Contains("SwitchCaseNode", StringComparison.OrdinalIgnoreCase);
    public bool IsAdvancedRenamerNode => NodeTypeName.Contains("AdvancedRenamerNode", StringComparison.OrdinalIgnoreCase);
    public bool IsFolderSourceNode => NodeTypeName.Contains("FolderSourceNode", StringComparison.OrdinalIgnoreCase);
    public bool IsSubflowNode => _nodeInstance is ISubflowNode || NodeTypeName.Contains("SubflowNode", StringComparison.OrdinalIgnoreCase);

    public void ExecuteCustomAction(string actionId)
    {
        if (_nodeInstance is INodeCustomActionProvider provider)
        {
            provider.ExecuteCustomAction(actionId, new NodeCustomActionContext(App.MainWindow, () => SyncParametersFromNodeInstance()));
            SyncParametersFromNodeInstance();
            return;
        }

        switch (actionId.ToLowerInvariant())
        {
            case "addvariable":
                AddVariable();
                break;
            case "addswitchcase":
                AddSwitchCase();
                break;
            case "opensubfloweditor":
                ParentEditor?.OpenSubflow(this);
                break;
        }
    }

    /// <summary>
    /// Redescubre los puertos frontera del subgrafo y los aplica al nodo. No reconcilia aquí: al cambiar la
    /// topología el nodo lo anuncia y el manejador reconstruye los puertos del lienzo, de modo que este
    /// método y cualquier otro camino que cambie los puertos acaban en el mismo sitio.
    ///
    /// El descubrimiento es el del <see cref="SubflowPortResolver"/> y no el del servicio global de
    /// subflujos: ese servicio sólo existe mientras corre una ejecución, así que en el editor devolvía los
    /// puertos genéricos y este método <i>borraba</i> los puertos configurados del contenedor.
    /// </summary>
    public void SyncSubflowPorts()
    {
        if (_nodeInstance is ISubflowNode subflowNode)
        {
            SubflowPortResolver.Materialize(subflowNode);
        }
    }

    /// <summary>
    /// ¿La definición de mi subflujo cambió en disco desde la última vez que se materializaron mis puertos?
    ///
    /// La comprobación es la <b>huella</b> del origen —el archivo resuelto con su fecha y su tamaño—, no su
    /// contenido: preguntarlo es tan barato que el lienzo puede hacerlo cada segundo para los contenedores
    /// que tenga abiertos. Quien reciba <c>true</c> refresca con <see cref="SyncSubflowPorts"/> y el lienzo
    /// revalida los cables solo, al anunciarse la topología nueva.
    ///
    /// Un nodo de puertos fijos, o un contenedor cuya definición va incrustada en el propio flujo —no hay
    /// archivo que pueda cambiar por fuera—, responden siempre que no.
    /// </summary>
    public bool HasSubflowDefinitionChanged() =>
        _nodeInstance is ISubflowNode subflowNode && SubflowPortResolver.HasSourceChanged(subflowNode);

    /// <summary>
    /// Reconstruye los puertos del lienzo a partir de los que expone el nodo: elimina los que ya no existen,
    /// añade los nuevos y respeta el orden del nodo. Los puertos que siguen existiendo <b>conservan su
    /// instancia</b>, porque las conexiones y los casos de un switch apuntan a ella: recrearlos dejaría el
    /// cable colgando de un puerto fantasma.
    /// </summary>
    public void SyncPortsFromNodeInstance()
    {
        ReconcilePorts(InputPorts, _nodeInstance.Inputs);
        ReconcilePorts(OutputPorts, _nodeInstance.Outputs);
        PruneSwitchCasesWithoutPort();
    }

    private void ReconcilePorts(ObservableCollection<PortViewModel> currentPorts, IReadOnlyList<NodePort> declaredPorts)
    {
        var declaredNames = declaredPorts.Select(p => p.Name).ToList();

        for (int i = currentPorts.Count - 1; i >= 0; i--)
        {
            if (!declaredNames.Contains(currentPorts[i].Name, StringComparer.OrdinalIgnoreCase))
            {
                currentPorts.RemoveAt(i);
            }
        }

        // Al llegar al índice i, la colección ya tiene los i primeros puertos en su sitio: un puerto que
        // sobrevive se mueve si hace falta y los que falten se insertan ahí mismo.
        for (int i = 0; i < declaredPorts.Count; i++)
        {
            var declared = declaredPorts[i];
            var existing = currentPorts.FirstOrDefault(p => p.Name.Equals(declared.Name, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                currentPorts.Insert(i, new PortViewModel(this, declared.Name, declared.DisplayName, declared.Direction, declared.DataType, declared.Description));
                continue;
            }

            existing.DisplayName = declared.DisplayName;
            existing.DataType = declared.DataType;

            int currentIndex = currentPorts.IndexOf(existing);
            if (currentIndex != i)
            {
                currentPorts.Move(currentIndex, i);
            }
        }
    }

    /// <summary>
    /// Descarta los casos de switch cuyo puerto ya no existe (por ejemplo, si el nodo se reconstruyó desde
    /// sus parámetros): un caso sin puerto es una fila del inspector que ya no enruta a ningún sitio.
    /// </summary>
    private void PruneSwitchCasesWithoutPort()
    {
        if (SwitchCases.Count == 0) return;

        for (int i = SwitchCases.Count - 1; i >= 0; i--)
        {
            var caseItem = SwitchCases[i];
            if (caseItem.Port != null && !OutputPorts.Contains(caseItem.Port))
            {
                caseItem.Port = null;
                SwitchCases.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// El nodo anuncia que sus puertos cambiaron: se reconstruye la tarjeta y el lienzo revalida sus cables,
    /// porque los que apunten a un puerto que ya no existe no pueden sobrevivir.
    /// </summary>
    private void OnPortsChanged(object? sender, PortTopologyChangedEventArgs e)
    {
        SyncPortsFromNodeInstance();
        ParentEditor?.RevalidateConnections(this);
    }

    public void SyncParametersFromNodeInstance()
    {
        var updatedDescriptors = _nodeInstance.ParameterDescriptors?.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var param in Parameters)
        {
            if (updatedDescriptors != null && updatedDescriptors.TryGetValue(param.Key, out var desc) && desc.Options != null)
            {
                param.UpdateOptions(desc.Options);
            }

            if (_nodeInstance.Parameters.TryGetValue(param.Key, out var updatedVal))
            {
                param.Value = updatedVal;
            }
        }
    }

    [RelayCommand]
    public void AddSwitchCase()
    {
        NodeSwitchCaseCoordinator.AddCase(this, OutputPorts, SwitchCases, SyncSwitchCasesToNodeInstance);
    }

    [RelayCommand]
    public void RemoveSwitchCase(SwitchCaseItemViewModel caseItem)
    {
        NodeSwitchCaseCoordinator.RemoveCase(caseItem, OutputPorts, SwitchCases, SyncSwitchCasesToNodeInstance);
    }

    public void OnSwitchCaseRenamed(string oldName, string newName, SwitchCaseItemViewModel item)
    {
        NodeSwitchCaseCoordinator.RenameCase(oldName, newName, item, OutputPorts, SyncSwitchCasesToNodeInstance);
    }

    public void SyncSwitchCasesToNodeInstance()
    {
        NodeSwitchCaseCoordinator.SyncCasesToNode(_nodeInstance, SwitchCases);
    }

    [RelayCommand]
    public void AddVariable()
    {
        _parameterManager.AddVariable();
    }

    [RelayCommand]
    public void RemoveParameter(NodeParameterViewModel param)
    {
        _parameterManager.RemoveParameter(param);
    }

    public void OnParameterKeyRenamed(string oldKey, string newKey, object? value)
    {
        _parameterManager.OnParameterKeyRenamed(oldKey, newKey, value);
    }

    public void OnParameterValueChanged(string key, object? newValue)
    {
        _parameterManager.OnParameterValueChanged(key, newValue);
    }

    private void OnLanguageChanged(object? sender, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(CustomTitle))
        {
            Title = _nodeInstance.Name;
        }
        Description = _nodeInstance.Description;
        Category = _nodeInstance.Category;
        UpdateModelStatus();
        OnPropertyChanged(nameof(ExecutionStatusText));
        OnPropertyChanged(nameof(BottleneckRatioText));

        // El estado de cada socket (tipo, dirección, conexión) es texto visible: se recompone en caliente.
        foreach (var port in InputPorts.Concat(OutputPorts))
        {
            port.RefreshLocalizedText();
        }
    }

    public void Cleanup()
    {
        LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
        if (_nodeInstance is IModelLifecycleNode lifecycleNode)
        {
            lifecycleNode.ModelStatusChanged -= OnModelStatusChanged;
        }
        if (_portTopologyNode != null)
        {
            _portTopologyNode.PortsChanged -= OnPortsChanged;
        }
        _parameterManager.Dispose();
        InputSnapshots.Clear();
        OutputSnapshots.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cleanup();
    }

    private List<NodeViewModel> GetTargetNodesForBatchAction()
    {
        if (IsSelected && ParentEditor != null)
        {
            var selected = ParentEditor.Nodes.Where(n => n.IsSelected).ToList();
            if (selected.Count > 1 && selected.Contains(this))
            {
                return selected;
            }
        }
        return [this];
    }

    [RelayCommand]
    public void Delete()
    {
        if (ParentEditor != null)
        {
            if (IsSelected && ParentEditor.Nodes.Count(n => n.IsSelected) > 1)
            {
                ParentEditor.DeleteSelectedNodesCommand.Execute(null);
            }
            else
            {
                ParentEditor.DeleteNode(this);
            }
        }
    }

    [RelayCommand]
    public void Copy()
    {
        if (ParentEditor != null)
        {
            if (!IsSelected) IsSelected = true;
            ParentEditor.CopySelectedNodesCommand.Execute(null);
        }
    }

    [RelayCommand]
    public void Cut()
    {
        if (ParentEditor != null)
        {
            if (!IsSelected) IsSelected = true;
            ParentEditor.CutSelectedNodesCommand.Execute(null);
        }
    }

    [RelayCommand]
    public void Duplicate()
    {
        if (ParentEditor != null)
        {
            if (!IsSelected) IsSelected = true;
            ParentEditor.DuplicateSelectedNodesCommand.Execute(null);
        }
    }

    [RelayCommand]
    public void ChangeColor(string colorHex)
    {
        var targets = GetTargetNodesForBatchAction();
        foreach (var node in targets)
        {
            node.HeaderColor = NodeCategoryStyling.GetHeaderColorFromAccent(colorHex);
            node.AccentColor = colorHex;
        }
    }

    [RelayCommand]
    public void ChooseCustomColor()
    {
        var hexColor = Services.ColorPickerService.Instance.PickColorHex();
        if (!string.IsNullOrEmpty(hexColor))
        {
            ChangeColor(hexColor);
        }
    }

    public void SetDefaultColorsForCategory(string category)
    {
        var (header, accent) = NodeCategoryStyling.GetColorsForCategory(category);
        HeaderColor = header;
        AccentColor = accent;
    }

    public static string GetHeaderColorFromAccent(string accentHex)
    {
        return NodeCategoryStyling.GetHeaderColorFromAccent(accentHex);
    }

    partial void OnIsExpandedChanged(bool oldValue, bool newValue)
    {
        if (oldValue == newValue) return;

        if (newValue)
        {
            CollapsedWidth = Width;
            Width = ExpandedWidth;
        }
        else
        {
            ExpandedWidth = Width;
            Width = CollapsedWidth;
        }
    }

    public void UpdateWidth(double newWidth)
    {
        Width = Math.Clamp(newWidth, 180, MaxWidth);
        if (IsExpanded)
        {
            ExpandedWidth = Width;
        }
        else
        {
            CollapsedWidth = Width;
        }
    }

    [RelayCommand]
    public void ToggleBreakpoint()
    {
        bool targetState = !HasBreakpoint;
        var targets = GetTargetNodesForBatchAction();
        foreach (var node in targets)
        {
            node.HasBreakpoint = targetState;
        }
    }

    [RelayCommand]
    public void ToggleLogging()
    {
        bool targetState = !IsLoggingEnabled;
        var targets = GetTargetNodesForBatchAction();
        foreach (var node in targets)
        {
            node.IsLoggingEnabled = targetState;
        }
    }

    public const int MaxRecordedSnapshots = 500;

    public void AddSnapshot(NodeDataSnapshot snapshot)
    {
        ApplySnapshotInternal(snapshot);
    }

    private void ApplySnapshotInternal(NodeDataSnapshot snapshot)
    {
        var targetCollection = snapshot.IsInput ? InputSnapshots : OutputSnapshots;
        if (targetCollection.Count >= MaxRecordedSnapshots)
        {
            targetCollection.RemoveAt(0);
        }
        targetCollection.Add(snapshot);

        var ports = snapshot.IsInput ? InputPorts : OutputPorts;
        var port = ports.FirstOrDefault(p => p.Name.Equals(snapshot.PortName, StringComparison.OrdinalIgnoreCase))
                  ?? ports.FirstOrDefault();
        if (port != null && snapshot.ItemSnapshot != null)
        {
            port.UpdatePortContext(snapshot.ItemSnapshot);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Presentación de la tarjeta (cabecera y telemetría)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Icono vectorial del tipo de nodo, para la cabecera de la tarjeta.</summary>
    public Material.Icons.MaterialIconKind Icon => NodeIconResolver.GetIconForNodeType(NodeTypeName);

    public bool IsExecuting => ExecutionStatus == NodeExecutionStatus.Running;

    public bool IsFaulted => ExecutionStatus is NodeExecutionStatus.Faulted or NodeExecutionStatus.PausedOnError;

    /// <summary>Estado de ejecución en texto, para la línea de contexto de la cabecera.</summary>
    public string ExecutionStatusText => ExecutionStatus switch
    {
        NodeExecutionStatus.Running => LocalizationManager.Instance.GetString("NodeStatus_Running", "Ejecutando"),
        NodeExecutionStatus.PausedAtBreakpoint => LocalizationManager.Instance.GetString("NodeStatus_Paused", "En pausa"),
        NodeExecutionStatus.PausedOnError => LocalizationManager.Instance.GetString("NodeStatus_PausedOnError", "Pausa por error"),
        NodeExecutionStatus.Completed => LocalizationManager.Instance.GetString("NodeStatus_Completed", "Completado"),
        NodeExecutionStatus.Faulted => LocalizationManager.Instance.GetString("NodeStatus_Faulted", "Error"),
        _ => LocalizationManager.Instance.GetString("NodeStatus_Idle", "En espera")
    };

    // ─────────────────────────────────────────────────────────────────────────────
    // Pie de telemetría: valores crudos (el formato lo aplica la vista con sus convertidores)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>El nodo ya se ha ejecutado al menos una vez: el pie de métricas tiene datos que mostrar.</summary>
    public bool HasTelemetry => CurrentStats.ProcessedCount > 0;

    /// <summary>Hay medida de memoria por elemento (no todos los nodos asignan memoria medible).</summary>
    public bool HasRamTelemetry => CurrentStats.RollingAvgAllocatedBytes > 0;

    /// <summary>
    /// Latencia por elemento: la media en rodadura cuando existe y, si aún no hay muestras suficientes,
    /// la media acumulada. En milisegundos, lista para el convertidor de duraciones del pie.
    /// </summary>
    public double RollingLatencyMs => CurrentStats.RollingAvgDurationMs > 0
        ? CurrentStats.RollingAvgDurationMs
        : CurrentStats.AverageTimeMs;

    public void SetExecutionStatus(NodeExecutionStatus status, string? errorDetails = null)
    {
        ExecutionStatus = status;
        LastErrorDetails = errorDetails;
    }

    public void ClearDebugData()
    {
        ExecutionStatus = NodeExecutionStatus.Idle;
        LastErrorDetails = null;
        InputSnapshots.Clear();
        OutputSnapshots.Clear();
    }
}


