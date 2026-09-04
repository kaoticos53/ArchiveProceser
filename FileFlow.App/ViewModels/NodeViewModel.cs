using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FileFlow.App.Messages;
using FileFlow.App.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public partial class NodeViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IFlowNode _nodeInstance;
    private readonly NodeParameterManager _parameterManager;

    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    public EditorViewModel? ParentEditor { get; set; }

    [ObservableProperty]
    private string _title = "Node";

    [ObservableProperty]
    private string _category = "General";

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
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
    private string _latencyText = string.Empty;

    [ObservableProperty]
    private string _rollingRamText = string.Empty;

    [ObservableProperty]
    private bool _isGpuAccelerated;

    [ObservableProperty]
    private string _detailedMetricsToolTip = string.Empty;

    [ObservableProperty]
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

        var dispatcher = Application.Current?.Dispatcher;
        void Action()
        {
            IsModelLoaded = lifecycleNode.IsModelLoaded;
            ModelIdentifier = lifecycleNode.ModelIdentifier;
            ModelStatusToolTip = IsModelLoaded
                ? LocalizationManager.Instance.GetString("Node_ModelLoaded_ToolTip", "El modelo de IA está cargado en memoria (RAM/VRAM). Haz clic para descargarlo y liberar memoria.")
                : LocalizationManager.Instance.GetString("Node_ModelUnloaded_ToolTip", "El modelo de IA no está cargado en memoria. Haz clic para precargarlo en memoria.");
        }

        if (dispatcher == null || dispatcher.CheckAccess())
        {
            Action();
        }
        else
        {
            dispatcher.InvokeAsync(Action);
        }
    }

    public void UpdateTelemetryStats(FileFlow.Sdk.Telemetry.NodeTelemetryStats stats)
    {
        CurrentStats = stats;
        if (stats.ProcessedCount > 0)
        {
            var effLatency = stats.RollingAvgDurationMs > 0 ? stats.RollingAvgDurationMs : stats.AverageTimeMs;
            LatencyText = effLatency < 1.0
                ? $"⚡ {effLatency * 1000:F0} µs"
                : (effLatency < 1000.0
                    ? $"⚡ {effLatency:F1} ms"
                    : $"⏱️ {effLatency / 1000.0:F2} s");

            if (stats.RollingAvgAllocatedBytes >= 1024 * 1024)
            {
                RollingRamText = $"💾 {stats.RollingAvgAllocatedBytes / (1024.0 * 1024.0):F1} MB";
            }
            else if (stats.RollingAvgAllocatedBytes >= 1024)
            {
                RollingRamText = $"💾 {stats.RollingAvgAllocatedBytes / 1024.0:F0} KB";
            }
            else if (stats.RollingAvgAllocatedBytes > 0)
            {
                RollingRamText = $"💾 {stats.RollingAvgAllocatedBytes} B";
            }
            else
            {
                RollingRamText = string.Empty;
            }

            IsGpuAccelerated = stats.IsGpuAccelerated;
            IsBottleneck = stats.IsBottleneck;
            HeatLevel = stats.HeatLevel;
            BottleneckRatioText = stats.RelativeBottleneckRatio > 0.05
                ? $"{stats.RelativeBottleneckRatio * 100:F0}% del tiempo"
                : string.Empty;

            DetailedMetricsToolTip = BuildDetailedMetricsToolTip(stats);
        }
        else
        {
            LatencyText = string.Empty;
            RollingRamText = string.Empty;
            IsGpuAccelerated = false;
            IsBottleneck = false;
            HeatLevel = FileFlow.Sdk.Telemetry.LatencyHeatLevel.None;
            BottleneckRatioText = string.Empty;
            DetailedMetricsToolTip = string.Empty;
        }
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
        if (value == NodeExecutionStatus.Idle)
        {
            IsProgressActive = false;
            ProgressPercentage = 0;
            ProgressMessage = string.Empty;
            LatencyText = string.Empty;
            RollingRamText = string.Empty;
            IsGpuAccelerated = false;
            IsBottleneck = false;
            HeatLevel = FileFlow.Sdk.Telemetry.LatencyHeatLevel.None;
            BottleneckRatioText = string.Empty;
            DetailedMetricsToolTip = string.Empty;
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

        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<NodeActionViewModel> CustomActions { get; } = [];

    public bool IsVariableInjectorNode => NodeTypeName.Contains("VariableInjectorNode", StringComparison.OrdinalIgnoreCase);
    public bool IsSwitchCaseNode => NodeTypeName.Contains("SwitchCaseNode", StringComparison.OrdinalIgnoreCase);
    public bool IsAdvancedRenamerNode => NodeTypeName.Contains("AdvancedRenamerNode", StringComparison.OrdinalIgnoreCase);
    public bool IsFolderSourceNode => NodeTypeName.Contains("FolderSourceNode", StringComparison.OrdinalIgnoreCase);

    public void ExecuteCustomAction(string actionId)
    {
        if (_nodeInstance is INodeCustomActionProvider provider)
        {
            provider.ExecuteCustomAction(actionId, Application.Current?.MainWindow);

            // Sincronizar cualquier parámetro modificado por el diálogo
            foreach (var param in Parameters)
            {
                if (_nodeInstance.Parameters.TryGetValue(param.Key, out var updatedVal))
                {
                    param.Value = updatedVal;
                }
            }
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
        Title = _nodeInstance.Name;
        Description = _nodeInstance.Description;
        Category = _nodeInstance.Category;
        UpdateModelStatus();
    }

    public void Cleanup()
    {
        LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
        if (_nodeInstance is IModelLifecycleNode lifecycleNode)
        {
            lifecycleNode.ModelStatusChanged -= OnModelStatusChanged;
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

    [RelayCommand]
    public void ChangeColor(string colorHex)
    {
        HeaderColor = NodeCategoryStyling.GetHeaderColorFromAccent(colorHex);
        AccentColor = colorHex;
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
        HasBreakpoint = !HasBreakpoint;
    }

    [RelayCommand]
    public void ToggleLogging()
    {
        IsLoggingEnabled = !IsLoggingEnabled;
    }

    public const int MaxRecordedSnapshots = 500;

    public void AddSnapshot(NodeDataSnapshot snapshot)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted) return;

        if (dispatcher.CheckAccess())
        {
            ApplySnapshotInternal(snapshot);
        }
        else
        {
            dispatcher.InvokeAsync(() => ApplySnapshotInternal(snapshot));
        }
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

    public void SetExecutionStatus(NodeExecutionStatus status, string? errorDetails = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
        {
            ExecutionStatus = status;
            LastErrorDetails = errorDetails;
            return;
        }

        if (dispatcher.CheckAccess())
        {
            ExecutionStatus = status;
            LastErrorDetails = errorDetails;
        }
        else
        {
            dispatcher.InvokeAsync(() =>
            {
                ExecutionStatus = status;
                LastErrorDetails = errorDetails;
            });
        }
    }

    public void ClearDebugData()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
        {
            ExecutionStatus = NodeExecutionStatus.Idle;
            LastErrorDetails = null;
            InputSnapshots.Clear();
            OutputSnapshots.Clear();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            ExecutionStatus = NodeExecutionStatus.Idle;
            LastErrorDetails = null;
            InputSnapshots.Clear();
            OutputSnapshots.Clear();
        }
        else
        {
            dispatcher.InvokeAsync(() =>
            {
                ExecutionStatus = NodeExecutionStatus.Idle;
                LastErrorDetails = null;
                InputSnapshots.Clear();
                OutputSnapshots.Clear();
            });
        }
    }
}
