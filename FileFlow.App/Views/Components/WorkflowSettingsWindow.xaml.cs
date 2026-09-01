using System.Windows;
using System.Windows.Controls;
using FileFlow.App.Services;
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

        // Tab 4: External Tools
        var tools = ExternalToolsService.Instance.Config;
        TxtFfmpeg.Text = tools.FfmpegPath;
        TxtFfprobe.Text = tools.FfprobePath;
        TxtSevenZip.Text = tools.SevenZipPath;
        TxtPython.Text = tools.PythonPath;
    }

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
        try
        {
            var detected = await ExternalToolsService.Instance.AutoDetectToolsAsync();
            TxtFfmpeg.Text = detected.FfmpegPath;
            TxtFfprobe.Text = detected.FfprobePath;
            TxtSevenZip.Text = detected.SevenZipPath;
            TxtPython.Text = detected.PythonPath;

            MessageBox.Show("Autobúsqueda de herramientas externas completada.", "FileFlow Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error durante la autobúsqueda: {ex.Message}", "Error UI", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void OpenThemeCustomizer_Click(object sender, RoutedEventArgs e)
    {
        var win = new ThemeCustomizerWindow();
        win.Owner = this;
        win.ShowDialog();
        PopulateThemesCombo(ThemeManager.Instance.CurrentThemeId);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
