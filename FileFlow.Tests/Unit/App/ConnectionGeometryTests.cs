using System;
using FileFlow.App.Services;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La <b>geometría del cable</b> del núcleo portable: las anclas de control de la curva, su interpolación,
/// su tangente y el hit-testing.
///
/// <para><b>Qué fija</b>: los valores esperados están calculados a mano a partir del algoritmo del control
/// <c>Connection</c> de la versión anterior (transcrito en <see cref="ConnectionGeometry"/>), no re-transcritos en código de
/// prueba — un espejo del código sólo probaría que el código es igual a sí mismo. Los casos cubren el
/// horizontal, el vertical, el invertido (destino a la izquierda del origen), el de nodos pegados (el cuello
/// se suaviza) y el de nodos lejanos (el cuello crece despacio).</para>
///
/// <para><b>Para qué sirve</b>: es el contrato que permite a un segundo host (Uno) dibujar <b>el mismo
/// cable</b> que la versión anterior, y es la única defensa de que una divergencia entre hosts sería un defecto del
/// producto y no una decisión de plataforma.</para>
/// </summary>
public class ConnectionGeometryTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Las anclas de control, contra valores calculados a mano
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HorizontalWire_ShouldBeATautLine_FromAnchorToAnchor()
    {
        // Origen (0,0), destino (400,0): ancho 400.
        // techo = 100 + sqrt(400*25) = 200; cuello = min(200, 400/2) = 200.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(400, 0));

        curve.P0.Should().Be(new Point(0, 0), "la curva NACE en el ancla de origen: no hay punta retirada");
        curve.P3.Should().Be(new Point(400, 0), "y MUERE en la del destino");
        curve.P1.Should().Be(new Point(200, 0), "el cuello de salida sale en horizontal desde el ancla");
        curve.P2.Should().Be(new Point(200, 0), "y el de llegada lo encuentra en el medio: el cable queda tirante");
    }

    [Fact]
    public void VerticalWire_ShouldCloseIntoALine_BecauseThereIsNoHorizontalRoom()
    {
        // Origen (0,0), destino (0,300): ancho 0 (no hay hueco horizontal), alto 300.
        // techo = 100 + sqrt(0) = 100; cuello = min(100, 0/2) = 0.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(0, 300));

        curve.P0.Should().Be(new Point(0, 0), "el cable nace en el ancla");
        curve.P1.Should().Be(new Point(0, 0), "sin hueco horizontal no hay cuello: el cable baja recto");
        curve.P2.Should().Be(new Point(0, 300), "la regla vieja asomaba 145 px a un lado y al otro");
        curve.P3.Should().Be(new Point(0, 300));
    }

    [Fact]
    public void BackwardWire_ShouldMirrorTheGeometry_WhenTargetIsLeftOfSource()
    {
        // Origen (500,100), destino (100,100), direction = Forward (el cable sale hacia la derecha del
        // origen y llega por la izquierda del destino, aunque el destino esté a la izquierda).
        // ancho 400, alto 0: offset = 200, techo = 200.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(500, 100), new Point(100, 100));

        curve.P0.Should().Be(new Point(500, 100), "la curva nace en el ancla, aunque el destino esté a la izquierda");
        curve.P3.Should().Be(new Point(100, 100), "y muere en la del destino");
        curve.P1.Should().Be(new Point(700, 100), "el cuello sale hacia la derecha del origen (el socket mira a la derecha)");
        curve.P2.Should().Be(new Point(-100, 100), "y el de llegada hacia la izquierda del destino");
    }

    [Fact]
    public void BackwardDirection_ShouldMirrorTheGeometry()
    {
        var forward = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(400, 0), direction: ConnectionGeometry.FlowDirection.Backward);

        // Con signo -1 los cuellos se invierten; las anclas no se mueven (el cable las toca siempre).
        forward.P0.Should().Be(new Point(0, 0));
        forward.P3.Should().Be(new Point(400, 0));
        forward.P1.Should().Be(new Point(-200, 0));
        forward.P2.Should().Be(new Point(600, 0));
    }

    [Fact]
    public void CloseNodes_ShouldFitTheNeckInTheHueco()
    {
        // Origen (0,0), destino (30,80): ancho 30 (hueco), alto 80.
        // techo = 100 + sqrt(750) ≈ 127,38; cuello = min(127,38, 15) = 15.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(30, 80));

        curve.P0.Should().Be(new Point(0, 0));
        curve.P1.Should().Be(new Point(15, 0), "el cuello se queda en la MITAD del hueco: los controles no se cruzan");
        curve.P2.Should().Be(new Point(15, 80), "y la curva se cierra dentro del hueco, sin asomarse por detrás");
        curve.P3.Should().Be(new Point(30, 80));
    }

    [Fact]
    public void FarNodes_ShouldGrowTheNeckSlowly()
    {
        // Origen (0,0), destino (4000,0): ancho 4000.
        // techo = 100 + sqrt(4000*25) = 100 + 316,23 ≈ 416,23; la mitad del hueco son 2000, así que manda el techo.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(4000, 0));

        curve.P1.X.Should().BeApproximately(100 + Math.Sqrt(4000 * 25), 0.001,
            "el cuello queda en el techo: crece con la raíz de la distancia, no linealmente, así que dos tarjetas muy " +
            "lejanas no dibujan un tramo recto larguísimo");
        curve.P2.X.Should().BeApproximately(4000 - (100 + Math.Sqrt(4000 * 25)), 0.001);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Interpolación, tangente y hit-testing
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Interpolation_AtTheEnds_ShouldBeTheSourceAndTheTarget()
    {
        var curve = ConnectionGeometry.BezierControlPoints(new Point(10, 20), new Point(310, 220));

        ConnectionGeometry.Interpolate(curve, 0).Should().Be(curve.P0);
        ConnectionGeometry.Interpolate(curve, 1).Should().Be(curve.P3);
        curve.P0.Should().Be(new Point(10, 20), "B(0) ES el ancla de origen: el cable sale del socket");
        curve.P3.Should().Be(new Point(310, 220), "y B(1) el ancla de destino");
    }

    [Fact]
    public void Interpolation_AtTheMiddle_ShouldBeTheAverageOfTheControls_ForASymmetricCurve()
    {
        // Curva simétrica en una horizontal: B(0.5) = (P0 + 3P1 + 3P2 + P3) / 8.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(400, 0));

        double expectedX = (curve.P0.X + (3 * curve.P1.X) + (3 * curve.P2.X) + curve.P3.X) / 8d;

        ConnectionGeometry.Interpolate(curve, 0.5).X.Should().BeApproximately(expectedX, 0.0001);
        ConnectionGeometry.Interpolate(curve, 0.5).Y.Should().BeApproximately(0, 0.0001,
            "una curva horizontal es simétrica: el punto medio no se sale del eje");
    }

    [Fact]
    public void Tangent_AtTheStart_ShouldPointToTheFirstControl()
    {
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(400, 200));

        var tangent = ConnectionGeometry.Tangent(curve, 0);

        // B'(0) = 3 (P1 - P0): la tangente inicial apunta del origen a su ancla de control.
        tangent.X.Should().BeApproximately(3 * (curve.P1.X - curve.P0.X), 0.0001);
        tangent.Y.Should().BeApproximately(3 * (curve.P1.Y - curve.P0.Y), 0.0001);
    }

    [Fact]
    public void Tangent_AtTheEnd_ShouldPointFromTheLastControl()
    {
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(400, 200));

        var tangent = ConnectionGeometry.Tangent(curve, 1);

        // B'(1) = 3 (P3 - P2).
        tangent.X.Should().BeApproximately(3 * (curve.P3.X - curve.P2.X), 0.0001);
        tangent.Y.Should().BeApproximately(3 * (curve.P3.Y - curve.P2.Y), 0.0001);
    }

    [Fact]
    public void HitTesting_ShouldFindTheWire_WithinTheStrokeTolerance_AndRejectFarPoints()
    {
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(400, 0));

        // Un punto sobre el eje del cable horizontal: el punto medio de la curva está sobre él. El muestreo
        // uniforme de 128 muestras deja ~3 px entre vecinas sobre una curva de 400 px, así que las tolerancias
        // del assert lo tienen en cuenta (un clic no necesita la distancia exacta, sí no falsar dentro ni fuera).
        var onTheWire = ConnectionGeometry.Interpolate(curve, 0.5);

        ConnectionGeometry.DistanceTo(curve, onTheWire, samples: 128).Should().BeLessThanOrEqualTo(2.0,
            "un punto de la curva está a distancia de muestreo (casi cero)");
        ConnectionGeometry.DistanceTo(curve, new Point(onTheWire.X, onTheWire.Y + 6), samples: 128).Should().BeLessThanOrEqualTo(8.0,
            "un punto a 6 px del eje queda dentro de la tolerancia de clic de un trazo de 3.5");
        ConnectionGeometry.DistanceTo(curve, new Point(onTheWire.X, onTheWire.Y + 40), samples: 128).Should().BeGreaterThan(20.0,
            "un punto lejos del cable no abre su menú contextual");
    }

    [Fact]
    public void HitTesting_ShouldWorkOnVerticalWires_Too()
    {
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(0, 300));

        var onTheWire = ConnectionGeometry.Interpolate(curve, 0.5);

        ConnectionGeometry.DistanceTo(curve, onTheWire, samples: 64).Should().BeLessThanOrEqualTo(5.0);
        ConnectionGeometry.DistanceTo(curve, new Point(onTheWire.X + 50, onTheWire.Y), samples: 64).Should().BeGreaterThan(20.0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El trazado del cable (hito 254): de ancla a ancla, sin tramos rectos
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El defecto que estas pruebas fijan: un host puede equivocarse dibujando la MISMA Bézier. Abrir la figura
    /// en el primer punto de control la deja separada de los dos sockets; y los dos tramos rectos con los que el
    /// control de la versión anterior unía las puntas <b>sí</b> existían allí y aquí se leían como una <b>Z</b> en pantalla
    /// —dos bajíos rectos y una ese apretada—, que es lo que el usuario pidió quitar. El trazado compartido
    /// empieza y termina en las anclas y no lleva ningún tramo recto: es contrato, no consejo.
    /// </summary>
    [Fact]
    public void TheTrace_ShouldStartAndEndAtTheAnchors_WithoutStraightBars()
    {
        var wire = ConnectionGeometry.BuildWire(new Point(0, 0), new Point(400, 0));

        wire.Trace.Should().HaveCount(4, "los cuatro puntos de una Bézier: ancla, dos cuellos y ancla");
        wire.Trace[0].Should().Be(new Point(0, 0), "el trazo EMPIEZA en el ancla de salida: es lo que hace que el " +
            "cable toque su socket");
        wire.Trace[^1].Should().Be(new Point(400, 0), "y TERMINA en el ancla de llegada");
        wire.Trace.Skip(1).Take(2).Should().Equal(
            [wire.Curve.P1, wire.Curve.P2],
            "y entre las dos anclas van los dos cuellos: el host dibuja UNA Bézier con ellos, sin tramos rectos");
    }

    [Fact]
    public void TheNeck_ShouldNeverOvershootTheHueco_EvenWhenTheNodesAreCupped()
    {
        // Anclas a 30 px: la regla del control retiraba 45 px de cada punta y dejaba los controles a 100 px del
        // hueco, así que la curva salía invertida: el rulo con forma de «2» que el usuario reportó al mover una
        // tarjeta. Ahora el cuello cabe en el hueco y el cable se cierra sin doblarse hacia atrás.
        var wire = ConnectionGeometry.BuildWire(new Point(0, 0), new Point(30, 0));

        wire.Exit.X.Should().Be(15, "el cuello es la MITAD del hueco");
        wire.Arrival.X.Should().Be(15, "y con las anclas a la misma altura los dos cuellos se encuentran en él");
        wire.Exit.X.Should().BeLessThanOrEqualTo(30, "el cuello de salida no sale del hueco");
        wire.Arrival.X.Should().BeGreaterThanOrEqualTo(0, "ni el de llegada se va por detrás del origen");
        wire.Trace[0].Should().Be(new Point(0, 0), "y el trazo sigue empezando y terminando en las anclas");
        wire.Trace[^1].Should().Be(new Point(30, 0));
    }

    [Fact]
    public void TheTrace_ShouldFollowTheFlowDirection_OfThePendingWire()
    {
        var forward = ConnectionGeometry.BuildWire(new Point(0, 0), new Point(400, 0));
        var backward = ConnectionGeometry.BuildWire(
            new Point(0, 0), new Point(400, 0), ConnectionGeometry.FlowDirection.Backward);

        forward.Trace[0].Should().Be(new Point(0, 0), "las anclas no dependen de la dirección del flujo");
        forward.Trace[^1].Should().Be(new Point(400, 0));
        backward.Trace[0].Should().Be(new Point(0, 0));
        backward.Trace[^1].Should().Be(new Point(400, 0));
        backward.Exit.X.Should().BeLessThan(forward.Exit.X,
            "lo que sí cambia con la dirección es hacia dónde sale el cuello");
    }

    [Fact]
    public void TheTrace_ShouldFollowTheWire_WhenTheCardMoves()
    {
        // El gesto que reportó el usuario: mover una tarjeta mueve su ancla. El trazado se recompone con las
        // anclas nuevas y sigue tocando las dos: no hay ningún punto del trazo que quede pegado al sitio viejo.
        var before = ConnectionGeometry.BuildWire(new Point(0, 0), new Point(520, 160));
        var after = ConnectionGeometry.BuildWire(new Point(0, 0), new Point(520, 300));

        after.Trace[0].Should().Be(before.Trace[0], "la ancla de salida no se movió: su tarjeta no se movió");
        after.Trace[^1].Should().Be(new Point(520, 300), "el trazo llega al ancla nueva, no a la vieja");
        after.Curve.P3.Should().Be(after.Target, "la curva muere en el ancla de destino, también en vertical");
        after.Curve.P0.Should().Be(after.Source, "y nace en la de origen");
    }
}
