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

    /// <summary>
    /// La superficie de observación de la ficha, en su archivo propio (<c>NodeInspectorPanel.Probes.cs</c>):
    /// los accesos por ancla, los censos de lo materializado y las sondas <c>Probe*</c> viven ahí, no en el
    /// fichero que construye la ficha. Se lee aparte porque son dos preocupaciones distintas: lo que pinta la
    /// vista y el instrumento que la mide.
    /// </summary>
    private static string ProbesCode() => SourceText.CodeWithoutComments(
        "FileFlow.App.Uno/Controls/NodeInspectorPanel.Probes.cs");

    /// <summary>
    /// La sonda del sondeo en runtime que mide los dos paneles del host: vive en su propio archivo
    /// (<c>SelfCheckPanels.cs</c>) desde el reorden del hito 276, no en el orquestador del sondeo. Ésta es la
    /// casa de las líneas <c>insp.*</c>/<c>check(...)</c> que este caso cita como medida de runtime.
    /// </summary>
    private static string PanelsSelfCheckCode() => SourceText.CodeWithoutComments(
        "FileFlow.App.Uno/SelfCheckPanels.cs");

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
            "la ruta lleva el botón explorar (BrowsePathAsyncCommand: la variante asíncrona, porque este host "
            + "abre sus pickers desde el clic de UI), como la fila del escritorio");

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

    /// <summary>
    /// El «Probar» se OFRECE sólo con un nodo inspeccionado (hito 274). Antes se dibujaba siempre y su condición era
    /// sólo la de <c>Visibility</c> de la ficha, así que con la ficha abierta y ninguna tarjeta seleccionada quedaba
    /// <b>ofrecido, habilitado y sin efecto</b>: el clic ejecutaba el comando, el comando encontraba
    /// <c>InspectedNode == null</c> y volvía sin hacer nada. El defecto no era del botón sino de la condición que
    /// decide qué se ofrece.
    ///
    /// <para><b>Qué se vigila</b>: que la oferta se decida en el estado de la ficha —<see cref="UpdateVisibility"/>—
    /// junto al cuerpo y al texto de «sin selección», con la MISMA condición; que no se corrija deshabilitando (el
    /// botón no debe ofrecerse, y un botón deshabilitado sigue ofreciéndose); y que el selfcheck recorra los dos
    /// estados y mida la oferta, no sólo el cableado (la guardia que dejó pasar el botón inerte medía
    /// <c>HasWiredTestButton</c>: que existiera y apuntara al comando del núcleo).</para>
    /// </summary>
    [Fact]
    public void InspectorPanel_ShouldOfferTheTestButton_OnlyWithAnInspectedNode()
    {
        string code = PanelCode();

        code.Should().Contain(
            "bool hasNode = _inspected is not null;",
            "la condición de estado se calcula una vez y manda sobre todo lo que se ofrece o no");

        code.Should().Contain(
            "_testButton.Visibility = isOpen && hasNode ? Visibility.Visible : Visibility.Collapsed;",
            "el «Probar» sigue la MISMA condición que el cuerpo de la ficha: sin nodo no hay nada que probar, así "
            + "que no se dibuja (la condición vive con las demás, no en el manejador del clic)");

        code.Should().NotContain(
            "_testButton.IsEnabled = false",
            "no se corrige deshabilitando: un botón deshabilitado sigue ofreciéndose y el usuario no sabe por qué");

        // Y la medición en runtime: la sonda recorre los dos estados por la propiedad del VM (el camino del arranque
        // y el de la selección del lienzo) y mide si el botón se ofrece en cada uno. Sin esa medida, la guardia de
        // cableado volvería a pasar con el botón mintiendo.
        string selfcheck = PanelsSelfCheckCode();

        selfcheck.Should().Contain(
            "insp.ProbeTestButtonOffer(firstNode)",
            "el selfcheck recorre los dos estados del «Probar»: sin nodo y con el nodo inspeccionado");

        selfcheck.Should().Contain(
            "!testOffer.OfferedWithoutNode",
            "y afirma lo que faltaba: sin nodo el botón NO se ofrece (era el botón inerte del arranque)");
    }

    /// <summary>
    /// El encabezado de la sección de PARÁMETROS sigue a sus editores (hito 274), la misma regla que el bloque de
    /// acciones del hito 269: un encabezado sobre una lista vacía promete algo que no hay. Un nodo sin parámetros
    /// —y sin nodo inspeccionado— deja el encabezado colapsado.
    /// </summary>
    [Fact]
    public void InspectorPanel_ShouldCollapseTheParametersHeader_WhenTheNodeDeclaresNoParameters()
    {
        string code = PanelCode();

        code.Should().Contain(
            "_paramsHeader.Visibility = _paramsHost.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;",
            "el encabezado se ata a los editores materializados: sin parámetros no promete una lista que no existe");

        code.Should().Contain(
            "_paramsHeader.Visibility = Visibility.Collapsed;",
            "sin nodo inspeccionado el encabezado también se colapsa (el nodo anterior dejó de estar)");

        string selfcheck = PanelsSelfCheckCode();

        selfcheck.Should().Contain(
            "insp.ParametersHeaderOffered == (paramEditors > 0)",
            "el selfcheck compara el encabezado con la cuenta de editores del nodo inspeccionado");
    }

    /// <summary>
    /// Las ACCIONES del nodo en la ficha (hito 269): son la puerta a las superficies que declara el nodo —el
    /// gestor de presets, la configuración del VLM, el estudio de scripts, el diseñador de datasets— y hasta
    /// aquí vivían SÓLO en el panel plegable de la tarjeta del lienzo. La acción existía, el comando existía y
    /// quien no supiera desplegar la tarjeta no la encontraba: es el mismo defecto de la puerta que faltaba, un
    /// paso más allá.
    ///
    /// <para><b>Qué se vigila</b>: que el bloque salga de la colección del NÚCLEO (las mismas acciones que
    /// pinta la tarjeta), que el botón ejecute el comando del view model portable y no una vía propia del host,
    /// que cada botón cante su ancla para la observación externa, que el encabezado esté localizado y que el
    /// bloque entero se colapse cuando el nodo no declara nada.</para>
    /// </summary>
    [Fact]
    public void InspectorPanel_ShouldPaintTheNodeActions_SoTheirSurfacesAreReachableWithoutTheCard()
    {
        string code = PanelCode();

        code.Should().Contain(
            "foreach (var action in _inspected.CustomActions)",
            "las acciones salen de la colección del NodeViewModel (las mismas que pinta la tarjeta y el " +
            "escritorio): una lista propia del host se quedaría corta en cuanto un nodo declarara la suya");

        code.Should().Contain(
            "action.ExecuteCommand.Execute(null)",
            "el botón ejecuta el comando del view model PORTABLE —la MISMA orden del núcleo que el botón de " +
            "la tarjeta (ExecuteCustomAction)—: una vía propia del host duplicaría la puerta a la superficie");

        code.Should().Contain(
            "AnchorAction(\"InspectorAction_\" + action.ActionId",
            "cada botón canta su ancla estable (InspectorAction_<ActionId>) para la observación UIA externa");

        code.Should().Contain(
            "loc.GetString(\"Uno_InspectorActions\", \"Acciones\")",
            "el encabezado del bloque está localizado por el mecanismo del host, como el resto de la ficha");

        code.Should().Contain(
            "_actionsHost.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed",
            "el bloque se colapsa entero sin acciones: un encabezado sobre una lista vacía promete algo que no hay");

        // Y la medición en runtime, cada mitad en su casa: el CENSO de botones lo mide el sondeo de los
        // paneles (`SelfCheckPanels`), y la puerta de un nodo CON acciones —el botón localizado por su
        // ancla— la mide el sondeo de los diálogos, sobre el transcodificador, en el orquestador.
        PanelsSelfCheckCode().Should().Contain(
            "insp.ActionButtonCount",
            "el selfcheck compara los botones de acción materializados con las acciones del nodo: sin esa " +
            "medida, una ficha que no pintara ninguna acción pasaría desapercibida");

        SourceText.CodeWithoutComments("FileFlow.App.Uno/SelfCheckDialogs.cs").Should().Contain(
            "inspector.ActionControl(\"ManageMediaPresets\")",
            "y comprueba, sobre el nodo que SÍ declara acciones, que el botón existe por su ancla " +
            "(la puerta, no sólo el rótulo): esa medida vive en el sondeo de los diálogos");
    }

    // La TELEMETRÍA salió de este fichero en el hito 275: sus medidas y su montaje los vigila
    // `UnoInspectorTelemetryGuardTests`, junto a la sección que los sirve (`NodeInspectorTelemetrySection`).

    [Fact]
    public void InspectorPanel_ShouldBuildSnapshotTabsFromTheNodeCollectionsAndTheCoreDiff()
    {
        string code = PanelCode();

        code.Should().Contain(
            "foreach (var snapshot in _inspected.InputSnapshots)",
            "las tarjetas materializan las colecciones del NODO (las mismas que el motor llena): una " +
            "colección local del host duplicaría el estado y mentiría al usuario — desde el 245 cada " +
            "bucle canta también el AutomationId de su colección para la paridad observable");

        code.Should().Contain(
            "foreach (var snapshot in _inspected.OutputSnapshots)",
            "el par de bucles (entradas, salidas) mantiene el orden del 241 y las anclas por colección " +
            "del 245: InspectorSnapshotCard_in_*/out_<puerto>_<i>");

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
            "las pestañas de snapshots siguen las colecciones del nodo por CollectionChanged (simetría " +
            "del contrato de vida, la lección del 227/232)");

        string selfcheck = PanelsSelfCheckCode();

        selfcheck.Should().Contain(
            "insp.ProbeSnapshotTabs()",
            "el selfcheck corre la sonda de las pestañas: sin esa línea, las pestañas podrían " +
            "quedar vacías sin que el sondeo se enterara");
    }

    [Fact]
    public void InspectorPanel_ShouldSeparateInputsAndOutputs_WithParityOfData()
    {
        string code = PanelCode();

        // Que las dos pestañas se alimenten de las colecciones del nodo (`foreach (var snapshot in
        // _inspected.InputSnapshots/OutputSnapshots)`) lo vigila el caso de las pestañas de snapshots: decirlo
        // otra vez aquí no añadía ninguna forma de romperse. Lo de ESTE caso es la paridad entre las tres
        // vistas (la combinada y las separadas) y las anclas de la tira.
        code.Should().Contain(
            "RebuildAllSnapshotViews()",
            "un cambio en las colecciones reconstruye las TRES vistas: la combinada y las " +
            "separadas comparten dato y ninguna puede quedar congelada respecto de otra");

        // Las anclas de las secciones (hito 273): Entradas y Salidas son dos de las cinco de la tira, cada
        // una declarada con su ancla en la tabla y aplicada al botón que la conmuta — el censo de la tira
        // (y su caja dentro de la ficha) es lo que la guardia del selfcheck mide.
        code.Should().Contain(
            "(\"Uno_InspectorTabInputs\", \"Entradas\", \"InspectorTabInputs\")",
            "las pestañas separadas cantan su ancla para la observación UIA externa (InspectorTabInputs)");

        code.Should().Contain(
            "(\"Uno_InspectorTabOutputs\", \"Salidas\", \"InspectorTabOutputs\")",
            "la pestaña de Salidas con su ancla (InspectorTabOutputs)");

        code.Should().Contain(
            "AutomationProperties.SetAutomationId(button, aid)",
            "cada sección recibe el AutomationId que declara la tabla: sin ese canal, la observación "
            + "externa no alcanza ninguna de las cinco");
    }

    // El «Ejecutar» del host y su canal observable salieron de este fichero en el reorden del hito 276: su
    // sujeto es la barra de control y la franja de estado, y viven en `UnoControlBarParityGuardTests`.

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
            "selfcheck: la sección de Telemetría —montada en su pestaña— pinta sus filas desde CurrentStats y el estado del VM"),
        ("El «Probar» ejecuta la prueba aislada con fichero",
            "TestNodeWithCustomFileAsync_ShouldPickThroughTheAsyncDialogVariant",
            "selfcheck: el botón existe, con su AutomationId, atado al comando canónico del núcleo"),
        ("Las acciones del nodo se pueden pulsar desde la ficha (sus superficies no dependen de la tarjeta)",
            "NodeViewModel_ShouldPopulateCustomActions_FromNodeDefinition",
            "selfcheck: botones de acción materializados == acciones del nodo, con el ancla del primero"),
        ("Las pestañas de snapshots y diff pintan los datos del nodo y del VM",
            "InspectNode_ShouldHandleEmptySnapshots_WithoutThrowing",
            "selfcheck: tarjetas materializadas (1 = entradas+salidas), diff 2 filas, Pivot conmuta"),
        ("El ciclo completo es observable desde fuera (Ejecutar + canal)",
            "TheUnoHost_ShouldExposeTheCanonicalExecuteCommand_AsAnObservableChannel",
            "guion QA 243: superficie UIA viva, botón expuesto, ciclo del motor por CLI (1 ítem, " +
            "3 nodos con stats) y canal del proceso legible"),
    ];
}

