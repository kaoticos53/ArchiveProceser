using System;
using System.IO;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del CABLE del lienzo Uno (hito 254): las líneas de conexión tienen que tocar sus sockets y seguir
/// tocándolos cuando el plano se mueve o cambia el zoom.
///
/// <para><b>Los dos defectos que guarda</b>, medidos con la sonda <c>ProbeWireTracking</c> y reportados por el
/// usuario («al mover o ajustar el zoom las líneas de conexión se desplazan quedando fuera de su sitio»):</para>
/// <list type="number">
/// <item>La figura del cable abría en el primer punto de control, así que el cable quedaba separado de cada
/// socket y salía invertido —el rulo con forma de «2»— cuando las anclas estaban cerca; su segundo intento
/// añadió los dos tramos rectos del trazo del control de Nodify y el resultado se leía como una <b>Z</b>.</item>
/// <item>El centro del socket se medía transformando el vértice <c>(0, 0)</c> y <b>sumando</b> después la mitad
/// del tamaño, lo que ignora la escala de la cadena: al 125 % el ancla se quedaba corta
/// <c>0,25 · (w/2)</c> y el cable aparecía desplazado en cuanto se tocaba el zoom.</item>
/// </list>
///
/// <para><b>Por qué se censa la fuente</b>: el host Uno (WinUI) no se compila en el suite, así que lo que se
/// guarda es <i>cómo</i> se construye la figura y <i>cómo</i> se mide el ancla; la parte de matemática vive en
/// el núcleo portable (<see cref="FileFlow.App.Services.ConnectionGeometry.WirePath"/>) y ahí sí hay pruebas de
/// comportamiento —el trazado empieza y termina en las anclas— además de la sonda, que lo mide en la app viva.</para>
/// </summary>
public class UnoCanvasWireGuardTests
{
    private const string CanvasCodePath = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs";
    private const string SelfCheckPath = "FileFlow.App.Uno/RuntimeSelfCheck.cs";
    private const string GeometryPath = "FileFlow.App.Core/Services/ConnectionGeometry.cs";
    private const string FigureMutationPath = "mutations/cable-que-no-toca-su-socket.json";
    private const string AnchorMutationPath = "mutations/ancla-que-ignora-la-escala.json";

    /// <summary>El caso que es testigo de la mutación de la figura (la cobertura casa el filtro por nombre).</summary>
    private const string FigureWitnessCase = "TheWireFigure_ShouldBeOneBezier_FromAnchorToAnchor";

    /// <summary>El caso que es testigo de la mutación de la medida del ancla.</summary>
    private const string AnchorWitnessCase = "TheAnchorMeasurement_ShouldTransformTheCenter_NotSumIt";

    private static string CanvasCode() => SourceText.CodeWithoutComments(CanvasCodePath);
    private static string SelfCheck() => SourceText.CodeWithoutComments(SelfCheckPath);
    private static string Geometry() => SourceText.CodeWithoutComments(GeometryPath);

    [Fact]
    public void TheWireFigure_ShouldBeOneBezier_FromAnchorToAnchor()
    {
        string code = CanvasCode();

        code.Should().Contain(
            "StartPoint = ToWindowsPoint(wire.Source),",
            "la figura abre en el ANCLA de salida: abrirla en el cuello deja el cable separado del socket, que " +
            "es el primer defecto medido con la sonda");

        code.Should().Contain(
            "Point1 = ToWindowsPoint(wire.Exit),",
            "el cuello de salida es el primer punto de CONTROL de la curva, no el final de un tramo recto");

        code.Should().Contain(
            "Point2 = ToWindowsPoint(wire.Arrival),",
            "y el de llegada el segundo");

        code.Should().Contain(
            "Point3 = ToWindowsPoint(wire.Target)",
            "la curva muere en el ANCLA de destino: el cable toca sus dos sockets");

        code.Should().NotContain(
            "new LineSegment",
            "ni un tramo recto: los dos bajíos del trazo del control de Nodify son los que se leían como una Z " +
            "en pantalla, que es lo que el usuario pidió quitar. El cable sale del socket ya curvando");

        code.Split("CreateWireGeometry(wire)").Length.Should().Be(
            3,
            "el trazado se construye en UN sitio y se usa en los dos cables (el del grafo y el pendiente del " +
            "arrastre): una figura sin anclas se colaría por el camino que no se probó");

        code.Split("ConnectionGeometry.BuildWire(").Length.Should().Be(
            4,
            "y los tres trazados (el cable del grafo, el pendiente del arrastre y el caso del hueco estrecho de la " +
            "sonda) piden la curva al núcleo compartido, no a puntos de control sueltos");

        Geometry().Should().Contain(
            "public IReadOnlyList<Point> Trace => [Source, Exit, Arrival, Target];",
            "el trazado con las anclas dentro vive en el núcleo portable, que es donde tiene pruebas de " +
            "comportamiento y no sólo censo");

        string mutation = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), FigureMutationPath));
        mutation.Should().Contain(
            $"\"FullyQualifiedName~{FigureWitnessCase}\"",
            "esta guardia es el testigo de la mutación de la figura: es lo que la convierte en una prueba que " +
            "muerde y no en una que se limita a describir el fuente");
    }

    [Fact]
    public void TheAnchorMeasurement_ShouldTransformTheCenter_NotSumIt()
    {
        string code = CanvasCode();

        code.Should().Contain(
            "var localCenter = UnoPointProjection.ToSdk(element.ActualWidth / 2, element.ActualHeight / 2);",
            "el centro que viaja por la cadena es el centro LOCAL del elemento, no su vértice");

        code.Should().Contain(
            "element.TransformToVisual(relativeTo).TransformPoint(new Windows.Foundation.Point(lx, ly))",
            "y se TRANSFORMA: sumarle la mitad del tamaño después de transformar el vértice ignora la escala " +
            "de la cadena, que es el defecto que sólo aparecía al ajustar el zoom");

        code.Should().NotContain(
            "topLeft.X + (element.ActualWidth / 2)",
            "el ancla corta no puede volver: al 125 % se quedaba 0,25 · (w/2) por debajo del centro real y esa " +
            "ancla viajaba al cable");

        // El centro mal medido no sólo movía los cables: es la misma medida del hit-testing del lienzo y de las
        // tarjetas, así que la corrección tiene que estar en el único sitio donde se calcula.
        code.Split("TransformToVisualCenter(").Length.Should().Be(
            7,
            "la medida del centro se declara una vez y tiene cinco usos declarados (las cajas del hit-testing, " +
            "los dos anclas del cable, la tarjeta bajo el puntero y la sonda del foco): una copia con la suma " +
            "vieja se colaría sin que nadie la mida");

        string mutation = File.ReadAllText(Path.Combine(TestRepositoryLocator.RepositoryRoot(), AnchorMutationPath));
        mutation.Should().Contain(
            $"\"FullyQualifiedName~{AnchorWitnessCase}\"",
            "esta guardia es el testigo de la mutación de la medida del ancla");
    }

    [Fact]
    public void TheWireProbe_ShouldMeasureBothGestures_AndTheSelfCheckShouldRunIt()
    {
        string code = CanvasCode();

        code.Should().Contain(
            "internal (bool Before, bool After, bool Crowded, string Detail) ProbeWireTracking()",
            "sin sonda, el defecto sólo se ve con dedos de verdad: el usuario lo reportó así y la medida tiene " +
            "que quedar en la app viva, no en la memoria de nadie");

        code.Should().Contain(
            "CanvasTransform.TranslateX += 140;",
            "la sonda mueve el plano por los mismos mandos que el gesto del puntero");

        code.Should().Contain(
            "ZoomBy(1.25);",
            "y cambia el zoom por los mismos mandos de la rueda y de los botones: los dos gestos que el usuario " +
            "reportó se miden, no se suponen");

        code.Should().Contain(
            "private static bool TryFigureEnds(",
            "el extremo dibujado se lee del ÚLTIMO segmento de la figura, no del último punto de control: con " +
            "la figura de Nodify el cable no acaba en la Bézier");

        code.Should().Contain(
            "private static bool CrowdedShapeFitsTheHueco(out string detail)",
            "la segunda mitad del reporte del usuario («al mover un nodo la parte recta es demasiado grande y se " +
            "ve mal») se mide en la misma sonda: la FORMA del cable en el hueco estrecho, no sólo sus extremos");

        SelfCheck().Should().Contain(
            "la forma del cable cabe en el hueco estrecho (sin el rulo del 2)",
            "el renglón de la forma nombra lo que mide: el rulo con forma de «2» que aparece cuando el cuello no " +
            "cabe en el hueco que queda entre las dos anclas");

        SelfCheck().Should().Contain(
            "canvas.ProbeWireTracking()",
            "el selfcheck corre la sonda en la app viva, que es donde el plano tiene tamaño y las tarjetas están " +
            "materializadas");

        SelfCheck().Should().Contain(
            "el cable dibujado toca su socket con el plano sin mover",
            "el renglón del selfcheck nombra lo que mide: se lee en el informe sin abrir el código");

        SelfCheck().Should().Contain(
            "el cable sigue tocando su socket tras mover el plano y cambiar el zoom",
            "los dos gestos del reporte del usuario tienen su renglón propio");
    }
}
