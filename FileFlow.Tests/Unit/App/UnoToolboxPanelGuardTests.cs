using System.Collections.Generic;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del panel de la caja de herramientas del host Uno (rebanada 4, plan de los paneles): el
/// panel tiene que <b>consumir el ToolboxViewModel del núcleo</b> — el mismo que la versión anterior — y
/// añadir nodos por el <c>EditorViewModel.AddNode</c> canónico.
///
/// <para><b>Por qué guarda la fuente y no el runtime</b>: el panel es WinUI (host Uno) y no se
/// materializa en la sesión de pruebas (la lección del lienzo, hito 232). El riesgo real es la
/// duplicación: que el host reinvente el catálogo, el filtro o la creación de nodos en la vista y el
/// núcleo quede burlado. La cura es que la fuente del host CANTE los contratos (retira comentarios
/// antes de buscar: la lección del 165) y que la tabla de paridad cite pruebas que existen.</para>
/// </summary>
public class UnoToolboxPanelGuardTests
{
    private const string PanelPath = "FileFlow.App.Uno/Controls/NodeToolboxPanel.xaml.cs";

    private static string PanelCode() => SourceText.CodeWithoutComments(PanelPath);

    [Fact]
    public void ToolboxPanel_ShouldConsumeThePortableToolboxViewModel()
    {
        string code = PanelCode();

        code.Should().Contain(
            "public ToolboxViewModel? Vm",
            "el panel consume el VM del núcleo portable (el mismo que la versión anterior): una vista que " +
            "reinventara el catálogo duplicaría la lógica que la suite ya defiende");

        code.Should().Contain(
            "using FileFlow.App.Models;",
            "el ítem del catálogo es el tipo compartido (NodeToolboxItem del núcleo), no una copia del host");
    }

    [Fact]
    public void ToolboxPanel_ShouldAddNodesThroughTheCanonicalEditorCommand()
    {
        PanelCode().Should().Contain(
            "_editor.AddNode(item.TypeName",
            "el añadir pasa por EditorViewModel.AddNode: preferencias (uso), undo y SelectedNode llegan " +
            "por el núcleo — un panel que creara tarjetas sin pasar por aquí burlaría el undo");
    }

    [Fact]
    public void ToolboxPanel_ShouldWireSearchAndCategoryFilterToTheViewModel()
    {
        string code = SourceText.CodeWithoutComments("FileFlow.App.Uno/Controls/NodeToolboxPanel.xaml");

        code.Should().Contain(
            "Vm.SearchText",
            "el buscador está atado por binding al SearchText del VM del núcleo: filtrar en la vista " +
            "haría un segundo filtro que el suite no defiende");

        code.Should().Contain(
            "Vm.AvailableCategories",
            "los chips de categoría son la colección observable del VM (contadores en vivo), no una lista local");

        code.Should().Contain(
            "Vm.CategoryGroups",
            "los grupos acordeón son los del VM: la expansión exclusiva ya vive en HandleGroupExpanded");
    }

    [Fact]
    public void ToolboxPanel_ShouldRenderRoleBadgesAndIconsWithTheSharedPipeline()
    {
        string code = SourceText.CodeWithoutComments("FileFlow.App.Uno/Controls/NodeToolboxPanel.xaml");

        code.Should().Contain(
            "Binding RoleBadge",
            "la insignia de rol es la propiedad del modelo compartido (localizada por el núcleo)");

        code.Should().Contain(
            "IconToGeometry",
            "el icono pasa por el conversor del paquete Material.Icons (los mismos datos que la versión anterior)");
    }

    [Fact]
    public void ToolboxPanel_ShouldWireTheCompactDetailedToggle_ToTheViewModelCommand()
    {
        string code = PanelCode();
        string xaml = SourceText.CodeWithoutComments("FileFlow.App.Uno/Controls/NodeToolboxPanel.xaml");

        xaml.Should().Contain(
            "AutomationProperties.AutomationId=\"ToolboxViewModeToggle\"",
            "el botón del toggle canta su AutomationId: la observación UIA y el sondeo lo alcanzan por " +
            "nombre, no por descifrar la cabecera");

        xaml.Should().Contain(
            "Tag=\"ToolboxItemDetails\"",
            "el bloque detallado (insignia + descripción) se identifica por Tag: el x:Name dentro de una " +
            "DataTemplate no es fiable fuera de su namescope (la lección del 246)");

        code.Should().Contain(
            "_vm?.ToggleViewModeCommand.Execute(null);",
            "el toggle pasa por el MISMO comando del VM del núcleo que el botón de la versión anterior: conmutar " +
            "la vista por su cuenta duplicaría el estado y burlaría la persistencia en preferencias");

        code.Should().Contain(
            "_vm.PropertyChanged -= OnVmPropertyChanged;",
            "la vista reacciona al IsCompactMode del VM por PropertyChanged (el x:Bind de una DataTemplate " +
            "de WinUI no alcanza la página — la lección que dejó el pendiente declarado)");

        code.Should().Contain(
            "_vm.PropertyChanged += OnVmPropertyChanged;",
            "la suscripción al PropertyChanged con su desuscripción simétrica en Dispose: un panel que " +
            "escucha eternamente a un VM liberado es la familia del defecto que el 230 cazó");

        code.Should().Contain(
            "OnToolboxItemDetailsLoading",
            "cada bloque que se materialice después (scroll, regeneración del catálogo) toma SU estado en " +
            "su Loading: sin esto, los ítems que entran tarde nacen visibles en compacto");

        code.Should().Contain(
            "internal (int Total, int Hidden, int Visible) ProbeDetailsBlocks()",
            "la sonda del modo vive en el panel: el selfcheck recorre el MISMO árbol que la vista pinta " +
            "y el veredicto del toggle es medido, no declarado");
    }

    [Fact]
    public void ThePanelParityTable_ShouldCiteRealSuiteTests()
    {
        var suiteNames = TestSuiteIndex.MethodNames(TestRepositoryLocator.RepositoryRoot());

        var unknown = PanelParity()
            .Where(row => !suiteNames.Contains(row.Test))
            .Select(row => $"{row.Workflow} -> {row.Test}")
            .ToList();

        unknown.Should().BeEmpty(
            "la tabla de paridad del panel cita pruebas que deben existir: una cita que no casa se " +
            "leería como cobertura donde no la hay (la lección de los filtros del 227)");
    }

    [Fact]
    public void ThePanelParityTable_ShouldCoverThePanelWorkflow()
    {
        PanelParity().Should().HaveCount(7,
            "el flujo del panel es encontrar → filtrar → añadir → favorito → conmutar el modo → " +
            "inspeccionar → restaurar; una tabla más corta declararía menos superficie de la que la " +
            "rebanada promete (el toggle del 246 entra en la paridad)");
    }

    /// <summary>
    /// La paridad del panel con la versión anterior: cada paso del flujo con su prueba del SUITE (la lógica
    /// del VM, verificada contra el índice real) y su cobertura en el HOST (el sondeo del selfcheck o
    /// la guardia de árbol de esta misma clase).
    /// </summary>
    private static IReadOnlyList<(string Workflow, string Test, string HostCoverage)> PanelParity() =>
    [
        ("El catálogo agrupa y desduplica los tipos de nodo",
            "ToolboxViewModel_ShouldNotContainDuplicateItems_WhenAssembliesRegistered",
            "selfcheck: catálogo del cajón poblado (81 ítems con todos los plugins)"),
        ("El filtro de categoría resalta el chip y filtra los grupos",
            "SetCategoryFilter_ShouldFilterNodesAndHighlightSelectedChip",
            "selfcheck: sonda de paneles (chips atados a AvailableCategories del VM)"),
        ("El acordeón conserva la categoría expandida al colocar un nodo",
            "ToolboxViewModel_PlacingNode_ShouldPreserveExpandedCategoryState",
            "selfcheck: sonda de paneles (grupos = CategoryGroups del VM)"),
        ("La búsqueda expande las categorías coincidentes",
            "ToolboxViewModel_SearchText_ShouldExpandMatchingCategories",
            "selfcheck: el filtro reduce 81 -> 5 con 'Folder' (restaurado)"),
        ("Añadir un nodo pasa por AddNode y respeta undo",
            "EditorViewModel_AddNode_UndoRedo_ShouldWorkCorrectly",
            "selfcheck: doble clic añade el nodo (undo restaurado)"),
        ("El filtro de búsqueda reduce el catálogo (testigo de la mutación)",
            "ToolboxViewModel_SearchText_ShouldExpandMatchingCategories",
            "guardia: el SearchText del VM ata el filtro; mutación toolbox-sin-filtro"),
        ("El toggle compacto/detallado conmuta por el comando del VM (hito 246)",
            "ToolboxViewModel_ToggleViewMode_ShouldPersistCompactMode",
            "selfcheck: la sonda conmuta por ToggleViewModeCommand y cuenta los bloques detallados " +
            "(ocultos en compacto, visibles en detallado, restaurados al volver)"),
    ];
}
