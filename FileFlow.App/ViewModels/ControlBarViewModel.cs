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
            MessageBox.Show("No se encontraron carpetas de origen existentes configuradas en los nodos del flujo (ej. FolderSourceNode).\nConfigure una carpeta de entrada antes de activar el Modo Vigilante.", "Modo Vigilante", MessageBoxButton.OK, MessageBoxImage.Information);
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
                _logViewModel.AddLog(LogLevel.Error, $"Error de Ejecución: {result.ErrorMessage}");
                if (!isDebug && !isWatchMode)
                {
                    MessageBox.Show($"Error al ejecutar el flujo: {result.ErrorMessage}", "Error de Ejecución", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show("No hay operaciones registradas para revertir.", "Deshacer Flujo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show($"¿Deseas revertir {_lastJournalService.Entries.Count} operaciones realizadas en la última ejecución?", "Confirmar Deshacer (Rollback)", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _logViewModel.AddLog(LogLevel.Information, "Iniciando Rollback de operaciones...");
            int undone = await _lastJournalService.RollbackAsync();
            _logViewModel.AddLog(LogLevel.Information, $"Rollback completado con éxito: {undone} operaciones revertidas.");
            MessageBox.Show($"Se han revertido {undone} operaciones con éxito.", "Rollback Completado", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var result = MessageBox.Show("¿Deseas crear un nuevo flujo? Se limpiará el lienzo actual.", "Nuevo Flujo", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
        var filePath = _fileDialogService.ShowSaveFileDialog("Guardar Flujo", "Flujo FileFlow (*.json)|*.json|Todos los archivos (*.*)|*.*", ".json", "flujo.json");
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
                _logViewModel.AddLog(LogLevel.Information, $"Flujo cargado desde {filePath}");
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
                MessageBox.Show("No se encontró la carpeta de ejemplos.", "Ejemplos de Flujos", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir la carpeta de ejemplos: {ex.Message}", "Ejemplos de Flujos", MessageBoxButton.OK, MessageBoxImage.Error);
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
