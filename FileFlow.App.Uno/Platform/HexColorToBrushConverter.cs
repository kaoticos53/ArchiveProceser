using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// Convierte un color hexadecimal del tema (los tokens del núcleo, p. ej. <c>#161B22</c>) en el pincel
/// sólido que consume el XAML del lienzo: el host Uno pinta con los mismos valores que genera
/// <c>ThemeResourceApplier</c> en el escritorio, hasta que la fase 3.5 traiga la republicación en caliente.
/// </summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
        {
            return DependencyProperty.UnsetValue;
        }

        string text = hex.Trim().TrimStart('#');

        byte r, g, b;
        try
        {
            switch (text.Length)
            {
                case 6:
                    r = byte.Parse(text[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    g = byte.Parse(text[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    b = byte.Parse(text[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    break;
                case 8:
                    // #AARRGGBB: el alfa de los tokens se respeta tal cual.
                    r = byte.Parse(text[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    g = byte.Parse(text[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    b = byte.Parse(text[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    break;
                default:
                    return DependencyProperty.UnsetValue;
            }
        }
        catch (FormatException)
        {
            return DependencyProperty.UnsetValue;
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}
