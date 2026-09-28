using Avalonia.Media;
using FileFlow.App.Converters;
using FileFlow.App.Services;
using FileFlow.App.Views.Components;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;
using SdkPoint = FileFlow.Sdk.Point;
using AvaloniaPoint = Avalonia.Point;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// El cable del lienzo del <b>escritorio</b>: la figura que se pinta tiene que ser la del trazador compartido
/// del núcleo (<see cref="ConnectionGeometry"/>), la misma que dibuja el host Uno.
///
/// <para><b>Por qué se mide la figura y no el XAML.</b> Que la plantilla use un control y no otro se lee en el
/// texto; que la CURVA sea la compartida sólo se ve en la geometría. Estos casos leen la figura que el control
/// va a dibujar (<c>DefiningGeometry</c>) y la comparan punto por punto con el núcleo, que es lo único que
/// distingue el cable del producto de la curva que traía el control de conexión de Nodify: su Bézier <b>sale
/// retirada de las anclas</b> y se une a ellas por dos tramos rectos, así que empieza en el cuello (no en el
/// socket) y se lee como una Z.</para>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class FlowConnectionGeometryTests
{
    private static SdkPoint Source { get; } = new(100, 100);

    private static SdkPoint Target { get; } = new(500, 220);

    /// <summary>
    /// Los cuatro puntos que el control tiene que haber dibujado, en orden: ancla de salida, cuello de salida,
    /// cuello de llegada y ancla de destino.
    /// </summary>
    private static (AvaloniaPoint Start, AvaloniaPoint P1, AvaloniaPoint P2, AvaloniaPoint End) DrawnFigure(FlowConnection wire)
    {
        var geometry = wire.DefiningGeometry.Should().BeOfType<PathGeometry>(
            "el cable se dibuja con una figura de trazo, no con un chorro de segmentos sueltos").Subject;

        PathFigure figure = geometry.Figures.Should().ContainSingle().Subject;
        var curve = figure.Segments.Should().ContainSingle(
            "el cable es UNA curva: los tramos rectos del control de Nodify son justo lo que se quitó").Subject
            .Should().BeOfType<BezierSegment>().Subject;

        figure.IsFilled.Should().BeFalse("un cable no tiene relleno: es un trazo");
        figure.IsClosed.Should().BeFalse("un cable no se cierra sobre sí mismo");

        return (figure.StartPoint, curve.Point1, curve.Point2, curve.Point3);
    }

    private static void ShouldBeTheCoreWire(
        (AvaloniaPoint Start, AvaloniaPoint P1, AvaloniaPoint P2, AvaloniaPoint End) drawn,
        SdkPoint source,
        SdkPoint target,
        ConnectionGeometry.FlowDirection direction = ConnectionGeometry.FlowDirection.Forward)
    {
        ConnectionGeometry.WirePath expected = ConnectionGeometry.BuildWire(source, target, direction);

        drawn.Start.Should().Be(expected.Source.ToAvalonia(), "el cable sale del ancla de origen");
        drawn.P1.Should().Be(expected.Exit.ToAvalonia(), "el primer cuello es el que decide el núcleo");
        drawn.P2.Should().Be(expected.Arrival.ToAvalonia(), "y el segundo también");
        drawn.End.Should().Be(expected.Target.ToAvalonia(), "y llega al ancla de destino");
    }

    [Fact]
    public void TheCable_ShouldBeTheCoreCurve_BetweenItsTwoAnchors()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var wire = new FlowConnection { Source = Source.ToAvalonia(), Target = Target.ToAvalonia() };

            var drawn = DrawnFigure(wire);

            ShouldBeTheCoreWire(drawn, Source, Target);

            // El cable NACE y MUERE en las anclas: son el primer y el último punto de la figura. El control de
            // Nodify empezaba en su primer punto de control, así que su trazo quedaba separado del socket.
            drawn.Start.Should().Be(Source.ToAvalonia());
            drawn.End.Should().Be(Target.ToAvalonia());
        });
    }

    [Fact]
    public void TheNeck_ShouldBeTheCoreOne_NotTheControlSpacing()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var wire = new FlowConnection { Source = Source.ToAvalonia(), Target = Target.ToAvalonia() };
            var (start, p1, _, _) = DrawnFigure(wire);

            // Con 400 unidades de hueco el núcleo pone el cuello a 200 (su techo, 100 + √(25·ancho), cae justo
            // en la mitad del hueco); el control de Nodify lo dejaba en su `Spacing`, que eran 45 fijas.
            double neck = p1.X - start.X;

            neck.Should().BeApproximately(200, 0.0001, "el cuello es el del núcleo, no el del control");
            neck.Should().NotBeApproximately(45, 0.5, "45 era el `Spacing` del control de Nodify");
        });
    }

    [Fact]
    public void TheCable_ShouldReverse_WhenTheDragComesFromAnInput()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var wire = new FlowConnection
            {
                Source = Source.ToAvalonia(),
                Target = Target.ToAvalonia(),
                Direction = ConnectionGeometry.FlowDirection.Backward
            };

            var drawn = DrawnFigure(wire);

            ShouldBeTheCoreWire(drawn, Source, Target, ConnectionGeometry.FlowDirection.Backward);

            // El trazo se da la vuelta: con el arrastre desde una entrada el cable sale hacia atrás.
            drawn.P1.X.Should().BeLessThan(drawn.Start.X, "hacia atrás el primer cuello queda a la izquierda de su ancla");
            drawn.P2.X.Should().BeGreaterThan(drawn.End.X, "y el segundo a la derecha de la suya");
        });
    }

    [Fact]
    public void TheCable_ShouldFollowItsAnchors_WhenTheyMove()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            var wire = new FlowConnection { Source = Source.ToAvalonia(), Target = Target.ToAvalonia() };
            var before = DrawnFigure(wire);

            // Una tarjeta que se arrastra mueve su ancla: el cable tiene que rehacerse, o el trazo se queda
            // dibujado donde estaba el socket (el defecto clásico de una figura cacheada).
            var moved = new SdkPoint(900, 500);
            wire.Target = moved.ToAvalonia();

            var after = DrawnFigure(wire);

            after.Should().NotBe(before, "mover un ancla cambia la figura");
            ShouldBeTheCoreWire(after, Source, moved);
        });
    }

    [Fact]
    public void TheDirectionConverter_ShouldSpeakBothVocabularies()
    {
        // El cable en curso lo arrastra el control de Nodify, que expresa la dirección con su propio flag; el
        // dibujo la pide en el vocabulario del núcleo. La traducción es lo que mantiene un solo trazador.
        var forward = ConnectionDirectionConverter.Instance.Convert(
            Nodify.Avalonia.Connections.ConnectionDirection.Forward,
            typeof(ConnectionGeometry.FlowDirection),
            null!,
            null!);
        var backward = ConnectionDirectionConverter.Instance.Convert(
            Nodify.Avalonia.Connections.ConnectionDirection.Backward,
            typeof(ConnectionGeometry.FlowDirection),
            null!,
            null!);

        forward.Should().Be(ConnectionGeometry.FlowDirection.Forward);
        backward.Should().Be(ConnectionGeometry.FlowDirection.Backward);
    }
}
