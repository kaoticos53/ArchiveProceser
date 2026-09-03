using System.IO;
using System.Text.Json;
using FileFlow.Sdk.Themes;

namespace FileFlow.App.Services;

/// <summary>
/// Catálogo inmutable de temas de fábrica predefinidos para FileFlow Studio cargado desde recurso embebido JSON.
/// </summary>
public static class BuiltInThemesCatalog
{
    private static readonly List<ThemeDefinition> CachedThemes = LoadThemes();

    public static List<ThemeDefinition> GetThemes() => CachedThemes.Select(t => t.Clone()).ToList();

    private static List<ThemeDefinition> LoadThemes()
    {
        var list = new List<ThemeDefinition>();
        try
        {
            var assembly = typeof(BuiltInThemesCatalog).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("builtin_themes.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                    string json = reader.ReadToEnd();
                    var loaded = JsonSerializer.Deserialize<List<ThemeDefinition>>(json);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading embedded builtin themes: {ex.Message}");
        }
        return list;
    }
}
