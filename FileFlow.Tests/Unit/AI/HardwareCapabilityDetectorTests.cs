using FileFlow.Plugin.AI;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.AI;

public class HardwareCapabilityDetectorTests
{
    [Fact]
    public void Specs_ShouldReturnRealisticHardwareValues()
    {
        // Act
        var specs = HardwareCapabilityDetector.Specs;

        // Assert
        specs.Should().NotBeNull();
        specs.TotalRamBytes.Should().BeGreaterThan(0, "Debe detectar memoria RAM física del equipo.");
        specs.LogicalCores.Should().BeGreaterThanOrEqualTo(1, "Debe haber al menos 1 núcleo de CPU.");
        specs.TotalRamGb.Should().BeGreaterThan(0);
        specs.HardwareTier.Should().BeOneOf("Lightweight", "Balanced", "Performance");
    }

    [Fact]
    public void GetCompatibility_WithLowRamRequirement_ShouldBePlayableOrRecommended()
    {
        // Arrange
        var lowModel = new AiModelInfo(
            Id: "mock-light",
            FileName: "light.onnx",
            DownloadUrl: "http://example.com/light.onnx",
            MinSizeBytes: 1000,
            Description: "Mock Lightweight",
            TaskType: AiTaskType.FaceDetection,
            MinRamBytes: 100_000_000, // 100 MB
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        );

        // Act
        var compatibility = HardwareCapabilityDetector.GetCompatibility(lowModel);

        // Assert
        compatibility.Should().BeOneOf(ModelCompatibility.Recommended, ModelCompatibility.Playable);
    }

    [Fact]
    public void GetCompatibility_WithImpossiblyHighRamRequirement_ShouldReturnInsufficientHardware()
    {
        // Arrange (100 TB de RAM)
        var heavyModel = new AiModelInfo(
            Id: "mock-huge",
            FileName: "huge.onnx",
            DownloadUrl: "http://example.com/huge.onnx",
            MinSizeBytes: 1000,
            Description: "Mock Huge",
            TaskType: AiTaskType.TextGenerationLlm,
            MinRamBytes: 100_000_000_000_000L,
            GpuRecommended: true,
            HardwareTier: "Performance"
        );

        // Act
        var compatibility = HardwareCapabilityDetector.GetCompatibility(heavyModel);

        // Assert
        compatibility.Should().Be(ModelCompatibility.InsufficientHardware);
    }

    [Theory]
    [InlineData(AiTaskType.ObjectDetection)]
    [InlineData(AiTaskType.FaceDetection)]
    [InlineData(AiTaskType.ImageClassification)]
    [InlineData(AiTaskType.SpeechToText)]
    [InlineData(AiTaskType.TextTranslation)]
    [InlineData(AiTaskType.TextGenerationLlm)]
    [InlineData(AiTaskType.Ocr)]
    public void GetOptimalModelForTask_ShouldReturnValidModelMatchingTaskType(AiTaskType task)
    {
        // Act
        var model = HardwareCapabilityDetector.GetOptimalModelForTask(task);

        // Assert
        model.Should().NotBeNull();
        model.TaskType.Should().Be(task);
        model.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AiModelManager_GetModelsForTask_ShouldReturnCatalogModelsForSpecificTask()
    {
        // Act
        var objectDetectionModels = AiModelManager.GetModelsForTask(AiTaskType.ObjectDetection);
        var translationModels = AiModelManager.GetModelsForTask(AiTaskType.TextTranslation);

        // Assert
        objectDetectionModels.Should().NotBeEmpty();
        objectDetectionModels.Should().OnlyContain(m => m.TaskType == AiTaskType.ObjectDetection);
        objectDetectionModels.Select(m => m.Id).Should().Contain(["tiny-yolov3", "grounding-dino"]);

        translationModels.Should().NotBeEmpty();
        translationModels.Should().OnlyContain(m => m.TaskType == AiTaskType.TextTranslation);
        translationModels.Select(m => m.Id).Should().Contain(["marian-es-en", "marian-en-es", "nllb-200-600m"]);
    }
}
