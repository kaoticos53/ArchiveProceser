using System;
using System.IO;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Sdk;

public class FileItemContextTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties_WhenValidFilePathGiven()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"FileItemTest_{Guid.NewGuid()}.tmp");
        File.WriteAllText(tempFile, "Hello FileFlow Studio SDK");

        try
        {
            // Act
            var context = new FileItemContext(tempFile, isDirectory: false);

            // Assert
            context.CurrentPath.Should().Be(tempFile);
            context.OriginalPath.Should().Be(tempFile);
            context.IsDirectory.Should().BeFalse();
            context.FileSizeBytes.Should().Be(25); // "Hello FileFlow Studio SDK" is 25 bytes
            context.Id.Should().NotBeEmpty();
            context.Metadata.Should().BeEmpty();
            context.Tags.Should().BeEmpty();
            context.ExecutionLog.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Constructor_ShouldSetSizeToZero_WhenPathIsDirectory()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), $"FileItemDirTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var context = new FileItemContext(tempDir, isDirectory: true);

            // Assert
            context.CurrentPath.Should().Be(tempDir);
            context.IsDirectory.Should().BeTrue();
            context.FileSizeBytes.Should().Be(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DeepClone_ShouldCreateIndependentCopy_WhenMetadataTagsAndLogsArePresent()
    {
        // Arrange
        var original = new FileItemContext(@"C:\Path\To\File.txt", isDirectory: false)
        {
            FileSizeBytes = 1024
        };
        original.Metadata["Category"] = "Document";
        original.Metadata["ProcessCount"] = 3;
        original.Tags.Add("Urgent");
        original.Tags.Add("PDF");
        original.AddLog("Initial creation log entry.");

        // Act
        var clone = original.DeepClone();

        // Mutate clone to verify independence
        clone.CurrentPath = @"C:\Path\To\NewFile.txt";
        clone.Metadata["Category"] = "Archived";
        clone.Metadata["NewKey"] = "NewValue";
        clone.Tags.Add("Processed");
        clone.AddLog("Clone modification log entry.");

        // Assert
        clone.Id.Should().Be(original.Id);
        original.CurrentPath.Should().Be(@"C:\Path\To\File.txt");
        original.Metadata["Category"].Should().Be("Document");
        original.Metadata.Should().NotContainKey("NewKey");
        original.Tags.Should().NotContain("Processed");
        original.ExecutionLog.Should().HaveCount(1);

        clone.CurrentPath.Should().Be(@"C:\Path\To\NewFile.txt");
        clone.Metadata["Category"].Should().Be("Archived");
        clone.Metadata["NewKey"].Should().Be("NewValue");
        clone.Tags.Should().Contain("Processed");
        clone.ExecutionLog.Should().HaveCount(2);
    }

    [Fact]
    public void AddLog_ShouldAppendTimestampedLogEntry()
    {
        // Arrange
        var context = new FileItemContext(@"C:\Test\File.txt");

        // Act
        context.AddLog("Step 1 executed successfully.");

        // Assert
        context.ExecutionLog.Should().HaveCount(1);
        context.ExecutionLog[0].Should().Contain("Step 1 executed successfully.");
        context.ExecutionLog[0].Should().StartWith("[");
    }

    [Fact]
    public void Constructor_ShouldHandleNonExistentFileGracefully_WithoutThrowingException()
    {
        // Arrange
        string nonExistentPath = @"C:\NonExistentDirectory\FakeFile_" + Guid.NewGuid() + ".txt";

        // Act
        var context = new FileItemContext(nonExistentPath, isDirectory: false);

        // Assert
        context.CurrentPath.Should().Be(nonExistentPath);
        context.FileSizeBytes.Should().Be(0);
        context.IsDirectory.Should().BeFalse();
    }
}
