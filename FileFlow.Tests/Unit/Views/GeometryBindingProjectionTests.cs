using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaPoint = Avalonia.Point;
using FileFlow.App;
using FileFlow.App.Converters;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views;
using AppNodeCardView = FileFlow.App.Views.Components.NodeCardView;
using FlowConnection = FileFlow.App.Views.Components.FlowConnection;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;
using Point = FileFlow.Sdk.Point;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// El <b>flujo de datos de geometría</b> del lienzo, ejecutado de verdad contra el editor real: los ViewModels
/// del grafo hablan en <see cref="Point"/> (Sdk) y Nodify habla en <see cref="AvaloniaPoint"/> — y como la app
/// compila los enlaces por reflexión (<c>AvaloniaUseCompiledBindingsByDefault=false</c>), cada enlace de
/// geometría entre un ViewModel y un control pasa por <see cref="SdkPointConverter"/>. Si un enlace nuevo (o un
/// reordenamiento de XAML) se queda sin conversor, el enlace muere <b>en silencio</b>: las tarjetas se
/// amontonan en (0,0), los cables desaparecen y el fallo cae en la captura visual —o en el usuario—, no en la
/// prueba del enlace. Ésta es la prueba del enlace.
///
/// <para><b>Por qué así</b>: el defecto del hito 211 no fue un error de lógica sino de <i>traducción</i>, y las
/// capturas visuales lo delataron tarde y de rebote. Aquí se ejecutan las tres mitades del flujo: el
/// VM→control (las posiciones que el lienzo pinta), el control→VM (el write-back de anclas de los sockets y el
/// viewport que el usuario mueve) y el censo estático del árbol (ningún enlace de geometría sin conversor).</para>
///
/// <para>Corre en la colección exclusiva de las capturas porque monta la sesión headless de Avalonia (el
/// contrato de colecciones lo exige).</para>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class GeometryBindingProjectionTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // VM → control: las posiciones que el lienzo pinta
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NodeLocations_ShouldLandInTheItemContainers_ThroughThePointConverter()
    {
        WithEditor((window, view, editor) =>
        {
            var canvas = Canvas(view);

            foreach (var node in editor.Nodes)
            {
                var container = ContainerOf(canvas, node);

                // El punto que el VM declara llega al control convertido: si el enlace está muerto, el
                // contenedor queda en (0,0) amontonado bajo los demás — el defecto del 211, aquí en rojo.
                var expected = Projected(node.Location);
                container.Location.Should().Be(
                    expected,
                    $"el contenedor de '{node.Title}' debe pintar la Location del ViewModel convertida al punto del framework");
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // control → VM: lo que el usuario mueve y el cable escribe
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ViewportLocation_ShouldRoundTripBetweenControlAndViewModel()
    {
        WithEditor((window, view, editor) =>
        {
            var canvas = Canvas(view);
            var moved = new AvaloniaPoint(137.5, -84.25);

            canvas.ViewportLocation = moved;
            InputSimulator.Settle();

            editor.ViewportLocation.Should().Be(
                new Point(moved.X, moved.Y),
                "el encuadre que el usuario mueve llega al ViewModel: sin conversor, el binding TwoWay muere y la barra de estado queda congelada");

            // Y el viaje de vuelta: el control lee del VM (el mismo enlace, otro sentido).
            editor.ViewportLocation = new Point(55, 66);
            InputSimulator.Settle();
            canvas.ViewportLocation.Should().Be(
                new AvaloniaPoint(55, 66),
                "el enlace ViewportLocation es TwoWay: el control también sigue al ViewModel");
        });
    }

    [Fact]
    public void SocketAnchors_ShouldBeWrittenBackInGraphSpace_ByTheEditor()
    {
        WithEditor((window, view, editor) =>
        {
            var node = editor.Nodes.First(n => n.InputPorts.Count > 0);
            var inputPort = node.InputPorts.First();

            var socket = SocketOf(view, inputPort);
            socket.Should().NotBeNull(
                "el socket del puerto debe estar materializado en el árbol visual " +
                $"(NodeInput en el árbol: {view.GetVisualDescendants().OfType<Nodify.Avalonia.Nodes.NodeInput>().Count()}, " +
                $"NodeOutput: {view.GetVisualDescendants().OfType<Nodify.Avalonia.Nodes.NodeOutput>().Count()}, " +
                $"tarjetas: {view.GetVisualDescendants().OfType<AppNodeCardView>().Count()})");

            // El write-back que Nodify hace (Anchor OneWayToSource): el control calcula su ancla en espacio
            // de grafo y la escribe en el ViewModel, ya convertida a Sdk.Point. El ancla del VM debe ser la
            // que el control calculó — si el enlace murió, queda congelada en lo que puso el modelo.
            InputSimulator.Settle();
            inputPort.Anchor.Should().Be(
                socket!.Anchor.ToSdk(),
                "el ancla calculada por el control aterriza en el ViewModel convertida a Sdk.Point: es la mitad que sostiene a los cables");

            // Y el write-back es vivo: al mover el nodo, el ancla del VM sigue al nodo (por eso los cables
            // lo siguen). Un ancla congelada en el valor del modelo no se movería y los cables quedarían
            // colgando en el sitio viejo.
            var anchorBefore = inputPort.Anchor;
            var displacement = new Point(80, 45);
            node.Location = new Point(node.Location.X + displacement.X, node.Location.Y + displacement.Y);
            InputSimulator.Settle();

            inputPort.Anchor.Should().Be(
                new Point(anchorBefore.X + displacement.X, anchorBefore.Y + displacement.Y),
                "el ancla del VM sigue al nodo al moverse: es lo que hace que los cables no queden colgando");
        });
    }

    [Fact]
    public void ConnectionEndpoints_ShouldBindTheConvertedAnchorsOfBothSides()
    {
        WithEditor((window, view, editor) =>
        {
            var connection = editor.Connections.Single();

            var source = connection.Source;
            var target = connection.Target;
            source.Anchor.Should().NotBe(default(Point), "el ancla de origen debe haber sido escrita por el lienzo");
            target.Anchor.Should().NotBe(default(Point), "el ancla de destino debe haber sido escrita por el lienzo");

            var wire = WireOf(view, connection);
            wire.Should().NotBeNull("el cable debe estar materializado en el árbol visual");

            // Los dos extremos del cable se alimentan del ancla convertida de su puerto; si un enlace
            // muere, el extremo queda en el punto por defecto y el cable desaparece del lienzo.
            wire!.Source.Should().Be(
                Projected(source.Anchor),
                "el extremo origen del cable debe pintar el ancla convertida de su puerto");
            wire.Target.Should().Be(
                Projected(target.Anchor),
                "el extremo destino del cable debe pintar el ancla convertida de su puerto");
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El censo estático: ningún enlace de geometría del árbol sin conversor
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryGeometryBindingInTheTree_ShouldCarryThePointConverter()
    {
        // El inventario de los ocho enlaces del hito 211. Si el XAML gana uno nuevo sin conversor, el
        // censo no lo conocerá y esta línea no lo defenderá — pero el resto de las pruebas sí cazarán su
        // efecto en caliente. Para un XAML nuevo, añádelo aquí o al contrato visual.
        foreach (var (file, binding) in new[]
                 {
                     ("FileFlow.App/Views/EditorView.axaml", "ViewportLocation=\"{Binding ViewportLocation, Mode=TwoWay, Converter={x:Static conv:SdkPointConverter.Instance}}\""),
                     ("FileFlow.App/Views/EditorView.axaml", "<Setter Property=\"Location\" Value=\"{Binding Location, Mode=TwoWay, Converter={x:Static conv:SdkPointConverter.Instance}}\" />"),
                     ("FileFlow.App/Views/EditorView.axaml", "SourceAnchor=\"{Binding Source.Anchor, Converter={x:Static conv:SdkPointConverter.Instance}}\""),
                     ("FileFlow.App/Views/EditorView.axaml", "Source=\"{Binding Source.Anchor, Converter={x:Static conv:SdkPointConverter.Instance}}\""),
                     ("FileFlow.App/Views/EditorView.axaml", "Target=\"{Binding Target.Anchor, Converter={x:Static conv:SdkPointConverter.Instance}}\""),
                     ("FileFlow.App/Views/Components/NodeCardView.axaml", "Anchor=\"{Binding Anchor, Mode=OneWayToSource, Converter={x:Static conv:SdkPointConverter.Instance}}\"")
                 })
        {
            string path = System.IO.Path.Combine(TestRepositoryLocator.RepositoryRoot(), file);
            System.IO.File.Exists(path).Should().BeTrue($"el fichero '{file}' debe existir: el censo lee el árbol real");

            string source = System.IO.File.ReadAllText(path);
            source.Should().Contain(binding, $"el enlace de geometría de '{file}' debe llevar el conversor: sin él muere en silencio");
        }

        // Y los dos enlaces de Location del editor son exactamente dos (contenedores de nodos y de decoradores):
        // un tercero sin conversor sería un enlace nuevo que el censo de arriba no está defendiendo.
        string editorSource = System.IO.File.ReadAllText(System.IO.Path.Combine(
            TestRepositoryLocator.RepositoryRoot(), "FileFlow.App/Views/EditorView.axaml"));
        editorSource.Split("Binding Location, Mode=TwoWay, Converter=").Length.Should().Be(3,
            "los dos estilos de Location (contenedores de nodos y decoradores) con conversor, ni uno más ni uno menos");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Andamiaje
    // ─────────────────────────────────────────────────────────────────────────────

    private static void WithEditor(Action<Window, EditorView, EditorViewModel> body)
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var editor = new EditorViewModel(PluginRegistryHelper.CreateConfiguredLoader());
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

    /// <summary>Dos nodos conectados y separados: lo mínimo para ejercitar Location, anclas y los dos extremos del cable.</summary>
    private static WorkflowGraph TwoNodesAndAWire() => new()
    {
        Name = "geometry-projection",
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

    private static Nodify.Avalonia.NodifyEditor Canvas(EditorView view) =>
        view.GetVisualDescendants().OfType<Nodify.Avalonia.NodifyEditor>().First();

    /// <summary>
    /// La proyección esperada, calculada con <b>el mismo conversor</b> que el XAML usa ({x:Static
    /// SdkPointConverter.Instance}): la expectativa y el enlace pasan por la misma pieza, así que el test
    /// defiende la traducción del enlace, no una copia de ella.
    /// </summary>
    private static AvaloniaPoint Projected(Point sdkPoint) =>
        (AvaloniaPoint)SdkPointConverter.Instance.Convert(sdkPoint, typeof(AvaloniaPoint), null, System.Globalization.CultureInfo.InvariantCulture)!;

    private static Nodify.Avalonia.ItemContainer ContainerOf(Nodify.Avalonia.NodifyEditor canvas, NodeViewModel node) =>
        canvas.GetVisualDescendants().OfType<Nodify.Avalonia.ItemContainer>()
            .First(container => ReferenceEquals(container.DataContext, node) || ReferenceEquals(container.Content, node));

    private static FlowConnection? WireOf(EditorView view, ConnectionViewModel connection) =>
        view.GetVisualDescendants().OfType<FlowConnection>()
            .FirstOrDefault(wire => ReferenceEquals(wire.DataContext, connection));

    /// <summary>
    /// El socket de un puerto de entrada: el control cuya plantilla de cabecera tiene al puerto como contexto.
    /// El DataContext del NodeInput en sí es la tarjeta (el ItemContainer lo proyecta); el puerto llega al
    /// Header por el ItemsSource de los puertos de la tarjeta, y de ahí a su plantilla.
    /// </summary>
    private static Nodify.Avalonia.Nodes.NodeInput? SocketOf(EditorView view, PortViewModel port) =>
        view.GetVisualDescendants().OfType<Nodify.Avalonia.Nodes.NodeInput>()
            .FirstOrDefault(socket => socket.Header is PortViewModel header && ReferenceEquals(header, port)
                                      || socket.GetVisualDescendants().OfType<Control>()
                                          .Any(descendant => ReferenceEquals(descendant.DataContext, port)));
}
