using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;

namespace FileFlow.App.ViewModels;

public partial class ControlBarViewModel : ObservableObject
{
    private readonly EditorViewModel _editorViewModel;
    private readonly PluginLoader _pluginLoader;
    private readonly LogViewModel _logViewModel;
    private readonly NodeInspectorViewModel _nodeInspectorViewModel;
    private readonly IFileDialogService _fileDialogService;
    private readonly IWorkflowStorageService _workflowStorageService;
    private WorkflowExecutor? _activeExecutor;
    private WorkflowDebugSession? _activeDebugSession;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isDebugging;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isPausedAtBreakpointOrError;

    [ObservableProperty]
    private bool _isDryRun;

    [ObservableProperty]
    private bool _isMenuOpen;

    [ObservableProperty]
    private string _workflowName = "Flujo de Procesamiento de Archivos";

    [ObservableProperty]
    private string _selectedLanguage = "es-ES";

    partial void OnSelectedLanguageChanged(string value)
    {
        FileFlow.Sdk.Localization.LocalizationManager.Instance.SetCulture(value);
    }

    [ObservableProperty]
    private string _selectedTheme = "Dark";

    partial void OnSelectedThemeChanged(string value)
    {
        if (Enum.TryParse<Services.AppTheme>(value, true, out var theme))
        {
            Services.ThemeManager.Instance.SetTheme(theme);
        }
    }

    public ControlBarViewModel(
        EditorViewModel editorViewModel, 
        PluginLoader pluginLoader, 
        LogViewModel logViewModel, 
        NodeInspectorViewModel nodeInspectorViewModel,
        IFileDialogService fileDialogService,
        IWorkflowStorageService workflowStorageService)
    {
        _editorViewModel = editorViewModel;
        _pluginLoader = pluginLoader;
        _logViewModel = logViewModel;
        _nodeInspectorViewModel = nodeInspectorViewModel;
        _fileDialogService = fileDialogService;
        _workflowStorageService = workflowStorageService;

        SyncFromPreferences();
        UserPreferencesService.Instance.PreferencesChanged += SyncFromPreferences;
    }

    private void SyncFromPreferences()
    {
        var prefs = UserPreferencesService.Instance.Preferences;
        SelectedTheme = prefs.ActiveTheme;
        IsDryRun = prefs.DefaultDryRunState;
    }

    public EditorViewModel Editor => _editorViewModel;
    public NodeInspectorViewModel NodeInspector => _nodeInspectorViewModel;

    [RelayCommand]
    public void OpenWorkflowSettings()
    {
        _editorViewModel.OpenWorkflowSettings();
    }

    [RelayCommand]
    public void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
    }

    [RelayCommand]
    public void ToggleInspector()
    {
        _nodeInspectorViewModel.TogglePanel();
    }

    [RelayCommand]
    public async Task ExecuteWorkflowAsync()
    {
        await RunWorkflowCoreAsync(isDebug: false);
    }

    [RelayCommand]
    public async Task DebugWorkflowAsync()
    {
        await RunWorkflowCoreAsync(isDebug: true);
    }

    private async Task RunWorkflowCoreAsync(bool isDebug)
    {
        if (IsRunning) return;

        try
        {
            IsRunning = true;
            IsDebugging = isDebug;
            IsPaused = false;
            IsPausedAtBreakpointOrError = false;
            _cts = new CancellationTokenSource();

            _editorViewModel.ClearDebugStates();

            var graph = _editorViewModel.ExportToGraphModel(WorkflowName);

            int maxParallelThreads = UserPreferencesService.Instance.Preferences.MaxParallelThreads;
            if (maxParallelThreads <= 0) maxParallelThreads = Environment.ProcessorCount;

            _activeExecutor = new WorkflowExecutor
            {
                IsDryRun = IsDryRun,
                MaxDegreeOfParallelism = isDebug ? 1 : maxParallelThreads
            };

            if (isDebug)
            {
                _activeDebugSession = new WorkflowDebugSession
                {
                    IsDebugMode = true,
                    BreakOnError = true
                };

                _activeDebugSession.NodeStatusChanged += (nodeId, status, details) =>
                {
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                        if (node != null)
                        {
                            node.SetExecutionStatus(status, details);

                            if (status == NodeExecutionStatus.PausedAtBreakpoint || status == NodeExecutionStatus.PausedOnError)
                            {
                                IsPausedAtBreakpointOrError = true;
                                _nodeInspectorViewModel.InspectNode(node, autoOpen: true);
                            }
                            else if (status == NodeExecutionStatus.Running)
                            {
                                IsPausedAtBreakpointOrError = false;
                            }
                        }
                    });
                };

                _activeDebugSession.SnapshotRecorded += (snapshot) =>
                {
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(snapshot.NodeId, StringComparison.OrdinalIgnoreCase));
                        node?.AddSnapshot(snapshot);
                    });
                };

                _activeExecutor.DebugSession = _activeDebugSession;
            }

            var pendingEdgeUpdates = new ConcurrentDictionary<string, (string src, string port, int count)>(StringComparer.OrdinalIgnoreCase);
            var pendingStatusUpdates = new ConcurrentDictionary<string, NodeExecutionStatus>(StringComparer.OrdinalIgnoreCase);
            (double pct, string status)? pendingProgress = null;

            var visualFlushTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(35)
            };
            visualFlushTimer.Tick += (_, _) =>
            {
                foreach (var key in pendingEdgeUpdates.Keys)
                {
                    if (pendingEdgeUpdates.TryRemove(key, out var edgeInfo))
                    {
                        _editorViewModel.UpdateEdgeDispatched(edgeInfo.src, edgeInfo.port, edgeInfo.count);
                    }
                }

                foreach (var nodeId in pendingStatusUpdates.Keys)
                {
                    if (pendingStatusUpdates.TryRemove(nodeId, out var status))
                    {
                        var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                        node?.SetExecutionStatus(status);
                    }
                }

                if (pendingProgress.HasValue)
                {
                    var p = pendingProgress.Value;
                    _logViewModel.ProgressPercentage = p.pct;
                    _logViewModel.StatusMessage = p.status;
                }
            };
            visualFlushTimer.Start();

            _activeExecutor.NodeStatusChanged += (nodeId, status) =>
            {
                if (_activeDebugSession != null && _activeDebugSession.IsPaused && _activeDebugSession.CurrentPausedNodeId == nodeId)
                {
                    return;
                }
                pendingStatusUpdates[nodeId] = status;
            };

            _activeExecutor.NodeProgressChanged += (nodeId, pct, message) =>
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                    node?.UpdateProgress(pct, message);
                }, DispatcherPriority.Background);
            };

            _activeExecutor.ProgressChanged += (pct, status) =>
            {
                pendingProgress = (pct, status);
            };

            _activeExecutor.LogEmitted += (msg, level) =>
            {
                _logViewModel.AddLog(level, msg);
            };

            _activeExecutor.EdgeItemDispatched += (src, port, count) =>
            {
                pendingEdgeUpdates[$"{src}:{port}"] = (src, port, count);
            };

            string startMsg = isDebug ? "Iniciando depuración del flujo..." : (IsDryRun ? "[Dry Run] Iniciando simulación virtual..." : FileFlow.Sdk.Localization.LocalizationManager.Instance["LogStartingExecution"]);
            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, startMsg);

            try
            {
                await Task.Run(async () =>
                {
                    await _activeExecutor.ExecuteAsync(graph, _pluginLoader, _cts.Token);
                }, _cts.Token);
            }
            finally
            {
                visualFlushTimer.Stop();
                // Final flush
                foreach (var key in pendingEdgeUpdates.Keys)
                {
                    if (pendingEdgeUpdates.TryRemove(key, out var edgeInfo))
                    {
                        _editorViewModel.UpdateEdgeDispatched(edgeInfo.src, edgeInfo.port, edgeInfo.count);
                    }
                }
                foreach (var nodeId in pendingStatusUpdates.Keys)
                {
                    if (pendingStatusUpdates.TryRemove(nodeId, out var status))
                    {
                        var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                        node?.SetExecutionStatus(status);
                    }
                }

                if (pendingProgress.HasValue)
                {
                    var p = pendingProgress.Value;
                    _logViewModel.ProgressPercentage = p.pct;
                    _logViewModel.StatusMessage = p.status;
                }

                _logViewModel.FlushAllPendingLogs();
            }

            _lastJournalService = _activeExecutor.JournalService;

            if (IsDryRun)
            {
                _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, $"[Dry Run] Simulación finalizada. {_activeExecutor.PlannedActions.Count} acciones planificadas registradas.");
            }
            else
            {
                _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, FileFlow.Sdk.Localization.LocalizationManager.Instance["LogExecutionFinished"]);
            }
        }
        catch (OperationCanceledException)
        {
            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Warning, FileFlow.Sdk.Localization.LocalizationManager.Instance["LogExecutionCancelled"]);
        }
        catch (Exception ex)
        {
            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Error, $"Error de Ejecución: {ex.Message}");
            if (!isDebug)
            {
                MessageBox.Show($"Error al ejecutar el flujo: {ex.Message}", "Error de Ejecución", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsRunning = false;
            IsDebugging = false;
            IsPaused = false;
            IsPausedAtBreakpointOrError = false;
            _cts?.Dispose();
            _cts = null;
            _activeExecutor = null;
            _activeDebugSession = null;
        }
    }


    private ExecutionJournalService? _lastJournalService;

    [RelayCommand]
    public async Task ExecuteDryRunAsync()
    {
        IsDryRun = true;
        try
        {
            await RunWorkflowCoreAsync(isDebug: false);
        }
        finally
        {
            IsDryRun = false;
        }
    }

    [RelayCommand]
    public async Task RollbackLastExecutionAsync()
    {
        if (_lastJournalService == null || _lastJournalService.Entries.Count == 0)
        {
            MessageBox.Show("No hay operaciones registradas para revertir.", "Deshacer Flujo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show($"¿Deseas revertir {_lastJournalService.Entries.Count} operaciones realizadas en la última ejecución?", "Confirmar Deshacer (Rollback)", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, "Iniciando Rollback de operaciones...");
            int undone = await _lastJournalService.RollbackAsync();
            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, $"Rollback completado con éxito: {undone} operaciones revertidas.");
            MessageBox.Show($"Se han revertido {undone} operaciones con éxito.", "Rollback Completado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    public void StepNext()
    {
        if (_activeDebugSession != null)
        {
            if (_activeDebugSession.IsPaused)
            {
                _activeDebugSession.StepNext();
            }
            else
            {
                _activeDebugSession.IsStepMode = true;
            }
            _activeExecutor?.Resume();
            IsPaused = false;
        }
    }

    [RelayCommand]
    public void ContinueWorkflow()
    {
        if (_activeDebugSession != null)
        {
            _activeDebugSession.Continue();
            _activeExecutor?.Resume();
            IsPaused = false;
            IsPausedAtBreakpointOrError = false;
        }
        else if (_activeExecutor != null && IsPaused)
        {
            _activeExecutor.Resume();
            IsPaused = false;
        }
    }

    [RelayCommand]
    public void TogglePause()
    {
        if (!IsRunning || _activeExecutor == null) return;

        if (IsPaused || IsPausedAtBreakpointOrError)
        {
            ContinueWorkflow();
        }
        else
        {
            if (_activeDebugSession != null)
            {
                _activeDebugSession.Pause();
            }
            _activeExecutor.Pause();
            IsPaused = true;
        }
    }

    [RelayCommand]
    public void PauseDebug()
    {
        if (!IsRunning || _activeExecutor == null) return;
        _activeDebugSession?.Pause();
        _activeExecutor.Pause();
        IsPaused = true;
    }

    [RelayCommand]
    public void StopWorkflow()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _activeDebugSession?.Continue(); // Desbloquear si estaba esperando en breakpoint
            _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Warning, "Cancelación solicitada...");
        }
    }

    [RelayCommand]
    public void NewWorkflow()
    {
        IsMenuOpen = false;
        if (_editorViewModel.Nodes.Count > 0)
        {
            var result = MessageBox.Show("¿Deseas crear un nuevo flujo? Se limpiará el lienzo actual.", "Nuevo Flujo", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _editorViewModel.ClearGraph();
        WorkflowName = "Flujo de Procesamiento de Archivos";
        _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, "Nuevo flujo creado.");
    }

    [RelayCommand]
    public async Task SaveWorkflowAsync()
    {
        IsMenuOpen = false;
        var filePath = _fileDialogService.ShowSaveFileDialog("Guardar Flujo", "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*", ".json", "flujo.json");
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                var graph = _editorViewModel.ExportToGraphModel(WorkflowName);
                await _workflowStorageService.SaveWorkflowAsync(filePath, graph);
                _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, $"Flujo guardado en {filePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el flujo: {ex.Message}", "Error al Guardar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public async Task LoadWorkflowAsync()
    {
        IsMenuOpen = false;
        var filePath = _fileDialogService.ShowOpenFileDialog("Cargar Flujo", "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*", ".json");
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                var graph = await _workflowStorageService.LoadWorkflowAsync(filePath);
                _editorViewModel.LoadFromGraphModel(graph);
                WorkflowName = graph.Name;
                _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, $"Flujo cargado desde {filePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el flujo: {ex.Message}", "Error al Cargar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
