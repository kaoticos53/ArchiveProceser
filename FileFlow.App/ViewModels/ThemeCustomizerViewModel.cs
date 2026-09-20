using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.App.Themes;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

namespace FileFlow.App.ViewModels;

/// <summary>
/// View model del Theme Studio.
///
/// El editor no está escrito a mano ajuste por ajuste: se genera a partir de <see cref="ThemeSettingCatalog"/>
/// y cada fila escribe sobre la propiedad real de <see cref="ThemeDefinition"/>. Al cambiar cualquier valor se
/// regeneran los tokens de <see cref="LivePreviewResources"/>, que es el diccionario que consume la vista previa,
/// de modo que mover un radio, una tipografía o una sombra se ve al instante sin aplicar el tema a la aplicación.
/// </summary>
public partial class ThemeCustomizerViewModel : ObservableObject
{
    private readonly CustomThemeService _themeService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ThemeDefinition? _selectedTheme;

    [ObservableProperty]
    private ThemeDefinition _editingTheme = new();

    [ObservableProperty]
    private bool _isCustomTheme;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Nombre del tema en edición (campo de la cabecera del editor).</summary>
    [ObservableProperty]
    private string _currentThemeName = string.Empty;

    /// <summary>
    /// Tokens del tema en edición. La instancia es <b>estable</b> a propósito: la vista previa la engancha una
    /// sola vez y cada ajuste reemplaza los valores en el sitio, de modo que los <c>DynamicResource</c> del
    /// panel de previsualización se re-resuelven en caliente. Reemplazar el diccionario entero dejaría a la
    /// vista apuntando al objeto antiguo.
    /// </summary>
    public ResourceDictionary LivePreviewResources { get; } = new();

    /// <summary>Editor por secciones, generado desde el catálogo de ajustes del tema.</summary>
    public ObservableCollection<ThemeSettingSectionViewModel> Sections { get; } = [];

    public ObservableCollection<ThemeDefinition> AvailableThemes { get; } = [];

    /// <summary>Familias tipográficas publicadas por el catálogo (misma fuente de verdad que el editor).</summary>
    public ObservableCollection<string> AvailableFontFamilies { get; }

    /// <summary>Familias monoespaciadas publicadas por el catálogo.</summary>
    public ObservableCollection<string> AvailableCodeFonts { get; }

    public ThemeCustomizerViewModel() : this(CustomThemeService.Instance, ResolveDialogService())
    {
    }

    /// <summary>
    /// Servicio de diálogos de la aplicación cuando el contenedor ya está levantado.
    ///
    /// El constructor sin argumentos es el que usa el estudio al abrirse por su cuenta, así que no puede
    /// quedarse con el doble nulo: sin diálogo real, eliminar un tema no pide confirmación y un error al
    /// importar o exportar no se le cuenta a nadie. Se resuelve del contenedor —igual que el resto de view
    /// models de la aplicación— con el doble nulo como último recurso.
    /// </summary>
    private static IDialogService ResolveDialogService() =>
        App.Services?.GetService(typeof(IDialogService)) as IDialogService ?? NullDialogService.Instance;

    public ThemeCustomizerViewModel(CustomThemeService themeService, IDialogService? dialogService = null)
    {
        _themeService = themeService;
        _dialogService = dialogService ?? NullDialogService.Instance;

        AvailableFontFamilies = [.. CatalogOptions(nameof(ThemeDefinition.FontFamily))];
        AvailableCodeFonts = [.. CatalogOptions(nameof(ThemeDefinition.CodeFontFamily))];

        LoadThemes();

        string currentId = ThemeManager.Instance.CurrentThemeId;
        SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase))
                     ?? AvailableThemes.FirstOrDefault();
    }

    /// <summary>Opciones que el catálogo publica para un ajuste de elección (las familias tipográficas).</summary>
    private static string[] CatalogOptions(string propertyName) =>
        ThemeSettingCatalog.Settings
            .FirstOrDefault(setting => setting.Property == propertyName)
            ?.Options?.ToArray()
        ?? [];

    public void LoadThemes()
    {
        AvailableThemes.Clear();
        foreach (var theme in _themeService.GetAllThemes())
        {
            AvailableThemes.Add(theme);
        }
    }

    partial void OnSelectedThemeChanged(ThemeDefinition? value)
    {
        if (value == null) return;

        EditingTheme = value.Clone();
        IsCustomTheme = !value.IsBuiltIn;
        CurrentThemeName = value.Name;
        RebuildEditor();
        UpdateLivePreview();
        StatusMessage = LocalizationManager.Instance.GetFormattedString("ThemeStudio_Status_Loaded", "Theme loaded: {0}", value.Name);
    }

    partial void OnCurrentThemeNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || EditingTheme.Name == value)
        {
            return;
        }

        EditingTheme.Name = value;
        RefreshSelectedThemeName();
    }

    /// <summary>Reconstruye el editor a partir del catálogo y engancha cada fila al refresco de la vista previa.</summary>
    private void RebuildEditor()
    {
        Sections.Clear();

        foreach (string sectionKey in ThemeSettingCatalog.Sections)
        {
            var rows = ThemeSettingCatalog.Settings
                .Where(setting => setting.SectionKey == sectionKey)
                .Select(CreateRow)
                .ToList();

            if (rows.Count == 0)
            {
                continue;
            }

            Sections.Add(new ThemeSettingSectionViewModel(sectionKey, rows));
        }
    }

    private ThemeSettingRowViewModel CreateRow(ThemeSettingDescriptor descriptor)
    {
        ThemeSettingRowViewModel row = descriptor.Kind switch
        {
            ThemeSettingKind.Color => new ThemeColorRowViewModel(descriptor, EditingTheme),
            ThemeSettingKind.Choice => new ThemeChoiceRowViewModel(descriptor, EditingTheme),
            _ => new ThemeNumberRowViewModel(descriptor, EditingTheme)
        };

        row.ValueChanged += UpdateLivePreview;
        return row;
    }

    /// <summary>
    /// Regenera los tokens del tema en edición y los vuelca sobre la instancia estable de
    /// <see cref="LivePreviewResources"/>, que es lo que consume la previsualización en vivo.
    /// </summary>
    public void UpdateLivePreview()
    {
        var generated = CustomThemeService.BuildResourceDictionary(EditingTheme);

        foreach (var entry in generated)
        {
            if (entry.Key is string key)
            {
                LivePreviewResources[key] = entry.Value;
            }
        }
    }

    private void RefreshSelectedThemeName()
    {
        if (SelectedTheme == null)
        {
            return;
        }

        int index = AvailableThemes.IndexOf(SelectedTheme);
        if (index < 0)
        {
            return;
        }

        SelectedTheme.Name = EditingTheme.Name;

        // La lista muestra instancias del servicio, así que se avisa del cambio de nombre de la fila en curso.
        AvailableThemes[index] = SelectedTheme;
    }

    [RelayCommand]
    public void NewCustomTheme()
    {
        var newTheme = new ThemeDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = LocalizationManager.Instance.GetString("ThemeStudio_DefaultNewThemeName", "My Custom Theme"),
            Description = LocalizationManager.Instance.GetString("ThemeStudio_DefaultNewThemeDescription", "Custom theme created by the user."),
            IsBuiltIn = false,
            IsDark = EditingTheme.IsDark
        };

        _themeService.SaveCustomTheme(newTheme);
        LoadThemes();
        SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == newTheme.Id);
        StatusMessage = LocalizationManager.Instance.GetString("ThemeStudio_Status_Created", "New custom theme created.");
    }

    [RelayCommand]
    public void DuplicateTheme()
    {
        if (SelectedTheme == null) return;

        string suffix = LocalizationManager.Instance.GetString("ThemeStudio_CopySuffix", "(Copy)");
        string newName = $"{SelectedTheme.Name} {suffix}";
        var duplicated = _themeService.DuplicateTheme(EditingTheme, newName);
        LoadThemes();
        SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == duplicated.Id);
        StatusMessage = LocalizationManager.Instance.GetFormattedString("ThemeStudio_Status_Duplicated", "Theme duplicated as '{0}'.", newName);
    }

    [RelayCommand]
    public void DeleteTheme()
    {
        if (SelectedTheme == null || SelectedTheme.IsBuiltIn)
        {
            _dialogService.ShowInformation(
                LocalizationManager.Instance.GetString("Msg_ThemeFactoryNoDelete", "Cannot delete factory default themes."),
                LocalizationManager.Instance.GetString("ThemeCustomizer_HeaderTitle", "Theme Studio"));
            return;
        }

        string confirmMessage = LocalizationManager.Instance.GetFormattedString(
            "ThemeStudio_ConfirmDelete",
            "Delete the custom theme '{0}'?",
            SelectedTheme.Name);
        string confirmTitle = LocalizationManager.Instance.GetString("ThemeStudio_ConfirmDeleteTitle", "Confirm deletion");

        if (!_dialogService.ShowConfirmation(confirmMessage, confirmTitle)) return;

        _themeService.DeleteCustomTheme(SelectedTheme.Id);
        LoadThemes();
        SelectedTheme = AvailableThemes.FirstOrDefault();
        StatusMessage = LocalizationManager.Instance.GetString("ThemeStudio_Status_Deleted", "Theme deleted.");
    }

    [RelayCommand]
    public void SaveCustomTheme()
    {
        if (EditingTheme.IsBuiltIn)
        {
            // Los temas de fábrica son inmutables: guardar sobre uno crea una copia personalizada.
            var copy = EditingTheme.Clone();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name = $"{EditingTheme.Name} {LocalizationManager.Instance.GetString("ThemeStudio_CustomSuffix", "(Custom)")}";
            copy.IsBuiltIn = false;
            _themeService.SaveCustomTheme(copy);
            LoadThemes();
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == copy.Id);
            StatusMessage = LocalizationManager.Instance.GetFormattedString("ThemeStudio_Status_SavedAs", "Saved as custom theme: {0}", copy.Name);
        }
        else
        {
            _themeService.SaveCustomTheme(EditingTheme);
            LoadThemes();
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == EditingTheme.Id);
            StatusMessage = LocalizationManager.Instance.GetFormattedString("ThemeStudio_Status_Saved", "Theme '{0}' saved.", EditingTheme.Name);
        }
    }

    [RelayCommand]
    public void ApplyToApplication()
    {
        ThemeManager.Instance.SetTheme(EditingTheme);

        var prefs = UserPreferencesService.Instance.Preferences;
        prefs.ActiveTheme = EditingTheme.Id;
        UserPreferencesService.Instance.Save();

        StatusMessage = LocalizationManager.Instance.GetFormattedString("ThemeStudio_Status_Applied", "Theme '{0}' applied to the application live.", EditingTheme.Name);
    }

    [RelayCommand]
    public void SaveAndApply(Window? window)
    {
        SaveCustomTheme();
        ApplyToApplication();

        window?.Close(true);
    }

    [RelayCommand]
    public async Task ExportThemeAsync()
    {
        var file = await PickFileAsync(save: true,
            LocalizationManager.Instance.GetString("Theme_ExportTitle", "Export Visual Theme"),
            $"{SanitizeFileName(EditingTheme.Name)}_theme.json");

        if (file == null) return;

        try
        {
            string json = _themeService.ExportThemeToJson(EditingTheme);
            await File.WriteAllTextAsync(file, json);
            StatusMessage = LocalizationManager.Instance.GetFormattedString(
                "ThemeStudio_Status_Exported", "Theme exported to '{0}'.", Path.GetFileName(file));

            _dialogService.ShowInformation(
                LocalizationManager.Instance.GetString("Msg_ThemeExportSuccess", "Theme exported successfully."),
                LocalizationManager.Instance.GetString("Theme_ExportTitle", "Export Visual Theme"));
        }
        catch (Exception ex)
        {
            ReportThemeError(ex);
        }
    }

    [RelayCommand]
    public async Task ImportThemeAsync()
    {
        var file = await PickFileAsync(save: false,
            LocalizationManager.Instance.GetString("Theme_ImportTitle", "Import Visual Theme"),
            suggestedFileName: null);

        if (file == null) return;

        try
        {
            string json = await File.ReadAllTextAsync(file);
            var imported = _themeService.ImportThemeFromJson(json);
            LoadThemes();
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == imported.Id);
            StatusMessage = LocalizationManager.Instance.GetFormattedString("ThemeStudio_Status_Imported", "Theme '{0}' imported successfully.", imported.Name);

            _dialogService.ShowInformation(
                LocalizationManager.Instance.GetFormattedString("Msg_ThemeImportSuccess", "Theme '{0}' imported and added to your custom themes.", imported.Name),
                LocalizationManager.Instance.GetString("Theme_ImportTitle", "Import Visual Theme"));
        }
        catch (Exception ex)
        {
            ReportThemeError(ex);
        }
    }

    private void ReportThemeError(Exception ex)
    {
        string message = LocalizationManager.Instance.GetFormattedString("ThemeStudio_ErrorFormat", "Theme error: {0}", ex.Message);
        StatusMessage = message;
        _dialogService.ShowError(message, LocalizationManager.Instance.GetString("ThemeStudio_ErrorTitle", "Theme error"));
    }

    private static async Task<string?> PickFileAsync(bool save, string title, string? suggestedFileName)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow == null)
        {
            return null;
        }

        var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        if (topLevel?.StorageProvider == null)
        {
            return null;
        }

        if (save)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                DefaultExtension = "json",
                SuggestedFileName = suggestedFileName ?? "theme.json"
            });

            return file?.Path.LocalPath;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace(' ', '_');
}
