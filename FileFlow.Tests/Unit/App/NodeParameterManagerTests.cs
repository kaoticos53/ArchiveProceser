using System.Linq;
using System.Windows;
using FileFlow.App.ViewModels;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Images;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class NodeParameterManagerTests
{
    [Fact]
    public void InitializeParameters_ShouldUseParameterDescriptors_WhenNodeDefinesThem()
    {
        // Arrange: ImageOptimizerNode define ParameterDescriptors (Width 1º, Height 2º, TargetFormat 3º, Quality 4º...)
        var node = new ImageOptimizerNode();
        using var nodeVm = new NodeViewModel(node, new Point(0, 0));

        // Act
        var paramsList = nodeVm.Parameters.ToList();

        // Assert
        paramsList.Should().NotBeEmpty();
        paramsList[0].Key.Should().Be("Width");
        paramsList[0].Value.Should().Be("");

        paramsList[1].Key.Should().Be("Height");
        paramsList[1].Value.Should().Be("100%");

        paramsList[2].Key.Should().Be("TargetFormat");
        paramsList[2].IsDropdown.Should().BeTrue();
        paramsList[2].Options.Should().Contain("WebP");

        paramsList[3].Key.Should().Be("Quality");
        paramsList[3].IsSlider.Should().BeTrue();
        paramsList[3].SliderMin.Should().Be(1);
        paramsList[3].SliderMax.Should().Be(100);
    }

    [Fact]
    public void InitializeParameters_ShouldCorrectlyIdentifyFolderAndDropdownTypes_FromDescriptors()
    {
        // Arrange
        var node = new FolderSourceNode();
        using var nodeVm = new NodeViewModel(node, new Point(0, 0));

        // Act
        var sourcePathParam = nodeVm.Parameters.FirstOrDefault(p => p.Key == "SourcePath");
        var emitModeParam = nodeVm.Parameters.FirstOrDefault(p => p.Key == "EmitMode");
        var recursiveParam = nodeVm.Parameters.FirstOrDefault(p => p.Key == "Recursive");

        // Assert
        sourcePathParam.Should().NotBeNull();
        sourcePathParam!.IsFolderPath.Should().BeTrue();
        sourcePathParam.HasBrowseButton.Should().BeTrue();

        emitModeParam.Should().NotBeNull();
        emitModeParam!.IsDropdown.Should().BeTrue();
        emitModeParam.Options.Should().Equal(["FilesOnly", "DirectoriesOnly", "FilesAndDirectories"]);

        recursiveParam.Should().NotBeNull();
        recursiveParam!.IsBooleanAndNoOptions.Should().BeTrue();
    }

    [Fact]
    public void InitializeParameters_ShouldNotExposeLegacyPatternOrMethodSteps_ForAdvancedRenamerNode()
    {
        // Arrange
        var node = new AdvancedRenamerNode();
        // Simular presencia de parámetros legados e internos
        node.Parameters["Pattern"] = "{ParentDir}_{FileName}";
        node.Parameters["NameTemplate"] = "{FileName}";
        node.Parameters["CaseTransformation"] = "Uppercase";
        node.Parameters["MethodSteps"] = "[{}]";

        using var nodeVm = new NodeViewModel(node, new Point(0, 0));

        // Act
        var paramKeys = nodeVm.Parameters.Select(p => p.Key).ToList();

        // Assert: Solo deben exponerse los descriptores oficiales (PipelineName, RenameMode, CollisionStrategy)
        paramKeys.Should().Contain("PipelineName");
        paramKeys.Should().Contain("RenameMode");
        paramKeys.Should().Contain("CollisionStrategy");
        paramKeys.Should().NotContain("Pattern");
        paramKeys.Should().NotContain("NameTemplate");
        paramKeys.Should().NotContain("CaseTransformation");
        paramKeys.Should().NotContain("MethodSteps");
    }

    [Fact]
    public void NodeViewModel_ShouldPopulateCustomActions_FromNodeDefinition()
    {
        // Arrange
        var renamerNode = new AdvancedRenamerNode();
        using var renamerVm = new NodeViewModel(renamerNode, new Point(0, 0));

        var varInjectorNode = new VariableInjectorNode();
        using var varInjectorVm = new NodeViewModel(varInjectorNode, new Point(0, 0));

        // Act & Assert
        renamerVm.CustomActions.Should().HaveCount(1);
        renamerVm.CustomActions[0].ActionId.Should().Be("OpenRenamerPipeline");
        renamerVm.CustomActions[0].Title.Should().Contain("Pipeline");

        varInjectorVm.CustomActions.Should().HaveCount(1);
        varInjectorVm.CustomActions[0].ActionId.Should().Be("AddVariable");
    }
}
