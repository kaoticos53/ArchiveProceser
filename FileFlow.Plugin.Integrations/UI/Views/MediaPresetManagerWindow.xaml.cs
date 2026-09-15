using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.Plugin.Integrations.UI.Services;
using FileFlow.Sdk.Services;

namespace FileFlow.Plugin.Integrations.UI.Views;

public partial class MediaPresetManagerWindow : Window
{
    private readonly IDialogService _dialogService;
    private MediaPreset? _selectedPreset;

    private ListBox? LstPresets => this.FindControl<ListBox>("LstPresets");
    private TextBox? TxtName => this.FindControl<TextBox>("TxtName");
    private TextBox? TxtDescription => this.FindControl<TextBox>("TxtDescription");
    private TextBox? TxtExtension => this.FindControl<TextBox>("TxtExtension");
    private TextBox? TxtFfmpegArgs => this.FindControl<TextBox>("TxtFfmpegArgs");
    private ComboBox? CmbCategory => this.FindControl<ComboBox>("CmbCategory");

    public MediaPresetManagerWindow(IDialogService? dialogService = null)
    {
        _dialogService = dialogService ?? NullDialogService.Instance;
        LoadPresetsList();
    }

    private void LoadPresetsList()
    {
        var presets = MediaPresetManagerService.Instance.GetPresets();
        if (LstPresets != null)
        {
            LstPresets.ItemsSource = presets;
            if (presets.Count > 0)
            {
                LstPresets.SelectedIndex = 0;
            }
        }
    }

    private void LstPresets_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LstPresets?.SelectedItem is MediaPreset preset)
        {
            _selectedPreset = preset;
            if (TxtName != null) TxtName.Text = preset.Name;
            if (TxtDescription != null) TxtDescription.Text = preset.Description;
            if (TxtExtension != null) TxtExtension.Text = preset.OutputExtension;
            if (TxtFfmpegArgs != null) TxtFfmpegArgs.Text = preset.FfmpegArguments;

            if (CmbCategory != null)
            {
                foreach (var item in CmbCategory.Items)
                {
                    if (item is ComboBoxItem cbi && cbi.Content?.ToString()?.Equals(preset.Category, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        CmbCategory.SelectedItem = cbi;
                        break;
                    }
                    else if (item?.ToString()?.Equals(preset.Category, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        CmbCategory.SelectedItem = item;
                        break;
                    }
                }
            }
        }
    }

    private void NewPreset_Click(object? sender, RoutedEventArgs e)
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
        if (created != null && LstPresets != null)
        {
            LstPresets.SelectedItem = created;
        }
    }

    private void SaveCurrent_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedPreset == null) return;

        string category = (CmbCategory?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? CmbCategory?.SelectedItem?.ToString() ?? "Video";

        _selectedPreset.Name = (TxtName?.Text ?? string.Empty).Trim();
        _selectedPreset.Description = (TxtDescription?.Text ?? string.Empty).Trim();
        _selectedPreset.OutputExtension = (TxtExtension?.Text ?? string.Empty).Trim();
        if (!_selectedPreset.OutputExtension.StartsWith('.'))
        {
            _selectedPreset.OutputExtension = "." + _selectedPreset.OutputExtension;
        }
        _selectedPreset.FfmpegArguments = (TxtFfmpegArgs?.Text ?? string.Empty).Trim();
        _selectedPreset.Category = category;

        MediaPresetManagerService.Instance.SavePreset(_selectedPreset);
        LoadPresetsList();
        string successMsg = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_MsgSaveSuccess", "Preset guardado con éxito.");
        string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_WindowTitle", "Media Preset Manager");
        _dialogService.ShowInformation(successMsg, title);
    }

    private void DeletePreset_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedPreset == null) return;

        if (_selectedPreset.IsSystemDefault)
        {
            string defaultMsg = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_MsgDefaultCannotDelete", "No se pueden eliminar los presets predeterminados del sistema.");
            string title = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Warning", "Aviso");
            _dialogService.ShowWarning(defaultMsg, title);
            return;
        }

        string confirmMsg = string.Format(FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_MsgDeleteConfirm", "¿Deseas eliminar el preset '{0}'?"), _selectedPreset.Name);
        string confirmTitle = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_DeleteBtn", "Eliminar");
        bool confirm = _dialogService.ShowConfirmation(confirmMsg, confirmTitle);
        if (confirm)
        {
            MediaPresetManagerService.Instance.DeletePreset(_selectedPreset.Id);
            LoadPresetsList();
        }
    }

    private void ResetDefaults_Click(object? sender, RoutedEventArgs e)
    {
        string resetMsg = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_MsgResetConfirm", "¿Deseas restablecer todos los presets a los valores por defecto del sistema?");
        string resetTitle = FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("PresetManager_ResetBtn", "Restablecer");
        bool confirm = _dialogService.ShowConfirmation(resetMsg, resetTitle);
        if (confirm)
        {
            MediaPresetManagerService.Instance.ResetToDefaults();
            LoadPresetsList();
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
