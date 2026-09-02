using System;
using System.IO;
using System.Linq;
using System.Windows;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class NodeInspectorViewModelTests
{
    [Fact]
    public void InspectNode_ShouldComputeMetadataDiff_WhenInputAndOutputSnapshotsExist()
    {
        // Arrange
        var mockFileDialog = new Mock<IFileDialogService>();
        var editorVm = new EditorViewModel(new PluginLoader());
        var inspectorVm = new NodeInspectorViewModel(editorVm, mockFileDialog.Object);

        var node = new FolderSourceNode();
        var nodeVm = new NodeViewModel(node, new Point(100, 100));

        // Create Input Snapshot
        var inItem = new FileItemContext(@"C:\Path\In.txt");
        inItem.Metadata["Category"] = "Docs";
        inItem.Metadata["Status"] = "Pending";
        var inSnap = NodeDataSnapshot.CreateInput(nodeVm.Id, "In", inItem);
        nodeVm.InputSnapshots.Add(inSnap);

        // Create Output Snapshot
        var outItem = new FileItemContext(@"C:\Path\Out.txt");
        outItem.Metadata["Category"] = "Docs"; // Unchanged
        outItem.Metadata["Status"] = "Processed"; // Modified
        outItem.Metadata["NewCounter"] = 42; // Added
        var outSnap = NodeDataSnapshot.CreateOutput(nodeVm.Id, "Out", outItem);
        nodeVm.OutputSnapshots.Add(outSnap);

        // Act
        inspectorVm.InspectNode(nodeVm, autoOpen: true);

        // Assert
        inspectorVm.IsOpen.Should().BeTrue();
        inspectorVm.InspectedNode.Should().Be(nodeVm);
        inspectorVm.MetadataDiffs.Should().HaveCount(3);

        var categoryDiff = inspectorVm.MetadataDiffs.First(d => d.Key == "Category");
        categoryDiff.ChangeType.Should().Be("Unchanged");

        var statusDiff = inspectorVm.MetadataDiffs.First(d => d.Key == "Status");
        statusDiff.ChangeType.Should().Be("Modified");
        statusDiff.OldValue.Should().Be("Pending");
        statusDiff.NewValue.Should().Be("Processed");

        var counterDiff = inspectorVm.MetadataDiffs.First(d => d.Key == "NewCounter");
        counterDiff.ChangeType.Should().Be("Added");
    }

    [Fact]
    public void InspectNode_ShouldHandleEmptySnapshots_WithoutThrowing()
    {
        // Arrange
        var mockFileDialog = new Mock<IFileDialogService>();
        var editorVm = new EditorViewModel(new PluginLoader());
        var inspectorVm = new NodeInspectorViewModel(editorVm, mockFileDialog.Object);

        var node = new FolderSourceNode();
        var nodeVm = new NodeViewModel(node, new Point(0, 0));

        // Act
        inspectorVm.InspectNode(nodeVm, autoOpen: false);

        // Assert
        inspectorVm.InspectedNode.Should().Be(nodeVm);
        inspectorVm.MetadataDiffs.Should().BeEmpty();
    }

    [Fact]
    public void InspectNode_ShouldUpdateParameterEvaluatedValues_WithSelectedSnapshotContext()
    {
        // Arrange
        var mockFileDialog = new Mock<IFileDialogService>();
        var editorVm = new EditorViewModel(new PluginLoader());
        var inspectorVm = new NodeInspectorViewModel(editorVm, mockFileDialog.Object);

        var node = new FolderSourceNode();
        var nodeVm = new NodeViewModel(node, new Point(0, 0));

        // Add a parameter with expression
        var customParam = new NodeParameterViewModel("CustomDest", @"{SourceDir}\Backup_{FileName}", nodeOwner: nodeVm);
        nodeVm.Parameters.Add(customParam);

        var inItem = new FileItemContext(@"D:\Projects\FileFlow\test_data.zip");
        var inSnap = NodeDataSnapshot.CreateInput(nodeVm.Id, "In", inItem);
        nodeVm.InputSnapshots.Add(inSnap);

        // Act
        inspectorVm.InspectNode(nodeVm, autoOpen: true);

        // Assert
        inspectorVm.HasActiveEvaluationSnapshot.Should().BeTrue();
        inspectorVm.ActiveEvaluationContextFileName.Should().Be("test_data.zip");
        customParam.HasExpression.Should().BeTrue();
        customParam.EvaluatedValue.Should().Be(@"D:\Projects\FileFlow\Backup_test_data.zip");
    }
}
