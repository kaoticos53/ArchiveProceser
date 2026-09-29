using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// La guardia de la FORMA del aparato de prueba del host Uno (hitos 276 y 277): varios pases de arreglos
/// fueron dejando sus medidas dentro de los dos ficheros que ya lo llevaban todo —el sondeo y el panel del
/// inspector—, y esta guardia existe para que no vuelvan: cada preocupación en su archivo, cada capa en su
/// sitio.
///
/// <para><b>Las capas</b> y su casa: la SONDA mide el comportamiento en runtime y afirma —los cuatro modos,
/// cada uno en su proceso y su archivo (<c>SelfCheckCanvas</c>, <c>SelfCheckSettings</c>,
/// <c>SelfCheckControlBar</c>, <c>SelfCheckDialogs</c>), despachados por <c>RuntimeSelfCheck</c>; las
/// preocupaciones que reciben el comprobador de quien las llama (<c>SelfCheckFrame</c>, <c>SelfCheckPanels</c>,
/// <c>SelfCheckPointerless</c>, <c>SelfCheckUia</c>); y el cinturón de medida compartido, que es recorrer el
/// árbol visual, en <c>SelfCheckTree</c>—. La GUARDIA de fuente fija la regla en el árbol de pruebas y es el
/// testigo de su mutación (los <c>Uno*GuardTests</c>); la MUTACIÓN demuestra que la guardia muerde
/// (<c>mutations/</c>).</para>
///
/// <para><b>Por qué guarda la fuente y no el runtime</b>: el sondeo corre en la aplicación arrancada —no en
/// la sesión de pruebas—, así que lo que aquí se fija es la FORMA: qué archivo alberga qué. Lo que cada modo
/// AFIRMA en la app viva lo vigilan sus propias guardias, no ésta.</para>
/// </summary>
public class UnoSelfCheckLayoutGuardTests
{
    private const string OrchestratorPath = "FileFlow.App.Uno/RuntimeSelfCheck.cs";
    private const string TreePath = "FileFlow.App.Uno/SelfCheckTree.cs";
    private const string CanvasPath = "FileFlow.App.Uno/SelfCheckCanvas.cs";
    private const string FramePath = "FileFlow.App.Uno/SelfCheckFrame.cs";
    private const string PanelsPath = "FileFlow.App.Uno/SelfCheckPanels.cs";
    private const string PointerlessPath = "FileFlow.App.Uno/SelfCheckPointerless.cs";
    private const string PanelPath = "FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs";
    private const string ProbesPath = "FileFlow.App.Uno/Controls/NodeInspectorPanel.Probes.cs";

    /// <summary>Los modos con proceso propio: cada uno arranca desde la línea de comandos y tiene su informe.</summary>
    private static readonly (string Name, string Path, string Report)[] OwnProcessModes =
    [
        ("SelfCheckSettings", "FileFlow.App.Uno/SelfCheckSettings.cs", "selfcheck-settings-report.txt"),
        ("SelfCheckControlBar", "FileFlow.App.Uno/SelfCheckControlBar.cs", "selfcheck-controlbar-report.txt"),
        ("SelfCheckDialogs", "FileFlow.App.Uno/SelfCheckDialogs.cs", "selfcheck-dialogs-report.txt"),
    ];

    /// <summary>Todos los modos, con el del lienzo: su informe lo escribe el despachador, por eso no lo declara.</summary>
    private static readonly string[] ModePaths =
    [
        CanvasPath,
        "FileFlow.App.Uno/SelfCheckSettings.cs",
        "FileFlow.App.Uno/SelfCheckControlBar.cs",
        "FileFlow.App.Uno/SelfCheckDialogs.cs",
    ];

    private static string Code(string path) => SourceText.CodeWithoutComments(path);

    [Fact]
    public void TheDispatcher_ShouldOnlyDispatch()
    {
        string orchestrator = Code(OrchestratorPath);
        string canvas = Code(CanvasPath);

        // El despachador arranca el modo del lienzo y nada más: los otros tres los arranca la línea de
        // comandos, y ninguno de los cuatro vive aquí (era un fichero con los cuatro modos dentro).
        orchestrator.Should().Contain("SelfCheckCanvas.Run(window, lastReport)",
            "el despachador corre el modo del lienzo por su nombre, no copiando su recorrido");
        foreach (string name in new[] { "SelfCheckSettings", "SelfCheckControlBar", "SelfCheckDialogs" })
        {
            orchestrator.Should().NotContain(name,
                $"el modo '{name}' vive en su archivo: el despachador ni lo contiene ni lo llama");
        }

        // Y la delegación se hace pasando el comprobador, como el marco y los paneles.
        canvas.Should().Contain("SelfCheckFrame.Check(window, Check)",
            "el marco se mide en SelfCheckFrame y el modo del lienzo sólo le pasa su comprobador");
        canvas.Should().Contain("SelfCheckPanels.Check(",
            "y los dos paneles, en SelfCheckPanels: el modo del lienzo no vuelve a albergarlos");
        canvas.Should().Contain("SelfCheckPointerless.Check(canvas, Check)",
            "lo que el puntero no puede recorrer se mide en SelfCheckPointerless, con el mismo trato");
    }

    [Fact]
    public void EachMode_ShouldHaveItsOwnFile_AndItsOwnVerdictFile()
    {
        foreach (var (name, path, report) in OwnProcessModes)
        {
            string mode = Code(path);
            mode.Should().Contain($"internal static class {name}",
                $"la preocupación '{name}' declara su propia clase en su archivo");
            mode.Should().Contain("public static int Run(Window window, DispatcherQueue dispatcher)",
                $"'{name}' es un modo: su punto de entrada lo arranca la línea de comandos");
            mode.Should().Contain(report,
                $"el veredicto de '{name}' queda en su informe, como el de las otras sondas");
            foreach (var (_, otherPath, _) in OwnProcessModes)
            {
                if (otherPath == path) continue;
                Code(otherPath).Should().NotContain(report,
                    $"y ese informe no se escribe desde {otherPath}: un veredicto con dos casas deja de decir cuál");
            }
        }

        // El modo del lienzo: su veredicto lo escribe el despachador (es él quien tiene el fichero), pero el
        // recorrido y el comprobador son suyos.
        Code(CanvasPath).Should().Contain("internal static bool Run(Window window, StringBuilder report)",
            "el modo del lienzo devuelve su veredicto al despachador, que es quien lo escribe");
        Code(OrchestratorPath).Should().Contain("selfcheck-report.txt",
            "y ese informe lo escribe el despachador, que es su único dueño");
    }

    [Fact]
    public void EachConcern_ShouldMeasureInItsOwnFile_AndOnlyThere()
    {
        // Cada medida con una sola casa: si reaparece fuera de la suya, es que se copió.
        var homes = new (string Path, string Measure)[]
        {
            (CanvasPath, "canvas.ProbeHitAreas()"),
            (PointerlessPath, "canvas.ProbeWireTracking()"),
            (PointerlessPath, "canvas.ProbeUiAccessibility()"),
            (PointerlessPath, "canvas.ProbeKeyboardReclaim()"),
            (FramePath, "FrameWorkspace"),
            (FramePath, "DragBy"),
            (PanelsPath, "ChipBoxesForProbe()"),
            (PanelsPath, "insp.ProbeTelemetrySection()"),
            (PanelsPath, "insp.ProbeSnapshotTabs()"),
        };

        foreach (var (path, measure) in homes)
        {
            Code(path).Should().Contain(measure, $"la medida '{measure}' vive en su archivo, junto a su instrumento");
            foreach (string other in new[] { OrchestratorPath, CanvasPath, PointerlessPath, TreePath })
            {
                if (other == path) continue;
                Code(other).Should().NotContain(measure,
                    $"y no se repite en {other}: una medida con dos casas es una medida que puede divergir");
            }
        }
    }

    [Fact]
    public void TheTreeWalk_ShouldHaveASingleHome()
    {
        // Recorrer el árbol visual es de WinUI y tiene una sola casa: el cinturón. Los modos lo piden.
        Code(TreePath).Should().Contain("VisualTreeHelper.GetChildrenCount",
            "el recorrido del árbol vive en SelfCheckTree, que es lo único que el sondeo comparte");
        foreach (string path in ModePaths)
        {
            Code(path).Should().NotContain("VisualTreeHelper",
                $"nadie recorre el árbol visual por su cuenta ({path}): se le pide a SelfCheckTree");
        }

        Code(CanvasPath).Should().Contain("SelfCheckTree.Find<",
            "los modos llegan al árbol por el cinturón compartido, no por su propia copia");
        Code("FileFlow.App.Uno/SelfCheckDialogs.cs").Should().Contain("SelfCheckTree.FindAll<",
            "y cuando necesitan todos los de un tipo, también: el recorrido es el mismo para los cuatro");
    }

    [Fact]
    public void TheInspectorPanel_ShouldKeepItsObservationSurfaceOutOfTheViewFile()
    {
        string panel = Code(PanelPath);
        string probes = Code(ProbesPath);

        // La vista declara la mitad que pinta; el instrumento, la que observa. Ambas mitades de la MISMA clase.
        panel.Should().Contain("public sealed partial class NodeInspectorPanel",
            "el fichero de la vista declara la clase como parcial: su otra mitad es el instrumento");
        probes.Should().Contain("public sealed partial class NodeInspectorPanel",
            "y el instrumento declara la misma clase parcial: son dos mitades, no dos copias");

        foreach (string member in new[]
                 {
                     "ProbeTelemetrySection", "ProbeTestButtonOffer", "ProbeSnapshotTabs",
                     "TabButtonBoxesForProbe", "InspectForProbe", "CloseViaCommand", "HasWiredTestButton"
                 })
        {
            probes.Should().Contain(member,
                $"la superficie de observación ('{member}') vive en su archivo");
            panel.Should().NotContain(member,
                $"y la vista no la alberga: '{member}' volvería a mezclar el instrumento con lo que pinta");
        }
        // El error simétrico —mover al instrumento algo que la aplicación usa— lo vigila la guardia de la
        // sección de Telemetría, que exige el montaje en el fichero de la vista: aquí no se repite.
    }

    [Fact]
    public void TheProbes_ShouldMeasureAndNotJudge()
    {
        // Quien decide el veredicto es el sondeo: el instrumento devuelve medidas, no [OK]/[FALLO]. Es la
        // frontera que impide que una sonda se afirme a sí misma (y que un fallo suyo pase por bueno).
        Code(ProbesPath).Should().NotContain("[OK]",
            "el instrumento no escribe veredictos: los escribe el sondeo que lo recorre");
        Code(ProbesPath).Should().NotContain("[FALLO]",
            "ni el [FALLO] que declararía la medida rota por su cuenta");

        foreach (string path in new[] { FramePath, PanelsPath, PointerlessPath, TreePath })
        {
            Code(path).Should().NotContain("report.AppendLine",
                $"una preocupación del sondeo afirma por el comprobador que recibe ({path}), no por el informe");
        }
    }
}
