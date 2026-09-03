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

public class LocalAiTranslatorNodeTests : IDisposable
{
    private readonly string _tempDir;

    public LocalAiTranslatorNodeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_TranslatorTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task ExecuteAsync_WithPlainTextFile_ShouldTranslateAndInjectMetadata()
    {
        // Arrange
        string testFilePath = Path.Combine(_tempDir, "documento.txt");
        await File.WriteAllTextAsync(testFilePath, "Hola mundo. Este es un archivo y documento de prueba.");

        var item = new FileItemContext(testFilePath);
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new LocalAiTranslatorNode();
        node.Parameters["SourceLanguage"] = "Spanish";
        node.Parameters["TargetLanguage"] = "English";
        node.Parameters["InputSource"] = "FileContent";
        node.Parameters["OutputMode"] = "InjectMetadata";

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Translated", item), Times.Once);
        item.Metadata.Should().ContainKey("AI:TranslatedText");
        string translated = item.Metadata["AI:TranslatedText"]?.ToString() ?? string.Empty;
        translated.Should().Contain("world");
        item.Metadata["AI:SourceLanguage"].Should().Be("es");
        item.Metadata["AI:TargetLanguage"].Should().Be("en");
    }

    [Fact]
    public async Task ExecuteAsync_WithMetadataKey_ShouldTranslateFromMetadata()
    {
        // Arrange
        var item = new FileItemContext(Path.Combine(_tempDir, "virtual.txt"))
        {
            Metadata = { ["Ocr:Text"] = "Hola mundo, gracias por su ayuda." }
        };
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new LocalAiTranslatorNode();
        node.Parameters["InputSource"] = "MetadataKey";
        node.Parameters["MetadataKeyName"] = "Ocr:Text";
        node.Parameters["SourceLanguage"] = "Spanish";
        node.Parameters["TargetLanguage"] = "English";
        node.Parameters["OutputMode"] = "InjectMetadata";

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Translated", item), Times.Once);
        item.Metadata.Should().ContainKey("AI:TranslatedText");
        string translated = item.Metadata["AI:TranslatedText"]?.ToString() ?? string.Empty;
        translated.Should().Contain("world");
    }

    [Fact]
    public async Task ExecuteAsync_WithSrtSubtitles_ShouldPreserveTimestampsAndTranslateDialogue()
    {
        // Arrange
        string srtContent = "1\r\n00:00:01,000 --> 00:00:04,000\r\nHola mundo\r\n\r\n2\r\n00:00:05,000 --> 00:00:08,000\r\nGracias\r\n";
        string srtPath = Path.Combine(_tempDir, "subtitulos.srt");
        await File.WriteAllTextAsync(srtPath, srtContent);

        var item = new FileItemContext(srtPath);
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new LocalAiTranslatorNode();
        node.Parameters["SourceLanguage"] = "Spanish";
        node.Parameters["TargetLanguage"] = "English";
        node.Parameters["InputSource"] = "FileContent";
        node.Parameters["TranslateSrtTimestamps"] = true;
        node.Parameters["OutputMode"] = "InjectMetadata";

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Translated", item), Times.Once);
        string translated = item.Metadata["AI:TranslatedText"]?.ToString() ?? string.Empty;

        // Timestamps must be strictly preserved
        translated.Should().Contain("00:00:01,000 --> 00:00:04,000");
        translated.Should().Contain("00:00:05,000 --> 00:00:08,000");
        translated.Should().Contain("world");
    }

    [Fact]
    public async Task ExecuteAsync_WithCreateNewFileMode_ShouldWriteOutputFile()
    {
        // Arrange
        string testFilePath = Path.Combine(_tempDir, "guia.txt");
        await File.WriteAllTextAsync(testFilePath, "Bienvenido al sistema.");

        var item = new FileItemContext(testFilePath);
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new LocalAiTranslatorNode();
        node.Parameters["SourceLanguage"] = "Spanish";
        node.Parameters["TargetLanguage"] = "English";
        node.Parameters["InputSource"] = "FileContent";
        node.Parameters["OutputMode"] = "CreateNewFile";
        node.Parameters["TargetFileNamePattern"] = "{FileNameWithoutExt}_{TargetLang}{Ext}";

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Translated", item), Times.Once);
        string expectedNewFile = Path.Combine(_tempDir, "guia_en.txt");
        File.Exists(expectedNewFile).Should().BeTrue();
        item.CurrentPath.Should().Be(expectedNewFile);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFileNotFound_ShouldEmitError()
    {
        // Arrange
        var item = new FileItemContext(Path.Combine(_tempDir, "inexistente.txt"));
        var mockContext = new Mock<IFlowExecutionContext>();

        var node = new LocalAiTranslatorNode();
        node.Parameters["InputSource"] = "FileContent";

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
    }
}
