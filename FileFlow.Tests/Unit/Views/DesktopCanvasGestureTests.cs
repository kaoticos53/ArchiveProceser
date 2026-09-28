using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views;
using FileFlow.App.Views.Components;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;
using AvaloniaPoint = Avalonia.Point;
using SdkPoint = FileFlow.Sdk.Point;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// El <b>gesto real</b> sobre el lienzo del escritorio: el plano se mueve con el botón derecho, se acerca y se
/// aleja con la rueda, y la tarjeta se arrastra con el izquierdo — con puntero inyectado por el pipeline de
/// entrada de la sesión headless, no llamando al ViewModel.
///
/// <para><b>Por qué existe</b>: la medición del hito 266 dejó declarado que el escritorio no tenía sonda propia
/// y que el trazo del cable se había medido sólo en la <i>figura</i> que el control va a pintar, nunca con un
/// gesto encima. Y esa frontera tapa una clase de defecto entera: todo lo medido hasta ahora iba en unidades
/// del GRAFO, así que un defecto que sólo aparece al proyectar el dibujo al <b>espacio de la ventana</b> —el
/// ancla medida ignorando la escala de la cadena, que es el defecto que el host Uno midió en el hito 254— no
/// tenía nada que lo cazara aquí.</para>
///
/// <para><b>Qué se afirma</b>: la distancia entre el extremo del trazo dibujado y el <b>punto dibujado del
/// socket</b>, medida con <c>TranslatePoint</c> sobre el árbol visual real (shape → lienzo → transformación del
/// encuadre → ventana), antes y después de cada gesto. No «el enlace tiene valor», sino «el cable entra y sale
/// por donde el usuario ve el socket» con el plano quieto, paneado, acercado y alejado.</para>
///
/// <para><b>Colección exclusiva</b>: abre ventanas y entra por el pipeline de entrada de la sesión headless,
/// que las capturas de otras clases están usando.</para>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class DesktopCanvasGestureTests
{
    /// <summary>Tolerancia del aterrizaje, en píxeles de ventana: medio píxel es redondeo, no un defecto.</summary>
    private const double LandingTolerance = 0.5;

    // ─────────────────────────────────────────────────────────────────────────────
    // El trazo, con el plano quieto
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheCable_ShouldTouchItsSockets_InWindowSpace()
    {
        WithCanvas((window, view, editor) =>
        {
            var landing = Measure(window, view, editor);

            landing.SourceGap.Should().BeLessThan(LandingTolerance, landing.Describe("en reposo"));
            landing.TargetGap.Should().BeLessThan(LandingTolerance, landing.Describe("en reposo"));

            // Y los dos extremos del trazo son puntos DISTINTOS (el cable no es un punto): si el lienzo pintara
            // una figura degenerada, el aterrizaje saldría perfecto sin haber dibujado nada.
            Gap(landing.StrokeStart, landing.StrokeEnd).Should().BeGreaterThan(50,
                landing.Describe("en reposo"));
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El pan con el botón derecho
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheRightDragOnTheCanvas_ShouldPanThePlane_AndTheCableShouldFollowItsSockets()
    {
        WithCanvas((window, view, editor) =>
        {
            var before = Measure(window, view, editor);
            var locationBefore = editor.ViewportLocation;
            var zoom = editor.ViewportZoom;
            var delta = new Vector(120, 80);

            RightDragOnTheCanvas(window, view, delta);

            var locationAfter = editor.ViewportLocation;
            var moved = new Vector(locationAfter.X - locationBefore.X, locationAfter.Y - locationBefore.Y);

            // El plano se mueve en unidades del GRAFO y el gesto va en píxeles de ventana: el desplazamiento es
            // el del puntero dividido por el zoom. El signo tampoco es un detalle — arrastrar hacia la derecha
            // descubre lo que hay a la izquierda, así que el encuadre retrocede.
            moved.X.Should().BeApproximately(-delta.X / zoom, 0.75,
                $"arrastrar el fondo {delta.X} px a la derecha con zoom {zoom} lleva el encuadre {delta.X / zoom} unidades atrás (medido: {moved.X})");
            moved.Y.Should().BeApproximately(-delta.Y / zoom, 0.75,
                $"lo mismo en vertical (medido: {moved.Y})");

            // Y lo que de verdad importa: el trazo sigue entrando y saliendo por sus sockets con el plano movido.
            var after = Measure(window, view, editor);
            after.SourceGap.Should().BeLessThan(LandingTolerance, after.Describe("tras el pan"));
            after.TargetGap.Should().BeLessThan(LandingTolerance, after.Describe("tras el pan"));

            // Y el contenido sigue a la mano: el trazo se desplaza en la ventana los mismos píxeles del gesto
            // (al zoom 1, uno a uno), en el sentido en que se arrastró — no en el del encuadre, que va al revés.
            var strokeShift = after.StrokeStart - before.StrokeStart;
            strokeShift.X.Should().BeApproximately(delta.X / zoom, 1.5,
                $"el extremo dibujado sigue a la mano {delta.X} px (medido: {strokeShift.X})");
            strokeShift.Y.Should().BeApproximately(delta.Y / zoom, 1.5,
                $"lo mismo en vertical (medido: {strokeShift.Y})");
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El zoom con la rueda
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheWheelOverTheCanvas_ShouldZoom_AndTheCableShouldStillTouchItsSockets()
    {
        WithCanvas((window, view, editor) =>
        {
            double zoomBefore = editor.ViewportZoom;

            WheelOverTheCanvas(window, view, 1);

            double zoomedIn = editor.ViewportZoom;
            zoomedIn.Should().BeGreaterThan(zoomBefore,
                $"la rueda hacia arriba acerca el plano (zoom {zoomBefore} → {zoomedIn})");

            var atZoomIn = Measure(window, view, editor);
            atZoomIn.SourceGap.Should().BeLessThan(LandingTolerance, atZoomIn.Describe("acercado"));
            atZoomIn.TargetGap.Should().BeLessThan(LandingTolerance, atZoomIn.Describe("acercado"));

            // Hacia el otro lado: alejar. Es la mitad que descubre el ancla medida ignorando la escala.
            WheelOverTheCanvas(window, view, -2);

            double zoomedOut = editor.ViewportZoom;
            zoomedOut.Should().BeLessThan(zoomedIn,
                $"la rueda hacia abajo aleja el plano (zoom {zoomedIn} → {zoomedOut})");

            var atZoomOut = Measure(window, view, editor);
            atZoomOut.SourceGap.Should().BeLessThan(LandingTolerance, atZoomOut.Describe("alejado"));
            atZoomOut.TargetGap.Should().BeLessThan(LandingTolerance, atZoomOut.Describe("alejado"));

            // El ancla vive en el espacio del GRAFO: al cambiar el zoom, el mismo punto cae en otro píxel de la
            // ventana. Si el aterrizaje se midiera sin la escala de la cadena, este caso se caería — es la mitad
            // que el host Uno ganó en el hito 254.
            Gap(atZoomOut.StrokeStart, atZoomIn.StrokeStart).Should().BeGreaterThan(1,
                "cambiar el zoom mueve el trazo en la ventana: si no se moviera, no se estaría midiendo la proyección");

            // Y el techo: la rueda no puede pasar del máximo del editor, ni con la escala al tope el trazo
            // puede dejar de caer sobre el socket (es donde un ancla mal proyectada se separa más).
            for (int notch = 0; notch < 20; notch++)
            {
                WheelOverTheCanvas(window, view, 1);
            }

            double atCeiling = editor.ViewportZoom;
            atCeiling.Should().BeLessThanOrEqualTo(MaxZoom + 0.0001,
                $"el zoom del editor tiene techo (medido: {atCeiling})");
            atCeiling.Should().BeGreaterThan(2, $"las veinte muescas llevan el zoom al techo, no a un valor tibio (medido: {atCeiling})");

            var atTop = Measure(window, view, editor);
            atTop.SourceGap.Should().BeLessThan(LandingTolerance, atTop.Describe($"con el zoom al tope ({atCeiling:F2})"));
            atTop.TargetGap.Should().BeLessThan(LandingTolerance, atTop.Describe($"con el zoom al tope ({atCeiling:F2})"));
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El arrastre de una tarjeta (el gesto que NO es pan)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheLeftDragOnANodeCard_ShouldMoveTheNode_AndNotThePlane()
    {
        WithCanvas((window, view, editor) =>
        {
            var node = editor.Nodes.First();
            var locationBefore = node.Location;
            var viewportBefore = editor.ViewportLocation;
            var delta = new Vector(60, 40);
            double zoom = editor.ViewportZoom;

            LeftDragOnTheCard(window, view, node, delta);

            var moved = new Vector(node.Location.X - locationBefore.X, node.Location.Y - locationBefore.Y);

            moved.X.Should().BeApproximately(delta.X / zoom, 0.75,
                $"arrastrar la tarjeta {delta.X} px mueve el nodo {delta.X / zoom} unidades del grafo (medido: {moved.X})");
            moved.Y.Should().BeApproximately(delta.Y / zoom, 0.75,
                $"lo mismo en vertical (medido: {moved.Y})");

            editor.ViewportLocation.Should().Be(viewportBefore,
                "el botón IZQUIERDO mueve la tarjeta: si el paneo respondiera al izquierdo, arrastrar un nodo movería el plano entero");

            // Y el cable, al que se le movió un extremo, sigue tocando los dos sockets.
            var landing = Measure(window, view, editor);
            landing.SourceGap.Should().BeLessThan(LandingTolerance, landing.Describe("tras arrastrar la tarjeta"));
            landing.TargetGap.Should().BeLessThan(LandingTolerance, landing.Describe("tras arrastrar la tarjeta"));
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La medida: el aterrizaje del trazo sobre el punto dibujado del socket
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>El techo de zoom que declara la vista del editor (<c>MaxViewportZoom</c> en el XAML).</summary>
    private const double MaxZoom = 2.5;

    /// <summary>
    /// Lo medido, con los puntos crudos para que un rojo diga <b>dónde</b> quedó el trazo y no sólo que se
    /// movió. Los huecos son la distancia en píxeles de ventana entre el extremo del trazo y el centro del
    /// punto dibujado de su socket, cada uno pasado por su cadena de transformaciones real.
    /// </summary>
    private sealed record Landing(
        double SourceGap,
        double TargetGap,
        AvaloniaPoint StrokeStart,
        AvaloniaPoint StrokeEnd,
        AvaloniaPoint SourceSocket,
        AvaloniaPoint TargetSocket)
    {
        public string Describe(string when) =>
            $"{when}: el trazo va de {StrokeStart} a {StrokeEnd}; los puntos dibujados de sus sockets están en "
            + $"{SourceSocket} y {TargetSocket} (huecos: origen {SourceGap:F2} px, destino {TargetGap:F2} px)";
    }

    private static Landing Measure(Window window, EditorView view, EditorViewModel editor)
    {
        var connection = editor.Connections.Single();

        var wire = WireOf(view, connection);
        wire.Should().NotBeNull("el cable del grafo debe estar dibujado en el lienzo");

        var figure = wire!.DefiningGeometry.Should().BeOfType<PathGeometry>().Subject.Figures!.Single();
        var curve = figure.Segments!.Single().Should().BeOfType<BezierSegment>().Subject;

        var source = SocketOf(view, connection.Source, isInput: false);
        var target = SocketOf(view, connection.Target, isInput: true);
        source.Should().NotBeNull("el socket de salida debe estar materializado en el árbol visual");
        target.Should().NotBeNull("el socket de entrada debe estar materializado en el árbol visual");

        // El trazo se mide donde se PINTA (su figura va en unidades del grafo) y se proyecta al espacio de la
        // ventana con la cadena entera: shape → contenedor → lienzo → transformación del encuadre.
        AvaloniaPoint strokeStart = wire.TranslatePoint(figure.StartPoint, window)!.Value;
        AvaloniaPoint strokeEnd = wire.TranslatePoint(curve.Point3, window)!.Value;

        AvaloniaPoint sourceSocket = SocketDotCentre(source!, window);
        AvaloniaPoint targetSocket = SocketDotCentre(target!, window);

        return new Landing(
            Gap(strokeStart, sourceSocket),
            Gap(strokeEnd, targetSocket),
            strokeStart,
            strokeEnd,
            sourceSocket,
            targetSocket);
    }

    /// <summary>Distancia entre dos puntos, en píxeles (Avalonia la da sobre el vector, no sobre <c>Point</c>).</summary>
    private static double Gap(AvaloniaPoint a, AvaloniaPoint b) => new Vector(a.X - b.X, a.Y - b.Y).Length;

    /// <summary>
    /// El <b>punto dibujado</b> del socket: el centro de la figura de la plantilla de socket
    /// (<c>PortSocketTemplate</c>: la clase <c>socket</c> o el triángulo), que es lo que el usuario ve y donde
    /// Nodify sitúa el ancla del puerto (su centro coincide con el <c>Anchor</c> que el control escribe en el
    /// ViewModel). Medir contra el control del conector entero —que incluye la etiqueta del puerto— daba un
    /// hueco de 14,5 px que era de la etiqueta, no del cable.
    /// </summary>
    private static AvaloniaPoint SocketDotCentre(Visual connector, Visual window)
    {
        Control? dot = connector.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.Classes.Contains("socket") || control.Classes.Contains("socketTriangle"));

        return CentreOf(dot ?? connector, window);
    }

    /// <summary>Centro de un control en coordenadas de la ventana (el punto donde el usuario lo ve).</summary>
    private static AvaloniaPoint CentreOf(Visual control, Visual window) =>
        control.TranslatePoint(
            new AvaloniaPoint(control.Bounds.Width / 2, control.Bounds.Height / 2),
            window)!.Value;

    // ─────────────────────────────────────────────────────────────────────────────
    // Los gestos
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Arrastra el fondo del lienzo con el <b>botón derecho</b>: es el gesto que el editor mapea a paneo en su
    /// constructor estático. En cuatro pasos, como lo hace una mano: un salto único no ejercita el arrastre.
    /// </summary>
    private static void RightDragOnTheCanvas(Window window, EditorView view, Vector delta)
    {
        var start = FreeCanvasPoint(window, view);

        InputSimulator.MovePointer(window, start);
        window.MouseDown(start, MouseButton.Right);
        InputSimulator.Settle();

        for (int step = 1; step <= 4; step++)
        {
            InputSimulator.MovePointer(window, start + (delta * (step / 4.0)));
        }

        window.MouseUp(start + delta, MouseButton.Right);
        InputSimulator.Settle();
    }

    /// <summary>Gira la rueda sobre el fondo del lienzo: hacia arriba acerca, hacia abajo aleja.</summary>
    private static void WheelOverTheCanvas(Window window, EditorView view, int notches)
    {
        var point = FreeCanvasPoint(window, view);

        InputSimulator.MovePointer(window, point);
        window.MouseWheel(point, new Vector(0, notches * 120));
        InputSimulator.Settle();
    }

    /// <summary>
    /// Arrastra una tarjeta con el botón izquierdo desde un punto que <b>agarra</b> la tarjeta: sobre el lienzo
    /// hay mandos (botones, campos, previsualizaciones) que se quedan con la pulsación, así que el punto se
    /// busca por hit-testing en lugar de fijarlo en el centro de la tarjeta.
    /// </summary>
    private static void LeftDragOnTheCard(Window window, EditorView view, NodeViewModel node, Vector delta)
    {
        var canvas = CanvasOf(view);
        var container = ContainerOf(canvas, node);
        var start = DragGrip(window, canvas, node);

        InputSimulator.MovePointer(window, start);
        window.MouseDown(start, MouseButton.Left);
        InputSimulator.Settle();

        for (int step = 1; step <= 4; step++)
        {
            InputSimulator.MovePointer(window, start + (delta * (step / 4.0)));
        }

        window.MouseUp(start + delta, MouseButton.Left);
        InputSimulator.Settle();

        container.GetVisualParent().Should().NotBeNull();
    }

    /// <summary>Un punto del fondo libre del lienzo, en coordenadas de la ventana (encontrado por hit-testing).</summary>
    private static AvaloniaPoint FreeCanvasPoint(Window window, EditorView view) =>
        Scan(window, view, IsFreeCanvas, "no se encontró fondo libre en el lienzo: no hay dónde empezar un paneo sin caer en una pieza flotante");

    /// <summary>
    /// Un punto de la tarjeta que la <b>agarra</b> sin que se lo quede un mando: el recorrido desde el que
    /// responde el hit-testing hasta la tarjeta no puede atravesar un control interactivo.
    /// </summary>
    private static AvaloniaPoint DragGrip(Window window, Visual canvas, NodeViewModel node)
    {
        var container = ContainerOf((Nodify.Avalonia.NodifyEditor)canvas, node);

        AvaloniaPoint[] candidates =
        [
            new(24, 12), new(60, 12), new(120, 12), new(24, 26), new(12, 60), new(12, 120),
            new(container.Bounds.Width - 12, 12), new(container.Bounds.Width / 2, 12), new(container.Bounds.Width / 2, 30)
        ];

        foreach (var local in candidates)
        {
            var inWindow = container.TranslatePoint(local, window);
            if (inWindow is null || !IsOnTheCard(window, inWindow.Value, node))
            {
                continue;
            }

            return inWindow.Value;
        }

        throw new InvalidOperationException("no se encontró un punto de la tarjeta que la agarre sin que se lo quede un mando");
    }

    /// <summary>
    /// ¿El punto lo responde el <b>lienzo</b>? Ni una tarjeta, ni un decorador, ni una conexión, ni una de las
    /// piezas flotantes del editor (la barra de zoom, el HUD de telemetría al pie, el buscador) — todas ellas
    /// encima del lienzo. Fijar la esquina inferior derecha, como se hizo primero, mandaba el gesto a la barra
    /// de zoom y la sonda medía el silencio del lienzo en vez del paneo.
    /// </summary>
    private static bool IsFreeCanvas(Window window, EditorView view, AvaloniaPoint point)
    {
        var canvas = CanvasOf(view);

        for (Visual? hit = window.InputHitTest(point) as Visual; hit is not null; hit = hit.GetVisualParent())
        {
            if (ReferenceEquals(hit, canvas))
            {
                return true;
            }

            if (hit is Nodify.Avalonia.ItemContainer or Nodify.Avalonia.DecoratorContainer
                or Nodify.Avalonia.Connections.ConnectionContainer
                || hit.GetType().Name.Contains("ZoomBar", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>¿El punto cae dentro de la tarjeta del nodo, sin atravesar ningún mando que se quede la pulsación?</summary>
    private static bool IsOnTheCard(Window window, AvaloniaPoint point, NodeViewModel node)
    {
        bool onTheCard = false;

        for (Visual? hit = window.InputHitTest(point) as Visual; hit is not null; hit = hit.GetVisualParent())
        {
            if (hit is Button or TextBox or ComboBox or Slider or CheckBox or ToggleSwitch or ListBox
                or Nodify.Avalonia.Nodes.NodeInput or Nodify.Avalonia.Nodes.NodeOutput)
            {
                return false;
            }

            if (ReferenceEquals(hit.DataContext, node) || ReferenceEquals(hit, ContainerOfNode(window, node)))
            {
                onTheCard = true;
                break;
            }
        }

        return onTheCard;
    }

    private static Visual? ContainerOfNode(Window window, NodeViewModel node) =>
        window.GetVisualDescendants().OfType<Nodify.Avalonia.ItemContainer>()
            .FirstOrDefault(container => ReferenceEquals(container.DataContext, node) || ReferenceEquals(container.Content, node));

    /// <summary>
    /// Busca en el lienzo, en rejilla, el primer punto que cumpla la condición: las piezas flotantes y las
    /// tarjetas se mueven con el documento, así que un punto fijo deja de servir en cuanto cambia el grafo.
    /// </summary>
    private static AvaloniaPoint Scan(Window window, EditorView view, Func<Window, EditorView, AvaloniaPoint, bool> accepts, string failure)
    {
        Visual canvas = CanvasOf(view);

        for (double y = 80; y < canvas.Bounds.Height - 80; y += 24)
        {
            for (double x = 80; x < canvas.Bounds.Width - 80; x += 24)
            {
                var inWindow = canvas.TranslatePoint(new AvaloniaPoint(x, y), window);
                if (inWindow is not null && accepts(window, view, inWindow.Value))
                {
                    return inWindow.Value;
                }
            }
        }

        throw new InvalidOperationException(failure);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Andamiaje: el editor de verdad, en una ventana de verdad
    // ─────────────────────────────────────────────────────────────────────────────

    private static void WithCanvas(Action<Window, EditorView, EditorViewModel> body)
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var editor = new EditorViewModel(CreateLoader());
            editor.LoadFromGraphModel(TwoNodesAndAWire());

            var view = new EditorView { DataContext = editor };
            var window = new Window { Content = view, Width = 1280, Height = 820 };
            window.Show();
            InputSimulator.Settle();

            try
            {
                body(window, view, editor);
            }
            finally
            {
                VisualSnapshot.DetachTree(window);
                window.Close();
                editor.Dispose();
            }
        });
    }

    private static PluginLoader CreateLoader()
    {
        var pluginLoader = new PluginLoader();
        pluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.FileSystem.FolderSourceNode).Assembly);
        pluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.ExpressionFilterNode).Assembly);
        pluginLoader.ScanCurrentAppDomain();
        return pluginLoader;
    }

    /// <summary>Dos nodos conectados y separados: lo mínimo para tener un cable con sus dos sockets.</summary>
    private static WorkflowGraph TwoNodesAndAWire() => new()
    {
        Name = "desktop-canvas-gestures",
        Nodes =
        {
            new WorkflowNode
            {
                Id = "source",
                NodeTypeName = "FolderSourceNode",
                X = 120,
                Y = 90,
                Parameters = new() { ["SourcePath"] = @"C:\data" }
            },
            new WorkflowNode
            {
                Id = "sink",
                NodeTypeName = "DestinationSinkNode",
                X = 520,
                Y = 260,
                Parameters = new() { ["DestinationRoot"] = @"C:\out" }
            }
        },
        Edges =
        {
            new WorkflowEdge { SourceNodeId = "source", SourcePortName = "Out", TargetNodeId = "sink", TargetPortName = "In" }
        }
    };

    private static Nodify.Avalonia.NodifyEditor CanvasOf(EditorView view) =>
        view.GetVisualDescendants().OfType<Nodify.Avalonia.NodifyEditor>().First();

    private static Nodify.Avalonia.ItemContainer ContainerOf(Nodify.Avalonia.NodifyEditor canvas, NodeViewModel node) =>
        canvas.GetVisualDescendants().OfType<Nodify.Avalonia.ItemContainer>()
            .First(container => ReferenceEquals(container.DataContext, node) || ReferenceEquals(container.Content, node));

    private static FlowConnection? WireOf(EditorView view, ConnectionViewModel connection) =>
        view.GetVisualDescendants().OfType<FlowConnection>()
            .FirstOrDefault(wire => ReferenceEquals(wire.DataContext, connection));

    /// <summary>
    /// El socket de un puerto, por el camino del árbol real: su cabecera lleva el puerto del ViewModel. Es la
    /// misma búsqueda que documenta <c>GeometryBindingProjectionTests</c> (el DataContext del control de Nodify
    /// es la tarjeta; el puerto llega por el <c>Header</c>).
    /// </summary>
    private static Visual? SocketOf(EditorView view, PortViewModel port, bool isInput)
    {
        if (isInput)
        {
            return view.GetVisualDescendants().OfType<Nodify.Avalonia.Nodes.NodeInput>()
                .Cast<Visual>()
                .FirstOrDefault(socket => Serves(socket, ((Nodify.Avalonia.Nodes.NodeInput)socket).Header, port));
        }

        return view.GetVisualDescendants().OfType<Nodify.Avalonia.Nodes.NodeOutput>()
            .Cast<Visual>()
            .FirstOrDefault(socket => Serves(socket, ((Nodify.Avalonia.Nodes.NodeOutput)socket).Header, port));
    }

    /// <summary>¿Este socket es el del puerto? Por su cabecera, o por el DataContext de lo que pinta dentro.</summary>
    private static bool Serves(Visual socket, object? header, PortViewModel port) =>
        ReferenceEquals(header, port)
        || socket.GetVisualDescendants().OfType<Control>()
            .Any(descendant => ReferenceEquals(descendant.DataContext, port));
}
