using System.IO;
using System.Text;
using FileFlow.Core.Engine;
using FileFlow.Core.Platform;
using FileFlow.Core.Storage;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class StorageServiceTests
{
    [Fact]
    public async Task PhysicalStorageService_FileAndDirectoryOperations_WorkCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "StorageTest_" + Guid.NewGuid().ToString("N"));
        var storage = new PhysicalStorageService();

        try
        {
            (await storage.DirectoryExistsAsync(tempDir)).Should().BeFalse();
            await storage.CreateDirectoryAsync(tempDir);
            (await storage.DirectoryExistsAsync(tempDir)).Should().BeTrue();

            string testFile = Path.Combine(tempDir, "sample.txt");
            (await storage.FileExistsAsync(testFile)).Should().BeFalse();

            byte[] data = Encoding.UTF8.GetBytes("FileFlow Physical Storage Test");
            await storage.WriteAllBytesAsync(testFile, data);

            (await storage.FileExistsAsync(testFile)).Should().BeTrue();
            var readData = await storage.ReadAllBytesAsync(testFile);
            readData.Should().BeEquivalentTo(data);
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
    public async Task PhysicalStorageService_CopyAsync_RenameIncrementalStrategy_ResolvesCollisions()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "StorageTest_" + Guid.NewGuid().ToString("N"));
        var storage = new PhysicalStorageService();

        try
        {
            await storage.CreateDirectoryAsync(tempDir);
            string src = Path.Combine(tempDir, "document.pdf");
            string dst = Path.Combine(tempDir, "out", "document.pdf");

            await storage.WriteAllBytesAsync(src, Encoding.UTF8.GetBytes("Source Original"));
            await storage.CreateDirectoryAsync(Path.GetDirectoryName(dst)!);
            await storage.WriteAllBytesAsync(dst, Encoding.UTF8.GetBytes("Destination Conflict"));

            var result = await storage.CopyAsync(src, dst, StorageCollisionStrategy.RenameIncremental);

            result.IsSuccess.Should().BeTrue();
            result.FinalPath.Should().NotBe(dst);
            result.FinalPath.Should().Contain("document_1.pdf");
            (await storage.FileExistsAsync(dst)).Should().BeTrue();
            (await storage.FileExistsAsync(result.FinalPath)).Should().BeTrue();
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
    public async Task PhysicalStorageService_DryRun_RegistersPlannedAction_WithoutModifyingDisk()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "StorageTest_" + Guid.NewGuid().ToString("N"));
        var plannedActions = new List<PlannedAction>();
        var storage = new PhysicalStorageService(
            platform: null,
            isDryRun: true,
            onRegisterPlannedAction: action => plannedActions.Add(action));

        try
        {
            Directory.CreateDirectory(tempDir);
            string src = Path.Combine(tempDir, "dryrun.txt");
            string dst = Path.Combine(tempDir, "dryrun_moved.txt");
            await File.WriteAllTextAsync(src, "dry run test content");

            var moveResult = await storage.MoveAsync(src, dst);

            moveResult.IsSuccess.Should().BeTrue();
            plannedActions.Should().HaveCount(1);
            plannedActions[0].OperationType.Should().Be(PlannedOperationType.Move);
            plannedActions[0].SourcePath.Should().Be(src);
            plannedActions[0].DestinationPath.Should().Be(dst);
            File.Exists(dst).Should().BeFalse();
            File.Exists(src).Should().BeTrue(); // Original preserved in dry-run
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
    public async Task VirtualStorageService_FullLifecycle_OperatesInMemory()
    {
        var vfs = new VirtualFileSystemStore();
        var storage = new FileFlow.Sdk.Storage.VirtualStorageService(vfs);

        string virtualPath = "vfs://documents/report.docx";
        byte[] payload = Encoding.UTF8.GetBytes("Confidential VFS Report");

        // Write
        await storage.WriteAllBytesAsync(virtualPath, payload);
        (await storage.FileExistsAsync(virtualPath)).Should().BeTrue();

        // Read
        var readBytes = await storage.ReadAllBytesAsync(virtualPath);
        readBytes.Should().BeEquivalentTo(payload);

        // Copy with RenameIncremental
        var copyResult = await storage.CopyAsync(virtualPath, virtualPath, StorageCollisionStrategy.RenameIncremental);
        copyResult.IsSuccess.Should().BeTrue();
        copyResult.FinalPath.Should().NotBe(virtualPath);
        copyResult.FinalPath.Should().Contain("report_1.docx");

        // Move
        string movedPath = "vfs://archive/report_final.docx";
        var moveResult = await storage.MoveAsync(virtualPath, movedPath);
        moveResult.IsSuccess.Should().BeTrue();
        (await storage.FileExistsAsync(virtualPath)).Should().BeFalse();
        (await storage.FileExistsAsync(movedPath)).Should().BeTrue();

        // Delete
        var deleteResult = await storage.DeleteAsync(movedPath);
        deleteResult.IsSuccess.Should().BeTrue();
        (await storage.FileExistsAsync(movedPath)).Should().BeFalse();
    }
}
