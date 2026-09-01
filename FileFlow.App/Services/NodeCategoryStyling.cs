using System.Windows.Media;

namespace FileFlow.App.Services;

/// <summary>
/// Provee la paleta de colores y estilos visuales por defecto según la categoría del nodo.
/// </summary>
public static class NodeCategoryStyling
{
    public static (string HeaderColor, string AccentColor) GetColorsForCategory(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "filesystem" => ("#143328", "#10B981"),
            "archives" => ("#362713", "#F59E0B"),
            "images" => ("#301438", "#A855F7"),
            _ => ("#1F2433", "#818CF8")
        };
    }

    public static string GetHeaderColorFromAccent(string accentHex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(accentHex);
            byte r = (byte)(color.R * 0.25);
            byte g = (byte)(color.G * 0.25);
            byte b = (byte)(color.B * 0.25);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch
        {
            return "#202430";
        }
    }
}
