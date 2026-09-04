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
    public async Task ResolveModelPathAsync_WithAuto_ShouldSelectOptimalModel()
    {
        // Arrange
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        // Auto resuelve al óptimo según HardwareCapabilityDetector
        var optimal = HardwareCapabilityDetector.GetOptimalModelForTask(AiTaskType.ObjectDetection);
        optimal.Should().NotBeNull();
        optimal.Id.Should().StartWith("yolov8");
    }

    [Fact]
    public void ObjectDetectorNode_ParametersAndDescriptors_ShouldSupportOfficialModels()
    {
        // Arrange & Act
        var node = new ObjectDetectorNode();

        // Assert
        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.EditorType.Should().Be(ParameterEditorType.Dropdown);
        modelDesc.Options.Should().Contain(["Auto", "yolov8n", "yolov8s", "yolov8m"]);
        modelDesc.Options.Should().NotContain("Custom");
    }

    [Fact]
    public void FaceDetectorNode_ParametersAndDescriptors_ShouldSupportOfficialModels()
    {
        // Arrange & Act
        var node = new FaceDetectorNode();

        // Assert
        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "ultraface"]);
        modelDesc.Options.Should().NotContain("Custom");
    }

    [Fact]
    public void SmartImageClassifierNode_ParametersAndDescriptors_ShouldSupportOfficialModels()
    {
        // Arrange & Act
        var node = new SmartImageClassifierNode();

        // Assert
        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "mobilenetv2"]);
        modelDesc.Options.Should().NotContain("Custom");
    }

    [Fact]
    public void LocalAiTranslatorNode_ParametersAndDescriptors_ShouldSupportOfficialModels()
    {
        // Arrange & Act
        var node = new LocalAiTranslatorNode();

        // Assert
        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "nllb-200-600m", "marian-es-en", "marian-en-es"]);
        modelDesc.Options.Should().NotContain("Custom");
    }

    [Fact]
    public void LocalLlmProcessorNode_ParametersAndDescriptors_ShouldSupportOfficialModels()
    {
        // Arrange & Act
        var node = new LocalLlmProcessorNode();

        // Assert
        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "qwen2.5-1.5b-instruct"]);
        modelDesc.Options.Should().NotContain("Custom");
    }

    [Fact]
    public void LocalWhisperTranscriberNode_ParametersAndDescriptors_ShouldSupportOfficialModels()
    {
        // Arrange & Act
        var node = new LocalWhisperTranscriberNode();

        // Assert
        node.Parameters.Should().ContainKey("ModelSize");
        node.Parameters["ModelSize"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "ModelSize");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "Tiny", "Base", "Small"]);
        modelDesc.Options.Should().NotContain("Custom");
    }
}
