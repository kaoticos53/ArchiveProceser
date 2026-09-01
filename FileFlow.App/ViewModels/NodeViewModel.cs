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

    partial void OnIsSelectedChanged(bool value)
    {
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

    partial void OnExecutionStatusChanged(NodeExecutionStatus value)
    {
        IsLedOn = value == NodeExecutionStatus.Running || value == NodeExecutionStatus.Completed;
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

        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    public bool IsVariableInjectorNode => NodeTypeName.Contains("VariableInjectorNode", StringComparison.OrdinalIgnoreCase);
    public bool IsSwitchCaseNode => NodeTypeName.Contains("SwitchCaseNode", StringComparison.OrdinalIgnoreCase);
    public bool IsAdvancedRenamerNode => NodeTypeName.Contains("AdvancedRenamerNode", StringComparison.OrdinalIgnoreCase);
    public bool IsFolderSourceNode => NodeTypeName.Contains("FolderSourceNode", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    public void OpenAdvancedRenamerEditor()
    {
        var win = new Views.Components.AdvancedRenamerEditorWindow(this);
        if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            win.Owner = Application.Current.MainWindow;
        }
        win.ShowDialog();
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
    }

    public void Cleanup()
    {
        LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
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
        Application.Current?.Dispatcher?.Invoke(() =>
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
        });
    }

    public void SetExecutionStatus(NodeExecutionStatus status, string? errorDetails = null)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            ExecutionStatus = status;
            LastErrorDetails = errorDetails;
        });
    }

    public void ClearDebugData()
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            ExecutionStatus = NodeExecutionStatus.Idle;
            LastErrorDetails = null;
            InputSnapshots.Clear();
            OutputSnapshots.Clear();
        });
    }
}
