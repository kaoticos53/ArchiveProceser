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

public partial class ThemeCustomizerViewModel : ObservableObject
{
    private readonly CustomThemeService _themeService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ThemeDefinition? _selectedTheme;

    [ObservableProperty]
    private ThemeDefinition _editingTheme = new();

    [ObservableProperty]
    private ResourceDictionary _livePreviewResources = new();

    [ObservableProperty]
    private bool _isCustomTheme;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<ThemeDefinition> AvailableThemes { get; } = [];
    public ObservableCollection<string> AvailableFontFamilies { get; } = [];
    public ObservableCollection<string> AvailableCodeFonts { get; } = [];
    public IReadOnlyList<double> FontSizes { get; } = [10.0, 11.0, 12.0, 13.0, 14.0, 15.0, 16.0];
    public IReadOnlyList<double> CornerRadiusOptions { get; } = [0.0, 2.0, 4.0, 6.0, 8.0, 10.0, 12.0, 16.0];

    public ThemeCustomizerViewModel() : this(CustomThemeService.Instance, null)
    {
    }

    public ThemeCustomizerViewModel(CustomThemeService themeService, IDialogService? dialogService = null)
    {
        _themeService = themeService;
        _dialogService = dialogService ?? NullDialogService.Instance;

        LoadFontLists();
        LoadThemes();

        string currentId = ThemeManager.Instance.CurrentThemeId;
        SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase))
                     ?? AvailableThemes.FirstOrDefault();
    }

    private void LoadFontLists()
    {
        var curatedFonts = new[] { "Segoe UI", "Segoe UI Variable Text", "Inter", "Roboto", "Outfit", "Arial", "Ubuntu", "Tahoma", "Verdana" };
        foreach (var font in curatedFonts)
        {
            AvailableFontFamilies.Add(font);
        }

        var curatedCodeFonts = new[] { "Cascadia Code, Consolas, monospace", "Cascadia Code", "Consolas", "Fira Code", "Courier New", "monospace" };
        foreach (var cf in curatedCodeFonts)
        {
            AvailableCodeFonts.Add(cf);
        }
    }

    public void LoadThemes()
    {
        AvailableThemes.Clear();
        var all = _themeService.GetAllThemes();
        foreach (var theme in all)
        {
            AvailableThemes.Add(theme);
        }
    }

    partial void OnSelectedThemeChanged(ThemeDefinition? value)
    {
        if (value == null) return;

        EditingTheme = value.Clone();
        IsCustomTheme = !value.IsBuiltIn;
        UpdateLivePreview();
        StatusMessage = $"Tema cargado: {value.Name}";
    }

    public void UpdateLivePreview()
    {
        LivePreviewResources = CustomThemeService.BuildResourceDictionary(EditingTheme);
    }

    [RelayCommand]
    public void NewCustomTheme()
    {
        var newTheme = new ThemeDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Mi Tema Personalizado",
            Description = "Tema personalizado creado por el usuario.",
            IsBuiltIn = false,
            IsDark = true
        };

        _themeService.SaveCustomTheme(newTheme);
        LoadThemes();
        SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == newTheme.Id);
        StatusMessage = "Nuevo tema personalizado creado.";
    }

    [RelayCommand]
    public void DuplicateTheme()
    {
        if (SelectedTheme == null) return;

        string newName = $"{SelectedTheme.Name} (Copia)";
        var duplicated = _themeService.DuplicateTheme(EditingTheme, newName);
        LoadThemes();
        SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == duplicated.Id);
        StatusMessage = $"Tema duplicado como '{newName}'.";
    }

    [RelayCommand]
    public void DeleteTheme()
    {
        if (SelectedTheme == null || SelectedTheme.IsBuiltIn)
        {
            string factoryMsg = LocalizationManager.Instance.GetString("Msg_ThemeFactoryNoDelete", "No se pueden eliminar los temas predefinidos de fábrica.");
            string title = LocalizationManager.Instance.GetString("ThemeCustomizer_Title", "Personalizador de Temas");
            _dialogService.ShowInformation(factoryMsg, title);
            return;
        }

        bool confirm = _dialogService.ShowConfirmation(
            $"¿Estás seguro de que deseas eliminar el tema personalizado '{SelectedTheme.Name}'?",
            "Confirmar Eliminación");

        if (!confirm) return;

        string idToDelete = SelectedTheme.Id;
        _themeService.DeleteCustomTheme(idToDelete);
        LoadThemes();
        SelectedTheme = AvailableThemes.FirstOrDefault();
        StatusMessage = "Tema eliminado.";
    }

    [RelayCommand]
    public void SaveCustomTheme()
    {
        if (EditingTheme.IsBuiltIn)
        {
            // Si es un tema de fábrica, crear una copia personalizada
            var copy = EditingTheme.Clone();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name = $"{EditingTheme.Name} (Personalizado)";
            copy.IsBuiltIn = false;
            _themeService.SaveCustomTheme(copy);
            LoadThemes();
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == copy.Id);
            StatusMessage = $"Guardado como tema personalizado: {copy.Name}";
        }
        else
        {
            _themeService.SaveCustomTheme(EditingTheme);
            LoadThemes();
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == EditingTheme.Id);
            StatusMessage = $"Tema '{EditingTheme.Name}' guardado correctamente.";
        }
    }

    [RelayCommand]
    public void ApplyToApplication()
    {
        ThemeManager.Instance.SetTheme(EditingTheme);

        var prefs = UserPreferencesService.Instance.Preferences;
        prefs.ActiveTheme = EditingTheme.Id;
        UserPreferencesService.Instance.Save();

        StatusMessage = $"Tema '{EditingTheme.Name}' aplicado a la aplicación en vivo.";
    }

    [RelayCommand]
    public void SaveAndApply(Window? window)
    {
        SaveCustomTheme();
        ApplyToApplication();

        if (window != null)
        {
            window.Close(true);
        }
    }

    [RelayCommand]
    public async Task ExportThemeAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.StorageProvider != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Exportar Tema Visual",
                    DefaultExtension = "json",
                    SuggestedFileName = $"{SanitizeFileName(EditingTheme.Name)}_theme.json"
                });

                if (file != null)
                {
                    try
                    {
                        string json = _themeService.ExportThemeToJson(EditingTheme);
                        await File.WriteAllTextAsync(file.Path.LocalPath, json);
                        StatusMessage = $"Tema exportado con éxito a '{Path.GetFileName(file.Path.LocalPath)}'.";
                        string successMsg = LocalizationManager.Instance.GetString("Msg_ThemeExportSuccess", "Tema exportado correctamente.");
                        string title = LocalizationManager.Instance.GetString("ThemeCustomizer_Title", "Temas");
                        _dialogService.ShowInformation(successMsg, title);
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowError($"Error: {ex.Message}", "Error");
                    }
                }
            }
        }
    }

    [RelayCommand]
    public async Task ImportThemeAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.StorageProvider != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Importar Tema Visual",
                    AllowMultiple = false
                });

                if (files != null && files.Count > 0)
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(files[0].Path.LocalPath);
                        var imported = _themeService.ImportThemeFromJson(json);
                        LoadThemes();
                        SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == imported.Id);
                        StatusMessage = $"Tema '{imported.Name}' importado con éxito.";
                        string successMsg = string.Format(LocalizationManager.Instance.GetString("Msg_ThemeImportSuccess", "Tema '{0}' importado y añadido a tus temas personalizados."), imported.Name);
                        string title = LocalizationManager.Instance.GetString("ThemeCustomizer_Title", "Temas");
                        _dialogService.ShowInformation(successMsg, title);
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowError($"Error: {ex.Message}", "Error");
                    }
                }
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");
    }
}
