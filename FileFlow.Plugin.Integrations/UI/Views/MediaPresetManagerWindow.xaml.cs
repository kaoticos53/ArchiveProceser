using System.Windows;
using System.Windows.Controls;
using FileFlow.Plugin.Integrations.UI.Services;

namespace FileFlow.Plugin.Integrations.UI.Views;

public partial class MediaPresetManagerWindow : Window
{
    private MediaPreset? _selectedPreset;

    public MediaPresetManagerWindow()
    {
        InitializeComponent();
        LoadPresetsList();
    }

    private void LoadPresetsList()
    {
        var presets = MediaPresetManagerService.Instance.GetPresets();
        LstPresets.ItemsSource = presets;

        if (presets.Count > 0)
        {
            LstPresets.SelectedIndex = 0;
        }
    }

    private void LstPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstPresets.SelectedItem is MediaPreset preset)
        {
            _selectedPreset = preset;
            TxtName.Text = preset.Name;
            TxtDescription.Text = preset.Description;
            TxtExtension.Text = preset.OutputExtension;
            TxtFfmpegArgs.Text = preset.FfmpegArguments;

            foreach (ComboBoxItem item in CmbCategory.Items)
            {
                if (item.Content.ToString()?.Equals(preset.Category, StringComparison.OrdinalIgnoreCase) == true)
                {
                    CmbCategory.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void NewPreset_Click(object sender, RoutedEventArgs e)
    {
        var newPreset = new MediaPreset
        {
            Name = "Nuevo Preset Personalizado",
            Description = "Descripción del nuevo preset...",
            Category = "Video",
            OutputExtension = ".mp4",
            FfmpegArguments = "-c:v libx264 -crf 23 -c:a aac",
            IsSystemDefault = false
        };

        MediaPresetManagerService.Instance.SavePreset(newPreset);
        LoadPresetsList();

        var created = MediaPresetManagerService.Instance.GetPresets().FirstOrDefault(p => p.Id == newPreset.Id);
        if (created != null)
        {
            LstPresets.SelectedItem = created;
        }
    }

    private void SaveCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPreset == null) return;

        string category = (CmbCategory.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Video";

        _selectedPreset.Name = TxtName.Text.Trim();
        _selectedPreset.Description = TxtDescription.Text.Trim();
        _selectedPreset.OutputExtension = TxtExtension.Text.Trim();
        if (!_selectedPreset.OutputExtension.StartsWith('.'))
        {
            _selectedPreset.OutputExtension = "." + _selectedPreset.OutputExtension;
        }
        _selectedPreset.FfmpegArguments = TxtFfmpegArgs.Text.Trim();
        _selectedPreset.Category = category;

        MediaPresetManagerService.Instance.SavePreset(_selectedPreset);
        LoadPresetsList();
        string successMsg = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_MsgSaveSuccess", "Preset guardado con éxito.");
        string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_WindowTitle", "Media Preset Manager");
        MessageBox.Show(successMsg, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPreset == null) return;

        if (_selectedPreset.IsSystemDefault)
        {
            string defaultMsg = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_MsgDefaultCannotDelete", "No se pueden eliminar los presets predeterminados del sistema.");
            string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Warning", "Aviso");
            MessageBox.Show(defaultMsg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string confirmMsg = string.Format(FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_MsgDeleteConfirm", "¿Deseas eliminar el preset '{0}'?"), _selectedPreset.Name);
        string confirmTitle = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_DeleteBtn", "Eliminar");
        var result = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            MediaPresetManagerService.Instance.DeletePreset(_selectedPreset.Id);
            LoadPresetsList();
        }
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        string resetMsg = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_MsgResetConfirm", "¿Deseas restablecer todos los presets a los valores por defecto del sistema?");
        string resetTitle = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_ResetBtn", "Restablecer");
        var result = MessageBox.Show(resetMsg, resetTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            MediaPresetManagerService.Instance.ResetToDefaults();
            LoadPresetsList();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
