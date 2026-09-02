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
        reportItem!.Category.Should().Be("FileSystem");
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
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Network.FtpUploadNode).Assembly);

        // Act
        using var toolbox = new ToolboxViewModel(loader);

        // Assert
        toolbox.AvailableCategories.Should().NotBeEmpty();
        var allCategory = toolbox.AvailableCategories.FirstOrDefault(c => c.Key == "Todas");
        allCategory.Should().NotBeNull();
        allCategory!.Count.Should().BeGreaterThan(0);

        var docCategory = toolbox.AvailableCategories.FirstOrDefault(c => c.Key.Equals("Documents", StringComparison.OrdinalIgnoreCase));
        docCategory.Should().NotBeNull("Documents category must be discovered dynamically from the Documents plugin");
        docCategory!.Count.Should().Be(4, "Documents plugin registers 4 PDF nodes");
        docCategory.Icon.Should().Be("📄");

        var netCategory = toolbox.AvailableCategories.FirstOrDefault(c => c.Key.Equals("Network & Remote", StringComparison.OrdinalIgnoreCase));
        netCategory.Should().NotBeNull("Network & Remote category must be discovered dynamically from the Network plugin");
        netCategory!.Count.Should().Be(5, "Network plugin registers 5 network/remote nodes");
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
}
