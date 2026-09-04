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

public class AudioSuiteNodesTests : IDisposable
{
    private readonly string _tempDir;

    public AudioSuiteNodesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_AudioTests_" + Guid.NewGuid().ToString("N"));
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
    public void VoiceActivityDetectorNode_ShouldHaveValidPortsAndParameters()
    {
        // Arrange & Act
        var node = new VoiceActivityDetectorNode();

        // Assert
        node.Inputs.Should().ContainSingle(p => p.Name == "In");
        node.Outputs.Select(p => p.Name).Should().Contain(["Speech", "Silent", "Out", "Error"]);

        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");
        node.Parameters.Should().ContainKey("Mode");
        node.Parameters["Mode"].Should().Be("DetectOnly");
        node.Parameters.Should().ContainKey("SensitivityThreshold");
        node.Parameters["SensitivityThreshold"].Should().Be(0.5);
        node.Parameters.Should().ContainKey("MinSpeechDurationMs");
        node.Parameters.Should().ContainKey("PaddingDurationMs");
        node.Parameters.Should().ContainKey("OutputDirectory");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "silero-vad"]);
        modelDesc.Options.Should().NotContain("Custom");

        var modeDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Mode");
        modeDesc.Should().NotBeNull();
        modeDesc!.Options.Should().Contain(["DetectOnly", "TrimSilence"]);
    }

    [Fact]
    public void TextToSpeechNode_ShouldHaveValidPortsAndParameters()
    {
        // Arrange & Act
        var node = new TextToSpeechNode();

        // Assert
        node.Inputs.Should().ContainSingle(p => p.Name == "In");
        node.Outputs.Select(p => p.Name).Should().Contain(["Out", "Error"]);

        node.Parameters.Should().ContainKey("Model");
        node.Parameters["Model"].Should().Be("Auto");
        node.Parameters.Should().NotContainKey("CustomModelPath");
        node.Parameters.Should().ContainKey("InputSource");
        node.Parameters["InputSource"].Should().Be("FileContent");
        node.Parameters.Should().ContainKey("MetadataKeyName");
        node.Parameters.Should().ContainKey("SpeechRate");
        node.Parameters["SpeechRate"].Should().Be(1.0);
        node.Parameters.Should().ContainKey("OutputDirectory");

        var modelDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "Model");
        modelDesc.Should().NotBeNull();
        modelDesc!.Options.Should().Contain(["Auto", "piper-es-davefx", "piper-en-lessac"]);
        modelDesc.Options.Should().NotContain("Custom");

        var sourceDesc = node.ParameterDescriptors.FirstOrDefault(d => d.Key == "InputSource");
        sourceDesc.Should().NotBeNull();
        sourceDesc!.Options.Should().Contain(["FileContent", "MetadataKey", "CustomText"]);
    }

    [Fact]
    public void Catalog_ShouldContainAudioModelsWithCorrectTaskTypes()
    {
        // Act & Assert
        var vadModels = AiModelManager.GetModelsForTask(AiTaskType.VoiceActivityDetection);
        vadModels.Should().NotBeEmpty();
        vadModels.Select(m => m.Id).Should().Contain("silero-vad");

        var ttsModels = AiModelManager.GetModelsForTask(AiTaskType.TextToSpeech);
        ttsModels.Should().NotBeEmpty();
        ttsModels.Select(m => m.Id).Should().Contain(["piper-es-davefx", "piper-en-lessac"]);
    }

    [Theory]
    [InlineData(AiTaskType.VoiceActivityDetection)]
    [InlineData(AiTaskType.TextToSpeech)]
    public void HardwareCapabilityDetector_ShouldSelectOptimalModelForAudioTasks(AiTaskType task)
    {
        // Act
        var model = HardwareCapabilityDetector.GetOptimalModelForTask(task);

        // Assert
        model.Should().NotBeNull();
        model.TaskType.Should().Be(task);
        model.FileName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task VoiceActivityDetectorNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError()
    {
        // Arrange
        var node = new VoiceActivityDetectorNode();
        var item = new FileItemContext(Path.Combine(_tempDir, "missing_audio.wav"));
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
    }

    [Fact]
    public async Task VoiceActivityDetectorNode_ExecuteAsync_WithUnsupportedExtension_ShouldEmitSilentAndOut()
    {
        // Arrange
        string textFile = Path.Combine(_tempDir, "sample.txt");
        await File.WriteAllTextAsync(textFile, "Hello world");
        var node = new VoiceActivityDetectorNode();
        var item = new FileItemContext(textFile);
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Silent", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("Out", item), Times.Once);
    }

    [Fact]
    public async Task TextToSpeechNode_ExecuteAsync_WithNonExistentFile_ShouldEmitError()
    {
        // Arrange
        var node = new TextToSpeechNode();
        var item = new FileItemContext(Path.Combine(_tempDir, "missing_text.txt"));
        var mockContext = new Mock<IFlowExecutionContext>();

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
    }

    [Fact]
    public async Task AudioInferenceEngine_SynthesizeSpeech_ShouldGenerateValidWavFile()
    {
        // Arrange
        string outputWav = Path.Combine(_tempDir, "test_synth.wav");
        string text = "FileFlow Studio es una plataforma modular de automatización de archivos.";

        // Act
        double duration = await AudioInferenceEngine.SynthesizeSpeechAsync(null, text, outputWav, 1.0, CancellationToken.None);

        // Assert
        File.Exists(outputWav).Should().BeTrue();
        new FileInfo(outputWav).Length.Should().BeGreaterThan(1000);
        duration.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task AudioInferenceEngine_DetectVoiceActivity_OnGeneratedAudio_ShouldAnalyzeSamples()
    {
        // Arrange
        string outputWav = Path.Combine(_tempDir, "speech_sample.wav");
        string text = "Probando la detección de voz con FileFlow Audio.";
        await AudioInferenceEngine.SynthesizeSpeechAsync(null, text, outputWav, 1.0, CancellationToken.None);

        // Act
        var result = await AudioInferenceEngine.DetectVoiceActivityAsync(null, outputWav, threshold: 0.3, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalDurationSeconds.Should().BeGreaterThan(0.0);
    }
}
