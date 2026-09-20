using System.Collections.ObjectModel;
using System.IO;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.App.Themes;

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
    private readonly CustomThemeService _customThemeService;
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
    private bool _hasVirtualFiles;

    [ObservableProperty]
    private int _virtualFilesCount;

    private FileFlow.Sdk.VirtualFileSystem.IVirtualFileSystemStore? _lastVirtualFileSystem;

    // In-App Auto-Updater Notification
    [ObservableProperty]
    private bool _hasPendingUpdate;

    [ObservableProperty]
    private string _pendingUpdateVersionTag = string.Empty;

    private FileFlow.Sdk.Services.AppUpdateInfo? _pendingUpdateInfo;

    public void SetPendingUpdate(FileFlow.Sdk.Services.AppUpdateInfo updateInfo)
    {
        _pendingUpdateInfo = updateInfo;
        PendingUpdateVersionTag = updateInfo.VersionTag;
        HasPendingUpdate = true;
    }

    [RelayCommand]
    public async Task OpenUpdateDialogAsync()
    {
        if (_pendingUpdateInfo == null) return;

        var updateVm = new UpdateDialogViewModel(_pendingUpdateInfo);
        var updateWindow = new Views.Components.UpdateDialogWindow(updateVm);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            await updateWindow.ShowDialog(desktop.MainWindow ?? updateWindow);
        }
    }

    [ObservableProperty]
    private bool _isMenuOpen;

    [ObservableProperty]
    private string _workflowName = "Flujo de Procesamiento de Archivos";

    /// <summary>
    /// Último idioma válido aplicado. Es la red que impide que un valor vacío devuelto por el desplegable
    /// borre el idioma activo: sin ella, el selector quedaba en blanco y ya no había nada que elegir.
    /// </summary>
    private string _appliedLanguage = LanguageCatalog.All[0].Code;

    [ObservableProperty]
    private string _selectedLanguage = LanguageCatalog.All[0].Code;

    partial void OnSelectedLanguageChanged(string value)
    {
        var option = LanguageCatalog.Resolve(value);

        if (option is null)
        {
            // Un ComboBox atado por valor escribe 'null' cuando no encuentra su valor entre los elementos: al
            // montarse, al desmontarse (cerrar la ventana) o si la preferencia guardada ya no se ofrece. El
            // idioma activo no se pierde: se restaura el último válido y el campo se vuelve a pintar.
            if (!string.Equals(SelectedLanguage, _appliedLanguage, StringComparison.Ordinal))
            {
                SelectedLanguage = _appliedLanguage;
            }

            return;
        }

        _appliedLanguage = option.Code;

        if (!string.Equals(option.Code, value, StringComparison.Ordinal))
        {
            // Normaliza la grafía ('es' → 'es-ES') y vuelve a entrar por aquí con un valor del catálogo.
            SelectedLanguage = option.Code;
            return;
        }

        _loc.SetCulture(option.Code);

        var prefs = _userPreferencesService.Preferences;
        if (!string.Equals(prefs.Language, option.Code, StringComparison.OrdinalIgnoreCase))
        {
            prefs.Language = option.Code;
            _userPreferencesService.Save();
        }
    }

    /// <summary>Último tema válido (existente en la lista) mostrado por el selector.</summary>
    private string _appliedThemeId = ThemeManager.DefaultThemeId;

    [ObservableProperty]
    private string _selectedTheme = ThemeManager.DefaultThemeId;

    public ObservableCollection<ThemeDefinition> AvailableThemes { get; } = [];

    /// <summary>Identificador reservado del tema que sigue al sistema operativo.</summary>
    private const string SystemThemeId = ThemeManager.SystemThemeId;

    /// <summary>Idiomas del selector. Es una lista de objetos: ver <see cref="SelectorOption"/>.</summary>
    public ObservableCollection<SelectorOption> AvailableLanguages { get; } = [.. LanguageCatalog.All];

    /// <summary>
    /// Rellena la lista de temas disponibles (catálogo + el tema que sigue al sistema).
    ///
    /// Sólo toca la colección si su contenido cambia. Reconstruirla —vaciar y volver a llenar— desde un
    /// manejador que corre dentro de una actualización de selección (por ejemplo al guardar las preferencias
    /// desde el enlace del propio desplegable) hace que Avalonia lance «Source collection was modified during
    /// selection update» y deje la lista vacía: el campo aparecía en blanco y ya no había nada que elegir.
    /// </summary>
    public void LoadAvailableThemes()
    {
        List<ThemeDefinition> themes = [.. _customThemeService.GetAllThemes()];
        themes.Add(new ThemeDefinition
        {
            Id = SystemThemeId,
            Name = "💻 Tema del Sistema (Windows)",
            Description = "Adapta automáticamente el tema según Windows.",
            IsBuiltIn = true
        });

        bool unchanged = themes.Count == AvailableThemes.Count &&
                         themes.Select(t => (t.Id, t.Name))
                               .SequenceEqual(AvailableThemes.Select(t => (t.Id, t.Name)));

        if (unchanged)
        {
            return;
        }

        AvailableThemes.Clear();
        foreach (var theme in themes)
        {
            AvailableThemes.Add(theme);
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        // Misma red que en el idioma: un valor que no está en la lista (incluido el 'null' que el control
        // devuelve al desmontarse) no puede borrar el tema que el usuario está viendo.
        if (!IsSelectableTheme(value))
        {
            if (!string.Equals(SelectedTheme, _appliedThemeId, StringComparison.Ordinal))
            {
                SelectedTheme = _appliedThemeId;
            }

            return;
        }

        string themeId = CanonicalThemeId(value);
        _appliedThemeId = themeId;

        if (!string.Equals(themeId, value, StringComparison.Ordinal))
        {
            SelectedTheme = themeId;
            return;
        }

        _themeService.SetThemeById(themeId);

        var prefs = _userPreferencesService.Preferences;
        if (!string.Equals(prefs.ActiveTheme, themeId, StringComparison.OrdinalIgnoreCase))
        {
            prefs.ActiveTheme = themeId;
            _userPreferencesService.Save();
        }
    }

    [RelayCommand]
    public void OpenThemeCustomizer()
    {
        var studio = CreateThemeStudio();

        // El menú se pone al día cuando el estudio se cierra, no al abrirse: es al cerrarse cuando el tema
        // puede haber cambiado (aplicado desde el estudio) o cuando acaba de nacer uno nuevo, y el selector
        // del menú tiene que enseñar lo que quedó aplicado de verdad.
        studio.Closed += (_, _) => SyncThemeSelectionWithAppliedTheme();
        studio.Show();
    }

    /// <summary>
    /// Construye el Theme Studio <b>con su view model ya conectado</b>.
    ///
    /// Sin <see cref="Avalonia.Controls.Control.DataContext">DataContext</see> la ventana no resuelve ningún
    /// <c>{Binding}</c>: el catálogo de temas aparece vacío, el editor por secciones no se genera y los botones
    /// de nuevo/duplicar/eliminar/aplicar no responden —el estudio parecía roto—. Aquí se le entrega el mismo
    /// catálogo de temas que usa el resto de la aplicación (no el singleton a pelo, para que las pruebas puedan
    /// aislarlo) y los diálogos reales, de modo que eliminar pida confirmación y exportar informe.
    ///
    /// Es <c>virtual</c> para que las pruebas puedan observar la ventana que se abre y comprobar su estado.
    /// </summary>
    protected virtual Views.Components.ThemeCustomizerWindow CreateThemeStudio() => new()
    {
        DataContext = new ThemeCustomizerViewModel(_customThemeService, _dialogService)
    };

    /// <summary>
    /// Alinea el selector de temas del menú con el tema <b>realmente aplicado</b> y con el catálogo vigente.
    ///
    /// Hace falta porque el estudio puede aplicar un tema o crear uno nuevo mientras está abierto: sin esto,
    /// el menú seguía mostrando el tema anterior y un tema recién creado no aparecía hasta reiniciar.
    /// </summary>
    public void SyncThemeSelectionWithAppliedTheme()
    {
        LoadAvailableThemes();

        string applied = _themeService.CurrentThemeId;
        if (IsSelectableTheme(applied))
        {
            _appliedThemeId = CanonicalThemeId(applied);

            // Se escribe el campo y no la propiedad con su efecto lateral: el estudio ya guardó la preferencia
            // al aplicar, y volver a pasar por el asignador reescribiría el disco por nada.
            SelectedTheme = _appliedThemeId;
            return;
        }

        // El tema aplicado ya no está en el catálogo (el estudio puede borrar el tema que estaba en uso): se
        // vuelve al último válido y se reaplica de verdad —no basta con mover el selector— para que el
        // campo no quede en blanco y lo que se ve en la interfaz sea lo que dice el menú.
        string fallback = IsSelectableTheme(_appliedThemeId)
            ? _appliedThemeId
            : AvailableThemes.FirstOrDefault()?.Id ?? ThemeManager.DefaultThemeId;

        _appliedThemeId = fallback;
        _themeService.SetThemeById(fallback);

        if (string.Equals(SelectedTheme, fallback, StringComparison.Ordinal))
        {
            // Mismo valor visible que antes: el enlace no se re-ejecutaría, pero el control necesita saber que
            // el valor que muestra sigue siendo válido tras recargar la lista.
            OnPropertyChanged(nameof(SelectedTheme));
        }
        else
        {
            SelectedTheme = fallback;
        }
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
        IProcessLauncherService? processLauncher = null,
        CustomThemeService? customThemeService = null)
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
        _dialogService = dialogService ?? AvaloniaDialogService.Instance;
        _processLauncher = processLauncher ?? ProcessLauncherService.Instance;
        _customThemeService = customThemeService ?? CustomThemeService.Instance;

        _executionCoordinator = new WorkflowExecutionCoordinator(
            editorViewModel,
            pluginLoader,
            logViewModel,
            nodeInspectorViewModel
        );

        _editorViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EditorViewModel.CanUndo) || e.PropertyName == nameof(EditorViewModel.CanRedo))
            {
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        };

        // La lista de temas se carga aquí y sólo se reconstruye al abrir el estudio de temas: hacerlo en
        // 'SyncFromPreferences' la reconstruía dentro del guardado de preferencias que dispara el propio
        // enlace del desplegable (ver LoadAvailableThemes).
        LoadAvailableThemes();
        SyncFromPreferences();
        _userPreferencesService.PreferencesChanged += SyncFromPreferences;
    }

    private void SyncFromPreferences()
    {
        var prefs = _userPreferencesService.Preferences;
        SelectedTheme = ResolveSelectableThemeId(prefs.ActiveTheme);
        IsDryRun = prefs.DefaultDryRunState;

        // La preferencia se traduce a una opción que esté en la lista: un idioma escrito como 'es' o que ya no
        // se ofrece dejaría el desplegable en blanco y sin poder seleccionar nada.
        string? storedLanguage = LanguageCatalog.Resolve(prefs.Language)?.Code;
        if (storedLanguage != null)
        {
            _appliedLanguage = storedLanguage;
        }

        if (storedLanguage != null && !string.Equals(SelectedLanguage, storedLanguage, StringComparison.Ordinal))
        {
            SelectedLanguage = storedLanguage;
        }
    }

    /// <summary>
    /// Devuelve un identificador de tema que <b>existe en la lista del desplegable</b>.
    ///
    /// Un <c>ComboBox</c> atado por valor sólo muestra una selección si encuentra su valor entre los
    /// elementos: con una preferencia heredada (<c>"Dark"</c>) o vacía no encontraba nada, el campo salía en
    /// blanco y el propio control escribía <c>null</c> de vuelta al view model. Manda el tema que está
    /// realmente aplicado —es lo que el usuario ve— y, si no está en la lista, la preferencia guardada
    /// traducida a su identificador real.
    /// </summary>
    private string ResolveSelectableThemeId(string? storedThemeId)
    {
        string applied = _themeService.CurrentThemeId;
        if (IsSelectableTheme(applied))
        {
            return CanonicalThemeId(applied);
        }

        string? stored = ThemeManager.ResolveThemeId(storedThemeId);
        if (stored != null && IsSelectableTheme(stored))
        {
            return stored;
        }

        return AvailableThemes.FirstOrDefault()?.Id ?? ThemeManager.DefaultThemeId;
    }

    private bool IsSelectableTheme(string themeId) =>
        AvailableThemes.Any(t => string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Grafía real del identificador en el catálogo: una preferencia con otra capitalización se normaliza.</summary>
    private string CanonicalThemeId(string themeId) =>
        AvailableThemes.FirstOrDefault(t => string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase))?.Id ?? themeId;

    public EditorViewModel Editor => _editorViewModel;
    public NodeInspectorViewModel NodeInspector => _nodeInspectorViewModel;

    public bool CanUndo => _editorViewModel.CanUndo;
    public bool CanRedo => _editorViewModel.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo() => _editorViewModel.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo() => _editorViewModel.Redo();

    [RelayCommand]
    public void OpenWorkflowSettings()
    {
        IsMenuOpen = false;
        _ = _editorViewModel.OpenWorkflowSettings();
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
                if (result.VirtualFileSystem != null && result.VirtualFileSystem.TotalFiles > 0)
                {
                    _lastVirtualFileSystem = result.VirtualFileSystem;
                    HasVirtualFiles = true;
                    VirtualFilesCount = result.VirtualFileSystem.TotalFiles;
                    _logViewModel.AddLog(LogLevel.Information, _loc.GetFormattedString("Log_VfsExecutionFinished", "[VFS] Simulación completada en Sistema de Archivos Virtual: {0} archivos generados.", VirtualFilesCount));
                }

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
            string rollbackTitle = _loc.GetString("RollbackExecutionBtn", "Revertir Archivos");
            _dialogService.ShowInformation(noEntriesMsg, rollbackTitle);
            return;
        }

        string confirmMsg = string.Format(_loc.GetString("Msg_RollbackConfirm", "¿Deseas revertir {0} operaciones realizadas en la última ejecución?"), _lastJournalService.Entries.Count);
        string confirmTitle = _loc.GetString("RollbackExecutionBtn", "Revertir Archivos");
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
    public void ContinueWorkflow() => ResumeWorkflow();

    [RelayCommand]
    public void TogglePause()
    {
        if (IsPaused)
        {
            ResumeWorkflow();
        }
        else
        {
            PauseWorkflow();
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
    public void OpenVirtualFileSystemExplorer()
    {
        var store = _lastVirtualFileSystem ?? _executionCoordinator.LastVirtualFileSystem;
        if (store != null)
        {
            var win = new Views.Components.VirtualFileSystemExplorerWindow(store);
            win.Show();
        }
        else
        {
            _dialogService.ShowInformation(
                _loc.GetString("VfsExplorer_NoData", "No hay datos de archivos virtuales en la última ejecución."),
                _loc.GetString("VfsExplorer_Title", "Explorador de Archivos Virtual"));
        }
    }

    /// <summary>
    /// Abre el diseñador de conjuntos de datos sintéticos declarado por el plugin del sistema de archivos.
    ///
    /// La acción vive en el nodo (es él quien conoce su ventana y su modelo de datos), así que la barra de control
    /// la invoca a través de <see cref="INodeCustomActionProvider"/>: la instancia se crea del catálogo de tipos ya
    /// descubierto por el cargador de plugins, que es la única fuente que conoce el tipo real —incluido el del
    /// ensamblado aislado del plugin—. El contexto lleva la ventana principal como propietaria para que el diálogo
    /// salga centrado sobre la aplicación y no como una ventana suelta.
    /// </summary>
    [RelayCommand]
    public void OpenSyntheticDataSetDesigner()
    {
        // El cajón se cierra al elegir su entrada, como el resto de las órdenes del menú.
        IsMenuOpen = false;

        var syntheticNodeType = _pluginLoader.DiscoveredNodeTypes.Values
            .FirstOrDefault(t => t.Name.Equals("SyntheticDataSourceNode", StringComparison.OrdinalIgnoreCase));

        if (syntheticNodeType != null && Activator.CreateInstance(syntheticNodeType) is INodeCustomActionProvider provider)
        {
            OpenDataSetDesigner(provider);
        }
    }

    /// <summary>
    /// Invoca la acción de apertura del diseñador sobre el nodo proporcionado.
    ///
    /// Es <c>virtual</c> para que las pruebas puedan observar que la orden del cajón <b>llega</b> al plugin con el
    /// identificador correcto sin quedarse como un botón mudo.
    /// </summary>
    protected virtual void OpenDataSetDesigner(INodeCustomActionProvider provider) =>
        provider.ExecuteCustomAction("OpenDataSetDesigner", new NodeCustomActionContext(App.MainWindow, null));

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
            var aboutDialog = new Views.AboutDialogWindow();
            aboutDialog.Show();
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
            var dashboardWindow = new Views.Components.WorkflowMetricsDashboardWindow(dashboardVm);
            dashboardWindow.Show();
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
