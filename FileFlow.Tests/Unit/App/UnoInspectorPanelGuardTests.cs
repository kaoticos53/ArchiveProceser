using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del panel inspector del host Uno (rebanada 4, plan de los paneles): la ficha tiene que
/// <b>decidir sus editores con los mismos flags del NodeParameterViewModel que el escritorio</b> y
/// <b>escribir el valor al NodeInstance</b> por el mismo camino (la edición del usuario es
/// <c>p.Value = ...</c>, no un API del host).
///
/// <para><b>Por qué guarda la fuente y no el runtime</b>: el panel es WinUI y no se materializa en la
/// sesión de pruebas (la lección del lienzo, hito 232). El riesgo real es la deriva de paridad: que el
/// host decida los editores con sus propias reglas (otro criterio = otra ficha) o que la edición
/// quede sólo en el VM sin llegar al nodo (el flujo guardaría valores viejos). La cura es que la
/// fuente del host cante la tabla de flags (retira comentarios antes de buscar: la lección del 165).</para>
/// </summary>
public class UnoInspectorPanelGuardTests
{
    private const string PanelPath = "FileFlow.App.Uno/Controls/NodeInspectorPanel.xaml.cs";

    private static string PanelCode() => SourceText.CodeWithoutComments(PanelPath);

    [Fact]
    public void InspectorPanel_ShouldConsumeThePortableInspectorViewModel()
    {
        string code = PanelCode();

        code.Should().Contain(
            "public NodeInspectorViewModel? Vm",
            "el panel consume el VM del núcleo portable (selección, IsOpen, cierre): una vista con su " +
            "propia noción de nodo inspeccionado duplicaría la lógica que la suite ya defiende");

        code.Should().Contain(
            "nameof(NodeInspectorViewModel.InspectedNode)",
            "el panel reacciona al nodo inspeccionado por PropertyChanged del VM, no por eventos propios");
    }

    [Fact]
    public void InspectorPanel_ShouldDecideEditorsWithTheSameFlagsAsTheDesktop()
    {
        string code = PanelCode();

        // Los flags del VM (los mismos que el Selector de estilos del escritorio), en el orden del
        // escritorio: toggle → slider → desplegable → ruta con explorar → multilínea → texto/number.
        code.Should().Contain("if (p.IsToggle)",
            "el booleano es un ToggleSwitch, como la fila 1 del escritorio");

        code.Should().Contain("else if (p.IsSlider)",
            "el slider usa SliderValue/SliderMin/SliderMax del VM, como la fila 2 del escritorio");

        code.Should().Contain("else if (p.IsDropdown)",
            "el desplegable es ComboBox atado a Value con Options del VM, como la fila 4 del escritorio");

        code.Should().Contain("else if (p.HasBrowseButton)",
            "la ruta lleva el botón explorar (BrowsePathCommand), como la fila del escritorio");

        code.Should().Contain("else if (p.IsMultiLine)",
            "el multilínea es TextBox con AcceptsReturn, como la fila del escritorio");

        // La fila SIEMPRE se construye: la cuenta de editores que el selfcheck compara con los
        // parámetros del nodo exige que ningún parámetro se quede sin fila (ni siquiera el número,
        // que comparte caja con el texto estándar como en la ficha del escritorio).
        code.Should().Contain("_paramsHost.Children.Add(row)",
            "toda fila construida entra al panel: el selfcheck compara editores con parámetros y una " +
            "excepción oculta dejaría la cuenta mintiendo");
    }

    [Fact]
    public void InspectorPanel_ShouldWriteParameterValuesBackToTheNodeInstance()
    {
        string code = PanelCode();

        code.Should().Contain(
            "p.Value = box.Text",
            "la edición del usuario pasa por el setter del VM (p.Value): OnValueChanged notifica al nodo " +
            "por OnParameterValueChanged y el write-back al NodeInstance es del NÚCLEO — el host no puede " +
            "escribir el diccionario por su cuenta o burlaría el undo y la validación");

        code.Should().Contain(
            "p.CopyEvaluatedValueCommand.Execute(null)",
            "el copiar del valor evaluado es el comando del VM (el aviso de confirmación vive en el núcleo)");
    }

    [Fact]
    public void InspectorPanel_ShouldWireTheTestButtonThroughTheCanonicalCoreCommand()
    {
        string code = PanelCode();

        code.Should().Contain(
            "_vm?.TestNodeWithCustomFileCommand.Execute(null)",
            "el «Probar» ejecuta el comando canónico del núcleo: la prueba aislada (estados, " +
            "snapshot, diff y diálogos de resultado) vive en el VM compartido, no en el host");

        code.Should().Contain(
            "AutomationProperties.SetAutomationId(_testButton, \"InspectorTestButton\")",
            "el botón canta su AutomationId: la observación UIA externa (hito 238/239) puede " +
            "invocarlo y leerlo por ancla estable");

        code.Should().Contain(
            "loc.GetString(\"Uno_InspectorTest\", \"Probar\")",
            "el botón está localizado por el mecanismo del host (misma regla que los textos de cabecera)");

        string service = SourceText.CodeWithoutComments("FileFlow.App.Uno/Platform/UnoFileDialogService.cs");

        service.Should().Contain(
            "EnqueueOnUiAsync",
            "el host Uno sirve la variante asíncrona con pickers encolados a UI: nunca bloquea el " +
            "hilo llamador y funciona TAMBIÉN desde el hilo de UI (donde el síncrono aborta con null)");
    }

    [Fact]
    public void InspectorPanel_ShouldShowTelemetryFromTheNodeViewModel()
    {
        string code = PanelCode();

        code.Should().Contain(
            "_inspected.CurrentStats",
            "la telemetría sale de las estadísticas del NodeViewModel (el agregado del motor), no de un contador local");

        code.Should().Contain(
            "NodeTelemetryStats.Empty(_inspected.Id)",
            "vaciar métricas pasa por la misma orden que el botón del escritorio (UpdateTelemetryStats con Empty)");

        code.Should().Contain(
            "_inspected.ExecutionStatusText",
            "el estado del nodo es la propiedad localizada del VM");
    }

    [Fact]
    public void InspectorPanel_ShouldBuildSnapshotTabsFromTheNodeCollectionsAndTheCoreDiff()
    {
        string code = PanelCode();

        code.Should().Contain(
            "_inspected.InputSnapshots.Concat(_inspected.OutputSnapshots)",
            "las tarjetas materializan las colecciones del NODO (las mismas que el motor llena): " +
            "una colección local del host duplicaría el estado y mentiría al usuario");

        code.Should().Contain(
            "_vm?.PreviewSpecificSnapshotCommand.Execute(snapshot)",
            "el «Ver» de cada tarjeta pasa por el comando canónico del VM (la vista previa es del núcleo, " +
            "no una ventana propia del host)");

        code.Should().Contain(
            "_vm.MetadataDiffs.CollectionChanged += (_, _) => RebuildDiff();",
            "la pestaña de diff vive de la colección del VM (el núcleo computa al inspeccionar o al " +
            "seleccionar un snapshot): pintarla una sola vez dejaría la ficha con datos viejos");

        code.Should().Contain(
            "_inspected.InputSnapshots.CollectionChanged += _inputsSub;",
            "la pestaña de snapshots sigue las colecciones del nodo por CollectionChanged (simetría " +
            "del contrato de vida, la lección del 227/232)");

        string selfcheck = SourceText.CodeWithoutComments("FileFlow.App.Uno/RuntimeSelfCheck.cs");

        selfcheck.Should().Contain(
            "insp.ProbeSnapshotTabs()",
            "el selfcheck corre la sonda de las pestañas: sin esa línea, las pestañas podrían " +
            "quedar vacías sin que el sondeo se enterara");
    }

    [Fact]
    public void TheInspectorParityTable_ShouldCiteRealSuiteTests()
    {
        var suiteNames = TestSuiteIndex.MethodNames(TestRepositoryLocator.RepositoryRoot());

        var unknown = InspectorParity()
            .Where(row => !suiteNames.Contains(row.Test))
            .Select(row => $"{row.Workflow} -> {row.Test}")
            .ToList();

        unknown.Should().BeEmpty(
            "la tabla de paridad del inspector cita pruebas que deben existir en el suite (la lección del 227)");
    }

    /// <summary>
    /// La paridad de la ficha: cada bloque con su prueba del SUITE (la lógica del VM) y su cobertura
    /// en el HOST (el sondeo del selfcheck o la guardia de árbol).
    /// </summary>
    private static IReadOnlyList<(string Workflow, string Test, string HostCoverage)> InspectorParity() =>
    [
        ("El nodo inspeccionado llega por la selección del lienzo",
            "InspectNode_ShouldHandleEmptySnapshots_WithoutThrowing",
            "selfcheck: la selección abre el inspector (IsOpen del VM)"),
        ("El valor evaluado del parámetro se recalcula con el contexto",
            "EvaluatedValue_ShouldRecalculate_WhenValueChanged",
            "guardia: el panel enlaza EvaluatedValue por binding OneWay del VM"),
        ("Los desplegables reconocen opciones y valor coincidente",
            "DropdownParameter_ShouldRecognizeDropdownAndMatchOption",
            "selfcheck: editores materializados (10/10 parámetros del nodo de ejemplo)"),
        ("La edición escribe al nodo (write-through por OnParameterValueChanged)",
            "CopyAndPaste_SingleNode_PreservesAllCustomParametersAndGeneratesNewId",
            "selfcheck: la edición escribe al NodeInstance ('Width' = '__probe__')"),
        ("La telemetría del nodo llega al panel",
            "InspectNode_ShouldComputeMetadataDiff_WhenInputAndOutputSnapshotsExist",
            "selfcheck: bloque de telemetría con CurrentStats y estado del VM"),
        ("El «Probar» ejecuta la prueba aislada con fichero",
            "TestNodeWithCustomFileAsync_ShouldPickThroughTheAsyncDialogVariant",
            "selfcheck: el botón existe, con su AutomationId, atado al comando canónico del núcleo"),
        ("Las pestañas de snapshots y diff pintan los datos del nodo y del VM",
            "InspectNode_ShouldHandleEmptySnapshots_WithoutThrowing",
            "selfcheck: tarjetas materializadas (1 = entradas+salidas), diff 2 filas, Pivot conmuta"),
    ];
}

