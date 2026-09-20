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

    public MediaPresetManagerWindow() : this(null)
    {
    }

    public MediaPresetManagerWindow(IDialogService? dialogService = null)
    {
        _dialogService = dialogService ?? NullDialogService.Instance;
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

    private void LstPresets_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LstPresets.SelectedItem is MediaPreset preset)
        {
            _selectedPreset = preset;
            TxtName.Text = preset.Name;
            TxtDescription.Text = preset.Description;
            TxtExtension.Text = preset.OutputExtension;
            TxtFfmpegArgs.Text = preset.FfmpegArguments;

            // La categoría del preset se selecciona buscando el elemento con ese texto. Si el preset trae una
            // categoría que no está en la lista (un preset importado o escrito a mano), se añade: sin ella el
            // desplegable aparecía en blanco y al guardar la categoría se sustituía en silencio por «Video».
            EnsureCategoryItem(preset.Category);

            if (CmbCategory != null)
            {
                foreach (var item in CmbCategory.Items)
                {
                    if (item is ComboBoxItem cbi && cbi.Content?.ToString()?.Equals(preset.Category, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        CmbCategory.SelectedItem = cbi;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>Añade a la lista la categoría pedida si todavía no está (el desplegable no pinta valores ausentes).</summary>
    private void EnsureCategoryItem(string? category)
    {
        if (string.IsNullOrWhiteSpace(category) || CmbCategory == null)
        {
            return;
        }

        bool exists = CmbCategory.Items.OfType<ComboBoxItem>()
            .Any(i => string.Equals(i.Content?.ToString(), category, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            CmbCategory.Items.Add(new ComboBoxItem { Content = category });
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
        if (created != null)
        {
            LstPresets.SelectedItem = created;
        }
    }

    private void SaveCurrent_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedPreset == null) return;

        // Nunca se inventa la categoría: se guarda lo seleccionado, o la del propio preset como respaldo.
        string category = (CmbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString()
                          ?? _selectedPreset.Category
                          ?? "Video";

        _selectedPreset.Name = TxtName.Text?.Trim() ?? string.Empty;
        _selectedPreset.Description = TxtDescription.Text?.Trim() ?? string.Empty;
        _selectedPreset.OutputExtension = TxtExtension.Text?.Trim() ?? string.Empty;
        if (!_selectedPreset.OutputExtension.StartsWith('.'))
        {
            _selectedPreset.OutputExtension = "." + _selectedPreset.OutputExtension;
        }
        _selectedPreset.FfmpegArguments = TxtFfmpegArgs.Text?.Trim() ?? string.Empty;
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
