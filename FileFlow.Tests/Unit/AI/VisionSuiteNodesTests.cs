using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public class VisionSuiteNodesTests : IDisposable
{
    private readonly string _tempDir;

    public VisionSuiteNodesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_VisionTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void BackgroundRemoverNode_ShouldHaveValidPortsAndParameters()
    {
        // Arrange & Act
        var node = new BackgroundRemoverNode();

        // Assert
        node.Inputs.Should().ContainSingle(p => p.Name == "In");
        node.Outputs.Select(p => p.Name).Should().Contain(["Out", "Bypass", "Mask", "Error"]);

        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");
        node.Parameters.Should().ContainKey("OutputMode");
        node.Parameters["OutputMode"].Should().Be("TransparentPng");
        node.Parameters.Should().ContainKey("BackgroundColor");
        node.Parameters.Should().ContainKey("OutputDirectory");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "rmbg-1.4", "modnet"]);
        modelDesc.Options.Should().NotContain("Custom");

        var modeDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "OutputMode");
        modeDesc.Should().NotBeNull();
        modeDesc!.Options.Should().Contain(["TransparentPng", "ColorBackground", "MaskOnly"]);
    }

    [Fact]
    public void SuperResolutionUpscalerNode_ShouldHaveValidPortsAndParameters()
    {
        // Arrange & Act
        var node = new SuperResolutionUpscalerNode();

        // Assert
        node.Inputs.Should().ContainSingle(p => p.Name == "In");
        node.Outputs.Select(p => p.Name).Should().Contain(["Out", "Skipped", "Error"]);

        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");
        node.Parameters.Should().ContainKey("ScaleFactor");
        node.Parameters["ScaleFactor"].Should().Be("4x");
        node.Parameters.Should().ContainKey("MaxInputDimension");
        node.Parameters["MaxInputDimension"].Should().Be(2048);

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "realesrgan-compact"]);
        modelDesc.Options.Should().NotContain("Custom");

        var scaleDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "ScaleFactor");
        scaleDesc.Should().NotBeNull();
        scaleDesc!.Options.Should().Contain(["2x", "4x"]);
    }

    [Fact]
    public void ContentModerationFilterNode_ShouldHaveValidPortsAndParameters()
    {
        // Arrange & Act
        var node = new ContentModerationFilterNode();

        // Assert
        node.Inputs.Should().ContainSingle(p => p.Name == "In");
        node.Outputs.Select(p => p.Name).Should().Contain(["Safe", "Sensitive", "Error"]);

        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");
        node.Parameters.Should().ContainKey("SensitivityThreshold");
        node.Parameters["SensitivityThreshold"].Should().Be(0.6);

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "opennsfw2"]);
        modelDesc.Options.Should().NotContain("Custom");

        var threshDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "SensitivityThreshold");
        threshDesc.Should().NotBeNull();
        threshDesc!.EditorType.Should().Be(ParameterEditorType.Slider);
    }

    [Fact]
    public void Catalog_ShouldContainNewVisionModelsWithCorrectTaskTypes()
    {
        // Act & Assert
        var bgModels = AiModelManager.GetModelsForTask(AiTaskType.BackgroundRemoval);
        bgModels.Should().NotBeEmpty();
        bgModels.Select(m => m.Id).Should().Contain(["rmbg-1.4", "modnet"]);

        var srModels = AiModelManager.GetModelsForTask(AiTaskType.SuperResolution);
        srModels.Should().NotBeEmpty();
        srModels.Select(m => m.Id).Should().Contain("realesrgan-compact");

        var modModels = AiModelManager.GetModelsForTask(AiTaskType.ContentModeration);
        modModels.Should().NotBeEmpty();
        modModels.Select(m => m.Id).Should().Contain("opennsfw2");
    }

    [Theory]
    [InlineData(AiTaskType.BackgroundRemoval)]
    [InlineData(AiTaskType.SuperResolution)]
    [InlineData(AiTaskType.ContentModeration)]
    public void HardwareCapabilityDetector_ShouldSelectOptimalModelForNewVisionTasks(AiTaskType task)
    {
        // Act
        var model = HardwareCapabilityDetector.GetOptimalModelForTask(task);

        // Assert
        model.Should().NotBeNull();
        model.TaskType.Should().Be(task);
        model.FileName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task BackgroundRemoverNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError()
    {
        // Arrange
        var node = new BackgroundRemoverNode();
        var item = new FileItemContext(Path.Combine(_tempDir, "missing.jpg"));
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
    }

    [Fact]
    public async Task SuperResolutionUpscalerNode_ExecuteAsync_WithUnsupportedFormat_ShouldEmitSkipped()
    {
        // Arrange
        string textFile = Path.Combine(_tempDir, "document.txt");
        await File.WriteAllTextAsync(textFile, "Test text content");
        var node = new SuperResolutionUpscalerNode();
        var item = new FileItemContext(textFile);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Skipped", item), Times.Once);
    }

    [Fact]
    public async Task ContentModerationFilterNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError()
    {
        // Arrange
        var node = new ContentModerationFilterNode();
        var item = new FileItemContext(Path.Combine(_tempDir, "missing.png"));
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
    }

    [Fact]
    public void ParameterHelper_ResolveOutputPath_WhenCustomDirectorySpecified_ShouldUseCustomDirectory()
    {
        // Arrange
        string sourceFile = Path.Combine(_tempDir, "sample.png");
        var item = new FileItemContext(sourceFile);
        string customOutputDir = Path.Combine(_tempDir, "CustomOutputs");

        // Act
        string resolved = ParameterHelper.ResolveOutputPath(customOutputDir, item);

        // Assert
        resolved.Should().Be(customOutputDir);
    }
}
