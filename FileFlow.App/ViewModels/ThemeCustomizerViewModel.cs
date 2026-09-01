using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Services;
using FileFlow.Sdk.Themes;
using Microsoft.Win32;

namespace FileFlow.App.ViewModels;

public partial class ThemeCustomizerViewModel : ObservableObject
{
    private readonly CustomThemeService _themeService;

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

    public ThemeCustomizerViewModel() : this(CustomThemeService.Instance)
    {
    }

    public ThemeCustomizerViewModel(CustomThemeService themeService)
    {
        _themeService = themeService;

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

        try
        {
            foreach (var font in Fonts.SystemFontFamilies)
            {
                string name = font.Source;
                if (!AvailableFontFamilies.Contains(name) && !string.IsNullOrWhiteSpace(name))
                {
                    AvailableFontFamilies.Add(name);
                }
            }
        }
        catch
        {
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
            MessageBox.Show("No se pueden eliminar los temas predefinidos de fábrica.", "Eliminar Tema", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"¿Estás seguro de que deseas eliminar el tema personalizado '{SelectedTheme.Name}'?",
            "Confirmar Eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

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
            window.DialogResult = true;
            window.Close();
        }
    }

    [RelayCommand]
    public void ExportTheme()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Archivo de Tema FileFlow (*.json)|*.json",
            FileName = $"{SanitizeFileName(EditingTheme.Name)}_theme.json",
            Title = "Exportar Tema Visual"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                string json = _themeService.ExportThemeToJson(EditingTheme);
                File.WriteAllText(sfd.FileName, json);
                StatusMessage = $"Tema exportado con éxito a '{Path.GetFileName(sfd.FileName)}'.";
                MessageBox.Show("Tema exportado correctamente.", "Exportación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar tema: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void ImportTheme()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Archivo de Tema FileFlow (*.json)|*.json",
            Title = "Importar Tema Visual"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                string json = File.ReadAllText(ofd.FileName);
                var imported = _themeService.ImportThemeFromJson(json);
                LoadThemes();
                SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == imported.Id);
                StatusMessage = $"Tema '{imported.Name}' importado con éxito.";
                MessageBox.Show($"Tema '{imported.Name}' importado y añadido a tus temas personalizados.", "Importación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar tema: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");
    }
}
