using System;
using FileFlow.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// El conversor de puntos del host Uno: los enlaces de geometría del lienzo (posiciones de contenedores,
/// anclas de sockets, viewport, extremos de cables) pasan por aquí, como los del host Avalonia pasan por
/// <c>SdkPointConverter</c>. La traducción vive en <see cref="UnoPointProjection"/> (núcleo portable); este
/// envoltorio es lo que el XAML de WinUI puede citar con <c>{x:Static}</c>-equivalente.
///
/// <para><b>La regla que ya está vigilada</b>: <c>UnoGeometryBindingGuardTests</c> censura el XAML del host
/// y falla si algún enlace de geometría no lleva este conversor. La lección del hito 211 se aplica antes
/// de que el primer enlace exista, no después de encontrar el lienzo vacío.</para>
/// </summary>
public sealed class UnoPointConverter : IValueConverter
{
    public static UnoPointConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is Sdk.Point point ? UnoPointProjection.ToUno(point) : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Windows.Foundation.Point point ? UnoPointProjection.ToSdk(point.X, point.Y) : DependencyProperty.UnsetValue;
}
