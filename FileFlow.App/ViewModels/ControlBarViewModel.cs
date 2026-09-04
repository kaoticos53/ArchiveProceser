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
        FileFlow.Sdk.Localization.LocalizationManager.Instance.SetCulture(value);
        var prefs = UserPreferencesService.Instance.Preferences;
        if (!string.Equals(prefs.Language, value, StringComparison.OrdinalIgnoreCase))
        {
            prefs.Language = value;
            UserPreferencesService.Instance.Save();
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
        ThemeManager.Instance.SetThemeById(value);

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
        SelectedTheme = ThemeManager.Instance.CurrentThemeId;
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

        _executionCoordinator = new WorkflowExecutionCoordinator(
            editorViewModel,
            pluginLoader,
            logViewModel,
            nodeInspectorViewModel
        );

        SyncFromPreferences();
        UserPreferencesService.Instance.PreferencesChanged += SyncFromPreferences;
    }

    private void SyncFromPreferences()
    {
        LoadAvailableThemes();
        var prefs = UserPreferencesService.Instance.Preferences;
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
            string msg = LocalizationManager.Instance.GetString("Msg_WatchModeNoSource", "No se encontraron carpetas de origen configuradas.");
            string title = LocalizationManager.Instance.GetString("WatchMode", "Modo Vigilante");
            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using var watcher = new FolderWatcherService();
        watcher.Start(watchFolders, filter: "*.*", includeSubdirectories: true, debounceMs: 1000);

        IsWatching = true;
        _logViewModel.AddLog(LogLevel.Information, $"👁️ Modo Vigilante activado. Escuchando {watchFolders.Count} carpetas: {string.Join(", ", watchFolders)}");

        try
        {
            await RunWorkflowCoreAsync(isDebug: false, isWatchMode: true, watcherService: watcher);
        }
        finally
        {
            watcher.Stop();
            IsWatching = false;
            _logViewModel.AddLog(LogLevel.Information, "👁️ Modo Vigilante detenido.");
        }
    }

    private async Task RunWorkflowCoreAsync(bool isDebug, bool isWatchMode = false, FolderWatcherService? watcherService = null)
    {
        if (IsRunning) return;

        try
        {
            IsRunning = true;
            IsDebugging = isDebug;
            IsPaused = false;
            IsPausedAtBreakpointOrError = false;
            _cts = new CancellationTokenSource();

            int maxParallelThreads = UserPreferencesService.Instance.Preferences.MaxParallelThreads;
            if (maxParallelThreads <= 0) maxParallelThreads = Environment.ProcessorCount;

            var options = new WorkflowExecutionOptions(
                IsDebug: isDebug,
                IsDryRun: IsDryRun,
                MaxParallelThreads: maxParallelThreads,
                WorkflowName: WorkflowName,
                IsWatchMode: isWatchMode,
                WatcherService: watcherService
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
                _logViewModel.AddLog(LogLevel.Warning, FileFlow.Sdk.Localization.LocalizationManager.Instance["LogExecutionCancelled"]);
            }
            else if (!result.Succeeded && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                _logViewModel.AddLog(LogLevel.Error, $"Error: {result.ErrorMessage}");
                if (!isDebug && !isWatchMode)
                {
                    string msg = string.Format(LocalizationManager.Instance.GetString("Msg_ExecutionError", "Error al ejecutar el flujo: {0}"), result.ErrorMessage);
                    string title = LocalizationManager.Instance.GetString("Error", "Error");
                    MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (result.Succeeded)
            {
                if (IsDryRun)
                {
                    _logViewModel.AddLog(LogLevel.Information, $"[Dry Run] Simulación finalizada. {result.PlannedActionsCount} acciones planificadas registradas.");
                }
                else if (!isWatchMode)
                {
                    _logViewModel.AddLog(LogLevel.Information, FileFlow.Sdk.Localization.LocalizationManager.Instance["LogExecutionFinished"]);
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
            string noEntriesMsg = LocalizationManager.Instance.GetString("Msg_RollbackNoEntries", "No hay operaciones registradas para revertir.");
            string rollbackTitle = LocalizationManager.Instance.GetString("RollbackBtn", "Deshacer");
            MessageBox.Show(noEntriesMsg, rollbackTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string confirmMsg = string.Format(LocalizationManager.Instance.GetString("Msg_RollbackConfirm", "¿Deseas revertir {0} operaciones realizadas en la última ejecución?"), _lastJournalService.Entries.Count);
        string confirmTitle = LocalizationManager.Instance.GetString("RollbackBtn", "Deshacer");
        var result = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _logViewModel.AddLog(LogLevel.Information, "Iniciando Rollback de operaciones...");
            int undone = await _lastJournalService.RollbackAsync();
            _logViewModel.AddLog(LogLevel.Information, $"Rollback completado con éxito: {undone} operaciones revertidas.");
            string successMsg = string.Format(LocalizationManager.Instance.GetString("Msg_RollbackSuccess", "Se han revertido {0} operaciones con éxito."), undone);
            MessageBox.Show(successMsg, confirmTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    public void StepNext()
    {
        var debugSession = _executionCoordinator.ActiveDebugSession;
        if (debugSession != null)
        {
            if (debugSession.IsPaused)
            {
                debugSession.StepNext();
            }
            else
            {
                debugSession.IsStepMode = true;
            }
            _executionCoordinator.ActiveExecutor?.Resume();
            IsPaused = false;
        }
    }

    [RelayCommand]
    public void ContinueWorkflow()
    {
        var debugSession = _executionCoordinator.ActiveDebugSession;
        if (debugSession != null)
        {
            debugSession.Continue();
            _executionCoordinator.ActiveExecutor?.Resume();
            IsPaused = false;
            IsPausedAtBreakpointOrError = false;
        }
        else if (_executionCoordinator.ActiveExecutor != null && IsPaused)
        {
            _executionCoordinator.ActiveExecutor.Resume();
            IsPaused = false;
        }
    }

    [RelayCommand]
    public void TogglePause()
    {
        var executor = _executionCoordinator.ActiveExecutor;
        if (!IsRunning || executor == null) return;

        if (IsPaused || IsPausedAtBreakpointOrError)
        {
            ContinueWorkflow();
        }
        else
        {
            _executionCoordinator.ActiveDebugSession?.Pause();
            executor.Pause();
            IsPaused = true;
        }
    }

    [RelayCommand]
    public void PauseDebug()
    {
        var executor = _executionCoordinator.ActiveExecutor;
        if (!IsRunning || executor == null) return;
        _executionCoordinator.ActiveDebugSession?.Pause();
        executor.Pause();
        IsPaused = true;
    }

    [RelayCommand]
    public void StopWorkflow()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _executionCoordinator.ActiveDebugSession?.Continue();
            _logViewModel.AddLog(LogLevel.Warning, "Cancelación solicitada...");
        }
    }

    [RelayCommand]
    public void NewWorkflow()
    {
        IsMenuOpen = false;
        if (_editorViewModel.Nodes.Count > 0)
        {
            string confirmMsg = LocalizationManager.Instance.GetString("Msg_NewWorkflowConfirm", "¿Deseas crear un nuevo flujo? Se limpiará el lienzo actual.");
            string confirmTitle = LocalizationManager.Instance.GetString("NewWorkflowBtn", "Nuevo Flujo");
            var result = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _editorViewModel.ClearGraph();
        WorkflowName = "Flujo de Procesamiento de Archivos";
        _logViewModel.AddLog(LogLevel.Information, "Nuevo flujo creado.");
    }

    [RelayCommand]
    public async Task SaveWorkflowAsync()
    {
        IsMenuOpen = false;
        string saveTitle = LocalizationManager.Instance.GetString("SaveWorkflowBtn", "Guardar Flujo");
        var filePath = _fileDialogService.ShowSaveFileDialog(saveTitle, "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*", ".json", "flujo.json");
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                var graph = _editorViewModel.ExportToGraphModel(WorkflowName);
                await _workflowStorageService.SaveWorkflowAsync(filePath, graph);
                _logViewModel.AddLog(LogLevel.Information, $"Flujo guardado en {filePath}");
            }
            catch (Exception ex)
            {
                string errorMsg = string.Format(LocalizationManager.Instance.GetString("Msg_SaveError", "Error al guardar el flujo: {0}"), ex.Message);
                string errorTitle = LocalizationManager.Instance.GetString("Error", "Error");
                MessageBox.Show(errorMsg, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public async Task LoadWorkflowAsync()
    {
        IsMenuOpen = false;
        string loadTitle = LocalizationManager.Instance.GetString("LoadWorkflowBtn", "Cargar Flujo");
        var filePath = _fileDialogService.ShowOpenFileDialog(loadTitle, "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*", ".json");
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                var graph = await _workflowStorageService.LoadWorkflowAsync(filePath);
                _editorViewModel.LoadFromGraphModel(graph);
                WorkflowName = graph.Name;
                _logViewModel.AddLog(LogLevel.Information, $"Flujo cargado desde {filePath}");
            }
            catch (Exception ex)
            {
                string errorMsg = string.Format(LocalizationManager.Instance.GetString("Msg_LoadError", "Error al cargar el flujo: {0}"), ex.Message);
                string errorTitle = LocalizationManager.Instance.GetString("Error", "Error");
                MessageBox.Show(errorMsg, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void OpenUserManual()
    {
        IsMenuOpen = false;
        try
        {
            bool isEnglish = LocalizationManager.Instance.CurrentLanguage.Equals("en", StringComparison.OrdinalIgnoreCase);

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
                _logViewModel.AddLog(LogLevel.Information, $"Abriendo manual de usuario: {manualPath}");
            }
            else
            {
                string title = LocalizationManager.Instance.GetString("ControlBar_UserManual", "Manual de Usuario");
                string notFoundMsg = LocalizationManager.Instance.GetString("ControlBar_ManualNotFound", "No se encontró el archivo del manual de usuario.");
                MessageBox.Show(notFoundMsg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            string title = LocalizationManager.Instance.GetString("ControlBar_UserManual", "Manual de Usuario");
            MessageBox.Show($"Error: {ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
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
                _logViewModel.AddLog(LogLevel.Information, $"Abriendo carpeta de ejemplos: {examplesPath}");
            }
            else
            {
                string notFoundMsg = LocalizationManager.Instance.GetString("Msg_ExamplesNotFound", "No se encontró la carpeta de ejemplos.");
                string examplesTitle = LocalizationManager.Instance.GetString("ControlBar_ExamplesBtn", "Ejemplos de Flujos");
                MessageBox.Show(notFoundMsg, examplesTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            string errorMsg = string.Format(LocalizationManager.Instance.GetString("Msg_ExamplesOpenError", "Error al abrir la carpeta de ejemplos: {0}"), ex.Message);
            string examplesTitle = LocalizationManager.Instance.GetString("ControlBar_ExamplesBtn", "Ejemplos de Flujos");
            MessageBox.Show(errorMsg, examplesTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show($"Error al abrir la ventana Acerca de: {ex.Message}", "FileFlow Studio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UserPreferencesService.Instance.PreferencesChanged -= SyncFromPreferences;
        GC.SuppressFinalize(this);
    }
}
