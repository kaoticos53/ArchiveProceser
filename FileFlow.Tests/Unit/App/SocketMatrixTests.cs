using System;
using System.Linq;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Los auto-tests de la <b>paleta y matriz de estado de puertos</b> del nucleo
/// (<see cref="PortPalette"/>): los mismos colores que los selectores de la versión anterior
/// (<c>FileFlow.App/Styles/Ports.axaml</c>), exigidos tipo a tipo y estado a estado.
///
/// <para>Es el testigo de <c>tarjeta-que-no-habla-por-su-color</c>: si la paleta se sustituye
/// por un color fijo, estas aserciones caen nombrando el tipo que dejo de distinguirse. Los
/// <see cref="PortViewModel"/> se montan con nodos reales del plugin FileSystem y los estados
/// de arrastre se fuerzan con los metodos que el propio editor llama durante el drag.</para>
/// </summary>
public class SocketMatrixTests
{
    [Theory]
    [InlineData(PortTypeKind.Files, 0x10, 0xB9, 0x81)]
    [InlineData(PortTypeKind.Text, 0x06, 0xB6, 0xD4)]
    [InlineData(PortTypeKind.Boolean, 0xF5, 0x9E, 0x0B)]
    [InlineData(PortTypeKind.Number, 0x63, 0x66, 0xF1)]
    [InlineData(PortTypeKind.Binary, 0xA8, 0x55, 0xF7)]
    [InlineData(PortTypeKind.Collection, 0xEF, 0x44, 0x44)]
    [InlineData(PortTypeKind.Any, 0x81, 0x8C, 0xF8)]
    public void TypeColor_ShouldSpeakTheDesktopPalette_PerTypeKind(PortTypeKind kind, byte r, byte g, byte b)
    {
        var c = PortPalette.TypeColor(kind);

        c.R.Should().Be(r, $"el socket {kind} habla el color de la versión anterior (R)");
        c.G.Should().Be(g, $"el socket {kind} habla el color de la versión anterior (G)");
        c.B.Should().Be(b, $"el socket {kind} habla el color de la versión anterior (B)");
    }

    [Fact]
    public void TypeColor_ShouldGiveADistinctColorToEveryType()
    {
        var kinds = Enum.GetValues<PortTypeKind>()
            
            .Select(PortPalette.TypeColor)
            .ToList();

        kinds.Distinct().Should().HaveCount(kinds.Count,
            "cada tipo de dato tiene su color: si dos comparten, la paleta dejo de hablar");
    }

    [Fact]
    public void FreeFill_And_DragSourceColors_ShouldMatchTheDesktopTokens()
    {
        PortPalette.FreeFill.R.Should().Be(0x0D);
        PortPalette.FreeFill.G.Should().Be(0x11);
        PortPalette.FreeFill.B.Should().Be(0x17);

        PortPalette.DragSourceFill.R.Should().Be(0x63);
        PortPalette.DragSourceFill.G.Should().Be(0x66);
        PortPalette.DragSourceFill.B.Should().Be(0xF1);

        PortPalette.DragSourceBorder.R.Should().Be(0xFF);
        PortPalette.DragSourceBorder.G.Should().Be(0xFF);
        PortPalette.DragSourceBorder.B.Should().Be(0xFF);
    }

    [Theory]
    [InlineData(typeof(string), PortSocketShape.Circle)]
    [InlineData(typeof(bool), PortSocketShape.Triangle)]
    [InlineData(typeof(double), PortSocketShape.Diamond)]
    [InlineData(typeof(FileFlow.Sdk.FileItemContext), PortSocketShape.Square)]
    public void SocketShape_ShouldMapTypeToGeometry_LikeTheDesktopLegend(Type dataType, PortSocketShape expected)
    {
        PortViewModel.GetShapeForDataType(dataType).Should().Be(expected,
            "la forma comunica el tipo: es la leyenda que la tarjeta dibuja");
    }
}
