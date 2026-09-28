using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Plugin.Integrations.UI.Services;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

namespace FileFlow.Plugin.Integrations.UI.ViewModels;

/// <summary>
/// El GESTOR DE PRESETS DE MEDIOS, sin toolkit: la lista de presets, el formulario del elegido y las cuatro
/// órdenes que los tocan (nuevo, guardar, eliminar, restablecer), todo contra el almacén que ya existía
/// (<see cref="MediaPresetManagerService"/>, el que lee el motor de transcodificación).
///
/// <para><b>Por qué existe.</b> El escritorio tenía esta lógica dentro del code-behind de su ventana: leer la
/// lista, volcar el preset elegido en los cuadros, normalizar la extensión al guardar, impedir borrar los
/// presets del sistema, confirmar el borrado y el restablecimiento. Una lógica así sólo la puede usar la
/// ventana que la tiene escrita —y sólo el host que sepa montar esa ventana—, así que ningún otro host podía
/// ofrecer el gestor sin reescribirlo (y una segunda copia de la regla de negocio es una segunda verdad).</para>
///
/// <para><b>Qué decide y qué NO decide la vista.</b> Aquí queda el QUÉ del producto: qué presets hay, qué
/// campos se editan, qué se normaliza y qué se prohíbe. Lo que la vista decide es de cada host: el escritorio
/// la monta en su ventana de Avalonia y un host WinUI en un panel suyo, y los avisos salen por el
/// <see cref="IDialogService"/> que le pasa quien la abre —el del host que la sirve—, no por uno inventado
/// aquí.</para>
/// </summary>
public partial class MediaPresetManagerViewModel : ObservableObject
{
    /// <summary>Las categorías que el desplegable ofrece de arranque (el resto se añade al encontrarlo).</summary>
    private static readonly string[] KnownCategories = ["Audio", "Video", "Animation", "Custom"];

    private readonly IMediaPresetStore _presetService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;

    /// <summary>Guarda de reentrada: cargar el formulario desde un preset no es una edición del usuario.</summary>
    private bool _loadingForm;

    public MediaPresetManagerViewModel(
        IMediaPresetStore? presetService = null,
        IDialogService? dialogService = null,
        ILocalizationService? localizationService = null)
    {
        _presetService = presetService ?? MediaPresetManagerService.Instance;
        _dialogService = dialogService ?? NullDialogService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;

        foreach (string category in KnownCategories)
        {
            Categories.Add(category);
        }

        Refresh();
    }

    /// <summary>Los presets del almacén, en el orden en que se guardaron (lo que la lista muestra).</summary>
    public ObservableCollection<MediaPreset> Presets { get; } = [];

    /// <summary>
    /// Las categorías del desplegable. La del preset elegido se añade si no está: un preset importado o
    /// escrito a mano con una categoría propia aparecía antes en un desplegable en blanco y al guardar se
    /// sustituía en silencio por «Video».
    /// </summary>
    public ObservableCollection<string> Categories { get; } = [];

    [ObservableProperty]
    private MediaPreset? _selectedPreset;

    [ObservableProperty]
    private string _presetName = string.Empty;

    [ObservableProperty]
    private string _presetDescription = string.Empty;

    [ObservableProperty]
    private string _outputExtension = string.Empty;

    [ObservableProperty]
    private string _ffmpegArguments = string.Empty;

    [ObservableProperty]
    private string _category = "Video";

    /// <summary>Hay un preset elegido (el formulario se refiere a él y las órdenes lo tocan).</summary>
    public bool HasSelection => SelectedPreset is not null;

    /// <summary>Vuelve a leer el almacén y elige el primero (el estado con el que la superficie se abre).</summary>
    public void Refresh()
    {
        Presets.Clear();
        foreach (MediaPreset preset in _presetService.GetPresets())
        {
            Presets.Add(preset);
        }

        SelectedPreset = Presets.FirstOrDefault();
    }

    /// <summary>
    /// Un preset nuevo con los valores de fábrica del gestor: se guarda en el almacén y queda elegido, para
    /// que el usuario lo edite en el formulario (que es lo que el escritorio hacía).
    /// </summary>
    [RelayCommand]
    public void NewPreset()
    {
        var created = new MediaPreset
        {
            Name = _loc.GetString("PresetManager_NewPresetName", "Nuevo Preset Personalizado"),
            Description = _loc.GetString("PresetManager_NewPresetDescription", "Descripción del nuevo preset..."),
            Category = "Video",
            OutputExtension = ".mp4",
            FfmpegArguments = "-c:v libx264 -crf 23 -c:a aac",
            IsSystemDefault = false
        };

        _presetService.SavePreset(created);
        Refresh();

        SelectedPreset = Presets.FirstOrDefault(p => p.Id == created.Id) ?? SelectedPreset;
    }

    /// <summary>
    /// Guarda el preset elegido con lo que el formulario tiene escrito. La extensión se normaliza (con punto)
    /// y la categoría es la ELEGIDA, con la del propio preset como respaldo: nunca se inventa una.
    /// </summary>
    [RelayCommand]
    public void SaveCurrent()
    {
        if (SelectedPreset is not { } preset)
        {
            return;
        }

        preset.Name = PresetName.Trim();
        preset.Description = PresetDescription.Trim();
        preset.OutputExtension = NormalizeExtension(OutputExtension);
        preset.FfmpegArguments = FfmpegArguments.Trim();
        preset.Category = string.IsNullOrWhiteSpace(Category) ? preset.Category : Category;

        _presetService.SavePreset(preset);
        Refresh();
        SelectedPreset = Presets.FirstOrDefault(p => p.Id == preset.Id) ?? SelectedPreset;

        _dialogService.ShowInformation(
            _loc.GetString("PresetManager_MsgSaveSuccess", "Preset guardado con éxito."),
            _loc.GetString("PresetManager_WindowTitle", "Media Preset Manager"));
    }

    /// <summary>
    /// Borra el preset elegido, previa confirmación, y nunca uno del sistema.
    ///
    /// <para><b>Por qué es asíncrona.</b> La pregunta se le hace al host por
    /// <see cref="IDialogService.ConfirmAsync"/> y no por la variante síncrona: un host cuyo modal sólo existe
    /// en asíncrono (WinUI) devolvía «no» sin preguntar —la orden no hacía nada y tampoco avisaba—, mientras
    /// que otro servicio sin diálogos respondía «sí» sin enseñar nada. Con la respuesta REAL en la mano, la
    /// regla es una sola: se borra si el usuario dijo que sí, y no se borra en ningún otro caso.</para>
    /// </summary>
    [RelayCommand]
    public async Task DeletePresetAsync()
    {
        if (SelectedPreset is not { } preset)
        {
            return;
        }

        if (preset.IsSystemDefault)
        {
            _dialogService.ShowWarning(
                _loc.GetString("PresetManager_MsgDefaultCannotDelete", "No se pueden eliminar los presets predeterminados del sistema."),
                _loc.GetString("Warning", "Aviso"));
            return;
        }

        string question = string.Format(
            _loc.GetString("PresetManager_MsgDeleteConfirm", "¿Deseas eliminar el preset '{0}'?"), preset.Name);

        if (!await _dialogService.ConfirmAsync(question, _loc.GetString("PresetManager_DeleteBtn", "Eliminar")))
        {
            return;
        }

        _presetService.DeletePreset(preset.Id);
        Refresh();
    }

    /// <summary>Restablece el catálogo a los presets del sistema, previa confirmación (misma respuesta real que el borrado).</summary>
    [RelayCommand]
    public async Task ResetDefaultsAsync()
    {
        if (!await _dialogService.ConfirmAsync(
                _loc.GetString("PresetManager_MsgResetConfirm", "¿Deseas restablecer todos los presets a los valores por defecto del sistema?"),
                _loc.GetString("PresetManager_ResetBtn", "Restablecer")))
        {
            return;
        }

        _presetService.ResetToDefaults();
        Refresh();
    }

    /// <summary>La extensión de salida, con el punto que el motor espera (un «mp4» a mano no vale).</summary>
    internal static string NormalizeExtension(string? extension)
    {
        string trimmed = (extension ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }

    /// <summary>
    /// Lleva el preset elegido al formulario. Es el único camino por el que los cuadros se llenan: la
    /// selección la puede cambiar la lista (el clic del usuario) o una orden (crear, guardar), y en los dos
    /// casos el formulario tiene que decir lo mismo que la lista.
    /// </summary>
    partial void OnSelectedPresetChanged(MediaPreset? value)
    {
        OnPropertyChanged(nameof(HasSelection));

        if (value is null)
        {
            return;
        }

        _loadingForm = true;
        try
        {
            PresetName = value.Name;
            PresetDescription = value.Description;
            OutputExtension = value.OutputExtension;
            FfmpegArguments = value.FfmpegArguments;
            EnsureCategory(value.Category);
            Category = value.Category;
        }
        finally
        {
            _loadingForm = false;
        }
    }

    /// <summary>¿Se está volcando un preset en el formulario? (la vista lo consulta si necesita distinguirlo).</summary>
    public bool IsLoadingForm => _loadingForm;

    private void EnsureCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return;
        }

        if (!Categories.Any(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)))
        {
            Categories.Add(category);
        }
    }
}
