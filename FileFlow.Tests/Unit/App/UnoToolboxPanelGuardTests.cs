using System.Collections.Generic;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del panel de la caja de herramientas del host Uno (rebanada 4, plan de los paneles): el
/// panel tiene que <b>consumir el ToolboxViewModel del núcleo</b> — el mismo que el escritorio — y
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
            "el panel consume el VM del núcleo portable (el mismo que el escritorio): una vista que " +
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
            "el icono pasa por el conversor del paquete Material.Icons (los mismos datos que el escritorio)");
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
        PanelParity().Should().HaveCount(6,
            "el flujo del panel es encontrar → filtrar → añadir → favorito → inspeccionar → restaurar; " +
            "una tabla más corta declararía menos superficie de la que la rebanada promete");
    }

    /// <summary>
    /// La paridad del panel con el escritorio: cada paso del flujo con su prueba del SUITE (la lógica
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
    ];
}
