namespace FileFlow.App.Services;

/// <summary>
/// Provee la paleta de colores y estilos visuales por defecto según la categoría del nodo.
/// Es portable: interpreta los hex a mano (#RRGGBB / #AARRGGBB) en lugar de depender del
/// <c>Color.Parse</c> del framework de interfaz, que vive en cada host.
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
        // El encabezado es el acento oscurecido al 25% de brillo. Antes se parseaba con el Color del
        // framework; ahora se hace a mano para que el núcleo no dependa de ningún host.
        if (TryParseHex(accentHex, out byte r, out byte g, out byte b))
        {
            byte dr = (byte)(r * 0.25);
            byte dg = (byte)(g * 0.25);
            byte db = (byte)(b * 0.25);
            return $"#{dr:X2}{dg:X2}{db:X2}";
        }

        return "#202430";
    }

    /// <summary>Parseo manual de #RGB, #RRGGBB y #AARRGGBB; false si el texto no es un color.</summary>
    private static bool TryParseHex(string? text, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(text) || text[0] != '#')
        {
            return false;
        }

        string hex = text[1..];
        try
        {
            switch (hex.Length)
            {
                case 3:
                    r = Convert.ToByte(new string(hex[0], 2), 16);
                    g = Convert.ToByte(new string(hex[1], 2), 16);
                    b = Convert.ToByte(new string(hex[2], 2), 16);
                    return true;
                case 6:
                    r = Convert.ToByte(hex[..2], 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return true;
                case 8:
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                    return true;
                default:
                    return false;
            }
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
