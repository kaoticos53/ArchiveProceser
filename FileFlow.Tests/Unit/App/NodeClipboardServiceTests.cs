using System.Windows;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Images;
using FileFlow.Plugin.Logic;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Suite de pruebas unitarias para <see cref="NodeClipboardService"/> y las operaciones de
/// copiado, pegado, corte y duplicación de nodos en el lienzo DAG con preservación íntegra de parámetros.
/// </summary>
public class NodeClipboardServiceTests
{
    private readonly PluginLoader _loader = new();

    [Fact]
    public void CopyAndPaste_SingleNode_PreservesAllCustomParametersAndGeneratesNewId()
    {
        // Arrange
        var editor = new EditorViewModel(_loader);
        var clipboard = new NodeClipboardService(_loader);

        var imageNode = new ImageOptimizerNode();
        imageNode.Parameters["Quality"] = "85";
        imageNode.Parameters["Width"] = "1920";
        imageNode.Parameters["Height"] = "1080";
        imageNode.Parameters["TargetFormat"] = "WebP";

        var nodeVm = new NodeViewModel(imageNode, new Point(100, 100))
        {
            ParentEditor = editor,
            Title = "Mi Optimizador 4K",
            AccentColor = "#10B981",
            HasBreakpoint = true,
            IsLoggingEnabled = false,
            IsSelected = true
        };
        editor.Nodes.Add(nodeVm);

        // Act: Copiar y Pegar
        clipboard.Copy([nodeVm], editor.Connections);
        clipboard.CanPaste().Should().BeTrue();

        var pastedNodes = clipboard.Paste(editor);

        // Assert
        pastedNodes.Should().HaveCount(1);
        var pasted = pastedNodes[0];

        pasted.Id.Should().NotBe(nodeVm.Id);
        pasted.Title.Should().Be("Mi Optimizador 4K");
        pasted.AccentColor.Should().Be("#10B981");
        pasted.HasBreakpoint.Should().BeTrue();
        pasted.IsLoggingEnabled.Should().BeFalse();
        pasted.Location.X.Should().Be(140);
        pasted.Location.Y.Should().Be(140);
        pasted.IsSelected.Should().BeTrue();

        // Parámetros en NodeInstance
        pasted.NodeInstance.Parameters["Quality"]?.ToString().Should().Be("85");
        pasted.NodeInstance.Parameters["Width"]?.ToString().Should().Be("1920");
        pasted.NodeInstance.Parameters["Height"]?.ToString().Should().Be("1080");
        pasted.NodeInstance.Parameters["TargetFormat"]?.ToString().Should().Be("WebP");

        // Parámetros en ViewModel
        pasted.Parameters.First(p => p.Key == "Quality").Value?.ToString().Should().Be("85");
        pasted.Parameters.First(p => p.Key == "Width").Value?.ToString().Should().Be("1920");
        pasted.Parameters.First(p => p.Key == "Height").Value?.ToString().Should().Be("1080");
        pasted.Parameters.First(p => p.Key == "TargetFormat").Value?.ToString().Should().Be("WebP");
    }

    [Fact]
    public void CopyAndPaste_MultipleConnectedNodes_PreservesInternalConnectionsAndParameters()
    {
        // Arrange
        var editor = new EditorViewModel(_loader);
        var clipboard = new NodeClipboardService(_loader);

        var srcNode = new NodeViewModel(new FolderSourceNode(), new Point(50, 50)) { ParentEditor = editor, IsSelected = true };
        var optNode = new NodeViewModel(new ImageOptimizerNode(), new Point(300, 50)) { ParentEditor = editor, IsSelected = true };
        var destNode = new NodeViewModel(new DestinationSinkNode(), new Point(600, 50)) { ParentEditor = editor, IsSelected = true };

        srcNode.NodeInstance.Parameters["SourcePath"] = @"C:\EntradaFotos";
        optNode.NodeInstance.Parameters["Quality"] = "90";
        destNode.NodeInstance.Parameters["DestinationFolder"] = @"C:\SalidaFotos";

        editor.Nodes.Add(srcNode);
        editor.Nodes.Add(optNode);
        editor.Nodes.Add(destNode);

        editor.CreateConnection(srcNode.OutputPorts.First(), optNode.InputPorts.First());
        editor.CreateConnection(optNode.OutputPorts.First(), destNode.InputPorts.First());

        editor.Connections.Should().HaveCount(2);

        // Act: Copiar los 3 nodos seleccionados
        clipboard.Copy([srcNode, optNode, destNode], editor.Connections);
        var pastedNodes = clipboard.Paste(editor);

        // Assert: 3 nuevos nodos y 2 nuevas conexiones internas entre ellos
        pastedNodes.Should().HaveCount(3);
        editor.Nodes.Should().HaveCount(6);
        editor.Connections.Should().HaveCount(4);

        var pastedSrc = pastedNodes.First(n => n.NodeTypeName.Contains("FolderSourceNode"));
        var pastedOpt = pastedNodes.First(n => n.NodeTypeName.Contains("ImageOptimizerNode"));
        var pastedDest = pastedNodes.First(n => n.NodeTypeName.Contains("DestinationSinkNode"));

        pastedSrc.NodeInstance.Parameters["SourcePath"]?.ToString().Should().Be(@"C:\EntradaFotos");
        pastedOpt.NodeInstance.Parameters["Quality"]?.ToString().Should().Be("90");
        pastedDest.NodeInstance.Parameters["DestinationFolder"]?.ToString().Should().Be(@"C:\SalidaFotos");

        // Comprobar que las nuevas conexiones unen a los nodos pegados entre sí
        var newConn1 = editor.Connections.FirstOrDefault(c => c.Source.NodeOwner == pastedSrc && c.Target.NodeOwner == pastedOpt);
        var newConn2 = editor.Connections.FirstOrDefault(c => c.Source.NodeOwner == pastedOpt && c.Target.NodeOwner == pastedDest);

        newConn1.Should().NotBeNull();
        newConn2.Should().NotBeNull();
    }

    [Fact]
    public void CopyAndPaste_SwitchCaseNode_PreservesRulesAndDynamicPorts()
    {
        // Arrange
        var editor = new EditorViewModel(_loader);
        var clipboard = new NodeClipboardService(_loader);

        var switchNode = new SwitchCaseNode();
        var nodeVm = new NodeViewModel(switchNode, new Point(100, 100)) { ParentEditor = editor, IsSelected = true };
        editor.Nodes.Add(nodeVm);

        // Añadir casos personalizados
        nodeVm.AddSwitchCaseCommand.Execute(null);
        nodeVm.SwitchCases[0].Name = "Fotos";
        nodeVm.SwitchCases[0].Pattern = "*.jpg;*.png";
        nodeVm.SwitchCases[1].Name = "Docs";
        nodeVm.SwitchCases[1].Pattern = "*.pdf;*.docx";
        nodeVm.SyncSwitchCasesToNodeInstance();

        // Act
        clipboard.Copy([nodeVm], editor.Connections);
        var pastedNodes = clipboard.Paste(editor);

        // Assert
        pastedNodes.Should().HaveCount(1);
        var pasted = pastedNodes[0];

        pasted.Id.Should().NotBe(nodeVm.Id);
        pasted.SwitchCases.Should().HaveCount(2);
        pasted.SwitchCases[0].Name.Should().Be("Fotos");
        pasted.SwitchCases[0].Pattern.Should().Be("*.jpg;*.png");
        pasted.SwitchCases[1].Name.Should().Be("Docs");
        pasted.SwitchCases[1].Pattern.Should().Be("*.pdf;*.docx");

        pasted.OutputPorts.Select(p => p.Name).Should().Equal(["Fotos", "Docs", "Default"]);
    }

    [Fact]
    public void CopyAndPaste_VariableInjectorNode_PreservesDynamicVariables()
    {
        // Arrange
        var editor = new EditorViewModel(_loader);
        var clipboard = new NodeClipboardService(_loader);

        var injectorNode = new VariableInjectorNode();
        var nodeVm = new NodeViewModel(injectorNode, new Point(100, 100)) { ParentEditor = editor, IsSelected = true };
        editor.Nodes.Add(nodeVm);

        nodeVm.AddVariableCommand.Execute(null);
        nodeVm.Parameters.First(p => p.Key.StartsWith("Var_")).Value = "ValorDinamico123";

        // Act
        clipboard.Copy([nodeVm], editor.Connections);
        var pastedNodes = clipboard.Paste(editor);

        // Assert
        pastedNodes.Should().HaveCount(1);
        var pasted = pastedNodes[0];

        pasted.Id.Should().NotBe(nodeVm.Id);
        var param = pasted.Parameters.FirstOrDefault(p => p.Key.StartsWith("Var_"));
        param.Should().NotBeNull();
        param!.Value?.ToString().Should().Be("ValorDinamico123");
    }

    [Fact]
    public void Duplicate_ShouldCreateOffsetCopy_AndSelectIt()
    {
        // Arrange
        var editor = new EditorViewModel(_loader);
        var clipboard = new NodeClipboardService(_loader);

        var node = new NodeViewModel(new FolderSourceNode(), new Point(100, 100))
        {
            ParentEditor = editor,
            IsSelected = true
        };
        node.NodeInstance.Parameters["SourcePath"] = @"C:\MyFolder";
        editor.Nodes.Add(node);

        // Act
        var duplicates = clipboard.Duplicate([node], editor.Connections, editor);

        // Assert
        duplicates.Should().HaveCount(1);
        var dup = duplicates[0];

        dup.Id.Should().NotBe(node.Id);
        dup.Location.X.Should().Be(140);
        dup.Location.Y.Should().Be(140);
        dup.IsSelected.Should().BeTrue();
        node.IsSelected.Should().BeFalse();
        dup.NodeInstance.Parameters["SourcePath"]?.ToString().Should().Be(@"C:\MyFolder");
    }

    [Fact]
    public void Cut_ShouldRemoveOriginalFromEditor_AndPasteAtNewPosition()
    {
        // Arrange
        var editor = new EditorViewModel(_loader);

        var node = new NodeViewModel(new FolderSourceNode(), new Point(100, 100))
        {
            ParentEditor = editor,
            IsSelected = true
        };
        node.NodeInstance.Parameters["SourcePath"] = @"C:\TestCut";
        editor.Nodes.Add(node);

        // Act: Cut
        editor.CutSelectedNodesCommand.Execute(null);

        // Assert cut
        editor.Nodes.Should().BeEmpty();
        editor.ClipboardService.CanPaste().Should().BeTrue();

        // Act: Paste
        editor.PasteNodesCommand.Execute(new Point(400, 300));

        // Assert paste
        editor.Nodes.Should().HaveCount(1);
        var pasted = editor.Nodes[0];
        pasted.Location.X.Should().Be(400);
        pasted.Location.Y.Should().Be(300);
        pasted.NodeInstance.Parameters["SourcePath"]?.ToString().Should().Be(@"C:\TestCut");
    }

    [Fact]
    public void Paste_WithExplicitTargetLocation_PositionsNodesAccurately()
    {
        // Arrange
        var editor = new EditorViewModel(_loader);
        var clipboard = new NodeClipboardService(_loader);

        var node1 = new NodeViewModel(new FolderSourceNode(), new Point(100, 100)) { ParentEditor = editor, IsSelected = true };
        var node2 = new NodeViewModel(new DestinationSinkNode(), new Point(250, 180)) { ParentEditor = editor, IsSelected = true };

        editor.Nodes.Add(node1);
        editor.Nodes.Add(node2);

        clipboard.Copy([node1, node2], editor.Connections);

        // Act: Pegar en punto objetivo (500, 500)
        var pasted = clipboard.Paste(editor, targetPosition: new Point(500, 500));

        // Assert
        pasted.Should().HaveCount(2);
        var pasted1 = pasted.First(n => n.NodeTypeName.Contains("FolderSourceNode"));
        var pasted2 = pasted.First(n => n.NodeTypeName.Contains("DestinationSinkNode"));

        pasted1.Location.X.Should().Be(500);
        pasted1.Location.Y.Should().Be(500);

        pasted2.Location.X.Should().Be(500 + (250 - 100)); // 650
        pasted2.Location.Y.Should().Be(500 + (180 - 100)); // 580
    }
}
