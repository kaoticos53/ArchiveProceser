using System.IO;
using System.IO.Compression;
using FileFlow.Plugin.Archives;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class ArchiveFanInNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPackArchiveAndEmitOut_WhenAllSessionItemsArrive()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_FanInTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string sessionDir = Path.Combine(tempDir, "Session_123");
        Directory.CreateDirectory(sessionDir);

        string file1 = Path.Combine(sessionDir, "page1.webp");
        string file2 = Path.Combine(sessionDir, "page2.webp");
        string file3 = Path.Combine(sessionDir, "ComicInfo.xml");

        await File.WriteAllTextAsync(file1, "webp content 1");
        await File.WriteAllTextAsync(file2, "webp content 2");
        await File.WriteAllTextAsync(file3, "<xml>info</xml>");

        string destDir = Path.Combine(tempDir, "Output");
        string sessionId = "test-session-guid-123";

        var item1 = new FileItemContext(file1, isDirectory: false);
        item1.Metadata["Archive:SessionId"] = sessionId;
        item1.Metadata["Archive:OriginalArchivePath"] = @"C:\Comics\MyComic.cbz";
        item1.Metadata["Archive:OriginalArchiveFileName"] = "MyComic.cbz";
        item1.Metadata["Archive:OriginalArchiveFormat"] = "CBZ";
        item1.Metadata["Archive:TotalEntries"] = 3;
        item1.Metadata["Archive:WorkingFolder"] = sessionDir;
        item1.Metadata["Archive:RelativePath"] = "page1.webp";

        var item2 = new FileItemContext(file2, isDirectory: false);
        item2.Metadata["Archive:SessionId"] = sessionId;
        item2.Metadata["Archive:OriginalArchivePath"] = @"C:\Comics\MyComic.cbz";
        item2.Metadata["Archive:OriginalArchiveFileName"] = "MyComic.cbz";
        item2.Metadata["Archive:OriginalArchiveFormat"] = "CBZ";
        item2.Metadata["Archive:TotalEntries"] = 3;
        item2.Metadata["Archive:WorkingFolder"] = sessionDir;
        item2.Metadata["Archive:RelativePath"] = "page2.webp";

        var item3 = new FileItemContext(file3, isDirectory: false);
        item3.Metadata["Archive:SessionId"] = sessionId;
        item3.Metadata["Archive:OriginalArchivePath"] = @"C:\Comics\MyComic.cbz";
        item3.Metadata["Archive:OriginalArchiveFileName"] = "MyComic.cbz";
        item3.Metadata["Archive:OriginalArchiveFormat"] = "CBZ";
        item3.Metadata["Archive:TotalEntries"] = 3;
        item3.Metadata["Archive:WorkingFolder"] = sessionDir;
        item3.Metadata["Archive:RelativePath"] = "ComicInfo.xml";

        try
        {
            var node = new ArchiveFanInNode();
            node.Parameters["DestinationFolder"] = destDir;
            node.Parameters["ArchiveName"] = "{Archive:OriginalArchiveFileName}";
            node.Parameters["ArchiveFormat"] = "Auto";
            node.Parameters["CleanWorkingFolder"] = true;

            var emittedOut = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedOut.Add(emItem))
                       .Returns(Task.CompletedTask);

            // Act: Send items 1, 2, then 3
            await node.ExecuteAsync("In", item1, mockContext.Object, CancellationToken.None);
            emittedOut.Should().BeEmpty(); // Not ready yet

            await node.ExecuteAsync("In", item2, mockContext.Object, CancellationToken.None);
            emittedOut.Should().BeEmpty(); // Not ready yet

            await node.ExecuteAsync("In", item3, mockContext.Object, CancellationToken.None);

            // Assert: All 3 received -> repacked archive emitted
            emittedOut.Should().HaveCount(1);
            var resultArchive = emittedOut[0];
            resultArchive.CurrentPath.Should().EndWith("MyComic.cbz");
            File.Exists(resultArchive.CurrentPath).Should().BeTrue();

            // Check contents of generated CBZ
            using (var zipStream = File.OpenRead(resultArchive.CurrentPath))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                zip.Entries.Should().HaveCount(3);
                zip.Entries.Select(e => e.Name).Should().Contain(["page1.webp", "page2.webp", "ComicInfo.xml"]);
            }

            // Verify temporary working folder was cleaned
            Directory.Exists(sessionDir).Should().BeFalse();
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
    public async Task ExecuteAsync_ShouldPreserveNestedDirectories_WhenSubfoldersExist()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_NestedFolderTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string sessionDir = Path.Combine(tempDir, "Session_Nested");
        string sub1 = Path.Combine(sessionDir, "Chapter1");
        string sub2 = Path.Combine(sessionDir, "Chapter2");
        Directory.CreateDirectory(sub1);
        Directory.CreateDirectory(sub2);

        string file1 = Path.Combine(sub1, "page01.jpg");
        string file2 = Path.Combine(sub1, "page02.jpg");
        string file3 = Path.Combine(sub2, "page03.jpg");
        string fileMeta = Path.Combine(sessionDir, "ComicInfo.xml");

        await File.WriteAllTextAsync(file1, "p1");
        await File.WriteAllTextAsync(file2, "p2");
        await File.WriteAllTextAsync(file3, "p3");
        await File.WriteAllTextAsync(fileMeta, "<meta/>");

        string destDir = Path.Combine(tempDir, "Output");
        string sessionId = "session-nested-456";

        var item1 = new FileItemContext(file1, isDirectory: false);
        item1.Metadata["Archive:SessionId"] = sessionId;
        item1.Metadata["Archive:OriginalArchivePath"] = @"C:\Comics\NestedComic.cbz";
        item1.Metadata["Archive:OriginalArchiveFileName"] = "NestedComic.cbz";
        item1.Metadata["Archive:OriginalArchiveFormat"] = "CBZ";
        item1.Metadata["Archive:TotalEntries"] = 4;
        item1.Metadata["Archive:WorkingFolder"] = sessionDir;
        item1.Metadata["Archive:RelativePath"] = Path.Combine("Chapter1", "page01.jpg");

        var item2 = new FileItemContext(file2, isDirectory: false);
        item2.Metadata["Archive:SessionId"] = sessionId;
        item2.Metadata["Archive:OriginalArchivePath"] = @"C:\Comics\NestedComic.cbz";
        item2.Metadata["Archive:OriginalArchiveFileName"] = "NestedComic.cbz";
        item2.Metadata["Archive:OriginalArchiveFormat"] = "CBZ";
        item2.Metadata["Archive:TotalEntries"] = 4;
        item2.Metadata["Archive:WorkingFolder"] = sessionDir;
        item2.Metadata["Archive:RelativePath"] = Path.Combine("Chapter1", "page02.jpg");

        var item3 = new FileItemContext(file3, isDirectory: false);
        item3.Metadata["Archive:SessionId"] = sessionId;
        item3.Metadata["Archive:OriginalArchivePath"] = @"C:\Comics\NestedComic.cbz";
        item3.Metadata["Archive:OriginalArchiveFileName"] = "NestedComic.cbz";
        item3.Metadata["Archive:OriginalArchiveFormat"] = "CBZ";
        item3.Metadata["Archive:TotalEntries"] = 4;
        item3.Metadata["Archive:WorkingFolder"] = sessionDir;
        item3.Metadata["Archive:RelativePath"] = Path.Combine("Chapter2", "page03.jpg");

        var item4 = new FileItemContext(fileMeta, isDirectory: false);
        item4.Metadata["Archive:SessionId"] = sessionId;
        item4.Metadata["Archive:OriginalArchivePath"] = @"C:\Comics\NestedComic.cbz";
        item4.Metadata["Archive:OriginalArchiveFileName"] = "NestedComic.cbz";
        item4.Metadata["Archive:OriginalArchiveFormat"] = "CBZ";
        item4.Metadata["Archive:TotalEntries"] = 4;
        item4.Metadata["Archive:WorkingFolder"] = sessionDir;
        item4.Metadata["Archive:RelativePath"] = "ComicInfo.xml";

        try
        {
            var node = new ArchiveFanInNode();
            node.Parameters["DestinationFolder"] = destDir;
            node.Parameters["ArchiveName"] = "NestedResult.cbz";
            node.Parameters["ArchiveFormat"] = "CBZ";
            node.Parameters["CleanWorkingFolder"] = true;

            var emittedOut = new List<FileItemContext>();
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                       .Callback<string, FileItemContext>((port, emItem) => emittedOut.Add(emItem))
                       .Returns(Task.CompletedTask);

            await node.ExecuteAsync("In", item1, mockContext.Object, CancellationToken.None);
            await node.ExecuteAsync("In", item2, mockContext.Object, CancellationToken.None);
            await node.ExecuteAsync("In", item3, mockContext.Object, CancellationToken.None);
            await node.ExecuteAsync("In", item4, mockContext.Object, CancellationToken.None);

            emittedOut.Should().HaveCount(1);
            var resultArchive = emittedOut[0];
            File.Exists(resultArchive.CurrentPath).Should().BeTrue();

            using (var zipStream = File.OpenRead(resultArchive.CurrentPath))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                var entryFullNames = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
                entryFullNames.Should().Contain("Chapter1/page01.jpg");
                entryFullNames.Should().Contain("Chapter1/page02.jpg");
                entryFullNames.Should().Contain("Chapter2/page03.jpg");
                entryFullNames.Should().Contain("ComicInfo.xml");
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
}

