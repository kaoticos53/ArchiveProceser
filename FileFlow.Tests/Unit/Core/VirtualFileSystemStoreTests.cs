using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Sdk.VirtualFileSystem;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class VirtualFileSystemStoreTests
{
    [Fact]
    public void AddOrUpdateFile_RegistersFileAndParentDirectories()
    {
        var store = new VirtualFileSystemStore();
        var file = new VirtualFileEntry(
            VirtualPath: @"C:\Output\Movies\2024\Inception.mkv",
            OriginalPath: @"C:\Muestras\Películas\Inception (2010).mkv",
            FileName: "Inception.mkv",
            Extension: ".mkv",
            DirectoryPath: @"C:\Output\Movies\2024",
            FileSizeBytes: 1048576,
            OperationType: VirtualOperationType.Saved,
            SourceNodeName: "Destination Sink",
            SourceNodeId: "node-1",
            Metadata: new Dictionary<string, object?> { ["Category"] = "Películas" },
            ExecutionLog: ["Saved to VFS"],
            TimestampUtc: DateTime.UtcNow
        );

        store.AddOrUpdateFile(file);

        store.TotalFiles.Should().Be(1);
        store.TotalBytes.Should().Be(1048576);
        store.FileExists(@"C:\Output\Movies\2024\Inception.mkv").Should().BeTrue();
        store.DirectoryExists(@"C:\Output\Movies\2024").Should().BeTrue();
        store.DirectoryExists(@"C:\Output\Movies").Should().BeTrue();
        store.DirectoryExists(@"C:\Output").Should().BeTrue();
    }

    [Fact]
    public void MoveFile_UpdatesPathAndLogs()
    {
        var store = new VirtualFileSystemStore();
        string source = @"C:\Virtual\Original.txt";
        string target = @"C:\Virtual\Organized\Moved.txt";

        store.AddOrUpdateFile(new VirtualFileEntry(
            source, source, "Original.txt", ".txt", @"C:\Virtual", 500,
            VirtualOperationType.Saved, "Sink", "1",
            new Dictionary<string, object?>(), ["Created"], DateTime.UtcNow));

        bool moved = store.MoveFile(source, target, "Relocator", "2");

        moved.Should().BeTrue();
        store.FileExists(source).Should().BeFalse();
        store.FileExists(target).Should().BeTrue();
        var movedEntry = store.GetFile(target);
        movedEntry.Should().NotBeNull();
        movedEntry!.OperationType.Should().Be(VirtualOperationType.Moved);
        movedEntry.ExecutionLog.Should().Contain(l => l.Contains("Moved in VFS"));
    }

    [Fact]
    public void CopyFile_CreatesDuplicateWithUpdatedType()
    {
        var store = new VirtualFileSystemStore();
        string source = @"C:\Virtual\Doc.pdf";
        string target = @"C:\Virtual\Backup\Doc.pdf";

        store.AddOrUpdateFile(new VirtualFileEntry(
            source, source, "Doc.pdf", ".pdf", @"C:\Virtual", 2048,
            VirtualOperationType.Saved, "Sink", "1",
            new Dictionary<string, object?>(), ["Created"], DateTime.UtcNow));

        bool copied = store.CopyFile(source, target, "Relocator", "2");

        copied.Should().BeTrue();
        store.FileExists(source).Should().BeTrue();
        store.FileExists(target).Should().BeTrue();
        store.TotalFiles.Should().Be(2);
    }

    [Fact]
    public void DeleteFile_MarksEntryAsDeletedAndDecrementsTotalFiles()
    {
        var store = new VirtualFileSystemStore();
        string file = @"C:\Virtual\Trash.tmp";

        store.AddOrUpdateFile(new VirtualFileEntry(
            file, file, "Trash.tmp", ".tmp", @"C:\Virtual", 100,
            VirtualOperationType.Saved, "Sink", "1",
            new Dictionary<string, object?>(), ["Created"], DateTime.UtcNow));

        store.TotalFiles.Should().Be(1);

        bool deleted = store.DeleteFile(file, "RecycleNode", "3", isRecycled: true);

        deleted.Should().BeTrue();
        store.TotalFiles.Should().Be(0);
        store.FileExists(file).Should().BeFalse();

        var entry = store.GetFile(file);
        entry.Should().NotBeNull();
        entry!.OperationType.Should().Be(VirtualOperationType.Recycled);
    }

    [Fact]
    public void GenerateAsciiTree_ReturnsHierarchicalString()
    {
        var store = new VirtualFileSystemStore();
        store.AddOrUpdateFile(new VirtualFileEntry(
            @"C:\Media\Series\S01\E01.mkv", @"C:\Media\Series\S01\E01.mkv", "E01.mkv", ".mkv", @"C:\Media\Series\S01", 1024,
            VirtualOperationType.Saved, "Sink", "1", new Dictionary<string, object?>(), [], DateTime.UtcNow));

        string tree = store.GenerateAsciiTree();

        tree.Should().Contain("📁 [Raíz Virtual]");
        tree.Should().Contain("E01.mkv");
        (tree.Contains("├──") || tree.Contains("└──")).Should().BeTrue();
    }

    [Fact]
    public async Task ExportToPhysicalDirectoryAsync_MaterializesFilesOnDisk()
    {
        var store = new VirtualFileSystemStore();
        store.AddOrUpdateFile(new VirtualFileEntry(
            @"C:\VirtualOutput\Report.txt", @"C:\Source\Report.txt", "Report.txt", ".txt", @"C:\VirtualOutput", 50,
            VirtualOperationType.Saved, "Sink", "1",
            new Dictionary<string, object?> { ["Author"] = "TestAgent" },
            ["Step1", "Step2"],
            DateTime.UtcNow,
            TextContent: "Contenido de prueba en texto"));

        string tempFolder = Path.Combine(Path.GetTempPath(), "VfsTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            await store.ExportToPhysicalDirectoryAsync(tempFolder, CancellationToken.None);

            string[] writtenFiles = Directory.GetFiles(tempFolder, "*.*", SearchOption.AllDirectories);
            writtenFiles.Should().NotBeEmpty();

            string exportedContent = await File.ReadAllTextAsync(writtenFiles[0]);
            exportedContent.Should().Be("Contenido de prueba en texto");
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }
    }
}
