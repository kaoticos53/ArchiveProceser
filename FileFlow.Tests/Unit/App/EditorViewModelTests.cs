using System.Windows;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Images;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Pruebas unitarias para <see cref="EditorViewModel"/> y la gestión del lienzo de diseño visual DAG en WPF.
/// </summary>
public class EditorViewModelTests
{
    /// <summary>
    /// OBJETO: Descubrimiento de variables globales en el editor.
    /// QUÉ:    Verifica que las variables del sistema (ej. FileName, RelativePath, DateNow) siempre estén disponibles para cualquier nodo.
    /// CÓMO:  Instancia el editor y solicita las variables disponibles río arriba para un nodo inicial, comprobando la presencia del grupo 'System'.
    /// </summary>
    [Fact]
    public void GetUpstreamAvailableVariables_ShouldIncludeSystemVariables_Always()
    {
        // Arrange
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node = new NodeViewModel(new FolderSourceNode(), new Point(0, 0));

        // Act
        var variables = editor.GetUpstreamAvailableVariables(node);

        // Assert
        variables.Should().Contain(g => g.GroupName.Contains("System"));
        var systemGroup = variables.First(g => g.GroupName.Contains("System"));
        systemGroup.Variables.Should().Contain(v => v.Name == "FileName");
        systemGroup.Variables.Should().Contain(v => v.Name == "RelativePath");
        systemGroup.Variables.Should().Contain(v => v.Name == "DateNow");
    }

    /// <summary>
    /// OBJETO: Recorrido topológico inverso para inspección de metadatos upstream.
    /// QUÉ:    Garantiza que un nodo destino pueda descubrir y autocompletar variables emitidas por nodos predecesores conectados (ej. EXIF de imágenes).
    /// CÓMO:  Crea dos nodos conectados (ExifMetadataNode -> DestinationSinkNode), solicita las variables del nodo destino y valida la presencia de metadatos EXIF.
    /// </summary>
    [Fact]
    public void GetUpstreamAvailableVariables_ShouldTraverseUpstreamConnections_ToIncludeExifVariables()
    {
        // Arrange
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var exifNode = new NodeViewModel(new ExifMetadataNode(), new Point(0, 0));
        var destNode = new NodeViewModel(new DestinationSinkNode(), new Point(300, 0));

        editor.Nodes.Add(exifNode);
        editor.Nodes.Add(destNode);

        var outPort = exifNode.OutputPorts.First();
        var inPort = destNode.InputPorts.First();

        editor.CreateConnection(outPort, inPort);

        // Act
        var variables = editor.GetUpstreamAvailableVariables(destNode);

        // Assert
        variables.Should().Contain(g => g.GroupName.Contains(exifNode.Title));
        var exifGroup = variables.First(g => g.GroupName.Contains(exifNode.Title));
        exifGroup.Variables.Should().Contain(v => v.Name == "DateTaken");
        exifGroup.Variables.Should().Contain(v => v.Name == "Orientation");
    }

    /// <summary>
    /// OBJETO: Enrutador dinámico <see cref="FileFlow.Plugin.Logic.SwitchCaseNode"/> en el ViewModel.
    /// QUÉ:    Valida la reactividad de la UI al añadir, renombrar y eliminar casos dinámicos, sincronizando los puertos de salida en tiempo real.
    /// CÓMO:  Instancia el NodeViewModel del SwitchCaseNode, ejecuta comandos de adición, renombra casos y elimina uno, verificando la lista de OutputPorts en cada paso.
    /// </summary>
    [Fact]
    public void SwitchCaseNodeViewModel_ShouldInitializeWithCase1AndDefault_AndSupportDynamicAdditionAndRenaming()
    {
        // Arrange
        var switchNode = new FileFlow.Plugin.Logic.SwitchCaseNode();
        var nodeVm = new NodeViewModel(switchNode, new Point(0, 0));

        // Assert initial state
        nodeVm.SwitchCases.Should().HaveCount(1);
        nodeVm.SwitchCases[0].Name.Should().Be("Case 1");
        nodeVm.SwitchCases[0].Pattern.Should().Be("jpg;jpeg;png;webp;gif");
        nodeVm.OutputPorts.Select(p => p.Name).Should().Equal(["Case 1", "Default"]);

        // Act 1: Add new case
        nodeVm.AddSwitchCaseCommand.Execute(null);

        // Assert after addition
        nodeVm.SwitchCases.Should().HaveCount(2);
        nodeVm.SwitchCases[1].Name.Should().Be("Case 2");
        nodeVm.OutputPorts.Select(p => p.Name).Should().Equal(["Case 1", "Case 2", "Default"]);

        // Act 2: Rename Case 2 to "Videos"
        nodeVm.SwitchCases[1].Name = "Videos";

        // Assert after renaming: Port 0 remains "Case 1", Port 1 becomes "Videos", Port 2 is "Default"
        nodeVm.OutputPorts.Select(p => p.Name).Should().Equal(["Case 1", "Videos", "Default"]);

        // Act 3: Rename Case 1 to "Imagenes"
        nodeVm.SwitchCases[0].Name = "Imagenes";
        nodeVm.OutputPorts.Select(p => p.Name).Should().Equal(["Imagenes", "Videos", "Default"]);

        // Act 4: Remove "Videos"
        nodeVm.RemoveSwitchCaseCommand.Execute(nodeVm.SwitchCases[1]);
        nodeVm.SwitchCases.Should().HaveCount(1);
        nodeVm.OutputPorts.Select(p => p.Name).Should().Equal(["Imagenes", "Default"]);
    }
}

