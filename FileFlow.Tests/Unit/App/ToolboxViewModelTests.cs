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
}
