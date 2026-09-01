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

public class OriginalFileActionNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldKeepOriginalFile_WhenActionTypeIsKeep()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), "OrigActionKeep_" + Guid.NewGuid() + ".txt");
        File.WriteAllText(tempFile, "Keep Me");

        try
        {
            var node = new OriginalFileActionNode();
            node.Parameters["ActionType"] = "Keep";

            var item = new FileItemContext(tempFile, isDirectory: false);
            var emittedOut = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedOut.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedOut.Should().HaveCount(1);
            File.Exists(tempFile).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMoveFileToQuarantine_WhenActionTypeIsMoveToQuarantine()
    {
        // Arrange
        string sourceDir = Path.Combine(Path.GetTempPath(), "OrigSource_" + Guid.NewGuid());
        string quarantineDir = Path.Combine(Path.GetTempPath(), "OrigQuarantine_" + Guid.NewGuid());
        Directory.CreateDirectory(sourceDir);

        string sourceFile = Path.Combine(sourceDir, "infected.txt");
        File.WriteAllText(sourceFile, "Quarantine Content");

        try
        {
            var node = new OriginalFileActionNode();
            node.Parameters["ActionType"] = "MoveToQuarantine";
            node.Parameters["QuarantinePath"] = quarantineDir;

            var item = new FileItemContext(sourceFile, isDirectory: false);
            var emittedOut = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedOut.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedOut.Should().HaveCount(1);
            File.Exists(sourceFile).Should().BeFalse();
            string expectedQuarantineFile = Path.Combine(quarantineDir, "infected.txt");
            File.Exists(expectedQuarantineFile).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            if (Directory.Exists(quarantineDir)) Directory.Delete(quarantineDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteFilePermanently_WhenActionTypeIsPermanentDelete()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), "OrigDelete_" + Guid.NewGuid() + ".tmp");
        File.WriteAllText(tempFile, "Delete Me");

        try
        {
            var node = new OriginalFileActionNode();
            node.Parameters["ActionType"] = "PermanentDelete";

            var item = new FileItemContext(tempFile, isDirectory: false);
            var emittedOut = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedOut.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedOut.Should().HaveCount(1);
            File.Exists(tempFile).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecycleFile_WhenActionTypeIsMoveToRecycleBin()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), "OrigRecycle_" + Guid.NewGuid() + ".tmp");
        File.WriteAllText(tempFile, "Recycle Me");

        try
        {
            var node = new OriginalFileActionNode();
            node.Parameters["ActionType"] = "MoveToRecycleBin";

            var item = new FileItemContext(tempFile, isDirectory: false);
            var emittedOut = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedOut.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedOut.Should().HaveCount(1);
            if (OperatingSystem.IsWindows())
            {
                File.Exists(tempFile).Should().BeFalse();
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotModifyFileOnDisk_WhenDryRunIsActive()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), "OrigDryRun_" + Guid.NewGuid() + ".tmp");
        File.WriteAllText(tempFile, "Dry Run Content");

        try
        {
            var node = new OriginalFileActionNode();
            node.Parameters["ActionType"] = "PermanentDelete";

            var item = new FileItemContext(tempFile, isDirectory: false);
            item.Metadata["DryRun"] = true;

            var emittedOut = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedOut.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedOut.Should().HaveCount(1);
            File.Exists(tempFile).Should().BeTrue(); // DryRun prevents deletion!
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
