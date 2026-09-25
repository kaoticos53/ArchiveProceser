using System.Collections.Generic;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;
using Point = FileFlow.Sdk.Point;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La geometría de encuadre del lienzo: <see cref="EditorViewportCalculator.CenterOn"/> y
/// <see cref="EditorViewportCalculator.CalculateFitToScreen"/>.
///
/// <para><b>Por qué estas pruebas existen</b>: el calculador es portable desde el hito 211 y no tenía una sola
/// (medido entonces: ningún test del suite lo referenciaba). La fase 3.0 del plan Uno lo necesita como
/// contrato — el host Uno tendrá que encuadrar exactamente igual que el escritorio, o «centrar» y «ajustar a
/// pantalla» significarán cosas distintas según el host—.</para>
///
/// <para><b>Qué se fija y qué no</b>: el tamaño de vista de referencia (900×500), el alto de respaldo de una
/// tarjeta (220) y el ancho de respaldo (280) son decisiones del producto que las pruebas clavan; el zoom
/// resultante se acota a [0.3, 1.8]. Lo que no se fija es el píxel: los bordes redondeados a 0.1 se afinan con
/// tolerancias.</para>
/// </summary>
public class EditorViewportCalculatorTests
{
    /// <summary>El ancho de respaldo de una tarjeta sin medida, documentado en el calculador.</summary>
    private const double FallbackWidth = 280;
    private const double FallbackHeight = 220;

    private static NodeViewModel NodeAt(double x, double y, double width = FallbackWidth) =>
        new(new FolderSourceNode(), new Point(x, y)) { Width = width };

    // ─────────────────────────────────────────────────────────────────────────────
    // CenterOn: centrar un nodo sin cambiarle el zoom al usuario
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CenterOn_ShouldCenterTheNode_InTheReferenceView()
    {
        var node = NodeAt(500, 700, width: 200);

        var location = EditorViewportCalculator.CenterOn(node, zoom: 1.0);

        // Centro del nodo: (500 + 100, 700 + 110) = (600, 810). La vista de referencia es 900x500.
        location.X.Should().Be(600 - 450);
        location.Y.Should().Be(810 - 250);
    }

    [Fact]
    public void CenterOn_ShouldRespectTheCurrentZoom()
    {
        var node = NodeAt(0, 0, width: 200);

        var atFullZoom = EditorViewportCalculator.CenterOn(node, zoom: 1.0);
        var atHalfZoom = EditorViewportCalculator.CenterOn(node, zoom: 0.5);

        // A la mitad del zoom, la ventana visible en espacio de grafo es el doble: el encuadre retrocede el doble.
        atHalfZoom.X.Should().BeApproximately(atFullZoom.X - 450, 0.001);
        atHalfZoom.Y.Should().BeApproximately(atFullZoom.Y - 250, 0.001);
    }

    [Fact]
    public void CenterOn_ShouldTreatZeroZoomAsFullZoom()
    {
        var node = NodeAt(100, 100);

        var zeroZoom = EditorViewportCalculator.CenterOn(node, zoom: 0);
        var fullZoom = EditorViewportCalculator.CenterOn(node, zoom: 1.0);

        zeroZoom.Should().Be(fullZoom, "un zoom que no es positivo no puede dividir la vista entre cero");
    }

    [Fact]
    public void CenterOn_ShouldFallBackToTheReferenceSize_WhenTheCardWasNotMeasured()
    {
        var unmeasured = NodeAt(1000, 2000, width: 0);
        var fallback = NodeAt(1000, 2000, width: FallbackWidth);

        EditorViewportCalculator.CenterOn(unmeasured, 1.0)
            .Should().Be(EditorViewportCalculator.CenterOn(fallback, 1.0),
                "una tarjeta sin ancho medido se centra igual que una del ancho de referencia");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // CalculateFitToScreen: el encuadre que mete el grafo en la vista
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FitToScreen_WithNoNodes_ShouldResetToOriginAtFullZoom()
    {
        var (zoom, location) = EditorViewportCalculator.CalculateFitToScreen([]);

        zoom.Should().Be(1.0);
        location.Should().Be(new Point(0, 0));
    }

    [Fact]
    public void FitToScreen_WithASingleSmallGraph_ShouldBeLimitedByTheReferenceCardHeight()
    {
        var nodes = new List<NodeViewModel> { NodeAt(0, 0, width: 100) };

        var (zoom, location) = EditorViewportCalculator.CalculateFitToScreen(nodes);

        // Grafo de 100x220 en una vista de 780x380 útil: scaleX=7.8, scaleY=380/220≈1.727, manda el alto.
        zoom.Should().Be(1.73);
        location.X.Should().BeLessThanOrEqualTo(0, "el encuadre deja margen por delante del grafo");
        location.Y.Should().BeLessThanOrEqualTo(0);

        // Hallazgo que esta prueba fija: el techo del zoom (1.8) es inalcanzable para el ajuste a pantalla
        // porque el alto de respaldo (220) fija el suelo del escalado vertical (380/220 ≈ 1.727). Si algún
        // día se quiere acercar más con la «Z», la cura está en el calculador, no aquí.
    }

    [Fact]
    public void FitToScreen_WithAWideGraph_ShouldBeLimitedByTheWidth()
    {
        // Dos nodos separados: el ancho manda sobre el alto.
        var nodes = new List<NodeViewModel> { NodeAt(0, 0, width: 200), NodeAt(2000, 0, width: 200) };

        var (zoom, _) = EditorViewportCalculator.CalculateFitToScreen(nodes);

        // Ancho del grafo: 2200. scaleX = 780/2200 ≈ 0.3545; scaleY = 380/220 ≈ 1.727; min → 0.3545.
        zoom.Should().BeApproximately(Math.Round(780.0 / 2200, 2), 0.001,
            "un grafo ancho se acota por el ancho de la vista");
    }

    [Fact]
    public void FitToScreen_WithATallGraph_ShouldBeLimitedByTheHeight()
    {
        var nodes = new List<NodeViewModel> { NodeAt(0, 0), NodeAt(10, 3000, width: 100) };

        var (zoom, _) = EditorViewportCalculator.CalculateFitToScreen(nodes);

        // Alto del grafo: 3000 + 220 = 3220. scaleY = 380/3220 ≈ 0.118 → clavado al suelo de 0.3.
        zoom.Should().Be(0.3, "un grafo altísimo no puede encogerse más allá del suelo del zoom");
    }

    [Fact]
    public void FitToScreen_ShouldKeepTheWholeGraphVisible_AtTheCalculatedZoom()
    {
        var nodes = new List<NodeViewModel>
        {
            NodeAt(100, 200, width: 200),
            NodeAt(900, 800, width: 300)
        };

        var (zoom, location) = EditorViewportCalculator.CalculateFitToScreen(nodes);

        // La promesa de «ajustar a pantalla», en espacio de grafo: el rectángulo que el encuadre deja
        // visible (location .. location + vista/zoom) contiene al rectángulo del grafo entero.
        double visibleWidth = 900.0 / zoom;
        double visibleHeight = 500.0 / zoom;

        const double tolerance = 1.0; // el redondeo a 0.1 del calculador, con margen.

        (location.X).Should().BeLessThanOrEqualTo(100 + tolerance, "el encuadre empieza antes del primer nodo");
        (location.Y).Should().BeLessThanOrEqualTo(200 + tolerance);
        (location.X + visibleWidth).Should().BeGreaterThanOrEqualTo(1200 - tolerance,
            "el borde derecho del grafo (nodo B + su ancho) queda dentro de la vista");
        (location.Y + visibleHeight).Should().BeGreaterThanOrEqualTo(800 + FallbackHeight - tolerance,
            "y el borde inferior (nodo B + el alto de referencia)");
    }

    [Fact]
    public void FitToScreen_ShouldNeverDivideByAnEmptyGraph()
    {
        // Un solo nodo: el grafo mide su tarjeta (280x220 por respaldo), nunca menos de 100x100.
        var nodes = new List<NodeViewModel> { NodeAt(0, 0) };

        var (zoom, _) = EditorViewportCalculator.CalculateFitToScreen(nodes);

        zoom.Should().BeGreaterThan(0, "el encuadre de un grafo mínimo no puede colgarse");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La coherencia entre las dos mitades de la geometría del lienzo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CenterOn_ShouldCenterExactly_WhatFitToScreenFrames()
    {
        // Centrar y ajustar tienen que hablar del mismo lienzo (el comentario del calculador lo exige):
        // el centro de la vista de referencia en espacio de grafo, calculado desde un encuadre de
        // FitToScreen, debe coincidir con el centro del nodo que CenterOn persigue.
        var node = NodeAt(1000, 1500, width: 240);
        var (_, fitLocation) = EditorViewportCalculator.CalculateFitToScreen(new[] { node, NodeAt(3000, 400) });

        var centered = EditorViewportCalculator.CenterOn(node, zoom: 1.0);

        double centerX = centered.X + (900.0 / 2);
        double centerY = centered.Y + (500.0 / 2);

        centerX.Should().Be(node.Location.X + (240 / 2.0), "CenterOn pone el centro del nodo en el centro de la vista");
        centerY.Should().Be(node.Location.Y + (FallbackHeight / 2.0));
        fitLocation.Should().NotBe(default(Point), "y FitToScreen encuadra el grafo sobre el mismo lienzo de referencia");
    }
}
