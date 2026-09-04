using System.Windows;
using System.Windows.Controls;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Sdk.Localization;
using Microsoft.Win32;

namespace FileFlow.App.Views.Components;

public partial class WorkflowSettingsWindow : Window
{
    public string GlobalOutputDir { get; private set; } = string.Empty;

    public WorkflowSettingsWindow(string currentGlobalOutputDir)
    {
        InitializeComponent();
        WindowThemeHelper.ApplyThemeToWindow(this);
        GlobalOutputDir = currentGlobalOutputDir;

        LoadAllPreferences();
    }

    private void LoadAllPreferences()
    {
        var prefs = UserPreferencesService.Instance.Preferences;

        // Tab 1: Storage & Paths
        TxtGlobalOutputDir.Text = !string.IsNullOrWhiteSpace(currentGlobalOutputDirOrDefault(prefs)) ? currentGlobalOutputDirOrDefault(prefs) : prefs.DefaultGlobalOutputDir;
        SelectComboBoxByTag(CmbConflictStrategy, prefs.DefaultConflictStrategy);
        ChkEnableAutoSave.IsChecked = prefs.EnableAutoSave;
        TxtAutoSaveInterval.Text = prefs.AutoSaveIntervalMinutes.ToString();

        // Tab 2: Appearance & UI
        PopulateThemesCombo(prefs.ActiveTheme);
        ChkIsCompactToolbox.IsChecked = prefs.IsCompactToolbox;
        ChkAutoScrollConsole.IsChecked = prefs.AutoScrollConsole;
        TxtMaxLogEntries.Text = prefs.MaxLogEntries.ToString();

        // Tab 3: Performance & Execution
        TxtMaxThreads.Text = prefs.MaxParallelThreads.ToString();
        ChkDefaultDryRun.IsChecked = prefs.DefaultDryRunState;
        SelectComboBoxByTag(CmbDefaultLogLevel, prefs.DefaultLogLevel);
        ChkEnableCheckpointing.IsChecked = prefs.EnableCheckpointing;
        ChkAutoUnloadAiModels.IsChecked = prefs.AutoUnloadAiModelsOnCompletion;

        // Tab 4: External Tools
        var tools = ExternalToolsService.Instance.Config;
        TxtFfmpeg.Text = tools.FfmpegPath;
        TxtFfprobe.Text = tools.FfprobePath;
        TxtSevenZip.Text = tools.SevenZipPath;
        TxtPython.Text = tools.PythonPath;

        // Tab 5: AI Models
        AiModelsItemsControl.ItemsSource = _aiModelManagerVm.Models;
        TxtAiModelsDir.Text = _aiModelManagerVm.ModelsDirectory;
        TxtAiModelsSummary.Text = _aiModelManagerVm.InstalledSummary;
    }

    private readonly ViewModels.AiModelManagerViewModel _aiModelManagerVm = new();

    private string currentGlobalOutputDirOrDefault(UserPreferencesData prefs)
    {
        return !string.IsNullOrWhiteSpace(GlobalOutputDir) ? GlobalOutputDir : prefs.DefaultGlobalOutputDir;
    }

    private void PopulateThemesCombo(string selectedThemeId)
    {
        CmbActiveTheme.Items.Clear();
        var all = CustomThemeService.Instance.GetAllThemes();
        foreach (var theme in all)
        {
            CmbActiveTheme.Items.Add(theme);
        }
        CmbActiveTheme.Items.Add(new FileFlow.Sdk.Themes.ThemeDefinition
        {
            Id = "system",
            Name = "💻 Tema del Sistema (Windows)",
            IsBuiltIn = true
        });

        CmbActiveTheme.SelectedValue = selectedThemeId;
        if (CmbActiveTheme.SelectedIndex < 0 && CmbActiveTheme.Items.Count > 0)
        {
            string mappedId = selectedThemeId.ToLowerInvariant() switch
            {
                "dark" => "dark_fluent",
                "light" => "light_studio",
                "cyber" => "cyber_neon",
                "pastel" => "pastel_spring",
                _ => selectedThemeId
            };
            CmbActiveTheme.SelectedValue = mappedId;
            if (CmbActiveTheme.SelectedIndex < 0)
            {
                CmbActiveTheme.SelectedIndex = 0;
            }
        }
    }

    private static void SelectComboBoxByTag(ComboBox combo, string tagValue)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString()?.Equals(tagValue, StringComparison.OrdinalIgnoreCase) == true)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private async void AutoDetect_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;

        try
        {
            var detected = await ExternalToolsService.Instance.AutoDetectToolsAsync();
            TxtFfmpeg.Text = detected.FfmpegPath;
            TxtFfprobe.Text = detected.FfprobePath;
            TxtSevenZip.Text = detected.SevenZipPath;
            TxtPython.Text = detected.PythonPath;

            string successMsg = LocalizationManager.Instance.GetString("Msg_ExternalToolsScanSuccess", "Autobúsqueda de herramientas externas completada.");
            string title = LocalizationManager.Instance.GetString("SettingsTitle", "Ajustes");
            MessageBox.Show(successMsg, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            string title = LocalizationManager.Instance.GetString("Error", "Error");
            MessageBox.Show($"Error: {ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private void BrowseGlobalOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccionar Ruta de Salida Global",
            InitialDirectory = TxtGlobalOutputDir.Text
        };

        if (dialog.ShowDialog() == true)
        {
            TxtGlobalOutputDir.Text = dialog.FolderName;
        }
    }

    private void BrowseFfmpeg_Click(object sender, RoutedEventArgs e) => BrowseExecutable("Seleccionar ejecutable FFmpeg", TxtFfmpeg);
    private void BrowseFfprobe_Click(object sender, RoutedEventArgs e) => BrowseExecutable("Seleccionar ejecutable FFprobe", TxtFfprobe);
    private void BrowseSevenZip_Click(object sender, RoutedEventArgs e) => BrowseExecutable("Seleccionar ejecutable 7-Zip", TxtSevenZip);
    private void BrowsePython_Click(object sender, RoutedEventArgs e) => BrowseExecutable("Seleccionar ejecutable Python", TxtPython);

    private static void BrowseExecutable(string title, TextBox targetTextBox)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*",
            InitialDirectory = targetTextBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            targetTextBox.Text = dialog.FileName;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        GlobalOutputDir = TxtGlobalOutputDir.Text.Trim();

        // 1. Save All User Preferences
        UserPreferencesService.Instance.UpdatePreferences(prefs =>
        {
            prefs.DefaultGlobalOutputDir = GlobalOutputDir;
            prefs.DefaultConflictStrategy = (CmbConflictStrategy.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "RenameIncremental";
            prefs.EnableAutoSave = ChkEnableAutoSave.IsChecked == true;
            if (int.TryParse(TxtAutoSaveInterval.Text, out int interval) && interval > 0)
            {
                prefs.AutoSaveIntervalMinutes = interval;
            }

            prefs.ActiveTheme = (CmbActiveTheme.SelectedItem as FileFlow.Sdk.Themes.ThemeDefinition)?.Id ?? CmbActiveTheme.SelectedValue?.ToString() ?? "dark_fluent";
            prefs.IsCompactToolbox = ChkIsCompactToolbox.IsChecked == true;
            prefs.AutoScrollConsole = ChkAutoScrollConsole.IsChecked == true;
            if (int.TryParse(TxtMaxLogEntries.Text, out int maxLogs) && maxLogs >= 0)
            {
                prefs.MaxLogEntries = maxLogs;
            }

            if (int.TryParse(TxtMaxThreads.Text, out int threads) && threads > 0)
            {
                prefs.MaxParallelThreads = threads;
            }
            prefs.DefaultDryRunState = ChkDefaultDryRun.IsChecked == true;
            prefs.DefaultLogLevel = (CmbDefaultLogLevel.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Information";
            prefs.EnableCheckpointing = ChkEnableCheckpointing.IsChecked == true;
            prefs.AutoUnloadAiModelsOnCompletion = ChkAutoUnloadAiModels.IsChecked == true;
        });

        // 2. Save External Tools Config
        var toolsConfig = new ExternalToolsConfig
        {
            FfmpegPath = TxtFfmpeg.Text.Trim(),
            FfprobePath = TxtFfprobe.Text.Trim(),
            SevenZipPath = TxtSevenZip.Text.Trim(),
            PythonPath = TxtPython.Text.Trim()
        };
        ExternalToolsService.Instance.SaveConfig(toolsConfig);

        // Apply Theme live if changed
        string activeThemeName = UserPreferencesService.Instance.Preferences.ActiveTheme;
        ThemeManager.Instance.SetThemeById(activeThemeName);

        DialogResult = true;
        Close();
    }

    private void ClearCheckpoints_Click(object sender, RoutedEventArgs e)
    {
        int deleted = WorkflowCheckpointManager.Instance.ClearAllCheckpoints();
        string title = LocalizationManager.Instance.GetString("Settings_CheckpointingTitle", "Puntos de Control");
        string msg = string.Format(
            LocalizationManager.Instance.GetString("Settings_CheckpointsClearedMsg", "Se han eliminado {0} punto(s) de control almacenados en disco."),
            deleted);
        MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenThemeCustomizer_Click(object sender, RoutedEventArgs e)
    {
        var win = new ThemeCustomizerWindow();
        win.Owner = this;
        win.ShowDialog();
        PopulateThemesCombo(ThemeManager.Instance.CurrentThemeId);
    }

    private async void DownloadAllMissing_Click(object sender, RoutedEventArgs e)
    {
        BtnDownloadAllMissing.IsEnabled = false;
        try
        {
            await _aiModelManagerVm.DownloadMissingModelsAsync();
        }
        finally
        {
            BtnDownloadAllMissing.IsEnabled = true;
            TxtAiModelsSummary.Text = _aiModelManagerVm.InstalledSummary;
        }
    }

    private void RefreshAiModels_Click(object sender, RoutedEventArgs e)
    {
        _aiModelManagerVm.RefreshStatus();
        TxtAiModelsSummary.Text = _aiModelManagerVm.InstalledSummary;
    }

    private void OpenAiModelsFolder_Click(object sender, RoutedEventArgs e)
    {
        _aiModelManagerVm.OpenModelsFolder();
    }

    private async void DownloadAiModelItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ViewModels.AiModelItemViewModel item)
        {
            await _aiModelManagerVm.DownloadModelAsync(item);
            TxtAiModelsSummary.Text = _aiModelManagerVm.InstalledSummary;
        }
    }

    private void DeleteAiModelItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ViewModels.AiModelItemViewModel item)
        {
            _aiModelManagerVm.DeleteModel(item);
            TxtAiModelsSummary.Text = _aiModelManagerVm.InstalledSummary;
        }
    }

    private void ConfigureAiModelUrls_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AiModelItemViewModel item)
        {
            var dialog = new AiModelUrlsConfigDialog(item.ModelId)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                item.RefreshState();
                TxtAiModelsSummary.Text = _aiModelManagerVm.InstalledSummary;
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
