using System.IO;
using FileFlow.Plugin.Archives;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Integrations;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class NewNodesPhase2Tests
{
    [Fact]
    public async Task ArchiveCompressorNode_CreatesZipArchive_FromInputFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            string sampleFile = Path.Combine(tempDir, "document.txt");
            await File.WriteAllTextAsync(sampleFile, "Compress Me");

            var node = new ArchiveCompressorNode();
            node.Parameters["DestinationDirectory"] = tempDir;
            node.Parameters["ArchiveName"] = "test.zip";

            var contextMock = new Mock<IFlowExecutionContext>();
            FileItemContext? emittedItem = null;

            contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                .Callback<string, FileItemContext>((_, item) => emittedItem = item)
                .Returns(Task.CompletedTask);

            var inputItem = new FileItemContext(sampleFile);
            await node.ExecuteAsync("In", inputItem, contextMock.Object, CancellationToken.None);

            emittedItem.Should().NotBeNull();
            string zipPath = Path.Combine(tempDir, "test.zip");
            File.Exists(zipPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DocumentProcessorNode_InspectsTextDocument()
    {
        string tempFile = Path.GetTempFileName() + ".txt";
        await File.WriteAllTextAsync(tempFile, "Line 1\nLine 2\nLine 3");

        try
        {
            var node = new DocumentProcessorNode();
            var contextMock = new Mock<IFlowExecutionContext>();
            FileItemContext? emittedItem = null;

            contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                .Callback<string, FileItemContext>((_, item) => emittedItem = item)
                .Returns(Task.CompletedTask);

            var inputItem = new FileItemContext(tempFile);
            await node.ExecuteAsync("In", inputItem, contextMock.Object, CancellationToken.None);

            emittedItem.Should().NotBeNull();
            emittedItem!.Metadata.Should().ContainKey("DocumentLineCount");
            emittedItem.Metadata["DocumentLineCount"].Should().Be(3);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task MediaTranscoderNode_ProcessesPreset()
    {
        string tempFile = Path.GetTempFileName() + ".mp4";
        await File.WriteAllTextAsync(tempFile, "video content");

        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var node = new MediaTranscoderNode();
            node.Parameters["Preset"] = "ExtractAudioMP3";
            node.Parameters["DestinationDirectory"] = tempDir;

            var contextMock = new Mock<IFlowExecutionContext>();
            FileItemContext? emittedItem = null;

            contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                .Callback<string, FileItemContext>((_, item) => emittedItem = item)
                .Returns(Task.CompletedTask);

            var inputItem = new FileItemContext(tempFile);
            await node.ExecuteAsync("In", inputItem, contextMock.Object, CancellationToken.None);

            emittedItem.Should().NotBeNull();
            emittedItem!.Metadata["TranscodePreset"].Should().Be("ExtractAudioMP3");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
