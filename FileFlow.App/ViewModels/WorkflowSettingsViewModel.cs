using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public event Action<bool>? RequestClose;

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
        SelectedConflictStrategy = !string.IsNullOrWhiteSpace(prefs.DefaultConflictStrategy)
            ? prefs.DefaultConflictStrategy
            : "RenameIncremental";
        EnableAutoSave = prefs.EnableAutoSave;
        AutoSaveIntervalMinutes = prefs.AutoSaveIntervalMinutes > 0 ? prefs.AutoSaveIntervalMinutes : 5;
        AutoCleanIntermediateTempFiles = prefs.AutoCleanIntermediateTempFiles;
        CleanStaleTempOnStartup = prefs.CleanStaleTempOnStartup;

        // Tab 2: Appearance
        ReloadThemes(prefs.ActiveTheme);
        IsCompactToolbox = prefs.IsCompactToolbox;
        AutoScrollConsole = prefs.AutoScrollConsole;
        MaxLogEntries = prefs.MaxLogEntries >= 0 ? prefs.MaxLogEntries : 1000;

        // Tab 3: Performance
        MaxParallelThreads = prefs.MaxParallelThreads > 0 ? prefs.MaxParallelThreads : Environment.ProcessorCount;
        DefaultDryRunState = prefs.DefaultDryRunState;
        SelectedLogLevel = !string.IsNullOrWhiteSpace(prefs.DefaultLogLevel) ? prefs.DefaultLogLevel : "Information";
        EnableCheckpointing = prefs.EnableCheckpointing;
        AutoUnloadAiModelsOnCompletion = prefs.AutoUnloadAiModelsOnCompletion;

        // Tab 4: External Tools
        var tools = _toolsService.Config;
        FfmpegPath = tools.FfmpegPath;
        FfprobePath = tools.FfprobePath;
        SevenZipPath = tools.SevenZipPath;
        PythonPath = tools.PythonPath;
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

        string mappedId = selectedThemeId.ToLowerInvariant() switch
        {
            "dark" => "dark_fluent",
            "light" => "light_studio",
            "cyber" => "cyber_neon",
            "pastel" => "pastel_spring",
            _ => selectedThemeId
        };

        SelectedThemeId = Themes.Any(t => string.Equals(t.Id, mappedId, StringComparison.OrdinalIgnoreCase))
            ? mappedId
            : Themes.FirstOrDefault()?.Id ?? "dark_fluent";
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
    public void Save()
    {
        _preferencesService.UpdatePreferences(prefs =>
        {
            prefs.DefaultGlobalOutputDir = GlobalOutputDir.Trim();
            prefs.TemporaryDirectory = TempWorkingDir.Trim();
            prefs.DefaultConflictStrategy = SelectedConflictStrategy;
            prefs.EnableAutoSave = EnableAutoSave;
            prefs.AutoSaveIntervalMinutes = AutoSaveIntervalMinutes > 0 ? AutoSaveIntervalMinutes : 5;

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
        });

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
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
