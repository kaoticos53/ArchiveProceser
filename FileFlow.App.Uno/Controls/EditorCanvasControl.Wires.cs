using System;
using System.Linq;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// La MITAD DE LOS CABLES del lienzo: la otra mitad de <see cref="EditorCanvasControl"/>, aparte del
/// pan/zoom y de las tarjetas.
///
/// <para><b>Por qué está separada</b>. El control tiene tres sujetos y este es el de los cables: el DIBUJO
/// de la capa (el cable que se ve y su DIANA —la misma Bézier con un trazo grueso e invisible, la que hace
/// pulsable lo que se ve—), el MENÚ de esa diana con la orden del núcleo, y la SONDA que mide las dos
/// mitades en la app viva. Van juntos porque se rompen juntos: quitar la diana deja un cable que se ve y no
/// se puede seleccionar, y quitar el resalte deja un cable marcado que nadie ve marcado. Quien toque un
/// cable no tiene que leer el fichero de las tarjetas, y quien lea las tarjetas no se tropieza con los
/// cables. El instrumento del inspector (<c>NodeInspectorPanel.Probes.cs</c>) es la misma convención.</para>
///
/// <para><b>Sin cambios en el traslado</b>: el código salió de <c>EditorCanvasControl.xaml.cs</c> tal cual
/// —las medidas van por los mismos métodos que los manejadores, y las sondas y mutaciones que lo miden
/// apuntan aquí desde este pase—.</para>
/// </summary>
public sealed partial class EditorCanvasControl
{
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
            // Fase 3.3: las anclas REALES (write-back por árbol visual) mandan; la estimación del centro
            // de tarjeta (Location.Y + 40) queda como respaldo si el árbol aún no materializó el socket.
            var source = AnchorOf(connection.Source)
                ?? new Sdk.Point(
                    connection.Source.NodeOwner.Location.X + connection.Source.NodeOwner.Width,
                    connection.Source.NodeOwner.Location.Y + 40);
            var target = AnchorOf(connection.Target)
                ?? new Sdk.Point(
                    connection.Target.NodeOwner.Location.X,
                    connection.Target.NodeOwner.Location.Y + 40);

            var wire = ConnectionGeometry.BuildWire(source, target);

            // El cable MARCADO se ve marcado: el trazo pasa al acento de selección y engorda (es el mismo
            // par que usa la tarjeta seleccionada, que se rodea con el acento). Sin esta mitad, el clic
            // marcaría un cable que se ve igual que los demás y el Supr borraría algo que nadie ve elegido.
            // La marca se pregunta al núcleo en cada trazado: el cable no lleva copia que pueda divergir.
            bool selected = _editor.SelectedConnections.Contains(connection);
            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = CanvasBrush(selected ? SelectedWireBrushKey : "CanvasWireBrush"),
                StrokeThickness = selected ? WireSelectedThickness : WireThickness,
                Data = CreateWireGeometry(wire)
            };
            AutomationProperties.SetAutomationId(path, WireAnchor);
            WireLayer.Children.Add(path);

            // La DIANA del cable: la MISMA Bézier con un trazo grueso e invisible. Sin ella el cable no
            // existe para el puntero (un trazo de 3,5 px no es una diana, y la capa no era alcanzable),
            // y el usuario que quería borrar la conexión no tenía sobre qué pulsar: el clic derecho caía
            // al fondo del lienzo y **paneaba**. Es la mitad que hace seleccionable lo que se ve.
            // La figura se construye DOS veces a propósito: una `Geometry` de WinUI no se puede compartir
            // entre dos `Path` —medido: la segunda asignación levanta `ArgumentException` y la capa se queda
            // dibujada a medias—, así que la diana traza su propia Bézier desde el MISMO `wire` del núcleo.
            var hit = new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                StrokeThickness = WireHitThickness,
                Data = CreateWireGeometry(wire)
            };
            AutomationProperties.SetAutomationId(hit, WireHitAnchor);
            AutomationProperties.SetName(hit, WireLabel(connection));

            // El botón derecho sobre el cable NO es un pan: el menú del cable manda (igual que en el
            // escritorio, donde el clic derecho abre el suyo antes que el desplazamiento del lienzo). El
            // IZQUIERDO marca el cable —y con Ctrl lo AÑADE a los que ya estén marcados—: eso es lo que hace
            // visible, y borrable con Supr, una conexión.
            hit.PointerPressed += (_, e) => OnWirePressed(connection, e);
            hit.RightTapped += (_, e) =>
            {
                ShowWireMenuAt(connection, e.GetPosition(RootGrid));
                e.Handled = true;
            };

            WireLayer.Children.Add(hit);
        }
    }

    /// <summary>
    /// Las anclas de TODOS los cables del grafo, medidas <b>una vez</b> —cuando arranca el rectángulo de
    /// selección—, porque decidir si un cable cae dentro del rectángulo no puede costar dos recorridos del árbol
    /// de sockets por cada movimiento del puntero.
    ///
    /// <para>Un cable sin sus DOS anclas no entra en el diccionario: sin medida, el rectángulo <b>no decide</b>
    /// sobre él (la estimación del centro de tarjeta es el respaldo del DIBUJO, no de una decisión de selección).</para>
    /// </summary>
    private Dictionary<ConnectionViewModel, (Sdk.Point Source, Sdk.Point Target)> MeasureWireAnchors()
    {
        var anchors = new Dictionary<ConnectionViewModel, (Sdk.Point Source, Sdk.Point Target)>();
        if (_editor is null)
        {
            return anchors;
        }

        foreach (var connection in _editor.Connections)
        {
            if (AnchorOf(connection.Source) is { } source && AnchorOf(connection.Target) is { } target)
            {
                anchors[connection] = (source, target);
            }
        }

        return anchors;
    }

    /// <summary>
    /// La figura del cable, a partir del trazado compartido (<see cref="ConnectionGeometry.WirePath"/>):
    /// <b>una sola Bézier</b>, del ancla de salida al ancla de destino, con los dos cuellos como puntos de
    /// control.
    ///
    /// <para><b>Los defectos que esto cierra</b>, los dos medidos con la sonda <c>ProbeWireTracking</c> y vistos
    /// por el usuario en la app: la primera versión abría la figura en el primer punto de control, así que el
    /// cable quedaba <b>separado del socket</b> y, con las anclas cerca, salía invertido (el rulo con forma de
    /// «2»); la segunda añadió los dos tramos rectos del trazo del control de Nodify —que existen porque allí la
    /// Bézier <i>sí</i> sale retirada— y el resultado se leía como una <b>Z</b>: dos bajíos rectos y una ese
    /// apretada en medio. Con la curva nacida en el ancla no hace falta ningún tramo recto: el cable sale del
    /// socket ya curvando.</para>
    /// </summary>
    private static Geometry CreateWireGeometry(ConnectionGeometry.WirePath wire)
    {
        var figure = new PathFigure
        {
            StartPoint = ToWindowsPoint(wire.Source),
            IsFilled = false,
            IsClosed = false
        };

        figure.Segments.Add(new BezierSegment
        {
            Point1 = ToWindowsPoint(wire.Exit),
            Point2 = ToWindowsPoint(wire.Arrival),
            Point3 = ToWindowsPoint(wire.Target)
        });

        return new PathGeometry { Figures = { figure } };
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

    /// <summary>El ancla UIA del cable dibujado: lo que cuenta la sonda de la capa de cables.</summary>
    internal const string WireAnchor = "CanvasWire";

    /// <summary>El ancla UIA de la DIANA del cable: la pieza que hace seleccionable lo que se ve.</summary>
    internal const string WireHitAnchor = "CanvasWireHit";

    /// <summary>
    /// El grosor de la diana del cable. Un trazo de 3,5 px no se puede pulsar, así que la diana es la MISMA
    /// Bézier con este trazo, invisible: el usuario acierta como acierta a un cable de verdad.
    /// </summary>
    private const double WireHitThickness = 14;

    /// <summary>El nombre con el que se anuncia el cable (lectores de pantalla y sondas): sus dos extremos.</summary>
    private static string WireLabel(ConnectionViewModel connection) =>
        $"{connection.Source.NodeOwner.Title}: {connection.Source.DisplayName} → "
        + $"{connection.Target.NodeOwner.Title}: {connection.Target.DisplayName}";

    /// <summary>
    /// El grosor del cable dibujado, y el del cable MARCADO. Los dos viven aquí para que la guardia y la
    /// sonda midan con la misma vara: el resalte tiene que verse, no sólo existir.
    /// </summary>
    private const double WireThickness = 3.5;

    /// <summary>El grosor del cable marcado (el resalte: el doble del trazo normal).</summary>
    private const double WireSelectedThickness = 6;

    /// <summary>El pincel del cable marcado: el mismo acento con el que la tarjeta seleccionada se rodea.</summary>
    internal const string SelectedWireBrushKey = "CanvasAccentPrimaryBrush";

    /// <summary>
    /// La pulsación del cable. El botón <b>derecho</b> no es el pan del lienzo: el menú del cable manda (el
    /// escritorio abre el suyo antes que el desplazamiento, por el mismo motivo) y se marca atendido aquí
    /// —el manejador del fondo no recibe lo ya atendido— y el menú se abre al soltar, que es el gesto de
    /// <c>RightTapped</c>. El <b>izquierdo</b> MARCA el cable: es la mitad que hace visible la selección, y
    /// de la que depende el Supr que la borra; para eso el clic tiene que entregarse el FOCO (hito 252) — el
    /// clic en el fondo lo hacía por su manejador, y el que se atiende aquí ya no llega allí.
    /// </summary>
    private void OnWirePressed(ConnectionViewModel connection, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
        {
            e.Handled = true;
            return;
        }

        // El clic izquierdo entrega el FOCO al lienzo por el mismo camino que el clic en el fondo (hito
        // 252): sin foco no llega ninguna tecla, y este clic existe para que Supr pueda borrar el cable
        // marcado. El botón DERECHO no reclama el teclado: su menú es el que manda, y la reclamación
        // —medida— devolvía el foco al lienzo y cerraba el menú recién abierto.
        if (FocusCanvasForShortcuts(e.OriginalSource as DependencyObject))
        {
            BeginKeyboardOwnership();
        }

        // La REGLA, la misma que en las tarjetas: pulsar reemplaza; Ctrl añade (el modificador lo lee el
        // lienzo, porque es del teclado, y la decisión la toma el núcleo).
        _editor?.SelectConnection(connection, add: IsKeyDown(Windows.System.VirtualKey.Control));
        e.Handled = true;
    }

    /// <summary>
    /// Abre el menú del cable DONDE ESTÁ EL PUNTERO. Es la mitad que el usuario reportó como defecto: anclado
    /// a la DIANA —cuyo rectángulo es TODO el cable, de un socket al otro— el menú salía en una **esquina del
    /// cable** (medido: **855 px** del cursor) en vez de bajo el ratón, que es donde el escritorio lo abre (allí
    /// es un <c>ContextMenu</c> de Avalonia, y ese sale en el puntero).
    ///
    /// <para>El punto va referido a <see cref="RootGrid"/> —el mismo espacio en el que el lienzo lee el puntero
    /// para todo lo demás— y no al elemento pulsado, para que el sitio del menú no dependa de por dónde cayó el
    /// clic dentro de la curva.</para>
    /// </summary>
    private void ShowWireMenuAt(ConnectionViewModel connection, Windows.Foundation.Point canvasPoint)
    {
        var flyout = BuildWireMenu(connection);
        flyout.ShowAt(RootGrid, new FlyoutShowOptions
        {
            Position = canvasPoint,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
        });
    }

    /// <summary>
    /// El menú del cable: UNA entrada, «Eliminar conexión», cuya orden es la del NÚCLEO
    /// (<see cref="ConnectionViewModel.DeleteCommand" />, la misma que cumple el menú del escritorio, con su
    /// undo). El host no borra nada por su cuenta: pide la orden y el grafo se entera.
    /// </summary>
    private static MenuFlyout BuildWireMenu(ConnectionViewModel connection)
    {
        var flyout = new MenuFlyout();
        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = LocalizationManager.Instance.GetString("Uno_Connection_Delete", "Eliminar conexión"),
            Command = connection.DeleteCommand
        });
        return flyout;
    }

    /// <summary>
    /// La sonda de la SELECCIÓN DEL CABLE (el defecto del usuario: «no puedo seleccionar las conexiones
    /// para borrarlas»): cada conexión del grafo tiene su diana en la capa de cables —la misma figura que el
    /// cable que se ve— y el menú que esa diana abre lleva la orden del NÚCLEO con su rótulo del diccionario.
    /// Sin las dos mitades, la conexión se ve y no se puede borrar.
    ///
    /// <para>Añade la MARCA visible: marcar el cable (lo que hace el clic izquierdo) cambia el trazo que se
    /// dibuja y el Supr de la tabla del núcleo lo borra, con el undo restaurando la medida. Una marca
    /// invisible sería un Supr que borra algo que nadie ve elegido.</para>
    ///
    /// <para>Añade la REGLA: pulsar reemplaza y Ctrl AÑADE a la marca, de modo que dos cables marcados se
    /// borran de una vez con un solo deshacer (el comando del núcleo los agrupa en una transacción).</para>
    /// </summary>
    internal (int Connections, int HitTargets, bool SameFigure, bool CoreOrder, string MenuText,
              bool MarkIsVisible, bool DeleteByShortcut, bool Restored) ProbeWireSelection()
    {
        if (_editor is null || _editor.Connections.Count == 0)
        {
            return (0, 0, false, false, string.Empty, false, false, false);
        }

        var hitTargets = WireLayer.Children
            .OfType<Microsoft.UI.Xaml.Shapes.Path>()
            .Where(p => AutomationProperties.GetAutomationId(p) == WireHitAnchor)
            .ToList();

        bool sameFigure = hitTargets.Count == _editor.Connections.Count
            && hitTargets.All(p => p.Data is not null && p.StrokeThickness >= WireHitThickness);

        var menu = BuildWireMenu(_editor.Connections[0]);
        bool coreOrder = menu.Items.Count == 1
            && menu.Items[0] is MenuFlyoutItem item
            && ReferenceEquals(item.Command, _editor.Connections[0].DeleteCommand);
        string menuText = menu.Items[0] is MenuFlyoutItem first ? first.Text : string.Empty;

        // La MARCA: marcar el cable (lo que hace el clic izquierdo) tiene que verse en la capa —el trazo
        // del acento y más grueso— y el Supr de la tabla del núcleo tiene que borrarlo. Las dos mitades se
        // miden por los MISMOS métodos que los handlers, y el cable vuelve con el undo de la sonda.
        int connectionsBefore = _editor.Connections.Count;
        _editor.SelectConnection(_editor.Connections[0]);
        bool markIsVisible = WireLayer.Children
            .OfType<Microsoft.UI.Xaml.Shapes.Path>()
            .Any(p => AutomationProperties.GetAutomationId(p) == WireAnchor
                   && p.StrokeThickness >= WireSelectedThickness
                   && ReferenceEquals(p.Stroke, Application.Current.Resources[SelectedWireBrushKey]));

        bool deleteByShortcut = EditorKeyboardShortcuts.Execute(
            EditorKeyboardShortcuts.ShortcutKey.Delete, _editor)
            && _editor.Connections.Count == connectionsBefore - 1;

        _editor.UndoRedoService.Undo();
        bool restored = _editor.Connections.Count == connectionsBefore;

        // La sonda deja el estado como lo encontró: sin cable marcado (el undo restaura el cable, no el
        // resalte) para no contaminar a las demás sondas que corren en el mismo informe.
        _editor.SelectConnection(null);

        return (_editor.Connections.Count, hitTargets.Count, sameFigure, coreOrder, menuText,
                markIsVisible, deleteByShortcut, restored);
    }
}
