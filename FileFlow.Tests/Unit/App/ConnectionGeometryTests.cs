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
/// <c>Connection</c> de Nodify (transcrito en <see cref="ConnectionGeometry"/>), no re-transcritos en código de
/// prueba — un espejo del código sólo probaría que el código es igual a sí mismo. Los casos cubren el
/// horizontal, el vertical, el invertido (destino a la izquierda del origen), el de nodos pegados (el cuello
/// se suaviza) y el de nodos lejanos (el cuello crece despacio).</para>
///
/// <para><b>Para qué sirve</b>: es el contrato que permite a un segundo host (Uno) dibujar <b>el mismo
/// cable</b> que el escritorio, y es la única defensa de que una divergencia entre hosts sería un defecto del
/// producto y no una decisión de plataforma.</para>
/// </summary>
public class ConnectionGeometryTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Las anclas de control, contra valores calculados a mano
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HorizontalWire_WithDefaultSpacing_ShouldExitAndArriveHorizontally()
    {
        // Origen (0,0), destino (400,0): ancho 400, alto 0.
        // smooth = min(100, 0) = 0; offset = max(0, 200) = 200; techo = 100 + sqrt(400*25) = 200.
        // offset = min(200, 200) = 200.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(400, 0));

        curve.P0.Should().Be(new Point(45, 0), "la curva sale del origen avanzando el spacing");
        curve.P3.Should().Be(new Point(355, 0), "y llega al destino retirándose el spacing");
        curve.P1.Should().Be(new Point(245, 0), "el cuello de salida es el offset calculado");
        curve.P2.Should().Be(new Point(155, 0), "el cuello de llegada es el mismo offset hacia atrás");
    }

    [Fact]
    public void VerticalWire_ShouldUseTheSameRuleWithTheHeights()
    {
        // Origen (0,0), destino (0,300): ancho 0, alto 300.
        // smooth = min(100, 300) = 100; offset = max(100, 0) = 100; techo = 100 + sqrt(0) = 100.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(0, 300));

        curve.P0.Should().Be(new Point(45, 0));
        curve.P3.Should().Be(new Point(-45, 300));
        curve.P1.Should().Be(new Point(145, 0), "el cuello sale en horizontal aunque el cable baje");
        curve.P2.Should().Be(new Point(-145, 300));
    }

    [Fact]
    public void BackwardWire_ShouldMirrorTheGeometry_WhenTargetIsLeftOfSource()
    {
        // Origen (500,100), destino (100,100), direction = Forward (el cable sale hacia la derecha del
        // origen y llega por la izquierda del destino, aunque el destino esté a la izquierda).
        // ancho 400, alto 0: offset = 200, techo = 200.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(500, 100), new Point(100, 100));

        curve.P0.Should().Be(new Point(545, 100));
        curve.P3.Should().Be(new Point(55, 100), "la curva termina a spacing del destino");
        curve.P1.Should().Be(new Point(745, 100), "el cuello crece hacia la derecha del origen");
        curve.P2.Should().Be(new Point(-145, 100), "y el de llegada hacia la izquierda del destino");
    }

    [Fact]
    public void BackwardDirection_ShouldMirrorTheGeometry()
    {
        var forward = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(400, 0), direction: ConnectionGeometry.FlowDirection.Backward);

        // Con signo -1, la salida se retira y la llegada avanza.
        forward.P0.Should().Be(new Point(-45, 0));
        forward.P3.Should().Be(new Point(445, 0));
        forward.P1.Should().Be(new Point(-245, 0));
        forward.P2.Should().Be(new Point(645, 0));
    }

    [Fact]
    public void CloseNodes_ShouldSoftenTheNeck()
    {
        // Origen (0,0), destino (30,80): ancho 30, alto 80.
        // smooth = min(100, 80) = 80; offset = max(80, 15) = 80; techo = 100 + sqrt(750) ≈ 127,38.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(30, 80));

        curve.P0.Should().Be(new Point(45, 0));
        curve.P1.Should().Be(new Point(125, 0), "el cuello es smooth (80), no width/2 (15): nodos pegados no hacen rulos");
        curve.P2.X.Should().BeApproximately(-95, 0.001, "y el techo no interviene: 100 + sqrt(30*25) ≈ 127 > 80");
    }

    [Fact]
    public void FarNodes_ShouldGrowTheNeckSlowly()
    {
        // Origen (0,0), destino (4000,0): ancho 4000, alto 0.
        // smooth = 0; offset = max(0, 2000) = 2000; techo = 100 + sqrt(4000*25) = 100 + 316,23 ≈ 416,23.
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(4000, 0));

        curve.P1.X.Should().BeApproximately(45 + (100 + Math.Sqrt(4000 * 25)), 0.001,
            "el cuello queda en el techo: crece con la raíz de la distancia, no linealmente");
    }

    [Fact]
    public void CustomSpacing_ShouldMoveTheStartAndEndOfTheCurve()
    {
        var curve = ConnectionGeometry.BezierControlPoints(new Point(0, 0), new Point(400, 0), spacing: 20);

        curve.P0.Should().Be(new Point(20, 0));
        curve.P3.Should().Be(new Point(380, 0));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Interpolación, tangente y hit-testing
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Interpolation_AtTheEnds_ShouldBeTheSourceAndTheTarget()
    {
        var curve = ConnectionGeometry.BezierControlPoints(new Point(10, 20), new Point(310, 220), spacing: 0);

        ConnectionGeometry.Interpolate(curve, 0).Should().Be(curve.P0);
        ConnectionGeometry.Interpolate(curve, 1).Should().Be(curve.P3);
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
}
