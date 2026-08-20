using System.IO;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class FolderSourceNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldEmitItemsWithSourceRootPathAndCounter_WhenDirectoryHasFiles()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowTestFolder_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string file1 = Path.Combine(tempDir, "test1.txt");
        File.WriteAllText(file1, "hello");

        try
        {
            var node = new FolderSourceNode();
            node.Parameters["SourcePath"] = tempDir;
            node.Parameters["Recursive"] = false;

            var emittedItems = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, item) => emittedItems.Add(item))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("", new FileItemContext("", false), mockContext.Object, CancellationToken.None);

            // Assert
            emittedItems.Should().HaveCount(1);
            emittedItems[0].Metadata.Should().ContainKey("SourceRootPath");
            emittedItems[0].Metadata["SourceRootPath"].Should().Be(tempDir);
            emittedItems[0].Metadata.Should().ContainKey("Counter");
            emittedItems[0].Metadata["Counter"].Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
