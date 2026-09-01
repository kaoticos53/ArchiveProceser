using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Themes;

namespace FileFlow.App.ViewModels;

public partial class ControlBarViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
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
    private string _selectedTheme = "dark_fluent";

    public ObservableCollection<ThemeDefinition> AvailableThemes { get; } = [];

    public void LoadAvailableThemes()
    {
        AvailableThemes.Clear();
        var all = CustomThemeService.Instance.GetAllThemes();
        foreach (var theme in all)
        {
            AvailableThemes.Add(theme);
        }

        AvailableThemes.Add(new ThemeDefinition
        {
            Id = "system",
            Name = "💻 Tema del Sistema (Windows)",
            Description = "Adapta automáticamente el tema según Windows.",
            IsBuiltIn = true
        });
    }

    partial void OnSelectedThemeChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        Services.ThemeManager.Instance.SetThemeById(value);

        var prefs = UserPreferencesService.Instance.Preferences;
        if (!string.Equals(prefs.ActiveTheme, value, StringComparison.OrdinalIgnoreCase))
        {
            prefs.ActiveTheme = value;
            UserPreferencesService.Instance.Save();
        }
    }

    [RelayCommand]
    public void OpenThemeCustomizer()
    {
        var win = new Views.Components.ThemeCustomizerWindow();
        if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            win.Owner = Application.Current.MainWindow;
        }
        win.ShowDialog();

        LoadAvailableThemes();
        SelectedTheme = Services.ThemeManager.Instance.CurrentThemeId;
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
        LoadAvailableThemes();
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
            var pendingNodeProgressUpdates = new ConcurrentDictionary<string, (double pct, string message)>(StringComparer.OrdinalIgnoreCase);

            var visualFlushTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(33) // 30 FPS
            };
            visualFlushTimer.Tick += (_, _) =>
            {
                if (_activeExecutor != null)
                {
                    var snapshot = _activeExecutor.GetTelemetrySnapshot();
                    _logViewModel.ProgressPercentage = snapshot.Percentage;
                    _logViewModel.StatusMessage = snapshot.StatusMessage;
                }

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

                foreach (var nodeId in pendingNodeProgressUpdates.Keys)
                {
                    if (pendingNodeProgressUpdates.TryRemove(nodeId, out var progressInfo))
                    {
                        var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                        node?.UpdateProgress(progressInfo.pct, progressInfo.message);
                    }
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
                pendingNodeProgressUpdates[nodeId] = (pct, message);
            };

            _activeExecutor.StructuredLogEmitted += (rec) =>
            {
                _logViewModel.AddStructuredLog(rec);
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
                if (_activeExecutor != null)
                {
                    var finalSnapshot = _activeExecutor.GetTelemetrySnapshot();
                    _logViewModel.ProgressPercentage = finalSnapshot.Percentage;
                    _logViewModel.StatusMessage = finalSnapshot.StatusMessage;
                }

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
                foreach (var nodeId in pendingNodeProgressUpdates.Keys)
                {
                    if (pendingNodeProgressUpdates.TryRemove(nodeId, out var progressInfo))
                    {
                        var node = _editorViewModel.Nodes.FirstOrDefault(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
                        node?.UpdateProgress(progressInfo.pct, progressInfo.message);
                    }
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

    [RelayCommand]
    public void OpenUserManual()
    {
        IsMenuOpen = false;
        try
        {
            string? manualPath = FindFileInAppOrRepo("Docs", "manual_de_usuario.md", "docs/manual_de_usuario.md");
            if (manualPath != null && File.Exists(manualPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = manualPath,
                    UseShellExecute = true
                });
                _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, $"Abriendo manual de usuario: {manualPath}");
            }
            else
            {
                MessageBox.Show("No se encontró el archivo del manual de usuario.", "Manual de Usuario", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir el manual de usuario: {ex.Message}", "Manual de Usuario", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void OpenExamplesFolder()
    {
        IsMenuOpen = false;
        try
        {
            string? examplesPath = FindDirectoryInAppOrRepo("Examples", "docs/examples");
            if (examplesPath != null && Directory.Exists(examplesPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = examplesPath,
                    UseShellExecute = true
                });
                _logViewModel.AddLog(FileFlow.Sdk.LogLevel.Information, $"Abriendo carpeta de ejemplos: {examplesPath}");
            }
            else
            {
                MessageBox.Show("No se encontró la carpeta de ejemplos.", "Ejemplos de Flujos", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir la carpeta de ejemplos: {ex.Message}", "Ejemplos de Flujos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? FindFileInAppOrRepo(string installedSubdir, string installedFileName, string repoRelativePath)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string installedPath = Path.Combine(baseDir, installedSubdir, installedFileName);
        if (File.Exists(installedPath)) return installedPath;

        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    private static string? FindDirectoryInAppOrRepo(string installedSubdir, string repoRelativePath)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string installedPath = Path.Combine(baseDir, installedSubdir);
        if (Directory.Exists(installedPath)) return installedPath;

        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UserPreferencesService.Instance.PreferencesChanged -= SyncFromPreferences;
        GC.SuppressFinalize(this);
    }
}
