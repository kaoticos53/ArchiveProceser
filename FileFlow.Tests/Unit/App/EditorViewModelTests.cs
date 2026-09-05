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
        variables.Should().Contain(g => g.GroupName.Contains("EXIF", StringComparison.OrdinalIgnoreCase) || g.GroupName.Contains(exifNode.Title, StringComparison.OrdinalIgnoreCase));
        var exifGroup = variables.First(g => g.GroupName.Contains("EXIF", StringComparison.OrdinalIgnoreCase) || g.GroupName.Contains(exifNode.Title, StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void ImageOptimizerNodeViewModel_ShouldInitializeWithDefaultWidthEmptyAndHeight100Pct()
    {
        // Arrange
        var imageNode = new ImageOptimizerNode();
        using var nodeVm = new NodeViewModel(imageNode, new Point(0, 0));

        // Assert
        var widthParam = nodeVm.Parameters.FirstOrDefault(p => p.Key.Equals("Width", StringComparison.OrdinalIgnoreCase));
        var heightParam = nodeVm.Parameters.FirstOrDefault(p => p.Key.Equals("Height", StringComparison.OrdinalIgnoreCase));

        widthParam.Should().NotBeNull();
        widthParam!.Value.Should().Be("");

        heightParam.Should().NotBeNull();
        heightParam!.Value.Should().Be("100%");
    }

    [Fact]
    public void NodeViewModel_SelectionAndBringToFront_IncrementsZIndex()
    {
        // Arrange
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node1 = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { ParentEditor = editor };
        var node2 = new NodeViewModel(new DestinationSinkNode(), new Point(100, 100)) { ParentEditor = editor };

        editor.Nodes.Add(node1);
        editor.Nodes.Add(node2);

        // Initial ZIndex
        node1.ZIndex.Should().Be(0);
        node2.ZIndex.Should().Be(0);

        // Act 1: Select node 1
        node1.IsSelected = true;

        // Assert 1: node1 should have higher ZIndex
        node1.ZIndex.Should().BeGreaterThan(0);
        node1.ZIndex.Should().BeGreaterThan(node2.ZIndex);

        // Act 2: Select node 2
        node2.IsSelected = true;

        // Assert 2: node2 should now have higher ZIndex than node1
        node2.ZIndex.Should().BeGreaterThan(node1.ZIndex);

        // Act 3: Explicit BringToFront on node 1
        editor.BringToFront(node1);
        node1.ZIndex.Should().BeGreaterThan(node2.ZIndex);
    }

    [Fact]
    public void NodeViewModel_BatchChangeColor_WhenMultipleNodesSelected_AffectsAllSelectedNodes()
    {
        // Arrange
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node1 = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { ParentEditor = editor, IsSelected = true };
        var node2 = new NodeViewModel(new DestinationSinkNode(), new Point(100, 100)) { ParentEditor = editor, IsSelected = true };
        var node3 = new NodeViewModel(new ImageOptimizerNode(), new Point(200, 200)) { ParentEditor = editor, IsSelected = false };

        editor.Nodes.Add(node1);
        editor.Nodes.Add(node2);
        editor.Nodes.Add(node3);

        // Act
        node1.ChangeColor("#EF4444");

        // Assert
        node1.AccentColor.Should().Be("#EF4444");
        node2.AccentColor.Should().Be("#EF4444");
        node3.AccentColor.Should().NotBe("#EF4444");
    }

    [Fact]
    public void NodeViewModel_BatchToggleBreakpoint_WhenMultipleNodesSelected_AffectsAllSelectedNodes()
    {
        // Arrange
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node1 = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { ParentEditor = editor, IsSelected = true, HasBreakpoint = false };
        var node2 = new NodeViewModel(new DestinationSinkNode(), new Point(100, 100)) { ParentEditor = editor, IsSelected = true, HasBreakpoint = false };
        var node3 = new NodeViewModel(new ImageOptimizerNode(), new Point(200, 200)) { ParentEditor = editor, IsSelected = false, HasBreakpoint = false };

        editor.Nodes.Add(node1);
        editor.Nodes.Add(node2);
        editor.Nodes.Add(node3);

        // Act: Toggle on node1
        node1.ToggleBreakpoint();

        // Assert
        node1.HasBreakpoint.Should().BeTrue();
        node2.HasBreakpoint.Should().BeTrue();
        node3.HasBreakpoint.Should().BeFalse();

        // Act: Toggle again on node2
        node2.ToggleBreakpoint();

        // Assert
        node1.HasBreakpoint.Should().BeFalse();
        node2.HasBreakpoint.Should().BeFalse();
        node3.HasBreakpoint.Should().BeFalse();
    }

    [Fact]
    public void NodeViewModel_BatchToggleLogging_WhenMultipleNodesSelected_AffectsAllSelectedNodes()
    {
        // Arrange
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node1 = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { ParentEditor = editor, IsSelected = true, IsLoggingEnabled = true };
        var node2 = new NodeViewModel(new DestinationSinkNode(), new Point(100, 100)) { ParentEditor = editor, IsSelected = true, IsLoggingEnabled = true };
        var node3 = new NodeViewModel(new ImageOptimizerNode(), new Point(200, 200)) { ParentEditor = editor, IsSelected = false, IsLoggingEnabled = true };

        editor.Nodes.Add(node1);
        editor.Nodes.Add(node2);
        editor.Nodes.Add(node3);

        // Act
        node1.ToggleLogging();

        // Assert
        node1.IsLoggingEnabled.Should().BeFalse();
        node2.IsLoggingEnabled.Should().BeFalse();
        node3.IsLoggingEnabled.Should().BeTrue();
    }

    [Fact]
    public void NodeViewModel_BatchActions_WhenOnlyOneNodeSelected_AffectsOnlyTargetNode()
    {
        // Arrange
        var loader = new PluginLoader();
        var editor = new EditorViewModel(loader);

        var node1 = new NodeViewModel(new FolderSourceNode(), new Point(0, 0)) { ParentEditor = editor, IsSelected = true };
        var node2 = new NodeViewModel(new DestinationSinkNode(), new Point(100, 100)) { ParentEditor = editor, IsSelected = false };

        editor.Nodes.Add(node1);
        editor.Nodes.Add(node2);

        // Act
        node1.ChangeColor("#10B981");
        node1.ToggleBreakpoint();
        node1.ToggleLogging();

        // Assert
        node1.AccentColor.Should().Be("#10B981");
        node1.HasBreakpoint.Should().BeTrue();
        node1.IsLoggingEnabled.Should().BeFalse();

        node2.AccentColor.Should().NotBe("#10B981");
        node2.HasBreakpoint.Should().BeFalse();
        node2.IsLoggingEnabled.Should().BeTrue();
    }
}

