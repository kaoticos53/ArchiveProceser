using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class DirectoryInspectorNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldEmitToSingleArchivePort_WhenFolderContainsOnlyOneArchiveFile()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "DirInspectorSingle_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string zipFile = Path.Combine(tempDir, "data.zip");
        File.WriteAllText(zipFile, "Dummy ZIP");

        try
        {
            var node = new DirectoryInspectorNode();
            var item = new FileItemContext(tempDir, isDirectory: true);

            var emittedPort = string.Empty;
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedPort = port)
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedPort.Should().Be("SingleArchive");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitToDirectoriesOnlyPort_WhenFolderContainsOnlySubdirectories()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "DirInspectorSubdirs_" + Guid.NewGuid());
        string subDir = Path.Combine(tempDir, "SubFolder1");
        Directory.CreateDirectory(subDir);

        try
        {
            var node = new DirectoryInspectorNode();
            var item = new FileItemContext(tempDir, isDirectory: true);

            var emittedPort = string.Empty;
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedPort = port)
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedPort.Should().Be("DirectoriesOnly");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitToMixedContentPort_WhenFolderContainsMultipleFiles()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "DirInspectorMixed_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "f1.txt"), "File 1");
        File.WriteAllText(Path.Combine(tempDir, "f2.txt"), "File 2");

        try
        {
            var node = new DirectoryInspectorNode();
            var item = new FileItemContext(tempDir, isDirectory: true);

            var emittedPort = string.Empty;
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedPort = port)
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedPort.Should().Be("MixedContent");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
