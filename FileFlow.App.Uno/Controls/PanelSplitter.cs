using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El ASA que redimensiona una columna del marco (hito 270): el cajón de nodos y la ficha del inspector del
/// editor del host Uno tenían un ancho fijo —el cajón en su columna y la ficha en <c>Width="300"</c>— y el
/// usuario no podía dar más sitio al lienzo ni a la ficha.
///
/// <para><b>Por qué una pieza y no un <c>GridSplitter</c></b>: el <c>GridSplitter</c> de WPF/UWP no existe en
/// WinUI 3 —vive en el paquete de la comunidad, que este host no referencia—, así que el asa se escribe aquí:
/// cinco píxeles de ancho, el puntero capturado durante el arrastre y el ancho aplicado a la
/// <see cref="ColumnDefinition"/> con el mismo reparto que la versión anterior declara en su
/// <c>MainWindow.axaml</c> (cajón <b>180–480</b>, ficha <b>220–750</b>, y el lienzo con su mínimo). Sobre
/// <see cref="Grid"/> y no sobre <see cref="Border"/> —que en WinUI 3 está <b>sellado</b>, medido al compilar
/// este tramo—: lo que el asa necesita del árbol es un <c>Background</c> que la haga opaca al puntero, y un
/// panel lo trae.</para>
///
/// <para><b>La cuenta vive en un solo sitio</b> (<see cref="Resolve"/>): el tope no es sólo el máximo
/// declarado, sino el que deja al LIENZO su ancho mínimo. Sin esa segunda cota, arrastrar el asa hacia el
/// centro del marco se come el lienzo y —como el área de clic de las tarjetas se mide del árbol visual— el
/// usuario acaba con un editor del que no puede seleccionar nada. La sonda del selfcheck ejercita el mismo
/// camino y vuelve a medir el área de clic después de redimensionar.</para>
/// </summary>
public sealed class PanelSplitter : Grid
{
    private ColumnDefinition? _column;
    private ColumnDefinition? _canvas;
    private double _min;
    private double _max;
    private bool _widensToTheRight;
    private double _dragStartX;
    private double _dragStartWidth;

    public PanelSplitter()
    {
        Width = 5;
        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CanvasSurfaceBrush"];
        ManipulationMode = ManipulationModes.None;

        PointerPressed += OnSplitterPressed;
        PointerMoved += OnSplitterMoved;
        PointerReleased += OnSplitterReleased;
        PointerCaptureLost += OnSplitterReleased;
        PointerEntered += (_, _) => Background = AccentBrush();
        PointerExited += (_, _) => Background = IdleBrush();
    }

    /// <summary>
    /// Ata el asa a su columna: el ancho que gobierna, sus dos cotas y hacia dónde CRECE.
    /// </summary>
    /// <param name="column">La columna que el arrastre redimensiona.</param>
    /// <param name="min">El ancho mínimo de esa columna (el de la versión anterior).</param>
    /// <param name="max">El ancho máximo que puede pedir el usuario.</param>
    /// <param name="widensToTheRight">
    /// ¿Su columna está a la IZQUIERDA del asa? El del cajón crece al arrastrar hacia la derecha; el de la
    /// ficha, que está a la derecha del suyo, crece hacia la izquierda.
    /// </param>
    /// <param name="canvas">La columna del lienzo: su mínimo es el segundo tope del arrastre.</param>
    public void Attach(ColumnDefinition column, double min, double max, bool widensToTheRight, ColumnDefinition canvas)
    {
        _column = column ?? throw new ArgumentNullException(nameof(column));
        _canvas = canvas;
        _min = min;
        _max = max;
        _widensToTheRight = widensToTheRight;
        _column.MinWidth = min;
        _column.MaxWidth = max;
        _column.Width = new GridLength(Math.Clamp(_column.Width.Value, min, max), GridUnitType.Pixel);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }

    /// <summary>El ancho que el asa ha fijado (el que hay que reponer cuando la columna vuelve a estar).</summary>
    internal double RememberedWidth => _column is { } column && column.ActualWidth > 0
        ? column.ActualWidth
        : _min;

    /// <summary>
    /// El ancho resultante de un arrastre de <paramref name="delta"/> píxeles: la cuenta ÚNICA del asa, con las
    /// dos cotas —el máximo declarado y el que deja al lienzo su mínimo— y el suelo del mínimo.
    /// </summary>
    internal static double Resolve(double startWidth, double delta, double min, double max, double room)
    {
        double ceiling = Math.Max(min, Math.Min(max, room));
        return Math.Clamp(startWidth + delta, min, ceiling);
    }

    /// <summary>El espacio que puede crecer esta columna sin comerse el mínimo del lienzo.</summary>
    private double Room()
    {
        if (_column is null)
        {
            return double.PositiveInfinity;
        }

        double canvasRoom = _canvas is { } canvas
            ? Math.Max(0, canvas.ActualWidth - canvas.MinWidth)
            : double.PositiveInfinity;

        return _column.ActualWidth + canvasRoom;
    }

    /// <summary>El mismo camino que el arrastre, sin puntero: lo usa la sonda del selfcheck.</summary>
    internal double DragBy(double delta)
    {
        if (_column is null)
        {
            return 0;
        }

        double width = Resolve(_column.ActualWidth, delta, _min, _max, Room());
        _column.Width = new GridLength(width, GridUnitType.Pixel);
        return width;
    }

    private double PointerX(PointerRoutedEventArgs e) =>
        Parent is UIElement parent ? e.GetCurrentPoint(parent).Position.X : e.GetCurrentPoint(null).Position.X;

    private void OnSplitterPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_column is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragStartX = PointerX(e);
        _dragStartWidth = _column.ActualWidth;
        Background = AccentBrush();
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnSplitterMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_column is null || e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        // El arrastre se mide contra el PUNTO DE PARTIDA del gesto (el asa se mueve con la columna, así que su
        // propio sistema de coordenadas no sirve como referencia) y el ancho sale de la misma cuenta que la
        // sonda ejercita.
        double delta = PointerX(e) - _dragStartX;
        double width = Resolve(
            _dragStartWidth, _widensToTheRight ? delta : -delta, _min, _max, Room());
        _column.Width = new GridLength(width, GridUnitType.Pixel);
        e.Handled = true;
    }

    private void OnSplitterReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_column is null)
        {
            return;
        }

        Background = IdleBrush();
        ReleasePointerCapture(e.Pointer);
    }

    private static Microsoft.UI.Xaml.Media.Brush IdleBrush() =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CanvasSurfaceBrush"];

    private static Microsoft.UI.Xaml.Media.Brush AccentBrush() =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CanvasAccentPrimaryBrush"];
}
