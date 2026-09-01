using System.IO;
using System.Text.Json;
using System.Windows;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.Themes;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio para la gestión, serialización, persistencia y ciclo de vida de temas visuales personalizados en FileFlow Studio.
/// </summary>
public class CustomThemeService
{
    private static readonly Lazy<CustomThemeService> _instance = new(() => new CustomThemeService());
    public static CustomThemeService Instance => _instance.Value;

    private readonly string _storagePath;
    private readonly System.Threading.Lock _lock = new();
    private readonly List<ThemeDefinition> _builtInThemes;
    private List<ThemeDefinition> _customThemes = [];

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public CustomThemeService() : this(AppPaths.CustomThemesFile)
    {
    }

    public CustomThemeService(string storagePath)
    {
        _storagePath = storagePath;
        _builtInThemes = BuiltInThemesCatalog.GetThemes();
        LoadCustomThemes();
    }

    public IReadOnlyList<ThemeDefinition> GetAllThemes()
    {
        lock (_lock)
        {
            var list = new List<ThemeDefinition>(_builtInThemes.Count + _customThemes.Count);
            list.AddRange(_builtInThemes.Select(t => t.Clone()));
            list.AddRange(_customThemes.Select(t => t.Clone()));
            return list;
        }
    }

    public ThemeDefinition? GetThemeById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        lock (_lock)
        {
            var found = _builtInThemes.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                     ?? _customThemes.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            return found?.Clone();
        }
    }

    public void SaveCustomTheme(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        lock (_lock)
        {
            theme.IsBuiltIn = false;
            int existingIndex = _customThemes.FindIndex(t => t.Id.Equals(theme.Id, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                _customThemes[existingIndex] = theme.Clone();
            }
            else
            {
                _customThemes.Add(theme.Clone());
            }

            PersistCustomThemes();
        }
    }

    public bool DeleteCustomTheme(string id)
    {
        lock (_lock)
        {
            int removed = _customThemes.RemoveAll(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                PersistCustomThemes();
                return true;
            }
            return false;
        }
    }

    public ThemeDefinition DuplicateTheme(ThemeDefinition original, string newName)
    {
        ArgumentNullException.ThrowIfNull(original);

        var copy = original.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = string.IsNullOrWhiteSpace(newName) ? $"{original.Name} (Copia)" : newName;
        copy.IsBuiltIn = false;

        SaveCustomTheme(copy);
        return copy;
    }

    public string ExportThemeToJson(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return JsonSerializer.Serialize(theme, _jsonOptions);
    }

    public ThemeDefinition ImportThemeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("El contenido JSON del tema no puede estar vacío.", nameof(json));
        }

        var imported = JsonSerializer.Deserialize<ThemeDefinition>(json, _jsonOptions)
            ?? throw new InvalidOperationException("No se pudo deserializar la definición del tema.");

        imported.Id = Guid.NewGuid().ToString("N");
        imported.IsBuiltIn = false;
        if (string.IsNullOrWhiteSpace(imported.Name))
        {
            imported.Name = "Tema Importado";
        }

        SaveCustomTheme(imported);
        return imported;
    }

    /// <summary>
    /// Genera el diccionario de recursos WPF correspondiente al tema indicado.
    /// </summary>
    public static ResourceDictionary BuildResourceDictionary(ThemeDefinition theme)
    {
        return ThemeResourceApplier.BuildResourceDictionary(theme);
    }

    private void LoadCustomThemes()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    string json = File.ReadAllText(_storagePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var loaded = JsonSerializer.Deserialize<List<ThemeDefinition>>(json, _jsonOptions);
                        if (loaded != null)
                        {
                            _customThemes = loaded;
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Fallback a lista vacía si hay error de I/O o JSON inválido
            }
            _customThemes = [];
        }
    }

    private void PersistCustomThemes()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(_customThemes, _jsonOptions);
            File.WriteAllText(_storagePath, json);
        }
        catch
        {
            // Manejar silenciosamente excepciones de permisos de disco
        }
    }
}
