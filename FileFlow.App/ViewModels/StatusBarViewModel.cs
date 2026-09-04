using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public partial class StatusBarViewModel : ObservableObject
{
    private readonly EditorViewModel _editorViewModel;
    private readonly ControlBarViewModel _controlBarViewModel;
    private readonly SystemPerformanceMonitor _performanceMonitor;

    [ObservableProperty]
    private int _nodeCount;

    [ObservableProperty]
    private int _connectionCount;

    [ObservableProperty]
    private string _selectedNodeName = "Ninguno";

    [ObservableProperty]
    private string _ramText = "-- MB";

    [ObservableProperty]
    private string _cpuText = "-- %";

    [ObservableProperty]
    private string _gpuText = "-- %";

    [ObservableProperty]
    private int _loadedAiModelsCount;

    [ObservableProperty]
    private bool _hasLoadedAiModels;

    [ObservableProperty]
    private string _loadedAiModelsText = string.Empty;

    [ObservableProperty]
    private string _loadedAiModelsToolTip = string.Empty;

    [ObservableProperty]
    private string _globalOutputDir = @"C:\FileFlowOutput";

    [ObservableProperty]
    private string _statusMessage = "🟢 Listo para ejecutar";

    [ObservableProperty]
    private double _zoomPercentage = 100;

    private readonly LogViewModel _logViewModel;

    public StatusBarViewModel(
        EditorViewModel editorViewModel, 
        ControlBarViewModel controlBarViewModel, 
        SystemPerformanceMonitor performanceMonitor,
        LogViewModel logViewModel)
    {
        _editorViewModel = editorViewModel;
        _controlBarViewModel = controlBarViewModel;
        _performanceMonitor = performanceMonitor;
        _logViewModel = logViewModel;

        // Subscripciones a eventos de EditorViewModel
        _editorViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EditorViewModel.GlobalOutputDir))
            {
                GlobalOutputDir = _editorViewModel.GlobalOutputDir;
            }
            else if (e.PropertyName == nameof(EditorViewModel.SelectedNode))
            {
                UpdateSelectedNodeInfo();
            }
        };

        // Subscripciones a eventos de nodos para seguimiento reactivo de modelos IA
        void HookNode(NodeViewModel node)
        {
            node.PropertyChanged += Node_PropertyChanged;
        }

        void UnhookNode(NodeViewModel node)
        {
            node.PropertyChanged -= Node_PropertyChanged;
        }

        foreach (var node in _editorViewModel.Nodes)
        {
            HookNode(node);
        }

        _editorViewModel.Nodes.CollectionChanged += (s, e) =>
        {
            NodeCount = _editorViewModel.Nodes.Count;
            if (e.OldItems != null)
            {
                foreach (NodeViewModel node in e.OldItems)
                {
                    UnhookNode(node);
                }
            }
            if (e.NewItems != null)
            {
                foreach (NodeViewModel node in e.NewItems)
                {
                    HookNode(node);
                }
            }
            Application.Current?.Dispatcher.InvokeAsync(UpdateAiModelCount);
        };
        _editorViewModel.Connections.CollectionChanged += (s, e) => ConnectionCount = _editorViewModel.Connections.Count;

        NodeCount = _editorViewModel.Nodes.Count;
        ConnectionCount = _editorViewModel.Connections.Count;
        GlobalOutputDir = _editorViewModel.GlobalOutputDir;
        UpdateSelectedNodeInfo();

        // Subscripciones a eventos de ControlBarViewModel
        _controlBarViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ControlBarViewModel.IsRunning))
            {
                if (_controlBarViewModel.IsRunning)
                {
                    UpdateActiveStatusMessage(_logViewModel.StatusMessage);
                }
                else
                {
                    StatusMessage = "🟢 Listo";
                }
            }
            else if (e.PropertyName == nameof(ControlBarViewModel.IsPaused))
            {
                if (_controlBarViewModel.IsPaused)
                {
                    StatusMessage = "⏸️ Flujo pausado";
                }
            }
        };

        // Subscripción reactiva a LogViewModel para estado en vivo
        _logViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LogViewModel.StatusMessage))
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    if (_controlBarViewModel.IsRunning)
                    {
                        UpdateActiveStatusMessage(_logViewModel.StatusMessage);
                    }
                    else
                    {
                        StatusMessage = "🟢 Listo";
                    }
                });
            }
        };

        // Rendimiento en tiempo real
        _performanceMonitor.PerformanceUpdated += (metrics) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                RamText = metrics.RamFormatted;
                CpuText = metrics.CpuFormatted;
                GpuText = metrics.GpuFormatted;
            });
        };

        // Estado de modelos de IA en memoria
        FileFlow.Plugin.AI.Inference.OnnxSessionManager.SessionStateChanged += () =>
        {
            Application.Current?.Dispatcher.InvokeAsync(UpdateAiModelCount);
        };
        FileFlow.Plugin.AI.AudioInferenceEngine.SessionStateChanged += () =>
        {
            Application.Current?.Dispatcher.InvokeAsync(UpdateAiModelCount);
        };
        LocalizationManager.Instance.LanguageChanged += (s, e) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(UpdateAiModelCount);
        };
        UpdateAiModelCount();
    }

    private void Node_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.IsModelLoaded))
        {
            Application.Current?.Dispatcher.InvokeAsync(UpdateAiModelCount);
        }
    }

    public void UpdateAiModelCount()
    {
        int onnxCount = FileFlow.Plugin.AI.Inference.OnnxSessionManager.GetLoadedSessionCount();
        int audioCount = FileFlow.Plugin.AI.AudioInferenceEngine.GetLoadedSessionCount();
        int canvasLoadedCount = _editorViewModel.Nodes.Count(n => n.IsModelLoaded);
        int total = Math.Max(canvasLoadedCount, onnxCount + audioCount);

        LoadedAiModelsCount = total;
        HasLoadedAiModels = total > 0;
        LoadedAiModelsText = string.Format(
            LocalizationManager.Instance.GetString("StatusBar_AiModelsLoadedCount", "{0} loaded"),
            total);
        LoadedAiModelsToolTip = LocalizationManager.Instance.GetString("StatusBar_ClearAiMemoryToolTip", "Descarga todos los modelos ONNX/IA de la memoria RAM/VRAM y libera memoria.");
    }

    [RelayCommand]
    public void ClearAllAiModels()
    {
        FileFlow.Plugin.AI.AiPluginInitializer.ClearAllSessions();
        foreach (var node in _editorViewModel.Nodes)
        {
            if (node.IsModelManaged)
            {
                node.UnloadModel();
            }
        }
        FileFlow.Core.Utils.MemoryReclamationHelper.ReclaimMemory(trimWorkingSet: true);
        UpdateAiModelCount();
    }

    private void UpdateActiveStatusMessage(string? rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage) || rawMessage == "Listo" || rawMessage == "Ready" || rawMessage == FileFlow.Sdk.Localization.LocalizationManager.Instance["StatusReady"])
        {
            StatusMessage = "⚡ Ejecutando flujo de trabajo...";
            return;
        }

        if (rawMessage.StartsWith("⚡"))
        {
            StatusMessage = rawMessage;
        }
        else if (rawMessage.StartsWith("🟢"))
        {
            StatusMessage = $"⚡ {rawMessage[1..].TrimStart()}";
        }
        else
        {
            StatusMessage = $"⚡ {rawMessage}";
        }
    }

    private void UpdateSelectedNodeInfo()
    {
        var sel = _editorViewModel.SelectedNode;
        SelectedNodeName = sel != null ? $"{sel.Title} ({sel.NodeTypeName})" : "Ninguno";
    }

    [RelayCommand]
    public void OpenGlobalOutputFolder()
    {
        try
        {
            string folder = string.IsNullOrWhiteSpace(GlobalOutputDir) ? @"C:\FileFlowOutput" : GlobalOutputDir;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            string errorMsg = string.Format(LocalizationManager.Instance.GetString("Msg_GlobalOutputFolderError", "No se pudo abrir la carpeta de salida global: {0}"), ex.Message);
            string title = LocalizationManager.Instance.GetString("Error", "Error");
            MessageBox.Show(errorMsg, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void OpenWorkflowSettings()
    {
        _editorViewModel.OpenWorkflowSettings();
    }

    [RelayCommand]
    public void FitToScreen()
    {
        _editorViewModel.FitToScreen();
    }
}
