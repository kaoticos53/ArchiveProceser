using System;
using FileFlow.Sdk;

namespace FileFlow.App.Services;

/// <summary>
/// La proyección de puntos del <b>host Uno</b>, en su mitad portable: los ViewModels del grafo hablan en
/// <see cref="Point"/> (Sdk) y WinUI/Uno habla en <c>Windows.Foundation.Point</c> — y el motor de enlaces
/// de WinUI no convierte entre tipos distintos de punto, igual que el de la interfaz original (la lección del hito 211).
///
/// <para><b>Por qué la mitad portable existe antes que el XAML</b>: la fase 3.1 del plan Uno aún no ha
/// escrito ningún enlace de geometría, y la regla del plan exige que <b>ninguno</b> pueda existir sin
/// conversor. La guardia <c>UnoGeometryBindingGuardTests</c> censura el XAML del host desde ahora: un
/// enlace de geometría sin el conversor sale rojo en el árbol, no como lienzo vacío en el producto.</para>
///
/// <para>Es la hermana de <see cref="EditorViewportCalculator"/> y <see cref="ConnectionGeometry"/>:
/// matemática pura del núcleo, con el host poniendo sólo el envoltorio del framework
/// (<c>UnoPointConverter</c> en FileFlow.App.Uno, el IValueConverter que llama aquí).</para>
/// </summary>
public static class UnoPointProjection
{
    /// <summary>
    /// Proyecta un punto del Sdk al par (X, Y) que consume WinUI/Uno. El host devuelve la estructura
    /// nativa; el núcleo no conoce los tipos de ventana de ninguna plataforma.
    /// </summary>
    public static (double X, double Y) ToUno(Point point) => (point.X, point.Y);

    /// <summary>El viaje de vuelta: lo que los eventos de manipulación y el arrastre reportan al núcleo.</summary>
    public static Point ToSdk(double x, double y) => new(x, y);
}
