using System.IO;
using System.IO.Compression;
using FileFlow.Plugin.Archives;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class SmartUnpackNodeTests : IDisposable
{
    private readonly string _testRoot;

    public SmartUnpackNodeTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SmartUnpackNodeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitError_WhenArchiveFileDoesNotExist()
    {
        // Arrange
        string nonExistentFile = Path.Combine(_testRoot, "FakeArchive_" + Guid.NewGuid() + ".zip");
        var node = new SmartUnpackNode();
        var item = new FileItemContext(nonExistentFile, isDirectory: false);

        var emittedErrors = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync("Error", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((port, emItem) => emittedErrors.Add(emItem))
                   .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedErrors.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUnpackRealCbzWithMultipleFolders_AndEmitOutPort()
    {
        // Arrange
        string cbzPath = Path.Combine(_testRoot, "ComicBook_01.cbz");
        using (var archive = ZipFile.Open(cbzPath, ZipArchiveMode.Create))
        {
            var p1 = archive.CreateEntry("pages/01.jpg");
            using (var w = new StreamWriter(p1.Open())) w.Write("IMAGE1");

            var p2 = archive.CreateEntry("pages/02.jpg");
            using (var w = new StreamWriter(p2.Open())) w.Write("IMAGE2");
        }

        string destFolder = Path.Combine(_testRoot, "UnpackedResult");
        var node = new SmartUnpackNode();
        node.Parameters["DestinationFolder"] = destFolder;
        node.Parameters["ExtractionEngine"] = "Auto";
        node.Parameters["CleanWrapper"] = false;

        var item = new FileItemContext(cbzPath, isDirectory: false);
        var emittedOut = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                   .Callback<string, FileItemContext>((port, emItem) => emittedOut.Add(emItem))
                   .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedOut.Should().HaveCount(1);
        var outItem = emittedOut[0];
        outItem.Metadata["UnpackedFileCount"].Should().Be(2);

        string expectedExtractDir = Path.Combine(destFolder, "ComicBook_01");
        File.Exists(Path.Combine(expectedExtractDir, "pages", "01.jpg")).Should().BeTrue();
        File.Exists(Path.Combine(expectedExtractDir, "pages", "02.jpg")).Should().BeTrue();
    }
}
