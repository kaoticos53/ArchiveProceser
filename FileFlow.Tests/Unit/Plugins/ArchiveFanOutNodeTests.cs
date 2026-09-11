using System.IO;
using System.IO.Compression;
using FileFlow.Plugin.Archives;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class ArchiveFanOutNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldEmitItemsWithSessionMetadata_WhenArchiveIsValid()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_FanOutTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string archivePath = Path.Combine(tempDir, "SampleComic.cbz");
        string workingDir = Path.Combine(tempDir, "Sessions");

        // Create a real zip file with 3 images and 1 metadata file
        using (var zipStream = new FileStream(archivePath, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var e1 = archive.CreateEntry("001.jpg");
            using (var writer = new StreamWriter(e1.Open())) writer.Write("dummy image 1 content");

            var e2 = archive.CreateEntry("002.jpg");
            using (var writer = new StreamWriter(e2.Open())) writer.Write("dummy image 2 content");

            var e3 = archive.CreateEntry("003.jpg");
            using (var writer = new StreamWriter(e3.Open())) writer.Write("dummy image 3 content");

            var eMeta = archive.CreateEntry("ComicInfo.xml");
            using (var writer = new StreamWriter(eMeta.Open())) writer.Write("<ComicInfo><Title>Hero</Title></ComicInfo>");
        }

        try
        {
            var node = new ArchiveFanOutNode();
            node.Parameters["WorkingFolder"] = workingDir;

            var inputItem = new FileItemContext(archivePath, isDirectory: false);
            var emittedItems = new List<FileItemContext>();

            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("ItemOut", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedItems.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", inputItem, mockContext.Object, CancellationToken.None);

            // Assert
            emittedItems.Should().HaveCount(4);

            string? sessionId = null;
            foreach (var item in emittedItems)
            {
                item.Metadata.Should().ContainKey("Archive:SessionId");
                item.Metadata.Should().ContainKey("Archive:TotalEntries");
                item.Metadata["Archive:TotalEntries"].Should().Be(4);
                item.Metadata.Should().ContainKey("Archive:OriginalArchiveFileName");
                item.Metadata["Archive:OriginalArchiveFileName"].Should().Be("SampleComic.cbz");

                if (sessionId == null)
                {
                    sessionId = item.Metadata["Archive:SessionId"]?.ToString();
                    sessionId.Should().NotBeNullOrWhiteSpace();
                }
                else
                {
                    item.Metadata["Archive:SessionId"].Should().Be(sessionId);
                }

                File.Exists(item.CurrentPath).Should().BeTrue();
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitError_WhenArchiveDoesNotExist()
    {
        // Arrange
        var node = new ArchiveFanOutNode();
        var item = new FileItemContext(@"C:\FakePath_" + Guid.NewGuid() + ".zip", isDirectory: false);

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                   .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        mockContext.Verify(c => c.EmitAsync("Error", item), Times.Once);
        mockContext.Verify(c => c.EmitAsync("ItemOut", It.IsAny<FileItemContext>()), Times.Never);
    }
}
