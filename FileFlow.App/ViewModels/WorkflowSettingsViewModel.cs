using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.Themes;
using FileFlow.Core.Engine;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.Storage;

namespace FileFlow.App.ViewModels;

/// <summary>
/// ViewModel desacoplado para la ventana de configuración y preferencias de FileFlow Studio.
/// Centraliza la gestión de almacenamiento, apariencia, rendimiento, herramientas externas y modelos de IA.
/// </summary>
public partial class WorkflowSettingsViewModel : ObservableObject
{
    private readonly IUserPreferencesService _preferencesService;
    private readonly IExternalToolsService _toolsService;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _loc;
    private readonly IFileDialogService _fileDialogService;
    private readonly IDialogService _dialogService;

    public AiModelManagerViewModel AiModelManager { get; }

    public ObservableCollection<ThemeDefinition> Themes { get; } = new();
    public ObservableCollection<ThemeDefinition> AvailableThemes => Themes;

    /// <summary>Idiomas del selector. Es la misma lista que usa el menú: ver <see cref="SelectorOption"/>.</summary>
    public ObservableCollection<SelectorOption> AvailableLanguages { get; } = [.. LanguageCatalog.All];

    /// <summary>
    /// Canales de actualización ofrecidos. El texto se resuelve con el idioma activo al abrir la ventana, que
    /// es cuando se construye el view model.
    /// </summary>
    public ObservableCollection<SelectorOption> UpdateChannels { get; } = [];

    /// <summary>Estrategias de conflicto ofrecidas, con su texto en el idioma activo al abrir la ventana.</summary>
    public ObservableCollection<SelectorOption> ConflictStrategies { get; } = [];

    /// <summary>
    /// Niveles de registro ofrecidos. El código es el valor que guarda la preferencia (<c>Information</c>, no el
    /// texto traducido): un selector atado por valor sólo muestra una opción si el valor existe en la lista.
    /// </summary>
    public ObservableCollection<SelectorOption> LogLevels { get; } = [];

    public event Action<bool>? RequestClose;

    [ObservableProperty]
    private string _selectedLanguage = "es-ES";

    [ObservableProperty]
    private string _globalOutputDir = string.Empty;

    [ObservableProperty]
    private string _tempWorkingDir = string.Empty;

    [ObservableProperty]
    private string _selectedConflictStrategy = "RenameIncremental";

    [ObservableProperty]
    private bool _enableAutoSave = true;

    [ObservableProperty]
    private int _autoSaveIntervalMinutes = 5;

    [ObservableProperty]
    private bool _autoCleanIntermediateTempFiles = true;

    [ObservableProperty]
    private bool _cleanStaleTempOnStartup = true;

    [ObservableProperty]
    private string _selectedThemeId = "dark_fluent";

    [ObservableProperty]
    private ThemeDefinition? _selectedTheme;

    partial void OnSelectedThemeChanged(ThemeDefinition? value)
    {
        if (value != null)
        {
            SelectedThemeId = value.Id;
        }
    }

    [ObservableProperty]
    private bool _isCompactToolbox;

    [ObservableProperty]
    private bool _autoScrollConsole = true;

    [ObservableProperty]
    private int _maxLogEntries = 1000;

    [ObservableProperty]
    private int _maxParallelThreads = Environment.ProcessorCount;

    [ObservableProperty]
    private bool _defaultDryRunState;

    [ObservableProperty]
    private string _selectedLogLevel = "Information";

    [ObservableProperty]
    private bool _enableCheckpointing = true;

    [ObservableProperty]
    private bool _autoUnloadAiModelsOnCompletion = true;

    [ObservableProperty]
    private string _ffmpegPath = string.Empty;

    [ObservableProperty]
    private string _ffprobePath = string.Empty;

    [ObservableProperty]
    private string _sevenZipPath = string.Empty;

    [ObservableProperty]
    private string _pythonPath = string.Empty;

    [ObservableProperty]
    private bool _isAutoDetecting;

    // Tab: Updates
    [ObservableProperty]
    private bool _autoCheckForUpdates = true;

    [ObservableProperty]
    private string _selectedUpdateChannel = "Stable";

    [ObservableProperty]
    private string _currentVersionDisplay = string.Empty;

    [ObservableProperty]
    private string _packagingFormatDisplay = string.Empty;

    [ObservableProperty]
    private string _lastUpdateCheckDisplay = string.Empty;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private string _checkUpdatesStatusMessage = string.Empty;

    public WorkflowSettingsViewModel(
        IUserPreferencesService preferencesService,
        IExternalToolsService toolsService,
        IThemeService themeService,
        ILocalizationService loc,
        IFileDialogService fileDialogService,
        IDialogService dialogService,
        AiModelManagerViewModel aiModelManagerVm)
    {
        _preferencesService = preferencesService;
        _toolsService = toolsService;
        _themeService = themeService;
        _loc = loc;
        _fileDialogService = fileDialogService;
        _dialogService = dialogService;
        AiModelManager = aiModelManagerVm;
    }

    public void Initialize(string? currentGlobalOutputDir = null)
    {
        var prefs = _preferencesService.Preferences;

        // Tab 1: Storage
        GlobalOutputDir = !string.IsNullOrWhiteSpace(currentGlobalOutputDir)
            ? currentGlobalOutputDir
            : prefs.DefaultGlobalOutputDir;
        TempWorkingDir = !string.IsNullOrWhiteSpace(prefs.TemporaryDirectory)
            ? prefs.TemporaryDirectory
            : AppPaths.DefaultTempDirectory;
        LoadConflictStrategies(prefs.DefaultConflictStrategy);
        EnableAutoSave = prefs.EnableAutoSave;
        AutoSaveIntervalMinutes = prefs.AutoSaveIntervalMinutes > 0 ? prefs.AutoSaveIntervalMinutes : 5;
        AutoCleanIntermediateTempFiles = prefs.AutoCleanIntermediateTempFiles;
        CleanStaleTempOnStartup = prefs.CleanStaleTempOnStartup;

        // Tab 2: Appearance
        ReloadThemes(prefs.ActiveTheme);
        // Traducido a una opción de la lista: un selector atado por valor no muestra un idioma que no esté.
        SelectedLanguage = LanguageCatalog.Resolve(prefs.Language)?.Code ?? LanguageCatalog.All[0].Code;
        IsCompactToolbox = prefs.IsCompactToolbox;
        AutoScrollConsole = prefs.AutoScrollConsole;
        MaxLogEntries = prefs.MaxLogEntries >= 0 ? prefs.MaxLogEntries : 1000;

        // Tab 3: Performance
        MaxParallelThreads = prefs.MaxParallelThreads > 0 ? prefs.MaxParallelThreads : Environment.ProcessorCount;
        DefaultDryRunState = prefs.DefaultDryRunState;
        LoadLogLevels(prefs.DefaultLogLevel);
        EnableCheckpointing = prefs.EnableCheckpointing;
        AutoUnloadAiModelsOnCompletion = prefs.AutoUnloadAiModelsOnCompletion;

        // Tab 4: External Tools
        var tools = _toolsService.Config;
        FfmpegPath = tools.FfmpegPath;
        FfprobePath = tools.FfprobePath;
        SevenZipPath = tools.SevenZipPath;
        PythonPath = tools.PythonPath;

        // Tab 5: Updates
        AutoCheckForUpdates = prefs.AutoCheckForUpdates;
        LoadUpdateChannels(prefs.UpdateChannel);
        CurrentVersionDisplay = AppUpdateService.Instance.CurrentVersion.ToString();
        PackagingFormatDisplay = AppUpdateService.Instance.CurrentPackagingFormat switch
        {
            AppPackagingFormat.WindowsInstalled => "Windows Setup (.exe)",
            AppPackagingFormat.WindowsPortable => "Windows Portable (.zip)",
            AppPackagingFormat.LinuxAppImage => "Linux AppImage (.AppImage)",
            AppPackagingFormat.LinuxFlatpak => "Linux Flatpak (.flatpak)",
            AppPackagingFormat.LinuxDebPackage => "Debian / Ubuntu (.deb)",
            AppPackagingFormat.LinuxGenericTarball => "Linux Portable (.tar.gz)",
            _ => "Portable / Universal"
        };
        LastUpdateCheckDisplay = prefs.LastUpdateCheckUtc.HasValue
            ? prefs.LastUpdateCheckUtc.Value.ToLocalTime().ToString("g")
            : _loc.GetString("Settings_LastUpdateNever", "Nunca comprobado");
    }

    /// <summary>
    /// Rellena los canales de actualización y traduce la preferencia guardada a una de las opciones.
    ///
    /// Un canal desconocido dejaría el desplegable en blanco y, peor, borraría la preferencia: el control
    /// escribe <c>null</c> de vuelta al view model y ese <c>null</c> se guarda al aceptar la ventana.
    /// </summary>
    /// <summary>
    /// Rellena las estrategias de conflicto y traduce la preferencia guardada a una de las opciones: un valor
    /// que no esté en la lista dejaría el desplegable en blanco (y el control escribiría <c>null</c> encima de
    /// la preferencia al aceptar la ventana).
    /// </summary>
    private void LoadConflictStrategies(string? storedStrategy)
    {
        ConflictStrategies.Clear();
        ConflictStrategies.Add(new SelectorOption("RenameIncremental", _loc.GetString("Settings_ConflictRename", "Renombrar de forma incremental")));
        ConflictStrategies.Add(new SelectorOption("Overwrite", _loc.GetString("Settings_ConflictOverwrite", "Sobrescribir")));
        ConflictStrategies.Add(new SelectorOption("Skip", _loc.GetString("Settings_ConflictSkip", "Omitir")));

        SelectedConflictStrategy = ConflictStrategies
            .FirstOrDefault(s => string.Equals(s.Code, storedStrategy?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.Code ?? ConflictStrategies[0].Code;
    }

    private void LoadUpdateChannels(string? storedChannel)
    {
        UpdateChannels.Clear();
        UpdateChannels.Add(new SelectorOption("Stable", _loc.GetString("Settings_ChannelStable", "Estable")));
        UpdateChannels.Add(new SelectorOption("Beta", _loc.GetString("Settings_ChannelBeta", "Beta")));

        // El desplegable sólo muestra una opción si el view model tiene ya un valor que exista en la lista.
        SelectedUpdateChannel = UpdateChannels
            .FirstOrDefault(c => string.Equals(c.Code, storedChannel?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.Code ?? UpdateChannels[0].Code;
    }

    /// <summary>
    /// Rellena los niveles de registro y traduce la preferencia guardada a una de las opciones: un nivel que no
    /// esté en la lista dejaría el desplegable en blanco (y el control escribiría <c>null</c> encima de la
    /// preferencia al aceptar la ventana).
    /// </summary>
    private void LoadLogLevels(string? storedLevel)
    {
        LogLevels.Clear();
        LogLevels.Add(new SelectorOption("Debug", _loc.GetString("Settings_LogLevelDebug", "Debug")));
        LogLevels.Add(new SelectorOption("Information", _loc.GetString("Settings_LogLevelInfo", "Information")));
        LogLevels.Add(new SelectorOption("Warning", _loc.GetString("Settings_LogLevelWarning", "Warning")));
        LogLevels.Add(new SelectorOption("Error", _loc.GetString("Settings_LogLevelError", "Error")));

        SelectedLogLevel = LogLevels
            .FirstOrDefault(l => string.Equals(l.Code, storedLevel?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.Code ?? LogLevels[1].Code;
    }

    public void ReloadThemes(string selectedThemeId)
    {
        Themes.Clear();
        var all = CustomThemeService.Instance.GetAllThemes();
        foreach (var theme in all)
        {
            Themes.Add(theme);
        }
        Themes.Add(new ThemeDefinition
        {
            Id = "system",
            Name = "💻 Tema del Sistema (Windows)",
            IsBuiltIn = true
        });

        // La traducción de identificadores heredados ('Dark' → 'dark_fluent') vive en el gestor de temas:
        // es el mismo valor que guarda una preferencia antigua y el que aplica el arranque.
        string? mappedId = ThemeManager.ResolveThemeId(selectedThemeId);

        SelectedThemeId = Themes.FirstOrDefault(t => string.Equals(t.Id, mappedId, StringComparison.OrdinalIgnoreCase))?.Id
            ?? Themes.FirstOrDefault()?.Id ?? ThemeManager.DefaultThemeId;

        SelectedTheme = Themes.FirstOrDefault(t => string.Equals(t.Id, SelectedThemeId, StringComparison.OrdinalIgnoreCase))
            ?? Themes.FirstOrDefault();
    }

    [RelayCommand]
    public void BrowseGlobalOutput()
    {
        string? folder = _fileDialogService.ShowFolderBrowserDialog("Seleccionar Ruta de Salida Global");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            GlobalOutputDir = folder;
        }
    }

    [RelayCommand]
    public void BrowseTempWorkingDir()
    {
        string title = _loc.GetString("Settings_SelectTempWorkingDirTitle", "Seleccionar Directorio de Trabajo Temporal");
        string? folder = _fileDialogService.ShowFolderBrowserDialog(title);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            TempWorkingDir = folder;
        }
    }

    [RelayCommand]
    public void BrowseFfmpeg()
    {
        string? file = _fileDialogService.ShowOpenFileDialog("Seleccionar ejecutable FFmpeg", "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(file))
        {
            FfmpegPath = file;
        }
    }

    [RelayCommand]
    public void BrowseFfprobe()
    {
        string? file = _fileDialogService.ShowOpenFileDialog("Seleccionar ejecutable FFprobe", "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(file))
        {
            FfprobePath = file;
        }
    }

    [RelayCommand]
    public void BrowseSevenZip()
    {
        string? file = _fileDialogService.ShowOpenFileDialog("Seleccionar ejecutable 7-Zip", "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(file))
        {
            SevenZipPath = file;
        }
    }

    [RelayCommand]
    public void BrowsePython()
    {
        string? file = _fileDialogService.ShowOpenFileDialog("Seleccionar ejecutable Python", "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(file))
        {
            PythonPath = file;
        }
    }

    [RelayCommand]
    public async Task AutoDetectToolsAsync()
    {
        IsAutoDetecting = true;
        try
        {
            var detected = await _toolsService.AutoDetectToolsAsync();
            FfmpegPath = detected.FfmpegPath;
            FfprobePath = detected.FfprobePath;
            SevenZipPath = detected.SevenZipPath;
            PythonPath = detected.PythonPath;

            string successMsg = _loc.GetString("Msg_ExternalToolsScanSuccess", "Autobúsqueda de herramientas externas completada.");
            string title = _loc.GetString("SettingsTitle", "Ajustes");
            _dialogService.ShowInformation(successMsg, title);
        }
        catch (Exception ex)
        {
            string title = _loc.GetString("Error", "Error");
            _dialogService.ShowError($"Error: {ex.Message}", title);
        }
        finally
        {
            IsAutoDetecting = false;
        }
    }

    [RelayCommand]
    public void ClearCheckpoints()
    {
        int deleted = WorkflowCheckpointManager.Instance.ClearAllCheckpoints();
        string title = _loc.GetString("Settings_CheckpointingTitle", "Puntos de Control");
        string msg = string.Format(
            _loc.GetString("Settings_CheckpointsClearedMsg", "Se han eliminado {0} punto(s) de control almacenados en disco."),
            deleted);
        _dialogService.ShowInformation(msg, title);
    }

    [RelayCommand]
    public void CleanTemporaryFilesNow()
    {
        long bytesFreed = AppPaths.CleanupStaleTempDirectories(TimeSpan.Zero);
        double mbFreed = bytesFreed / (1024.0 * 1024.0);
        string title = _loc.GetString("Settings_TempCleanedTitle", "Espacio Temporal");
        string msg = string.Format(
            _loc.GetString("Settings_TempCleanedMsg", "Se han liberado {0:F2} MB de espacio temporal de ejecuciones pasadas."),
            mbFreed);
        _dialogService.ShowInformation(msg, title);
    }

    [RelayCommand]
    public void SaveSettings() => Save();

    [RelayCommand]
    public void CancelSettings() => Cancel();

    [RelayCommand]
    public void Save()
    {
        _preferencesService.UpdatePreferences(prefs =>
        {
            prefs.DefaultGlobalOutputDir = GlobalOutputDir.Trim();
            prefs.TemporaryDirectory = TempWorkingDir.Trim();
            prefs.DefaultConflictStrategy = SelectedConflictStrategy;
            prefs.EnableAutoSave = EnableAutoSave;
            prefs.AutoSaveIntervalMinutes = AutoSaveIntervalMinutes > 0 ? AutoSaveIntervalMinutes : 5;

            prefs.Language = SelectedLanguage;
            prefs.ActiveTheme = SelectedThemeId;
            prefs.IsCompactToolbox = IsCompactToolbox;
            prefs.AutoScrollConsole = AutoScrollConsole;
            prefs.MaxLogEntries = MaxLogEntries >= 0 ? MaxLogEntries : 1000;

            prefs.MaxParallelThreads = MaxParallelThreads > 0 ? MaxParallelThreads : Environment.ProcessorCount;
            prefs.DefaultDryRunState = DefaultDryRunState;
            prefs.DefaultLogLevel = SelectedLogLevel;
            prefs.EnableCheckpointing = EnableCheckpointing;
            prefs.AutoUnloadAiModelsOnCompletion = AutoUnloadAiModelsOnCompletion;
            prefs.AutoCleanIntermediateTempFiles = AutoCleanIntermediateTempFiles;
            prefs.CleanStaleTempOnStartup = CleanStaleTempOnStartup;

            prefs.AutoCheckForUpdates = AutoCheckForUpdates;
            prefs.UpdateChannel = SelectedUpdateChannel;
        });

        if (!string.IsNullOrWhiteSpace(SelectedLanguage))
        {
            _loc.SetCulture(SelectedLanguage);
        }

        var toolsConfig = new ExternalToolsConfig
        {
            FfmpegPath = FfmpegPath.Trim(),
            FfprobePath = FfprobePath.Trim(),
            SevenZipPath = SevenZipPath.Trim(),
            PythonPath = PythonPath.Trim()
        };
        _toolsService.SaveConfig(toolsConfig);

        _themeService.SetThemeById(SelectedThemeId);

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    public async Task CheckForUpdatesNowAsync()
    {
        if (IsCheckingForUpdates) return;

        IsCheckingForUpdates = true;
        CheckUpdatesStatusMessage = _loc.GetString("Update_StatusChecking", "Buscando actualizaciones en GitHub...");

        try
        {
            var channel = string.Equals(SelectedUpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase)
                ? UpdateChannel.Beta
                : UpdateChannel.Stable;

            var result = await AppUpdateService.Instance.CheckForUpdatesAsync(channel, force: true, CancellationToken.None);

            LastUpdateCheckDisplay = DateTime.Now.ToString("g");

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                CheckUpdatesStatusMessage = string.Format(_loc.GetString("Update_StatusError", "Error: {0}"), result.ErrorMessage);
                _dialogService.ShowError(result.ErrorMessage, _loc.GetString("Settings_UpdatesTitle", "Actualizaciones"));
            }
            else if (result.UpdateAvailable && result.UpdateInfo != null)
            {
                CheckUpdatesStatusMessage = string.Format(_loc.GetString("Update_AvailableBadge", "⚡ Actualización {0} disponible"), result.UpdateInfo.VersionTag);

                // Abrir diálogo modal de actualización
                var updateVm = new UpdateDialogViewModel(result.UpdateInfo);
                var updateWindow = new Views.Components.UpdateDialogWindow(updateVm);
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    await updateWindow.ShowDialog(desktop.MainWindow ?? updateWindow);
                }
            }
            else
            {
                CheckUpdatesStatusMessage = _loc.GetString("Update_StatusUpToDate", "🟢 FileFlow Studio está actualizado a la versión más reciente.");
                _dialogService.ShowInformation(CheckUpdatesStatusMessage, _loc.GetString("Settings_UpdatesTitle", "Actualizaciones"));
            }
        }
        catch (Exception ex)
        {
            CheckUpdatesStatusMessage = ex.Message;
            _dialogService.ShowError(ex.Message, _loc.GetString("Settings_UpdatesTitle", "Actualizaciones"));
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
