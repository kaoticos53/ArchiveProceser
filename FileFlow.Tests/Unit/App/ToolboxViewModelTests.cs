using System.Linq;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

[Collection("Localization")]
/// <summary>
/// Pruebas unitarias para <see cref="ToolboxViewModel"/> y la sincronización del catálogo de nodos en la barra de herramientas.
/// </summary>
public class ToolboxViewModelTests
{
    /// <summary>
    /// OBJETO: Descubrimiento y visualización de nodos en <see cref="ToolboxViewModel"/>.
    /// QUÉ:    Verifica que el nodo <see cref="OperationReportNode"/> sea descubierto y clasificado en la categoría 'FileSystem' con el icono '📋'.
    /// CÓMO:  Registra el ensamblado del plugin FileSystem en el PluginLoader, instancia el ToolboxViewModel y comprueba la presencia y propiedades del ítem en la colección agrupada.
    /// </summary>
    [Fact]
    public void ToolboxViewModel_ShouldContainOperationReportNode_WhenFileSystemAssemblyRegistered()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);

        // Act
        using var toolbox = new ToolboxViewModel(loader);

        // Assert
        loader.DiscoveredNodeTypes.Should().ContainKey(typeof(OperationReportNode).FullName!);

        var allItems = toolbox.CategoryGroups.SelectMany(g => g.Items).ToList();
        var reportItem = allItems.FirstOrDefault(i => i.TypeName.Contains("OperationReportNode"));

        reportItem.Should().NotBeNull("OperationReportNode must appear in the toolbox items");
        reportItem!.Category.Should().Be("Integrations");
        reportItem.Icon.Should().Be("📋");
    }

    /// <summary>
    /// OBJETO: No duplicidad en el catálogo de nodos de <see cref="ToolboxViewModel"/>.
    /// QUÉ:    Verifica que ningún nodo aparezca repetido dentro de sus grupos de categorías.
    /// CÓMO:  Registra ensamblados de plugins, instancia el ToolboxViewModel y comprueba que en cada grupo de categoría todos los TypeName sean únicos.
    /// </summary>
    [Fact]
    public void ToolboxViewModel_ShouldNotContainDuplicateItems_WhenAssembliesRegistered()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Archives.SmartUnpackNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Images.ImageOptimizerNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.SwitchCaseNode).Assembly);

        // Act
        using var toolbox = new ToolboxViewModel(loader);

        // Assert - In each category group, item TypeNames must be distinct
        foreach (var group in toolbox.CategoryGroups)
        {
            var typeNames = group.Items.Select(i => i.TypeName).ToList();
            typeNames.Should().OnlyHaveUniqueItems($"Category group '{group.CategoryName}' should not contain duplicated items.");
        }
    }

    /// <summary>
    /// OBJETO: Descubrimiento dinámico de categorías y conteos en <see cref="ToolboxViewModel.AvailableCategories"/>.
    /// QUÉ:    Verifica que las categorías de nuevos plugins (como 'Documents') se descubran dinámicamente y calculen sus conteos.
    /// CÓMO:  Registra el plugin de Documents y comprueba que la categoría 'Documents' aparezca en AvailableCategories con conteo > 0.
    /// </summary>
    [Fact]
    public void AvailableCategories_ShouldDynamicallyIncludeNewPluginCategoriesAndCounts()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Documents.PdfMergeNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Network.NetworkUploadNode).Assembly);

        // Act
        using var toolbox = new ToolboxViewModel(loader);

        // Assert
        toolbox.AvailableCategories.Should().NotBeEmpty();
        var allCategory = toolbox.AvailableCategories.FirstOrDefault(c => c.Key == "Todas");
        allCategory.Should().NotBeNull();
        allCategory!.Count.Should().BeGreaterThan(0);

        var docCategory = toolbox.AvailableCategories.FirstOrDefault(c => c.Key.Equals("Documents", StringComparison.OrdinalIgnoreCase));
        docCategory.Should().NotBeNull("Documents category must be discovered dynamically from the Documents plugin");
        docCategory!.Count.Should().Be(5, "Documents plugin registers 4 PDF nodes and FileSystem has DocumentProcessor");
        docCategory.Icon.Should().Be("📄");

        var netCategory = toolbox.AvailableCategories.FirstOrDefault(c => c.Key.Equals("Network", StringComparison.OrdinalIgnoreCase) || c.Key.Equals("Network & Remote", StringComparison.OrdinalIgnoreCase));
        netCategory.Should().NotBeNull("Network category must be discovered dynamically from the Network plugin");
        netCategory!.Count.Should().Be(2, "Network plugin registers 2 unified network nodes (Download + Upload)");
        netCategory.Icon.Should().Be("🌐");
    }

    /// <summary>
    /// OBJETO: Filtrado reactivo por categoría en <see cref="ToolboxViewModel"/>.
    /// QUÉ:    Verifica que al seleccionar una categoría específica se marque IsSelected y se muestren solo los nodos de esa categoría.
    /// CÓMO:  Invoca SetCategoryFilterCommand("Documents") y valida que CategoryGroups solo contenga nodos de Documents.
    /// </summary>
    [Fact]
    public void SetCategoryFilter_ShouldFilterNodesAndHighlightSelectedChip()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Documents.PdfMergeNode).Assembly);
        using var toolbox = new ToolboxViewModel(loader);

        // Act
        toolbox.SetCategoryFilter("Documents");

        // Assert
        toolbox.SelectedCategoryFilter.Should().Be("Documents");
        var docCategory = toolbox.AvailableCategories.FirstOrDefault(c => c.Key.Equals("Documents", StringComparison.OrdinalIgnoreCase));
        docCategory.Should().NotBeNull();
        docCategory!.IsSelected.Should().BeTrue();

        var allCategory = toolbox.AvailableCategories.FirstOrDefault(c => c.Key == "Todas");
        allCategory!.IsSelected.Should().BeFalse();

        var filteredItems = toolbox.CategoryGroups.SelectMany(g => g.Items).ToList();
        filteredItems.Should().NotBeEmpty();
        filteredItems.Should().AllSatisfy(i => i.Category.Should().Be("Documents"));
    }

    /// <summary>
    /// OBJETO: Selección mediante ComboBox (<see cref="ToolboxViewModel.SelectedCategoryItem"/>).
    /// QUÉ:    Verifica que al asignar SelectedCategoryItem en el ComboBox se actualice el filtro y se recargue la lista de nodos.
    /// CÓMO:  Asigna SelectedCategoryItem al elemento 'Documents' y valida que los nodos queden filtrados.
    /// </summary>
    [Fact]
    public void SelectedCategoryItem_ShouldFilterNodes_WhenChangedByDropdown()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Documents.PdfMergeNode).Assembly);
        using var toolbox = new ToolboxViewModel(loader);

        var docCategory = toolbox.AvailableCategories.FirstOrDefault(c => c.Key.Equals("Documents", StringComparison.OrdinalIgnoreCase));
        docCategory.Should().NotBeNull();

        // Act - Simula selección del usuario en el ComboBox desplegable
        toolbox.SelectedCategoryItem = docCategory;

        // Assert
        toolbox.SelectedCategoryFilter.Should().Be("Documents");
        var filteredItems = toolbox.CategoryGroups.SelectMany(g => g.Items).ToList();
        filteredItems.Should().NotBeEmpty();
        filteredItems.Should().AllSatisfy(i => i.Category.Should().Be("Documents"));
    }

    /// <summary>
    /// OBJETO: Expansión por defecto del catálogo de nodos.
    /// QUÉ:    Verifica que por defecto todas las categorías estén colapsadas excepto la de más usados ('Frequent').
    /// CÓMO:  Registra nodos y un uso en UserPreferencesService, instancia ToolboxViewModel y valida que solo 'Frequent' esté expandido.
    /// </summary>
    [Fact]
    public void ToolboxViewModel_DefaultExpansion_ShouldOnlyExpandFrequentCategory()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        FileFlow.App.Services.UserPreferencesService.Instance.IncrementNodeUsage(typeof(FolderSourceNode).FullName!);

        // Act
        using var toolbox = new ToolboxViewModel(loader);

        // Assert
        toolbox.CategoryGroups.Should().NotBeEmpty();
        var freqGroup = toolbox.CategoryGroups.FirstOrDefault(g => g.CategoryKey.Equals("Frequent", StringComparison.OrdinalIgnoreCase));
        freqGroup.Should().NotBeNull("Frequent category should exist when there are used nodes");
        freqGroup!.IsExpanded.Should().BeTrue("Only the 'Frequent' category must be expanded by default");

        var otherGroups = toolbox.CategoryGroups.Where(g => !g.CategoryKey.Equals("Frequent", StringComparison.OrdinalIgnoreCase)).ToList();
        otherGroups.Should().NotBeEmpty();
        otherGroups.Should().AllSatisfy(g => g.IsExpanded.Should().BeFalse("All categories other than 'Frequent' must be collapsed by default"));
    }

    /// <summary>
    /// OBJETO: Comportamiento de acordeón exclusivo en el catálogo de nodos.
    /// QUÉ:    Verifica que al expandir una categoría cualquiera, las demás categorías abiertas se colapsen automáticamente.
    /// CÓMO:  Instancia el toolbox, abre una categoría distinta de 'Frequent' y verifica que 'Frequent' y el resto queden colapsadas.
    /// </summary>
    [Fact]
    public void ToolboxViewModel_AccordionBehavior_ShouldCollapseOtherCategoriesWhenOneExpands()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        FileFlow.App.Services.UserPreferencesService.Instance.IncrementNodeUsage(typeof(FolderSourceNode).FullName!);

        using var toolbox = new ToolboxViewModel(loader);
        var freqGroup = toolbox.CategoryGroups.FirstOrDefault(g => g.CategoryKey.Equals("Frequent", StringComparison.OrdinalIgnoreCase));
        freqGroup.Should().NotBeNull();
        freqGroup!.IsExpanded.Should().BeTrue();

        var nonFreqGroup = toolbox.CategoryGroups.FirstOrDefault(g => !g.CategoryKey.Equals("Frequent", StringComparison.OrdinalIgnoreCase));
        nonFreqGroup.Should().NotBeNull();
        nonFreqGroup!.IsExpanded.Should().BeFalse();

        // Act - Abre la otra categoría
        nonFreqGroup.IsExpanded = true;

        // Assert - Comprueba que 'Frequent' se cerró y solo la nueva está abierta (acordeón)
        nonFreqGroup.IsExpanded.Should().BeTrue();
        freqGroup.IsExpanded.Should().BeFalse("Opening another category must automatically collapse 'Frequent'");

        var allOtherGroups = toolbox.CategoryGroups.Where(g => g != nonFreqGroup).ToList();
        allOtherGroups.Should().AllSatisfy(g => g.IsExpanded.Should().BeFalse("Accordion mode requires all other categories to be collapsed"));
    }

    /// <summary>
    /// OBJETO: Preservación de estado de categorías al añadir nodos al lienzo.
    /// QUÉ:    Verifica que cuando se coloca un nodo en el lienzo (disparando incremento de uso y refresco del toolbox), la categoría que el usuario tenía abierta se conserve y no se descolapsen todas.
    /// CÓMO:  Expande una categoría específica, invoca IncrementNodeUsage y valida que tras el refresco automático la misma categoría permanezca expandida y las demás colapsadas.
    /// </summary>
    [Fact]
    public void ToolboxViewModel_PlacingNode_ShouldPreserveExpandedCategoryState()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Documents.PdfMergeNode).Assembly);

        using var toolbox = new ToolboxViewModel(loader);

        // Seleccionamos una categoría específica, por ejemplo 'Files', y la expandimos
        var filesGroup = toolbox.CategoryGroups.FirstOrDefault(g => g.CategoryKey.Equals("Files", StringComparison.OrdinalIgnoreCase));
        filesGroup.Should().NotBeNull();
        filesGroup!.IsExpanded = true;

        var otherGroupsBefore = toolbox.CategoryGroups.Where(g => g != filesGroup).ToList();
        otherGroupsBefore.Should().AllSatisfy(g => g.IsExpanded.Should().BeFalse());

        // Act - Simula la colocación de un nuevo nodo en el lienzo de trabajo
        FileFlow.App.Services.UserPreferencesService.Instance.IncrementNodeUsage(typeof(FileFlow.Plugin.Documents.PdfMergeNode).FullName!);

        // Assert - Comprueba que 'Files' sigue abierta y las demás siguen colapsadas
        var filesGroupAfter = toolbox.CategoryGroups.FirstOrDefault(g => g.CategoryKey.Equals("Files", StringComparison.OrdinalIgnoreCase));
        filesGroupAfter.Should().NotBeNull();
        filesGroupAfter!.IsExpanded.Should().BeTrue("The user's opened category must remain expanded after placing a node");

        var otherGroupsAfter = toolbox.CategoryGroups.Where(g => !g.CategoryKey.Equals("Files", StringComparison.OrdinalIgnoreCase)).ToList();
        otherGroupsAfter.Should().NotBeEmpty();
        otherGroupsAfter.Should().AllSatisfy(g => g.IsExpanded.Should().BeFalse("All other categories must remain collapsed"));
    }

    /// <summary>
    /// OBJETO: Expansión inteligente durante búsqueda activa en el catálogo.
    /// QUÉ:    Verifica que al escribir un término en SearchText las categorías coincidentes se expandan para mostrar los resultados, y al limpiar el texto se respete la categoría activa.
    /// </summary>
    [Fact]
    public void ToolboxViewModel_SearchText_ShouldExpandMatchingCategories()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        using var toolbox = new ToolboxViewModel(loader);

        // Act - Búsqueda activa
        toolbox.SearchText = "Folder";

        // Assert - Todos los grupos con resultados de búsqueda deben estar expandidos
        toolbox.CategoryGroups.Should().NotBeEmpty();
        toolbox.CategoryGroups.Should().AllSatisfy(g => g.IsExpanded.Should().BeTrue("Categories with search results must be expanded"));

        // Act - Limpia la búsqueda
        toolbox.SearchText = string.Empty;

        // Assert - Solo 1 categoría (o la de Frequent si existe) queda expandida
        var expandedCount = toolbox.CategoryGroups.Count(g => g.IsExpanded);
        expandedCount.Should().BeLessThanOrEqualTo(1, "Clearing search text should return to accordion mode");
    }
}
