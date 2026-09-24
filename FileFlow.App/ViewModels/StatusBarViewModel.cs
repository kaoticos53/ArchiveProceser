using System.Diagnostics;
using System.IO;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public partial class StatusBarViewModel : ObservableObject
{
    private readonly EditorViewModel _editorViewModel;
    private readonly ControlBarViewModel _controlBarViewModel;
    private readonly ISystemPerformanceMonitor _performanceMonitor;
    private readonly ILocalizationService _loc;
    private readonly IDialogService _dialogService;
    private readonly IProcessLauncherService _processLauncher;

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
    private int _unrebuiltConnectionsCount;

    [ObservableProperty]
    private bool _hasUnrebuiltConnections;

    [ObservableProperty]
    private string _unrebuiltConnectionsText = string.Empty;

    [ObservableProperty]
    private string _unrebuiltConnectionsToolTip = string.Empty;

    [ObservableProperty]
    private string _globalOutputDir = @"C:\FileFlowOutput";

    [ObservableProperty]
    private string _statusMessage = LocalizationManager.Instance.GetString("StatusBar_ReadyToExecute", "🟢 Listo para ejecutar");

    [ObservableProperty]
    private double _zoomPercentage = 100;

    private readonly LogViewModel _logViewModel;

    public StatusBarViewModel(
        EditorViewModel editorViewModel, 
        ControlBarViewModel controlBarViewModel, 
        ISystemPerformanceMonitor performanceMonitor,
        LogViewModel logViewModel,
        ILocalizationService? localizationService = null,
        IDialogService? dialogService = null,
        IProcessLauncherService? processLauncher = null)
    {
        _editorViewModel = editorViewModel;
        _controlBarViewModel = controlBarViewModel;
        _performanceMonitor = performanceMonitor;
        _logViewModel = logViewModel;
        _loc = localizationService ?? LocalizationManager.Instance;
        _dialogService = dialogService ?? AvaloniaDialogService.Instance;
        _processLauncher = processLauncher ?? ProcessLauncherService.Instance;

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
            else if (e.PropertyName == nameof(EditorViewModel.UnrebuiltConnectionsCount))
            {
                UpdateUnrebuiltConnections();
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
            Dispatcher.UIThread.InvokeAsync(UpdateAiModelCount);
        };
        _editorViewModel.Connections.CollectionChanged += (s, e) => ConnectionCount = _editorViewModel.Connections.Count;

        NodeCount = _editorViewModel.Nodes.Count;
        ConnectionCount = _editorViewModel.Connections.Count;
        GlobalOutputDir = _editorViewModel.GlobalOutputDir;
        UpdateSelectedNodeInfo();
        UpdateUnrebuiltConnections();

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
                    StatusMessage = LocalizationManager.Instance.GetString("StatusBar_Ready", "🟢 Listo");
                }
            }
            else if (e.PropertyName == nameof(ControlBarViewModel.IsPaused))
            {
                if (_controlBarViewModel.IsPaused)
                {
                    StatusMessage = LocalizationManager.Instance.GetString("StatusBar_Paused", "⏸️ Flujo pausado");
                }
            }
        };

        // Subscripción reactiva a LogViewModel para estado en vivo
        _logViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LogViewModel.StatusMessage))
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_controlBarViewModel.IsRunning)
                    {
                        UpdateActiveStatusMessage(_logViewModel.StatusMessage);
                    }
                    else
                    {
                        StatusMessage = LocalizationManager.Instance.GetString("StatusBar_Ready", "🟢 Listo");
                    }
                });
            }
        };

        // Rendimiento en tiempo real
        _performanceMonitor.PerformanceUpdated += (metrics) =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                RamText = metrics.RamFormatted;
                CpuText = metrics.CpuFormatted;
                GpuText = metrics.GpuFormatted;
            });
        };

        // Estado de modelos de IA en memoria
        FileFlow.Sdk.ModelSessionRegistry.SessionStateChanged += () =>
        {
            Dispatcher.UIThread.InvokeAsync(UpdateAiModelCount);
        };
        _loc.LanguageChanged += (s, e) =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateAiModelCount();
                UpdateSelectedNodeInfo();
                UpdateUnrebuiltConnections();
                if (_controlBarViewModel.IsRunning)
                {
                    UpdateActiveStatusMessage(_logViewModel.StatusMessage);
                }
                else if (_controlBarViewModel.IsPaused)
                {
                    StatusMessage = _loc.GetString("StatusBar_Paused", "⏸️ Flujo pausado");
                }
                else
                {
                    StatusMessage = _loc.GetString("StatusBar_Ready", "🟢 Listo");
                }
            });
        };
        UpdateAiModelCount();
    }

    private void Node_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.IsModelLoaded))
        {
            Dispatcher.UIThread.InvokeAsync(UpdateAiModelCount);
        }
    }

    public void UpdateAiModelCount()
    {
        int sessionCount = FileFlow.Sdk.ModelSessionRegistry.GetTotalLoadedSessions();
        int canvasLoadedCount = _editorViewModel.Nodes.Count(n => n.IsModelLoaded);
        int total = Math.Max(canvasLoadedCount, sessionCount);

        LoadedAiModelsCount = total;
        HasLoadedAiModels = total > 0;
        LoadedAiModelsText = string.Format(
            _loc.GetString("StatusBar_AiModelsLoadedCount", "{0} loaded"),
            total);
        LoadedAiModelsToolTip = _loc.GetString("StatusBar_ClearAiMemoryToolTip", "Descarga todos los modelos ONNX/IA de la memoria RAM/VRAM y libera memoria.");
    }

    [RelayCommand]
    public void ClearAllAiModels()
    {
        FileFlow.Sdk.ModelSessionRegistry.ClearAllSessions();
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

    /// <summary>
    /// El resumen de lo que la última acción no pudo reconstruir, en la barra de estado: el mismo recuento que
    /// el aviso del lienzo, que no se lleva aquí sino que se lee de allí.
    ///
    /// Está en la barra y no sólo en el cartel porque son dos sitios distintos para lo mismo: el cartel dice
    /// <b>qué</b> pasó y ofrece el arreglo donde el usuario acaba de actuar, y la barra —que está a la vista
    /// aunque ese lienzo no lo esté— deja constancia de que el grafo que se está mirando tiene cables de
    /// menos. Es un dato del grafo, no del último cartel, y por eso vive junto a los nodos y las conexiones que
    /// ya se cuentan ahí.
    /// </summary>
    public void UpdateUnrebuiltConnections()
    {
        int count = _editorViewModel.UnrebuiltConnectionsCount;

        UnrebuiltConnectionsCount = count;
        HasUnrebuiltConnections = count > 0;
        UnrebuiltConnectionsText = count == 1
            ? _loc.GetString("StatusBar_UnrebuiltConnection", "🔌 1 conexión perdida")
            : _loc.GetFormattedString("StatusBar_UnrebuiltConnections", "🔌 {0} conexiones perdidas", count);
        UnrebuiltConnectionsToolTip = _loc.GetString(
            "StatusBar_UnrebuiltConnectionsToolTip",
            "Conexiones que no se pudieron reconstruir: el detalle y el arreglo están en el aviso del lienzo");
    }

    private void UpdateActiveStatusMessage(string? rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage) || rawMessage == "Listo" || rawMessage == "Ready" || rawMessage == _loc["StatusReady"])
        {
            StatusMessage = _loc.GetString("StatusBar_Running", "⚡ Ejecutando flujo de trabajo...");
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
        SelectedNodeName = sel != null 
            ? $"{sel.Title} ({sel.NodeTypeName})" 
            : _loc.GetString("Common_None", "Ninguno");
    }

    [RelayCommand]
    public void OpenGlobalOutputFolder()
    {
        try
        {
            // La carpeta de salida por defecto de verdad —la de los ajustes, que es la que el motor usa cuando el
            // flujo no declara ninguna—, no una ruta de Windows escrita a mano que en otros sistemas no existe.
            string folder = string.IsNullOrWhiteSpace(GlobalOutputDir)
                ? FileFlow.Sdk.Storage.AppPaths.DefaultGlobalOutputDir
                : GlobalOutputDir;
            if (!_processLauncher.OpenFolder(folder))
            {
                string errorMsg = string.Format(_loc.GetString("Msg_GlobalOutputFolderError", "No se pudo abrir la carpeta de salida global: {0}"), folder);
                string title = _loc.GetString("Error", "Error");
                _dialogService.ShowError(errorMsg, title);
            }
        }
        catch (Exception ex)
        {
            string errorMsg = string.Format(_loc.GetString("Msg_GlobalOutputFolderError", "No se pudo abrir la carpeta de salida global: {0}"), ex.Message);
            string title = _loc.GetString("Error", "Error");
            _dialogService.ShowError(errorMsg, title);
        }
    }

    [RelayCommand]
    public async Task OpenWorkflowSettings()
    {
        await _editorViewModel.OpenWorkflowSettings();
    }

    [RelayCommand]
    public void FitToScreen()
    {
        _editorViewModel.FitToScreen();
    }
}
