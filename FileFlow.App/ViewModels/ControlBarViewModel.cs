using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
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
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _loc;
    private readonly IDialogService _dialogService;
    private readonly IProcessLauncherService _processLauncher;
    private readonly WorkflowExecutionCoordinator _executionCoordinator;

    private CancellationTokenSource? _cts;
    private ExecutionJournalService? _lastJournalService;

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
    private bool _isWatching;

    [ObservableProperty]
    private bool _isMenuOpen;

    [ObservableProperty]
    private string _workflowName = "Flujo de Procesamiento de Archivos";

    [ObservableProperty]
    private string _selectedLanguage = "es-ES";

    partial void OnSelectedLanguageChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        _loc.SetCulture(value);
        var prefs = _userPreferencesService.Preferences;
        if (!string.Equals(prefs.Language, value, StringComparison.OrdinalIgnoreCase))
        {
            prefs.Language = value;
            _userPreferencesService.Save();
        }
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
        _themeService.SetThemeById(value);

        var prefs = _userPreferencesService.Preferences;
        if (!string.Equals(prefs.ActiveTheme, value, StringComparison.OrdinalIgnoreCase))
        {
            prefs.ActiveTheme = value;
            _userPreferencesService.Save();
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
        SelectedTheme = _themeService.CurrentThemeId;
    }

    public ControlBarViewModel(
        EditorViewModel editorViewModel, 
        PluginLoader pluginLoader, 
        LogViewModel logViewModel, 
        NodeInspectorViewModel nodeInspectorViewModel,
        IFileDialogService fileDialogService,
        IWorkflowStorageService workflowStorageService,
        IUserPreferencesService? userPreferencesService = null,
        IThemeService? themeService = null,
        ILocalizationService? localizationService = null,
        IDialogService? dialogService = null,
        IProcessLauncherService? processLauncher = null)
    {
        _editorViewModel = editorViewModel;
        _pluginLoader = pluginLoader;
        _logViewModel = logViewModel;
        _nodeInspectorViewModel = nodeInspectorViewModel;
        _fileDialogService = fileDialogService;
        _workflowStorageService = workflowStorageService;
        _userPreferencesService = userPreferencesService ?? UserPreferencesService.Instance;
        _themeService = themeService ?? ThemeManager.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;
        _dialogService = dialogService ?? WpfDialogService.Instance;
        _processLauncher = processLauncher ?? ProcessLauncherService.Instance;

        _executionCoordinator = new WorkflowExecutionCoordinator(
            editorViewModel,
            pluginLoader,
            logViewModel,
            nodeInspectorViewModel
        );

        SyncFromPreferences();
        _userPreferencesService.PreferencesChanged += SyncFromPreferences;
    }

    private void SyncFromPreferences()
    {
        LoadAvailableThemes();
        var prefs = _userPreferencesService.Preferences;
        SelectedTheme = prefs.ActiveTheme;
        IsDryRun = prefs.DefaultDryRunState;
        if (!string.IsNullOrWhiteSpace(prefs.Language) && !string.Equals(SelectedLanguage, prefs.Language, StringComparison.OrdinalIgnoreCase))
        {
            SelectedLanguage = prefs.Language;
        }
    }

    public EditorViewModel Editor => _editorViewModel;
    public NodeInspectorViewModel NodeInspector => _nodeInspectorViewModel;

    [RelayCommand]
    public void OpenWorkflowSettings()
    {
        IsMenuOpen = false;
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

    [RelayCommand]
    public async Task ToggleWatchModeAsync()
    {
        if (IsWatching)
        {
            _cts?.Cancel();
            return;
        }

        if (IsRunning) return;

        // Extraer carpetas de origen del grafo actual
        var graph = _editorViewModel.ExportToGraphModel(WorkflowName);
        var watchFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes)
        {
            foreach (var param in node.Parameters)
            {
                if (param.Value == null) continue;
                string valStr = param.Value.ToString() ?? string.Empty;

                if ((param.Key.Contains("Folder", StringComparison.OrdinalIgnoreCase) ||
                     param.Key.Contains("Directory", StringComparison.OrdinalIgnoreCase) ||
                     param.Key.Contains("Path", StringComparison.OrdinalIgnoreCase)) &&
                    !param.Key.Contains("Output", StringComparison.OrdinalIgnoreCase) &&
                    !param.Key.Contains("Destination", StringComparison.OrdinalIgnoreCase))
                {
                    string expanded = Environment.ExpandEnvironmentVariables(valStr);
                    if (Directory.Exists(expanded))
                    {
                        watchFolders.Add(expanded);
                    }
                }
            }
        }

        if (watchFolders.Count == 0)
        {
            string msg = _loc.GetString("Msg_WatchModeNoSource", "No se encontraron carpetas de origen configuradas.");
            string title = _loc.GetString("WatchMode", "Modo Vigilante");
            _dialogService.ShowInformation(msg, title);
            return;
        }

        using var watcher = new FolderWatcherService();
        watcher.Start(watchFolders, filter: "*.*", includeSubdirectories: true, debounceMs: 1000);

        IsWatching = true;
        _logViewModel.AddLog(LogLevel.Information, _loc.GetFormattedString("Log_WatchModeActive", "👁️ Modo Vigilante activado. Escuchando {0} carpetas: {1}", watchFolders.Count, string.Join(", ", watchFolders)));

        try
        {
            await RunWorkflowCoreAsync(isDebug: false, isWatchMode: true, watcherService: watcher);
        }
        finally
        {
            watcher.Stop();
            IsWatching = false;
            _logViewModel.AddLog(LogLevel.Information, _loc.GetString("Log_WatchModeStopped", "👁️ Modo Vigilante detenido."));
        }
    }

    private async Task RunWorkflowCoreAsync(bool isDebug, bool isWatchMode = false, FolderWatcherService? watcherService = null)
    {
        if (IsRunning) return;

        bool enableCheckpointing = _userPreferencesService.Preferences.EnableCheckpointing;
        if (enableCheckpointing && !isWatchMode && !IsDryRun)
        {
            if (WorkflowCheckpointManager.Instance.HasPendingCheckpoint(WorkflowName, out var savedCp) && savedCp != null && savedCp.CompletedFileKeys.Count > 0)
            {
                string resumeTitle = _loc.GetString("Checkpoint_ResumeTitle", "Punto de Control Detectado");
                string resumeMsg = string.Format(
                    _loc.GetString("Checkpoint_ResumePrompt", "Se ha detectado una ejecución previa de '{0}' interrumpida con {1} archivo(s) ya completados.\n\n¿Deseas REANUDAR la ejecución previa (omitiendo archivos completados)?\n\n• Sí: Reanudar desde el último punto.\n• No: Reiniciar ejecución limpia desde cero.\n• Cancelar: Abortar ejecución."),
                    WorkflowName, savedCp.CompletedFileKeys.Count);

                var userChoice = _dialogService.ShowYesNoCancel(resumeMsg, resumeTitle);
                if (userChoice == DialogResult.Cancel)
                {
                    return;
                }

                if (userChoice == DialogResult.No)
                {
                    WorkflowCheckpointManager.Instance.ClearCheckpoint(WorkflowName);
                    _logViewModel.AddLog(LogLevel.Information, _loc.GetFormattedString("Log_CheckpointReset", "[Checkpoint] Punto de control de '{0}' reiniciado. Iniciando ejecución limpia desde cero.", WorkflowName));
                }
            }
        }

        try
        {
            IsRunning = true;
            IsDebugging = isDebug;
            IsPaused = false;
            IsPausedAtBreakpointOrError = false;
            _cts = new CancellationTokenSource();

            int maxParallelThreads = _userPreferencesService.Preferences.MaxParallelThreads;
            if (maxParallelThreads <= 0) maxParallelThreads = Environment.ProcessorCount;

            var options = new WorkflowExecutionOptions(
                IsDebug: isDebug,
                IsDryRun: IsDryRun,
                MaxParallelThreads: maxParallelThreads,
                WorkflowName: WorkflowName,
                IsWatchMode: isWatchMode,
                WatcherService: watcherService,
                EnableCheckpointing: enableCheckpointing
            );

            var result = await _executionCoordinator.RunAsync(
                options,
                onBreakpointStateChanged: isPausedAtBreakpoint =>
                {
                    IsPausedAtBreakpointOrError = isPausedAtBreakpoint;
                },
                _cts.Token
            );

            _lastJournalService = result.JournalService;

            if (result.Cancelled)
            {
                _logViewModel.AddLog(LogLevel.Warning, _loc["LogExecutionCancelled"]);
            }
            else if (!result.Succeeded && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                _logViewModel.AddLog(LogLevel.Error, $"Error: {result.ErrorMessage}");
                if (!isDebug && !isWatchMode)
                {
                    string msg = string.Format(_loc.GetString("Msg_ExecutionError", "Error al ejecutar el flujo: {0}"), result.ErrorMessage);
                    string title = _loc.GetString("Error", "Error");
                    _dialogService.ShowError(msg, title);
                }
            }
            else if (result.Succeeded)
            {
                if (IsDryRun)
                {
                    _logViewModel.AddLog(LogLevel.Information, _loc.GetFormattedString("Log_DryRunFinished", "[Dry Run] Simulación finalizada. {0} acciones planificadas registradas.", result.PlannedActionsCount));
                }
                else if (!isWatchMode)
                {
                    _logViewModel.AddLog(LogLevel.Information, _loc["LogExecutionFinished"]);
                }
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
        }
    }

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
            string noEntriesMsg = _loc.GetString("Msg_RollbackNoEntries", "No hay operaciones registradas para revertir.");
            string rollbackTitle = _loc.GetString("RollbackBtn", "Deshacer");
            _dialogService.ShowInformation(noEntriesMsg, rollbackTitle);
            return;
        }

        string confirmMsg = string.Format(_loc.GetString("Msg_RollbackConfirm", "¿Deseas revertir {0} operaciones realizadas en la última ejecución?"), _lastJournalService.Entries.Count);
        string confirmTitle = _loc.GetString("RollbackBtn", "Deshacer");
        if (_dialogService.ShowConfirmation(confirmMsg, confirmTitle))
        {
            _logViewModel.AddLog(LogLevel.Information, _loc.GetString("Log_RollbackStarting", "Iniciando Rollback de operaciones..."));
            int undone = await _lastJournalService.RollbackAsync();
            _logViewModel.AddLog(LogLevel.Information, _loc.GetFormattedString("Log_RollbackCompleted", "Rollback completado con éxito: {0} operaciones revertidas.", undone));
            string successMsg = string.Format(_loc.GetString("Msg_RollbackSuccess", "Se han revertido {0} operaciones con éxito."), undone);
            _dialogService.ShowInformation(successMsg, confirmTitle);
        }
    }

    [RelayCommand]
    public void StepNext()
    {
        if (_executionCoordinator.ActiveDebugSession != null && _executionCoordinator.ActiveDebugSession.IsPaused)
        {
            _executionCoordinator.ActiveDebugSession.StepNext();
            IsPausedAtBreakpointOrError = false;
        }
    }

    [RelayCommand]
    public void ResumeWorkflow()
    {
        if (_executionCoordinator.ActiveDebugSession != null && _executionCoordinator.ActiveDebugSession.IsPaused)
        {
            _executionCoordinator.ActiveDebugSession.Continue();
            IsPausedAtBreakpointOrError = false;
        }
        else if (IsPaused)
        {
            _executionCoordinator.ActiveExecutor?.Resume();
            IsPaused = false;
            _logViewModel.AddLog(LogLevel.Information, _loc.GetString("LogExecutionResumed", "Flujo reanudado por el usuario."));
        }
    }

    [RelayCommand]
    public void PauseWorkflow()
    {
        if (IsRunning && !IsPaused)
        {
            _executionCoordinator.ActiveExecutor?.Pause();
            IsPaused = true;
            _logViewModel.AddLog(LogLevel.Warning, _loc.GetString("LogExecutionPaused", "Flujo pausado por el usuario."));
        }
    }

    [RelayCommand]
    public void StopWorkflow()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _executionCoordinator.ActiveDebugSession?.Continue();
            _logViewModel.AddLog(LogLevel.Warning, _loc.GetString("LogCancellationRequested", "Cancelación solicitada..."));
        }
    }

    [RelayCommand]
    public void NewWorkflow()
    {
        IsMenuOpen = false;
        if (_editorViewModel.Nodes.Count > 0)
        {
            string confirmMsg = _loc.GetString("Msg_NewWorkflowConfirm", "¿Deseas crear un nuevo flujo? Se limpiará el lienzo actual.");
            string confirmTitle = _loc.GetString("NewWorkflowBtn", "Nuevo Flujo");
            if (!_dialogService.ShowConfirmation(confirmMsg, confirmTitle))
            {
                return;
            }
        }

        _editorViewModel.ClearGraph();
        WorkflowName = "Flujo de Procesamiento de Archivos";
        _logViewModel.AddLog(LogLevel.Information, _loc.GetString("Log_NewWorkflowCreated", "Nuevo flujo creado."));
    }

    [RelayCommand]
    public async Task SaveWorkflowAsync()
    {
        IsMenuOpen = false;
        string saveTitle = _loc.GetString("SaveWorkflowBtn", "Guardar Flujo");
        var filePath = _fileDialogService.ShowSaveFileDialog(saveTitle, "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*", ".json", "flujo.json");
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                var graph = _editorViewModel.ExportToGraphModel(WorkflowName);
                await _workflowStorageService.SaveWorkflowAsync(filePath, graph);
                _logViewModel.AddLog(LogLevel.Information, _loc.GetFormattedString("LogSavedWorkflow", "Flujo guardado en {0}", filePath));
            }
            catch (Exception ex)
            {
                string errorMsg = string.Format(_loc.GetString("Msg_SaveError", "Error al guardar el flujo: {0}"), ex.Message);
                string errorTitle = _loc.GetString("Error", "Error");
                _dialogService.ShowError(errorMsg, errorTitle);
            }
        }
    }

    [RelayCommand]
    public async Task LoadWorkflowAsync()
    {
        IsMenuOpen = false;
        string loadTitle = _loc.GetString("LoadWorkflowBtn", "Cargar Flujo");
        var filePath = _fileDialogService.ShowOpenFileDialog(loadTitle, "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*", ".json");
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                var graph = await _workflowStorageService.LoadWorkflowAsync(filePath);
                _editorViewModel.LoadFromGraphModel(graph);
                WorkflowName = graph.Name;
                _logViewModel.AddLog(LogLevel.Information, _loc.GetFormattedString("LogLoadedWorkflow", "Flujo cargado desde {0}", filePath));
            }
            catch (Exception ex)
            {
                string errorMsg = string.Format(_loc.GetString("Msg_LoadError", "Error al cargar el flujo: {0}"), ex.Message);
                string errorTitle = _loc.GetString("Error", "Error");
                _dialogService.ShowError(errorMsg, errorTitle);
            }
        }
    }

    [RelayCommand]
    public void OpenUserManual()
    {
        IsMenuOpen = false;
        try
        {
            bool isEnglish = _loc.CurrentLanguage.Equals("en", StringComparison.OrdinalIgnoreCase);

            string? manualPath = null;
            if (isEnglish)
            {
                manualPath = AppResourceLocator.FindFileInAppOrRepo("Docs", "user_manual.pdf", "docs/user_manual.pdf")
                          ?? AppResourceLocator.FindFileInAppOrRepo("Docs", "user_manual.md", "docs/user_manual.md");
            }

            manualPath ??= AppResourceLocator.FindFileInAppOrRepo("Docs", "manual_de_usuario.pdf", "docs/manual_de_usuario.pdf")
                       ?? AppResourceLocator.FindFileInAppOrRepo("Docs", "manual_de_usuario.md", "docs/manual_de_usuario.md")
                       ?? AppResourceLocator.FindFileInAppOrRepo("Docs", "user_manual.pdf", "docs/user_manual.pdf");

            if (manualPath != null && File.Exists(manualPath) && AppResourceLocator.TryOpenPath(manualPath))
            {
                _logViewModel.AddLog(LogLevel.Information, _loc.GetFormattedString("Log_OpenManual", "Abriendo manual de usuario: {0}", manualPath));
            }
            else
            {
                string title = _loc.GetString("ControlBar_UserManual", "Manual de Usuario");
                string notFoundMsg = _loc.GetString("ControlBar_ManualNotFound", "No se encontró el archivo del manual de usuario.");
                _dialogService.ShowWarning(notFoundMsg, title);
            }
        }
        catch (Exception ex)
        {
            string title = _loc.GetString("ControlBar_UserManual", "Manual de Usuario");
            _dialogService.ShowError($"Error: {ex.Message}", title);
        }
    }

    [RelayCommand]
    public void OpenExamplesFolder()
    {
        IsMenuOpen = false;
        try
        {
            string? examplesPath = AppResourceLocator.FindDirectoryInAppOrRepo("Examples", "docs/examples");
            if (examplesPath != null && Directory.Exists(examplesPath) && AppResourceLocator.TryOpenPath(examplesPath))
            {
                _logViewModel.AddLog(LogLevel.Information, _loc.GetFormattedString("Log_OpenExamples", "Abriendo carpeta de ejemplos: {0}", examplesPath));
            }
            else
            {
                string notFoundMsg = _loc.GetString("Msg_ExamplesNotFound", "No se encontró la carpeta de ejemplos.");
                string examplesTitle = _loc.GetString("ControlBar_ExamplesBtn", "Ejemplos de Flujos");
                _dialogService.ShowWarning(notFoundMsg, examplesTitle);
            }
        }
        catch (Exception ex)
        {
            string errorMsg = string.Format(_loc.GetString("Msg_ExamplesOpenError", "Error al abrir la carpeta de ejemplos: {0}"), ex.Message);
            string examplesTitle = _loc.GetString("ControlBar_ExamplesBtn", "Ejemplos de Flujos");
            _dialogService.ShowError(errorMsg, examplesTitle);
        }
    }

    [RelayCommand]
    public void OpenAboutDialog()
    {
        IsMenuOpen = false;
        try
        {
            var aboutDialog = new Views.AboutDialogWindow
            {
                Owner = Application.Current?.MainWindow
            };
            aboutDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            string msg = string.Format(_loc.GetString("Msg_OpenAboutError", "Error al abrir la ventana Acerca de: {0}"), ex.Message);
            string title = _loc.GetString("App_Name", "FileFlow Studio");
            _dialogService.ShowError(msg, title);
        }
    }

    [RelayCommand]
    public void OpenMetricsDashboard()
    {
        IsMenuOpen = false;
        try
        {
            var dashboardVm = new WorkflowMetricsDashboardViewModel(_editorViewModel);
            var dashboardWindow = new Views.Components.WorkflowMetricsDashboardWindow(dashboardVm)
            {
                Owner = Application.Current?.MainWindow
            };
            dashboardWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            string msg = string.Format(_loc.GetString("Msg_OpenMetricsError", "Error al abrir el panel de métricas: {0}"), ex.Message);
            string title = _loc.GetString("App_Name", "FileFlow Studio");
            _dialogService.ShowError(msg, title);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _userPreferencesService.PreferencesChanged -= SyncFromPreferences;
        GC.SuppressFinalize(this);
    }
}
