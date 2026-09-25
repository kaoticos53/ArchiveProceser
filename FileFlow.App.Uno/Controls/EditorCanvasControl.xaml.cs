using System;
using System.Collections.Specialized;
using System.Linq;
using FileFlow.App.Services;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El lienzo del editor en el host Uno (fase 3.1 del plan): tarjetas en modo lectura posicionadas por
/// <see cref="NodeViewModel.Location"/> —proyectada con <see cref="UnoPointConverter"/>, la regla del 217—,
/// cables estáticos dibujados con <see cref="ConnectionGeometry"/> (la Bézier del núcleo, la misma que pinta
/// Nodify en el escritorio) y pan/zoom/acotar.
///
/// <para><b>Por qué la posición va por código y no por Setter con Binding</b>: el motor XAML de WinUI no
/// evalúa enlaces dentro de <c>Setter.Value</c> (silenciosamente no hacen nada), así que el aplicador
/// <see cref="ApplyNodePosition"/> lee la posición proyectada del adaptador y la escribe en el Canvas —
/// pasando por el conversor, que es lo que la guardia de geometría censura en este fichero.</para>
///
/// <para><b>Modo lectura</b>: sin selección, arrastre ni puertos vivos (fases 3.2/3.3).</para>
/// </summary>
public sealed partial class EditorCanvasControl : UserControl
{
    /// <summary>El paso del grid, el doble del lienzo de Avalonia dibujado sutil para no robar contraste.</summary>
    private const double GridStep = 50.0;

    private const double MinZoom = 0.2;
    private const double MaxZoom = 2.5;

    private EditorViewModel? _editor;
    private bool _isPanning;
    private Windows.Foundation.Point _panStart;

    public EditorCanvasControl()
    {
        InitializeComponent();
        DrawBackgroundGrid();
    }

    /// <summary>El ViewModel del editor que el lienzo pinta (sólo lectura en esta fase).</summary>
    public EditorViewModel? Editor
    {
        get => _editor;
        set
        {
            if (ReferenceEquals(_editor, value))
            {
                return;
            }

            if (_editor is not null)
            {
                ((INotifyCollectionChanged)_editor.Nodes).CollectionChanged -= OnNodesChanged;
            }

            _editor = value;

            if (_editor is not null)
            {
                ((INotifyCollectionChanged)_editor.Nodes).CollectionChanged += OnNodesChanged;
            }

            Rebuild();
        }
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        var cards = _editor?.Nodes.Select(node => new NodeCardViewModel(node)).ToList();
        NodesHost.ItemsSource = cards;
        DrawWires();
        ApplyAllNodePositions();
    }

    /// <summary>
    /// Aplica la posición proyectada de un nodo al Canvas. La lectura pasa por
    /// <see cref="NodeCardViewModel.Position"/>, que viene del <see cref="UnoPointConverter"/> — la
    /// proyección explícita que la guardia de geometría exige en cada enlace equivalente.
    /// </summary>
    private void ApplyNodePosition(ContentPresenter container, NodeCardViewModel card)
    {
        Canvas.SetLeft(container, card.Position.X);
        Canvas.SetTop(container, card.Position.Y);
    }

    private void ApplyAllNodePositions()
    {
        int count = Math.Min(VisualTreeHelper.GetChildrenCount(NodesHost), 1);
        if (count == 0)
        {
            return;
        }

        if (VisualTreeHelper.GetChild(NodesHost, 0) is not ItemsPresenter presenter)
        {
            return;
        }

        RecurseContainers(presenter);
    }

    private void RecurseContainers(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is ContentPresenter container && container.Content is NodeCardViewModel card)
            {
                ApplyNodePosition(container, card);
                container.LayoutUpdated += (_, _) => ApplyNodePosition(container, card);
            }

            RecurseContainers(child);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Los cables: ConnectionGeometry, la misma curva que pinta Nodify en el escritorio
    // ─────────────────────────────────────────────────────────────────────────────

    private void DrawWires()
    {
        WireLayer.Children.Clear();

        if (_editor is null)
        {
            return;
        }

        foreach (var connection in _editor.Connections)
        {
            // Los puertos aún no tienen ancla write-back (fase 3.3): el cable sale del centro-derecha de la
            // tarjeta de origen y llega al centro-izquierda de la de destino, con la ubicación del grafo.
            var sourceNode = connection.Source.NodeOwner;
            var targetNode = connection.Target.NodeOwner;

            var source = new Sdk.Point(
                sourceNode.Location.X + sourceNode.Width,
                sourceNode.Location.Y + 40);
            var target = new Sdk.Point(
                targetNode.Location.X,
                targetNode.Location.Y + 40);

            var curve = ConnectionGeometry.BezierControlPoints(source, target, ConnectionGeometry.DefaultSpacing);

            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = (Brush)Resources["CanvasWireBrush"],
                StrokeThickness = 3.5,
                Data = CreateBezierGeometry(curve)
            };

            WireLayer.Children.Add(path);
        }
    }

    private static Geometry CreateBezierGeometry(ConnectionGeometry.CubicBezier curve)
    {
        var geometry = new PathGeometry
        {
            Figures =
            {
                new PathFigure
                {
                    StartPoint = ToWindowsPoint(curve.P0),
                    IsFilled = false,
                    Segments =
                    {
                        new BezierSegment
                        {
                            Point1 = ToWindowsPoint(curve.P1),
                            Point2 = ToWindowsPoint(curve.P2),
                            Point3 = ToWindowsPoint(curve.P3)
                        }
                    }
                }
            }
        };

        return geometry;
    }

    /// <summary>
    /// La proyección del cable: <see cref="UnoPointProjection.ToUno"/> (núcleo portable), el mismo corazón
    /// que <see cref="UnoPointConverter"/> — la geometría del cable se calcula en espacio de grafo
    /// (<see cref="Sdk.Point"/>) y sólo se proyecta al dibujar, como en el escritorio.
    /// </summary>
    private static Windows.Foundation.Point ToWindowsPoint(Sdk.Point point)
    {
        (double x, double y) = UnoPointProjection.ToUno(point);
        return new Windows.Foundation.Point(x, y);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Pan, zoom y encuadre
    // ─────────────────────────────────────────────────────────────────────────────

    private void OnCanvasPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement element && element is not Panel)
        {
            return; // Los clics sobre tarjetas (fase 3.2) no arrastran el lienzo.
        }

        _isPanning = true;
        _panStart = e.GetCurrentPoint(CanvasPlane).Position;
        ((FrameworkElement)sender).CapturePointer(e.Pointer);
    }

    private void OnCanvasMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var current = e.GetCurrentPoint(CanvasPlane).Position;
        CanvasTransform.TranslateX += current.X - _panStart.X;
        CanvasTransform.TranslateY += current.Y - _panStart.Y;
        _panStart = current;
    }

    private void OnCanvasReleased(object sender, PointerRoutedEventArgs e)
    {
        _isPanning = false;
        ((FrameworkElement)sender).ReleasePointerCapture(e.Pointer);
    }

    private void OnWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(RootGrid).Properties.MouseWheelDelta;
        ZoomBy(delta > 0 ? 1.1 : 1 / 1.1);
    }

    private void OnZoomIn(object sender, RoutedEventArgs e) => ZoomBy(1.1);

    private void OnZoomOut(object sender, RoutedEventArgs e) => ZoomBy(1 / 1.1);

    private void ZoomBy(double factor)
    {
        double zoom = Math.Clamp(CanvasTransform.ScaleX * factor, MinZoom, MaxZoom);
        CanvasTransform.ScaleX = zoom;
        CanvasTransform.ScaleY = zoom;
        ZoomText.Text = $"{Math.Round(zoom * 100)} %";
    }

    private void OnFitToScreen(object sender, RoutedEventArgs e)
    {
        if (_editor is null || _editor.Nodes.Count == 0)
        {
            return;
        }

        // El mismo calculador del núcleo que usa el escritorio: un solo «ajustar a pantalla» para los dos hosts.
        var (zoom, location) = EditorViewportCalculator.CalculateFitToScreen(_editor.Nodes);

        CanvasTransform.ScaleX = zoom;
        CanvasTransform.ScaleY = zoom;
        CanvasTransform.TranslateX = -location.X * zoom;
        CanvasTransform.TranslateY = -location.Y * zoom;
        ZoomText.Text = $"{Math.Round(zoom * 100)} %";
    }

    private void DrawBackgroundGrid()
    {
        for (double x = 0; x <= 2400; x += GridStep)
        {
            GridLayer.Children.Add(new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = 2400,
                Stroke = (Brush)Resources["CanvasGridBrush"],
                StrokeThickness = x % (GridStep * 2) == 0 ? 1 : 0.5
            });
        }

        for (double y = 0; y <= 2400; y += GridStep)
        {
            GridLayer.Children.Add(new Line
            {
                X1 = 0, Y1 = y, X2 = 2400, Y2 = y,
                Stroke = (Brush)Resources["CanvasGridBrush"],
                StrokeThickness = y % (GridStep * 2) == 0 ? 1 : 0.5
            });
        }
    }
}
