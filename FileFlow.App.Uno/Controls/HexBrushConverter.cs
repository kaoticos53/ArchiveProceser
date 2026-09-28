using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// Convierte el hex de un token de tema (<c>#RRGGBB</c> o <c>#AARRGGBB</c>) en un pincel, para la MUESTRA
/// de una fila de color del Estudio de Temas.
///
/// <para>Es infraestructura de VISTA del host, no una segunda fuente de verdad: el valor lo sigue teniendo el
/// tema del núcleo (la fila lo lee con <c>SelectedColorHex</c>) y esto sólo lo pinta. Un valor ilegible no
/// revienta la superficie: devuelve un pincel transparente, de modo que una fila rara se ve vacía en vez de
/// tumbar la ventana.</para>
/// </summary>
public sealed partial class HexBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Transparent = new(Color.FromArgb(0, 0, 0, 0));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string text)
        {
            return Transparent;
        }

        string hex = text.Trim().TrimStart('#');
        if (hex.Length is not (6 or 8)
            || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packed))
        {
            return Transparent;
        }

        Color color = hex.Length == 8
            ? Color.FromArgb(
                (byte)(packed >> 24), (byte)(packed >> 16), (byte)(packed >> 8), (byte)packed)
            : Color.FromArgb(0xFF, (byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException("La muestra sólo pinta: el valor lo escribe el campo de la fila.");
}
