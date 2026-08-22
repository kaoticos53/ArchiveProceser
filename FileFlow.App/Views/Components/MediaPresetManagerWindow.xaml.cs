using System.Windows;
using System.Windows.Controls;
using FileFlow.App.Services;

namespace FileFlow.App.Views.Components;

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
        MessageBox.Show("Preset guardado con éxito.", "FileFlow Studio", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPreset == null) return;

        if (_selectedPreset.IsSystemDefault)
        {
            MessageBox.Show("No se pueden eliminar los presets predeterminados del sistema.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show($"¿Deseas eliminar el preset '{_selectedPreset.Name}'?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            MediaPresetManagerService.Instance.DeletePreset(_selectedPreset.Id);
            LoadPresetsList();
        }
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("¿Deseas restablecer todos los presets a los valores por defecto del sistema?", "Restablecer Presets", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
