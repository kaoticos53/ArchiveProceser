using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using static FileFlow.Tests.TestHelpers.UnoGeometryBindingScanner;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia de la regla del plan Uno (riesgo #1, escrita <b>antes</b> de que exista el XAML): cada enlace de
/// geometría del XAML del host Uno debe llevar <c>conv:UnoPointConverter.Instance</c>.
///
/// <para><b>Por qué ahora</b>: el host aún no tiene ni un enlace de geometría (la fase 3.1 del plan escribirá
/// el lienzo), y esa es la ventana para blindar la regla sin excepciones históricas. La lección del hito 211:
/// el conversor por defecto del motor de enlaces no traduce entre <see cref="FileFlow.Sdk.Point"/> y el punto
/// del framework, el enlace muere en silencio y el síntoma —un lienzo sin nodos— cae lejos de la causa.</para>
///
/// <para><b>Qué se afirma</b>: (1) el conversor existe y proyecta en los dos sentidos; (2) el árbol real del
/// host no tiene ni un enlace de geometría sin conversor — hoy son cero enlaces, mañana serán los de la fase
/// 3.1 con su conversor ya puesto; (3) el escáner detecta cada forma de escribir un enlace de geometría
/// (Location, Anchor, Source, Target, ViewportLocation, el spotlight) con snippets sintéticos.</para>
/// </summary>
public class UnoGeometryBindingGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FileFlow.slnx")))
        {
            dir = dir.Parent!;
        }
        return dir!.FullName;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La guardia sobre el árbol real del host Uno
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryGeometryBindingInTheUnoHost_ShouldCarryThePointConverter()
    {
        var violations = new System.Collections.Generic.List<string>();

        foreach (string file in UnoXamlFiles(RepoRoot()))
        {
            string relative = Path.GetRelativePath(RepoRoot(), file).Replace('\\', '/');
            string source = File.ReadAllText(file);

            foreach (var scan in Scan(source).Where(finding => !finding.HasConverter))
            {
                violations.Add(
                    $"{relative}:{scan.Line}: el enlace de '{scan.Path}' mueve un Sdk.Point sin conversor — " +
                    "añade Converter={x:Static conv:UnoPointConverter.Instance} (los ViewModels hablan Sdk.Point " +
                    "y el motor de enlaces de WinUI no traduce: sin conversor el enlace muere en silencio, hito 211).");
            }
        }

        violations.Should().BeEmpty(
            "la regla del plan Uno (riesgo #1) exige la proyección explícita en cada enlace de geometría del host: " +
            "la guardia nació antes del primer enlace para que no exista ninguna excepción histórica");
    }

    [Fact]
    public void TheUnoHost_ShouldShipThePointConverter()
    {
        string converter = Path.Combine(RepoRoot(), "FileFlow.App.Uno", "Platform", "UnoPointConverter.cs");
        File.Exists(converter).Should().BeTrue("el conversor que la regla exige tiene que existir en el host");

        string source = File.ReadAllText(converter);
        source.Should().Contain("IValueConverter", "el XAML de WinUI sólo puede citar un IValueConverter");
        source.Should().Contain("UnoPointProjection.ToUno(", "la traducción vive en el núcleo portable, el host sólo envuelve");
        source.Should().Contain("UnoPointProjection.ToSdk(", "y el viaje de vuelta también pasa por el núcleo");
    }

    [Fact]
    public void TheCanvasCodeBehind_ShouldPositionThroughTheProjection_NotThroughRawLocationReads()
    {
        // Donde WinUI no puede enlazar (Setter con Binding no evalúa), la posición la aplica el código:
        // ese sitio debe leer la posición YA proyectada (NodeCardViewModel.Position, que pasa por el
        // conversor). Una lectura directa de node.Location.X es el defecto del 211 en código.
        string codeBehind = Path.Combine(RepoRoot(), "FileFlow.App.Uno", "Controls", "EditorCanvasControl.xaml.cs");
        File.Exists(codeBehind).Should().BeTrue("el lienzo de la fase 3.1 debe existir");

        var violations = FindCodeBehindViolations(File.ReadAllText(codeBehind));

        violations.Should().BeEmpty(
            "la proyección explícita es una regla del plan (riesgo #1) y se aplica también donde el enlace no llega: en el código");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Auto-tests del escáner con snippets sintéticos
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Location=\"{Binding Location, Mode=TwoWay}\"", "Location")]
    [InlineData("Anchor=\"{Binding Anchor, Mode=OneWayToSource}\"", "Anchor")]
    [InlineData("Source=\"{Binding Source.Anchor}\"", "Source")]
    [InlineData("Target=\"{Binding Target.Anchor, Mode=OneWay}\"", "Target")]
    [InlineData("ViewportLocation=\"{Binding ViewportLocation, Mode=TwoWay}\"", "ViewportLocation")]
    [InlineData("TargetLocation=\"{Binding TargetLocation, Mode=TwoWay}\"", "TargetLocation")]
    public void Scanner_ShouldFlagEveryGeometryBindingWithoutTheConverter(string xamlLine, string path)
    {
        var findings = Scan(xamlLine);

        findings.Should().ContainSingle("un enlace de geometría sin conversor es exactamente el defecto del 211")
            .Which.Should().Match<GeometryBindingScan>(scan =>
                !scan.HasConverter && scan.Path == path);
    }

    [Theory]
    [InlineData("Location=\"{Binding Location, Mode=TwoWay, Converter={x:Static conv:UnoPointConverter.Instance}}\"")]
    [InlineData("Anchor=\"{Binding Anchor, Mode=OneWayToSource, Converter={x:Static conv:UnoPointConverter.Instance}}\"")]
    [InlineData("Source=\"{Binding Source.Anchor, Converter={x:Static conv:UnoPointConverter.Instance}}\"")]
    public void Scanner_ShouldAcceptEveryGeometryBindingWithTheConverter(string xamlLine)
    {
        Scan(xamlLine).Should().ContainSingle().Which.HasConverter.Should().BeTrue();
    }

    [Fact]
    public void Scanner_ShouldNotFlagBindingsThatDoNotMovePoints()
    {
        string innocent =
            """
            <TextBlock Text="{Binding Title}" />
            <TextBox Text="{Binding EditingTitleText, Mode=TwoWay}" />
            <Border Visibility="{Binding HasCanvasNotice, Converter={StaticResource BoolToVisibility}}" />
            <controls:NodeCardView Width="{Binding Width}" />
            """;

        Scan(innocent).Should().BeEmpty("las propiedades que no son puntos no están censadas");
    }

    [Fact]
    public void Scanner_ShouldScanTheWholeHostSurface()
    {
        var files = UnoXamlFiles(RepoRoot()).ToList();

        files.Should().NotBeEmpty("el host Uno tiene XAML que barrer");
        files.Should().Contain(f => f.EndsWith("MainWindow.xaml", StringComparison.OrdinalIgnoreCase),
            "la ventana principal es parte de la superficie vigilada");
        files.Should().Contain(f => f.EndsWith("EditorCanvasControl.xaml", StringComparison.OrdinalIgnoreCase),
            "el lienzo de la fase 3.1 es parte de la superficie vigilada");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Auto-tests del censo de code-behind (el equivalente de los enlaces donde WinUI no puede enlazar)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CodeBehindScan_ShouldFlagRawLocationReads()
    {
        string source = "Canvas.SetLeft(container, node.Location.X);";

        FindCodeBehindViolations(source)
            .Should().ContainSingle("leer Location.X del ViewModel para posicionar es el defecto del 211 en código");
    }

    [Fact]
    public void CodeBehindScan_ShouldFlagHandmadeFrameworkPoints()
    {
        string source = "return new Windows.Foundation.Point(point.X, point.Y);";

        FindCodeBehindViolations(source)
            .Should().ContainSingle("construir el punto del framework a mano es el cruce sin proyección: la pieza que puede olvidarse del conversor");
    }

    [Fact]
    public void CodeBehindScan_ShouldAcceptProjectedPositionReads()
    {
        string source = "Canvas.SetLeft(container, card.Position.X);";

        FindCodeBehindViolations(source).Should().BeEmpty(
            "Position es la proyección del adaptador, que pasa por el conversor: es lo que la regla exige");
    }
}
