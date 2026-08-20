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
}
