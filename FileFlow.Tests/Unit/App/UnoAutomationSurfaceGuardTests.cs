using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia de la superficie UIA del lienzo Uno (hito 238): el lienzo y la barra de zoom exponen
/// AutomationIds explícitos (los estables, independientes del x:Name renombrable) y el lienzo tiene
/// peer de automatización para que la observación externa (pywinauto, sin UIAccess) pueda darle el
/// foco — el paso que el acotamiento del hito 237 dejó en el puntero del usuario.
///
/// <para><b>Por qué guarda la fuente y no el runtime</b>: el lienzo es WinUI y no se materializa en
/// la sesión de pruebas (la lección del lienzo, hito 232). El riesgo real es que una limpieza o un
/// renombrado deje el árbol sin las anclas y las sondas QA (237) pierdan su vía de observación sin
/// que la compilación se entere. La cura es que la fuente CANTE las anclas (retira comentarios antes
/// de buscar: la lección del 165) y que la tabla de anclas cite pruebas que existen.</para>
/// </summary>
public class UnoAutomationSurfaceGuardTests
{
    private const string CanvasXamlPath = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml";
    private const string CanvasCodePath = "FileFlow.App.Uno/Controls/EditorCanvasControl.xaml.cs";

    private static string CanvasXaml() => SourceText.CodeWithoutComments(CanvasXamlPath);
    private static string CanvasCode() => SourceText.CodeWithoutComments(CanvasCodePath);

    [Fact]
    public void TheCanvas_ShouldExposeItsExplicitAnchor_AndAFocusableAutomationPeer()
    {
        string xaml = CanvasXaml();
        string code = CanvasCode();

        xaml.Should().Contain(
            "AutomationProperties.AutomationId=\"CanvasRoot\"",
            "la ancla explícita del lienzo es el contrato estable de las sondas UIA: un x:Name se " +
            "podría renombrar sin que la compilación avise y las sondas perderían su vía en silencio");

        code.Should().Contain(
            "protected override AutomationPeer OnCreateAutomationPeer() => new CanvasAutomationPeer(this);",
            "sin peer de automatización el contenedor (UserControl + Grid) no expone NADA por UIA — " +
            "el árbol del 237 llegaba a las tarjetas pero no a la superficie que recibe el foco y el teclado");

        code.Should().Contain(
            "class CanvasAutomationPeer(EditorCanvasControl owner) : FrameworkElementAutomationPeer(owner)",
            "el peer canta control y contenido (IsControlElementCore/IsContentElementCore): la " +
            "observación externa lo ve y el SetFocus de UIA tiene a quién entregarle el foco");
    }

    [Fact]
    public void TheCanvas_ShouldKeepItselfTabStoppable_AndTheFocusableSurfaceInTheTree()
    {
        string xaml = CanvasXaml();

        xaml.Should().Contain(
            "IsTabStop=\"True\"",
            "el UserControl no es enfocable por defecto: sin IsTabStop ni el foco programático ni el " +
            "de tabulación entran en la superficie (el bloqueo del foco que el 237 acotó)");

        xaml.Should().Contain(
            "AutomationProperties.AutomationId=\"CanvasSurface\"",
            "la superficie con los handlers de puntero lleva su propia ancla: el observador externo " +
            "distingue la raíz enfocable del plano con los gestos");
    }

    [Fact]
    public void TheZoomBar_ShouldExposeItsAnchor_ItsLevel_AndItsThreeButtons()
    {
        string xaml = CanvasXaml();

        xaml.Should().Contain(
            "AutomationProperties.AutomationId=\"ZoomBar\"",
            "la barra entera lleva ancla: una sonda la localiza sin recorrer sus hijos a ciegas");

        xaml.Should().Contain(
            "AutomationProperties.AutomationId=\"ZoomLevelText\"",
            "el nivel de zoom es el ESTADO observable de la barra: la sonda B del 237 buscaba el " +
            "texto del buscador a ciegas — con ancla propia, el nivel se lee por nombre");

        xaml.Should().Contain(
            "AutomationProperties.AutomationId=\"ZoomInButton\"",
            "los tres botones con ancla explícita: InvokePattern sin descifrar qué 'Button' es cuál");

        xaml.Should().Contain(
            "AutomationProperties.AutomationId=\"ZoomOutButton\"",
            "el botón de alejar con su ancla (el par del de acercar, y el 'Ajustar' con la suya)");
    }

    [Fact]
    public void TheGraphPlane_ShouldKeepItsAnchor_NamedByResourceNotByRepetition()
    {
        string xaml = CanvasXaml();

        xaml.Should().Contain(
            "AutomationProperties.AutomationId=\"CanvasGraphPlane\"",
            "el plano del grafo (pan/zoom, transform del viewport) es el segundo ancla del lienzo: " +
            "el estado de la cámara se lee donde vive");

        xaml.Should().Contain(
            "<x:String x:Key=\"UiAnchorCanvas\">CanvasRoot</x:String>",
            "las anclas viven como recursos nombrados: renombrar una es tocar UNA línea, no cazar " +
            "literales repetidos");

        xaml.Should().Contain(
            "<x:String x:Key=\"UiAnchorZoomLevel\">ZoomLevelText</x:String>",
            "la ancla del nivel de zoom también es recurso nombrado (la guardia vigila el contrato, " +
            "el recurso guarda el valor)");
    }

    [Fact]
    public void TheCanvas_ShouldVerifyItsOwnAnchor_InTheRuntimeSelfCheck()
    {
        string code = CanvasCode();

        code.Should().Contain(
            "internal (bool AnchorExposed, bool FocusEntered, bool ZoomStateObservable) ProbeUiAccessibility()",
            "la sonda de la superficie UIA vive en el lienzo: el selfcheck la corre en la app viva " +
            "con la misma firma que la guardia defiende");

        code.Should().Contain(
            "FrameworkElementAutomationPeer.CreatePeerForElement(this) is not null",
            "la sonda comprueba el peer expuesto, no sólo la propiedad: es lo que una sonda externa " +
            "necesita para SetFocus");

        string selfcheck = SourceText.CodeWithoutComments("FileFlow.App.Uno/SelfCheckPointerless.cs");

        selfcheck.Should().Contain(
            "canvas.ProbeUiAccessibility()",
            "el selfcheck corre la sonda: sin esa línea, las anclas podrían desaparecer del árbol " +
            "vivo sin que el sondeo se enterara");
    }

    [Fact]
    public void TheExternalUiaProbeMode_ShouldBeWired_WithTheHouseInstrumentAndHonestVerdicts()
    {
        string app = SourceText.CodeWithoutComments("FileFlow.App.Uno/App.xaml.cs");

        app.Should().Contain(
            "Environment.GetCommandLineArgs().Contains(\"--selfcheck-uia\", StringComparer.Ordinal)",
            "el modo vive en la línea de comandos como --selfcheck: sin la rama, la opción es un " +
            "argumento muerto y nadie puede pedir la observación externa");

        app.Should().Contain(
            "SelfCheckUia.Run(s_mainWindow);",
            "la rama lanza el sondeo externo — y ANTES del if de --selfcheck, para que un argumento " +
            "--selfcheck-uia jamás entre en el sondeo interno (el add/remove masivo lo deja frágil); " +
            "desde el 245 recibe la ventana para montar la escena del inspector antes del hijo");

        string mode = SourceText.CodeWithoutComments("FileFlow.App.Uno/SelfCheckUia.cs");

        mode.Should().Contain(
            "new Thread(() =>",
            "la espera del hijo corre en hilo de fondo: UIA responde por los mensajes de la ventana " +
            "(WM_GETOBJECT) y un proceso bloqueado no despacha — la observación externa moriría con " +
            "timeout");

        mode.Should().Contain(
            "FILEFLOW_UIA_TARGET_PID",
            "la app pasa su pid al instrumento: la sonda conecta al proceso vivo, no a una copia " +
            "de otro lanzamiento");

        mode.Should().Contain(
            "ResolveProbePath()",
            "el instrumento es el de la casa (docs/qa/selfcheck_uia_probe.py) con FILEFLOW_UIA_PROBE " +
            "para apuntar a otro: un modo que no declara su instrumento no es reproducible");
    }

    [Fact]
    public void TheExternalUiaProbe_ShouldObserveTheInspectorTabs_WithTheAppMountedFixture()
    {
        string probe = SourceText.CodeWithoutComments("docs/qa/selfcheck_uia_probe.py");

        probe.Should().Contain(
            "\"InspectorTabParams\", \"InspectorTabSnapshots\", \"InspectorTabInputs\"",
            "las cinco pestañas del Pivot del inspector son anclas del sondeo externo: sin ellas el " +
            "observador no distingue qué pestaña vive en el árbol (el 244 las separó con AID propio)");

        probe.Should().Contain(
            "FILEFLOW_UIA_FIXTURE_SIGNAL",
            "la escena la monta la app y la señal la canta el fichero (S0): el observador llega a " +
            "escena quieta y el veredicto declara si el fixture cayó en vez de fingir");

        probe.Should().Contain(
            "UIA_SelectionItemPatternId",
            "la conmutación de pestaña es por el patrón SelectionItem: el Invoke de UIA no dispara el " +
            "cambio del Pivot de WinUI (la frontera medida del 231/243) — fingir un click no es observar");

        probe.Should().Contain(
            "\"InspectorDiffKey_Category\"",
            "la fila de diff del fixture es observable por su AutomationId: la clave con peer es la " +
            "fila viva del árbol, y el switch de Diff es el ÚNICO del sondeo (la pestaña ligera " +
            "sobrevive a la frontera medida)");

        probe.Should().Contain(
            "LATENTE",
            "el contenido de snapshots se declara LATENTE para el canal externo: la frontera medida " +
            "del 245 (materializado y en pie tumba al proveedor UIA) se declara en el veredicto — " +
            "honestidad medida, no cobertura fingida");
    }

    [Fact]
    public void TheInspector_ShouldMountTheUiaExternalFixture_WithRealSnapshots()
    {
        string runtime = SourceText.CodeWithoutComments("FileFlow.App.Uno/SelfCheckUia.cs");
        string app = SourceText.CodeWithoutComments("FileFlow.App.Uno/App.xaml.cs");
        string panel = SourceText.CodeWithoutComments("FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs");

        runtime.Should().Contain(
            "public static bool MountUiaExternalScene(Window? window)",
            "el fixture lo monta la APP antes de lanzar al hijo: el observador externo observa, no " +
            "manipula — su ventana al árbol es la lectura, no la escritura");

        runtime.Should().Contain(
            "NodeDataSnapshot.CreateInput(firstNode.Id, \"In\", probeItem)",
            "el snapshot del fixture es de la vía de producción (la misma fábrica que usa el motor): son " +
            "los datos reales que el observador va a contar, no una escena falsificada");

        runtime.Should().Contain(
            "Thread.Sleep(4000);",
            "el asentamiento SIN cliente: la materialización del contenido con Expander dispara la " +
            "tormenta de eventos UIA que, con cliente conectado, tumba el proceso (la muerte medida " +
            "del 245) — el hijo llega a escena quieta");

        app.Should().Contain(
            "SelfCheckUia.Run(s_mainWindow);",
            "el modo monta la escena ANTES de lanzar el sondeo: sin escena, las pestañas del inspector " +
            "nacerían vacías y el veredicto sería falso");

        runtime.Should().Contain(
            "MountUiaExternalScene(mainWindow);",
            "el orden que la medición impuso vive dentro del modo: app → escena → observador");

        panel.Should().Contain(
            "AutomationProperties.SetAutomationId(expander,",
            "cada tarjeta canta su colección y su índice EN EL EXPANDER (con peer): un StackPanel raíz " +
            "sin peer no materializa en el árbol UIA (la lección del 238) — el selfcheck interno " +
            "verifica la paridad y el AID es el contrato para cuando la plataforma abra la frontera");
    }

    [Fact]
    public void TheUiAnchorTable_ShouldCiteRealSuiteTests()
    {
        var suiteNames = TestSuiteIndex.MethodNames(TestRepositoryLocator.RepositoryRoot());

        var unknown = AnchorParity()
            .Where(row => !suiteNames.Contains(row.Test))
            .Select(row => $"{row.Anchor} -> {row.Test}")
            .ToList();

        unknown.Should().BeEmpty(
            "la tabla de anclas UIA cita pruebas que deben existir: una cita que no casa se leería " +
            "como cobertura donde no la hay (la lección de los filtros del 227)");
    }

    [Fact]
    public void TheUiAnchorTable_ShouldCoverTheObservableSurface()
    {
        AnchorParity().Should().HaveCount(8,
            "la superficie expuesta es: raíz enfocable, superficie de gestos, plano, barra, nivel, " +
            "botones, el modo de observación externa con veredicto y el inspector observable con sus " +
            "pestañas, tarjetas y diff (hito 245) — una tabla más corta declararía menos de lo que el hito expone");
    }

    /// <summary>
    /// La tabla de anclas UIA: cada ancla explícita con su prueba del SUITE (verificada contra el
    /// índice real) y su cobertura en el HOST (sonda del selfcheck, guardia de árbol, o el estado
    /// medido por la Sonda C de QA — las anclas de contenedores sin peer se DECLARAN pero no
    /// materializan en el árbol UIA: honestidad medida, no cobertura fingida).
    /// </summary>
    private static IReadOnlyList<(string Anchor, string Test, string HostCoverage)> AnchorParity() =>
    [
        ("La raíz del lienzo es enfocable y expuesta (CanvasRoot)",
            "TheCanvas_ShouldExposeItsExplicitAnchor_AndAFocusableAutomationPeer",
            "selfcheck: ancla + peer + foco programático; Sonda C: set_focus UIA externo ENTRA y el " +
            "atajo Shift+A del lienzo se dispara (el canal de teclado del 237 queda abierto)"),
        ("La superficie de gestos distinta de la raíz (CanvasSurface)",
            "TheCanvas_ShouldKeepItselfTabStoppable_AndTheFocusableSurfaceInTheTree",
            "guardia: el XAML la declara; Sonda C: LATENTE — un Grid sin peer no materializa en el árbol"),
        ("El plano del grafo con el estado de la cámara (CanvasGraphPlane)",
            "TheGraphPlane_ShouldKeepItsAnchor_NamedByResourceNotByRepetition",
            "guardia: el plano y los recursos nombrados; Sonda C: LATENTE — un Canvas sin peer no materializa"),
        ("La barra de zoom localizable como conjunto (ZoomBar)",
            "TheZoomBar_ShouldExposeItsAnchor_ItsLevel_AndItsThreeButtons",
            "guardia: el XAML la declara; Sonda C: LATENTE — un Border sin peer no materializa (sus " +
            "botones y su nivel SÍ se exponen, con peer propio)"),
        ("El nivel de zoom legible por nombre (ZoomLevelText)",
            "TheZoomBar_ShouldExposeItsAnchor_ItsLevel_AndItsThreeButtons",
            "Sonda C: observable — Invoke cambia el nivel '100 %' a '110 %' y la ancla lo refleja (restaurado)"),
        ("Los botones invocables por ancla (ZoomIn/ZoomOut/Fit)",
            "TheUiAnchorTable_ShouldCoverTheObservableSurface",
            "Sonda C: los tres en el árbol por su AutomationId, InvokePattern sin descifrar cuál es cuál"),
        ("La observación externa con veredicto propio (--selfcheck-uia)",
            "TheExternalUiaProbeMode_ShouldBeWired_WithTheHouseInstrumentAndHonestVerdicts",
            "guardia: la rama, el pid pasado, el hilo de fondo y el instrumento de la casa como " +
            "código vivo — el modo CI-ready de la observación"),
        ("El inspector observable desde fuera: 5 pestañas, señal de escena y diff (hito 245)",
            "TheExternalUiaProbe_ShouldObserveTheInspectorTabs_WithTheAppMountedFixture",
            "sondeos S0/S6/S7 del instrumento: la señal del fixture lista, las 5 cabeceras por su AID " +
            "y el switch seguro de Diff por SelectionItem; el contenido de snapshots se declara " +
            "LATENTE (frontera medida: en pie tumba al proveedor UIA) y sus tarjetas las verifica el " +
            "selfcheck interno — observación honesta, no cobertura fingida"),
    ];
}
