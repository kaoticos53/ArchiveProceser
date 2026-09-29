using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del ESPACIO del hit-testing del lienzo Uno (hito 249): el área de clic de las tarjetas tiene
/// que coincidir con lo que está DIBUJADO.
///
/// <para><b>El defecto que guarda</b>: <c>VisualTreeHelper.FindElementsInHostCoordinates</c> espera el
/// punto en el espacio de la RAÍZ del contenido, y el lienzo vive en la columna 1 (cajón de 280) y la fila
/// 1 (barra superior) de <c>MainWindow.xaml</c>. Pasarle el punto relativo al lienzo desplazaba la sonda
/// esa posición —medido en el hito 247 con puntero real: <c>(280, 41)</c>— y con ella el área de clic de
/// TODAS las tarjetas: clicar la cara de una seleccionaba OTRA. Ni la suite ni las sondas lo veían, porque
/// el hit-testing vive en el árbol visual.</para>
///
/// <para><b>Por qué guarda la fuente y no el runtime</b>: el lienzo es WinUI y no se materializa en la
/// sesión de pruebas (la lección del 232). Lo que sí se puede censar es que NINGUNA llamada de hit-testing
/// del control se salte el cruce de espacio — la lección del 217 (ningún cruce de puntos hecho a mano)
/// aplicada al hit-testing — y que la sonda que mide el área de clic entre por el MISMO <c>CardAt</c> que
/// los handlers, para que no pueda medir un camino que el usuario no recorre.</para>
/// </summary>
public class UnoHitTestSpaceGuardTests
{
    private const string CanvasCodePath = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs";
    private const string SelfCheckPath = "FileFlow.App.Uno/SelfCheckCanvas.cs";
    private const string MutationPath = "mutations/hit-test-en-el-espacio-equivocado.json";
    private const string HitTestApi = "FindElementsInHostCoordinates(";

    private static string CanvasCode() => SourceText.CodeWithoutComments(CanvasCodePath);

    /// <summary>
    /// Las llamadas de hit-testing del control, con el argumento con el que entran al API y su línea.
    /// </summary>
    private static IReadOnlyList<(int Line, string Argument)> HitTestCallSites()
    {
        string code = CanvasCode();
        var sites = new List<(int, string)>();
        int start = 0;

        while (true)
        {
            int index = code.IndexOf(HitTestApi, start, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            int argumentStart = index + HitTestApi.Length;
            int argumentEnd = code.IndexOf(')', argumentStart);
            sites.Add((
                code.Take(index).Count(c => c == '\n') + 1,
                argumentEnd < 0 ? "" : code[argumentStart..argumentEnd].Trim()));
            start = argumentStart;
        }

        return sites;
    }

    /// <summary>
    /// El censo del hit-testing del lienzo: cada punto de entrada del gesto y el espacio con el que entra.
    /// Es la tabla que obliga a que una llamada nueva se declare, en vez de aparecer sin espacio.
    /// </summary>
    private static IReadOnlyList<(string EntryPoint, string Space)> HitTestCensus() =>
    [
        ("CardAt (la tarjeta bajo el puntero: selección, arrastre y doble clic)",
            "lienzo -> raíz, vía PointInHostSpace"),
        ("HitsInteractiveControl (la guardia de la barra de zoom: ni pan ni rubber band encima)",
            "lienzo -> raíz, vía PointInHostSpace"),
    ];

    [Fact]
    public void EveryHitTestCall_ShouldCrossToTheHostSpace_ThroughTheSingleConversionHelper()
    {
        var sites = HitTestCallSites();

        sites.Should().NotBeEmpty(
            "el lienzo resuelve tarjetas y controles interactivos por hit-testing: sin llamadas, esta " +
            "guardia estaría vigilando nada");

        sites.Should().OnlyContain(
            site => site.Argument.StartsWith("PointInHostSpace(", StringComparison.Ordinal),
            "el API de hit-testing espera el punto de la RAÍZ del contenido: pasarle el del lienzo " +
            "desplaza el área de clic la posición del control en la ventana, que es el defecto que la " +
            "sesión con puntero real midió en el 247 (clicar la cara de una tarjeta seleccionaba otra)");
    }

    [Fact]
    public void TheSpaceConversion_ShouldLiveInOnePlace_AndGoThroughTheVisualTree()
    {
        string code = CanvasCode();

        code.Should().Contain(
            "private Windows.Foundation.Point PointInHostSpace(Windows.Foundation.Point canvasPoint)",
            "el cruce de espacio es UN sitio: dos cruces distintos serían dos formas de equivocarse, y " +
            "el que se olvidara en una rama volvería a desplazar el área de clic");

        code.Should().Contain(
            "var toRoot = RootGrid.TransformToVisual(null);",
            "el cruce se pregunta al árbol visual (TransformToVisual), como el resto de cruces del host: " +
            "la posición del lienzo dentro de la ventana no se puede suponer, se mide");

        code.Should().Contain(
            "return toRoot is null ? canvasPoint : toRoot.TransformPoint(canvasPoint);",
            "el transform nulo (el control aún sin enganchar al árbol) se resuelve devolviendo el punto " +
            "tal cual: una excepción ahí tumbaría el primer gesto de la sesión, justo antes del layout");

        HitTestCensus().Should().HaveCount(
            HitTestCallSites().Count,
            "el censo declara TODAS las llamadas de hit-testing: una llamada nueva sin declarar es " +
            "exactamente la que entraría con el espacio equivocado sin que nadie lo revisara");

        HitTestCensus().Should().OnlyContain(
            row => row.Space.Contains("raíz", StringComparison.Ordinal),
            "todo punto de entrada del gesto cruza al espacio del host; ninguno entra con el del lienzo");

        foreach (var (entryPoint, _) in HitTestCensus())
        {
            string name = entryPoint.Split(' ')[0];
            code.Should().Contain(
                name + "(Windows.Foundation.Point position)",
                $"el censo cita {name} como punto de entrada del gesto y esa firma tiene que existir");
        }
    }

    [Fact]
    public void TheClickAreaProbe_ShouldMeasureTheDrawnGeometry_ThroughTheHandlersOwnHitTest()
    {
        string code = CanvasCode();
        string selfCheck = SourceText.CodeWithoutComments(SelfCheckPath);

        code.Should().Contain(
            "internal (int Measured, int Matched, string Detail) ProbeHitAreas()",
            "sin sonda, el área de clic solo se puede medir con un puntero real: el 247 necesitó una " +
            "sesión humana para ver el desplazamiento que esta sonda caza sola");

        code.Should().Contain(
            "var centre = TransformToVisualCenter(pair.Value, RootGrid);",
            "la sonda mide el centro DIBUJADO de la tarjeta del árbol visual, no la posición del modelo: " +
            "es la única forma de comparar lo que se ve con lo que se clica");

        code.Should().Contain(
            "var hit = CardAt(centre);",
            "la sonda entra por el MISMO CardAt que los handlers: medir por otro camino certificaría un " +
            "area de clic que el usuario no recorre");

        selfCheck.Should().Contain(
            "var (clickMeasured, clickMatched, clickDetail) = canvas.ProbeHitAreas();",
            "el selfcheck corre la sonda en la app viva, que es donde el hit-testing existe");

        selfCheck.Should().Contain(
            "el área de clic coincide con la tarjeta dibujada",
            "el renglón del selfcheck nombra lo que mide: se lee en el informe sin abrir el código");
    }

    [Fact]
    public void TheClickAreaProbe_ShouldHaveAMutationThatBites_ThroughThisGuard()
    {
        string mutation = File.ReadAllText(
            Path.Combine(TestRepositoryLocator.RepositoryRoot(), MutationPath));

        // El testigo se declara por el NOMBRE DEL CASO (así lo ata la cobertura de mutaciones, que casa el
        // filtro contra los bloques del suite): citar la clase no lo ataría a ningún caso concreto.
        mutation.Should().Contain(
            "\"FullyQualifiedName~EveryHitTestCall_ShouldCrossToTheHostSpace_ThroughTheSingleConversionHelper\"",
            "esta guardia es el testigo de la mutación del área de clic: es lo que convierte la guardia " +
            "en una prueba que muerde, y no en una que se limita a describir el fuente");
    }
}
