using System.Windows;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Images;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class EditorViewModelTests
{
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

