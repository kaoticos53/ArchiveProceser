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

    [Fact]
    public async Task ExecuteAsync_ShouldEmitOnlyDirectories_WhenEmitModeIsDirectoriesOnly()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowTestFolder_" + Guid.NewGuid());
        string sub1 = Path.Combine(tempDir, "Sub1");
        string sub2 = Path.Combine(sub1, "Sub2");
        Directory.CreateDirectory(sub2);
        File.WriteAllText(Path.Combine(tempDir, "file1.txt"), "hello");
        File.WriteAllText(Path.Combine(sub1, "file2.txt"), "world");

        try
        {
            var node = new FolderSourceNode();
            node.Parameters["SourcePath"] = tempDir;
            node.Parameters["EmitMode"] = "DirectoriesOnly";
            node.Parameters["MaxRecursionDepth"] = -1;

            var emittedItems = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, item) => emittedItems.Add(item))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("", new FileItemContext("", false), mockContext.Object, CancellationToken.None);

            // Assert
            emittedItems.Should().HaveCount(2);
            emittedItems.Should().AllSatisfy(item => item.IsDirectory.Should().BeTrue());
            emittedItems.Select(i => i.CurrentPath).Should().Contain(new[] { sub1, sub2 });
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitBothFilesAndDirectories_WhenEmitModeIsFilesAndDirectories()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowTestFolder_" + Guid.NewGuid());
        string sub1 = Path.Combine(tempDir, "Sub1");
        Directory.CreateDirectory(sub1);
        string file1 = Path.Combine(tempDir, "file1.txt");
        File.WriteAllText(file1, "hello");

        try
        {
            var node = new FolderSourceNode();
            node.Parameters["SourcePath"] = tempDir;
            node.Parameters["EmitMode"] = "FilesAndDirectories";
            node.Parameters["MaxRecursionDepth"] = -1;

            var emittedItems = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, item) => emittedItems.Add(item))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("", new FileItemContext("", false), mockContext.Object, CancellationToken.None);

            // Assert
            emittedItems.Should().HaveCount(2);
            emittedItems.Should().ContainSingle(i => i.IsDirectory && i.CurrentPath == sub1);
            emittedItems.Should().ContainSingle(i => !i.IsDirectory && i.CurrentPath == file1);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLimitRecursionDepth_WhenMaxRecursionDepthIsSet()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowTestFolder_" + Guid.NewGuid());
        string level1Dir = Path.Combine(tempDir, "Level1");
        string level2Dir = Path.Combine(level1Dir, "Level2");
        Directory.CreateDirectory(level2Dir);

        string f0 = Path.Combine(tempDir, "f0.txt");
        string f1 = Path.Combine(level1Dir, "f1.txt");
        string f2 = Path.Combine(level2Dir, "f2.txt");

        File.WriteAllText(f0, "0");
        File.WriteAllText(f1, "1");
        File.WriteAllText(f2, "2");

        try
        {
            var node = new FolderSourceNode();
            node.Parameters["SourcePath"] = tempDir;
            node.Parameters["EmitMode"] = "FilesOnly";

            // Test MaxRecursionDepth = 0 (top level only)
            node.Parameters["MaxRecursionDepth"] = 0;
            var emittedDepth0 = new List<FileItemContext>();
            var mockContext0 = new Mock<IFlowExecutionContext>();
            mockContext0.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                        .Callback<string, FileItemContext>((port, item) => emittedDepth0.Add(item))
                        .Returns(Task.CompletedTask);

            await node.ExecuteAsync("", new FileItemContext("", false), mockContext0.Object, CancellationToken.None);
            emittedDepth0.Select(i => i.CurrentPath).Should().Equal(f0);

            // Test MaxRecursionDepth = 1 (top level + 1 level deep)
            node.Parameters["MaxRecursionDepth"] = 1;
            var emittedDepth1 = new List<FileItemContext>();
            var mockContext1 = new Mock<IFlowExecutionContext>();
            mockContext1.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                        .Callback<string, FileItemContext>((port, item) => emittedDepth1.Add(item))
                        .Returns(Task.CompletedTask);

            await node.ExecuteAsync("", new FileItemContext("", false), mockContext1.Object, CancellationToken.None);
            emittedDepth1.Select(i => i.CurrentPath).Should().BeEquivalentTo(new[] { f0, f1 });
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFilterFilesByExtension_WhenExtensionFilterIsSpecified()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowTestFolder_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        string fileJpg = Path.Combine(tempDir, "photo.jpg");
        string filePng = Path.Combine(tempDir, "graphic.PNG");
        string fileTxt = Path.Combine(tempDir, "notes.txt");
        string filePdf = Path.Combine(tempDir, "document.pdf");

        File.WriteAllText(fileJpg, "jpg");
        File.WriteAllText(filePng, "png");
        File.WriteAllText(fileTxt, "txt");
        File.WriteAllText(filePdf, "pdf");

        try
        {
            var node = new FolderSourceNode();
            node.Parameters["SourcePath"] = tempDir;
            node.Parameters["ExtensionFilter"] = "*.jpg, png";
            node.Parameters["Recursive"] = false;

            var emittedItems = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, item) => emittedItems.Add(item))
                       .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("", new FileItemContext("", false), mockContext.Object, CancellationToken.None);

            // Assert
            emittedItems.Should().HaveCount(2);
            emittedItems.Select(i => i.CurrentPath).Should().BeEquivalentTo(new[] { fileJpg, filePng });
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Theory]
    [InlineData(".jpg, .png; *.webp|gif bmp", new[] { ".jpg", ".png", ".webp", ".gif", ".bmp" })]
    [InlineData("*.zip, *.rar", new[] { ".zip", ".rar" })]
    [InlineData("", new string[0])]
    [InlineData("*", new string[0])]
    [InlineData("*.*", new string[0])]
    [InlineData("   ", new string[0])]
    public void ParseExtensionFilter_ShouldParseCorrectly(string input, string[] expected)
    {
        // Act
        var result = FolderSourceNode.ParseExtensionFilter(input);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}
