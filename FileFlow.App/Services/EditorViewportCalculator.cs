using Avalonia;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Services;

/// <summary>
/// Calculador de geometría de encuadre (FitToScreen) y zoom para el lienzo de Nodify.
/// </summary>
public static class EditorViewportCalculator
{
    /// <summary>
    /// El tamaño de vista con el que se calcula el encuadre. No es la ventana real —el lienzo no la conoce
    /// cuando sólo quiere centrar algo— sino el encuadre de referencia que ya usaba el «ajustar a pantalla»:
    /// centrar y ajustar tienen que hablar del mismo lienzo.
    /// </summary>
    private const double ViewWidth = 900;
    private const double ViewHeight = 500;

    /// <summary>Alto de referencia de una tarjeta sin medida propia, el mismo que supone el ajuste a pantalla.</summary>
    private const double FallbackNodeHeight = 220;

    /// <summary>
    /// Dónde hay que poner el encuadre para que un nodo quede centrado, <b>al zoom que ya tiene</b> el lienzo:
    /// llevar al usuario hasta un nodo no es motivo para cambiarle el zoom con el que estaba trabajando.
    /// </summary>
    public static Point CenterOn(NodeViewModel node, double zoom)
    {
        ArgumentNullException.ThrowIfNull(node);

        double effectiveZoom = zoom > 0 ? zoom : 1.0;
        double width = node.Width > 0 ? node.Width : 280;

        double centerX = node.Location.X + (width / 2.0);
        double centerY = node.Location.Y + (FallbackNodeHeight / 2.0);

        return new Point(
            Math.Round(centerX - (ViewWidth / (2.0 * effectiveZoom)), 1),
            Math.Round(centerY - (ViewHeight / (2.0 * effectiveZoom)), 1));
    }

    public static (double Zoom, Point Location) CalculateFitToScreen(IReadOnlyCollection<NodeViewModel> nodes)
    {
        if (nodes.Count == 0)
        {
            return (1.0, new Point(0, 0));
        }

        double minX = nodes.Min(n => n.Location.X);
        double minY = nodes.Min(n => n.Location.Y);
        double maxX = nodes.Max(n => n.Location.X + (n.Width > 0 ? n.Width : 280));
        double maxY = nodes.Max(n => n.Location.Y + FallbackNodeHeight);

        double graphWidth = Math.Max(maxX - minX, 100);
        double graphHeight = Math.Max(maxY - minY, 100);

        const double paddingX = 120;
        const double paddingY = 120;

        double scaleX = (ViewWidth - paddingX) / graphWidth;
        double scaleY = (ViewHeight - paddingY) / graphHeight;

        double targetZoom = Math.Clamp(Math.Min(scaleX, scaleY), 0.3, 1.8);
        double finalZoom = Math.Round(targetZoom, 2);

        double visibleCanvasWidth = ViewWidth / finalZoom;
        double visibleCanvasHeight = ViewHeight / finalZoom;

        double extraCanvasX = Math.Max(50, (visibleCanvasWidth - graphWidth) / 2.0);
        double extraCanvasY = Math.Max(50, (visibleCanvasHeight - graphHeight) / 2.0);

        double locX = minX - extraCanvasX;
        double locY = minY - extraCanvasY;

        return (finalZoom, new Point(Math.Round(locX, 1), Math.Round(locY, 1)));
    }
}
