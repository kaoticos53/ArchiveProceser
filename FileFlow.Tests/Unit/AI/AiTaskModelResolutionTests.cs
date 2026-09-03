using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public class AiTaskModelResolutionTests : IDisposable
{
    private readonly string _tempDir;

    public AiTaskModelResolutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_ResolutionTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task ResolveModelPathAsync_WithCustomAndEmptyPath_ShouldReturnNull()
    {
        // Arrange
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        var result = await AiModelManager.ResolveModelPathAsync("Custom", "", AiTaskType.ObjectDetection, mockContext.Object);

        // Assert
        result.Should().BeNull();
        mockContext.Verify(c => c.Log(It.Is<string>(s => s.Contains("Custom") && s.Contains("vacía")), LogLevel.Error, (FileItemContext?)null, It.IsAny<double>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ResolveModelPathAsync_WithCustomAndNonExistentFile_ShouldReturnNull()
    {
        // Arrange
        var mockContext = new Mock<IFlowExecutionContext>();
        string nonExistent = Path.Combine(_tempDir, "does_not_exist.onnx");

        // Act
        var result = await AiModelManager.ResolveModelPathAsync("Custom", nonExistent, AiTaskType.ObjectDetection, mockContext.Object);

        // Assert
        result.Should().BeNull();
        mockContext.Verify(c => c.Log(It.Is<string>(s => s.Contains("no encontrado")), LogLevel.Error, (FileItemContext?)null, It.IsAny<double>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ResolveModelPathAsync_WithCustomAndExistingFile_ShouldReturnFullPath()
    {
        // Arrange
        var mockContext = new Mock<IFlowExecutionContext>();
        string customFile = Path.Combine(_tempDir, "my_custom_model.onnx");
        await File.WriteAllBytesAsync(customFile, new byte[1024]);

        // Act
        var result = await AiModelManager.ResolveModelPathAsync("Custom", customFile, AiTaskType.ObjectDetection, mockContext.Object);

        // Assert
        result.Should().Be(Path.GetFullPath(customFile));
        mockContext.Verify(c => c.Log(It.Is<string>(s => s.Contains("Usando modelo personalizado")), LogLevel.Information, (FileItemContext?)null, It.IsAny<double>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public void ObjectDetectorNode_ParametersAndDescriptors_ShouldSupportModelAndCustomPath()
    {
        // Arrange & Act
        var node = new ObjectDetectorNode();

        // Assert
        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().ContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.EditorType.Should().Be(ParameterEditorType.Dropdown);
        modelDesc.Options.Should().Contain(["Auto", "tiny-yolov3", "grounding-dino", "Custom"]);

        var pathDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "CustomModelPath");
        pathDesc.Should().NotBeNull();
        pathDesc!.EditorType.Should().Be(ParameterEditorType.FilePath);
    }

    [Fact]
    public void FaceDetectorNode_ParametersAndDescriptors_ShouldSupportModelAndCustomPath()
    {
        // Arrange & Act
        var node = new FaceDetectorNode();

        // Assert
        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().ContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "ultraface", "Custom"]);
    }

    [Fact]
    public void SmartImageClassifierNode_ParametersAndDescriptors_ShouldSupportModelAndCustomPath()
    {
        // Arrange & Act
        var node = new SmartImageClassifierNode();

        // Assert
        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().ContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "mobilenetv2", "Custom"]);
    }

    [Fact]
    public void LocalAiTranslatorNode_ParametersAndDescriptors_ShouldSupportModelAndCustomPath()
    {
        // Arrange & Act
        var node = new LocalAiTranslatorNode();

        // Assert
        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().ContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "nllb-200-600m", "marian-es-en", "marian-en-es", "Custom"]);
    }

    [Fact]
    public void LocalLlmProcessorNode_ParametersAndDescriptors_ShouldSupportModelAndCustomPath()
    {
        // Arrange & Act
        var node = new LocalLlmProcessorNode();

        // Assert
        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().ContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "qwen2.5-1.5b-instruct", "Custom"]);
    }

    [Fact]
    public void LocalWhisperTranscriberNode_ParametersAndDescriptors_ShouldSupportAutoAndCustom()
    {
        // Arrange & Act
        var node = new LocalWhisperTranscriberNode();

        // Assert
        node.Parameters.Should().ContainKey("ModelSize");
        node.Parameters["ModelSize"].Should().Be("Auto");
        node.Parameters.Should().ContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "ModelSize");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "Tiny", "Base", "Small", "Custom"]);
    }
}
