using System.Linq;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

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
        var toolbox = new ToolboxViewModel(loader);

        // Assert
        loader.DiscoveredNodeTypes.Should().ContainKey(typeof(OperationReportNode).FullName!);

        var allItems = toolbox.CategoryGroups.SelectMany(g => g.Items).ToList();
        var reportItem = allItems.FirstOrDefault(i => i.TypeName.Contains("OperationReportNode"));

        reportItem.Should().NotBeNull("OperationReportNode must appear in the toolbox items");
        reportItem!.Category.Should().Be("FileSystem");
        reportItem.Icon.Should().Be("📋");
    }
}
