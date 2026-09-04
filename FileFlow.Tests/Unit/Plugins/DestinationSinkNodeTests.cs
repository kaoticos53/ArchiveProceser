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

public class DestinationSinkNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCopyFileToDestinationRoot_WhenValidFileAndDirectoryGiven()
    {
        // Arrange
        string sourceDir = Path.Combine(Path.GetTempPath(), "DestSinkSource_" + Guid.NewGuid());
        string destDir = Path.Combine(Path.GetTempPath(), "DestSinkTarget_" + Guid.NewGuid());
        Directory.CreateDirectory(sourceDir);

        string sourceFile = Path.Combine(sourceDir, "document.txt");
        File.WriteAllText(sourceFile, "Destination Sink Content");

        try
        {
            var node = new DestinationSinkNode();
            node.Parameters["DestinationRoot"] = destDir;
            node.Parameters["ConflictStrategy"] = "Overwrite";

            var item = new FileItemContext(sourceFile, isDirectory: false);
            var emittedDone = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Done", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedDone.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedDone.Should().HaveCount(1);
            string expectedTarget = Path.Combine(destDir, "document.txt");
            File.Exists(expectedTarget).Should().BeTrue();
            File.ReadAllText(expectedTarget).Should().Be("Destination Sink Content");
            emittedDone[0].CurrentPath.Should().Be(expectedTarget);
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRenameIncrementally_WhenConflictStrategyIsRenameIncremental()
    {
        // Arrange
        string sourceDir = Path.Combine(Path.GetTempPath(), "DestSinkSource_" + Guid.NewGuid());
        string destDir = Path.Combine(Path.GetTempPath(), "DestSinkTarget_" + Guid.NewGuid());
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destDir);

        string sourceFile = Path.Combine(sourceDir, "data.txt");
        File.WriteAllText(sourceFile, "New Data");

        // Pre-create existing file in destination to force conflict
        string existingFile = Path.Combine(destDir, "data.txt");
        File.WriteAllText(existingFile, "Original Data");

        try
        {
            var node = new DestinationSinkNode();
            node.Parameters["DestinationRoot"] = destDir;
            node.Parameters["ConflictStrategy"] = "RenameIncremental";

            var item = new FileItemContext(sourceFile, isDirectory: false);
            var emittedDone = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Done", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedDone.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedDone.Should().HaveCount(1);
            string expectedIncrementalTarget = Path.Combine(destDir, "data_1.txt");
            File.Exists(expectedIncrementalTarget).Should().BeTrue();
            File.ReadAllText(existingFile).Should().Be("Original Data");
            File.ReadAllText(expectedIncrementalTarget).Should().Be("New Data");
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitToErrorPort_WhenInputFileDoesNotExist()
    {
        // Arrange
        string nonExistentFile = @"C:\NonExistentFolder\FakeFile_" + Guid.NewGuid() + ".txt";
        var node = new DestinationSinkNode();
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
    public async Task ExecuteAsync_WhenItemWasTransformedByPriorNode_ShouldCopyTransformedFileNotOriginal()
    {
        // Arrange
        string sourceDir = Path.Combine(Path.GetTempPath(), "DestSinkOrig_" + Guid.NewGuid());
        string processedDir = Path.Combine(Path.GetTempPath(), "DestSinkProc_" + Guid.NewGuid());
        string finalDestDir = Path.Combine(Path.GetTempPath(), "DestSinkFinal_" + Guid.NewGuid());
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(processedDir);

        string originalFile = Path.Combine(sourceDir, "photo.jpg");
        File.WriteAllText(originalFile, "RAW_ORIGINAL_IMAGE_BYTES");

        string processedFile = Path.Combine(processedDir, "photo_nobg.png");
        File.WriteAllText(processedFile, "TRANSPARENT_NOBG_IMAGE_BYTES");

        try
        {
            var node = new DestinationSinkNode();
            node.Parameters["DestinationRoot"] = finalDestDir;
            node.Parameters["ConflictStrategy"] = "Overwrite";

            // Simular un FileItemContext que salió de BackgroundRemoverNode
            var item = new FileItemContext(originalFile, isDirectory: false);
            item.CurrentPath = processedFile;
            item.PhysicalPath = processedFile;

            var emittedDone = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Done", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedDone.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            string expectedTarget = Path.Combine(finalDestDir, "photo_nobg.png");
            File.Exists(expectedTarget).Should().BeTrue();
            File.ReadAllText(expectedTarget).Should().Be("TRANSPARENT_NOBG_IMAGE_BYTES");
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            if (Directory.Exists(processedDir)) Directory.Delete(processedDir, true);
            if (Directory.Exists(finalDestDir)) Directory.Delete(finalDestDir, true);
        }
    }
}
