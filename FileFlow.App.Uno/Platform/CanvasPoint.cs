using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// El marcador de la proyección de geometría en el lienzo del host Uno: los elementos que reciben puntos
/// del núcleo llevan <c>CanvasPoint.Converter</c> en cada enlace de posición (<c>Canvas.Left</c>,
/// <c>Canvas.Top</c>, transformadas), y esa propiedad adjunta es lo que la guardia del hito 217 —ampliada
/// en la fase 3.1— censura en el XAML: un enlace de geometría sin este marcador sale rojo en el árbol.
///
/// <para>La instancia vive en los recursos del control (<c>conv:UnoPointConverter.Instance</c> vía
/// <c>StaticResource</c>) porque el compilador XAML de WinUI resuelve <c>{StaticResource}</c> en tiempo de
/// parseo, mientras que el <c>{x:Bind}</c> por reflexión de los converters no admite instancias estáticas
/// citadas por tipo.</para>
/// </summary>
public static class CanvasPoint
{
    /// <summary>El conversor de puntos que cada enlace de geometría del lienzo debe llevar.</summary>
    public static readonly DependencyProperty ConverterProperty =
        DependencyProperty.RegisterAttached(
            "Converter",
            typeof(IValueConverter),
            typeof(CanvasPoint),
            new PropertyMetadata(UnoPointConverter.Instance));

    public static IValueConverter GetConverter(DependencyObject obj) =>
        (IValueConverter)obj.GetValue(ConverterProperty);

    public static void SetConverter(DependencyObject obj, IValueConverter value) =>
        obj.SetValue(ConverterProperty, value);
}
