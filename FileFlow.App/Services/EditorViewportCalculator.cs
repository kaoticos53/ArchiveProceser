using System.Windows;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Services;

/// <summary>
/// Calculador de geometría de encuadre (FitToScreen) y zoom para el lienzo de Nodify.
/// </summary>
public static class EditorViewportCalculator
{
    public static (double Zoom, Point Location) CalculateFitToScreen(IReadOnlyCollection<NodeViewModel> nodes)
    {
        if (nodes.Count == 0)
        {
            return (1.0, new Point(0, 0));
        }

        double minX = nodes.Min(n => n.Location.X);
        double minY = nodes.Min(n => n.Location.Y);
        double maxX = nodes.Max(n => n.Location.X + (n.Width > 0 ? n.Width : 280));
        double maxY = nodes.Max(n => n.Location.Y + 220);

        double graphWidth = Math.Max(maxX - minX, 100);
        double graphHeight = Math.Max(maxY - minY, 100);

        const double viewWidth = 900;
        const double viewHeight = 500;

        const double paddingX = 120;
        const double paddingY = 120;

        double scaleX = (viewWidth - paddingX) / graphWidth;
        double scaleY = (viewHeight - paddingY) / graphHeight;

        double targetZoom = Math.Clamp(Math.Min(scaleX, scaleY), 0.3, 1.8);
        double finalZoom = Math.Round(targetZoom, 2);

        double visibleCanvasWidth = viewWidth / finalZoom;
        double visibleCanvasHeight = viewHeight / finalZoom;

        double extraCanvasX = Math.Max(50, (visibleCanvasWidth - graphWidth) / 2.0);
        double extraCanvasY = Math.Max(50, (visibleCanvasHeight - graphHeight) / 2.0);

        double locX = minX - extraCanvasX;
        double locY = minY - extraCanvasY;

        return (finalZoom, new Point(Math.Round(locX, 1), Math.Round(locY, 1)));
    }
}
