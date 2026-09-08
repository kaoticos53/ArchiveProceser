using FileFlow.Core.Engine;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FileFlow.Sdk.VirtualFileSystem;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class VirtualPipelineExecutionTests
{
    [Fact]
    public async Task DestinationSinkNode_WithVirtualItem_SavesToVirtualFileSystemStore()
    {
        var sinkNode = new DestinationSinkNode();
        sinkNode.Parameters["DestinationRoot"] = @"C:\VirtualOutput\{Category}";
        sinkNode.Parameters["ConflictStrategy"] = "RenameIncremental";

        var vfsStore = new VirtualFileSystemStore();

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.VirtualFileSystem).Returns(vfsStore);
        mockContext.SetupGet(c => c.IsVirtualFileSystemEnabled).Returns(true);
        mockContext.SetupGet(c => c.IsDryRun).Returns(false);

        var emittedItems = new List<FileItemContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, it) => emittedItems.Add(it))
            .Returns(Task.CompletedTask);

        var syntheticItem = new FileItemContext(@"C:\Muestras\Películas\Inception (2010).mkv")
        {
            FileSizeBytes = 1073741824
        };
        syntheticItem.Metadata["Category"] = "Películas";
        syntheticItem.Metadata["VirtualSample"] = true;

        await sinkNode.ExecuteAsync("In", syntheticItem, mockContext.Object, CancellationToken.None);

        emittedItems.Should().ContainSingle();
        vfsStore.TotalFiles.Should().Be(1);
        vfsStore.FileExists(@"C:\VirtualOutput\Películas\Inception (2010).mkv").Should().BeTrue();

        var entry = vfsStore.GetFile(@"C:\VirtualOutput\Películas\Inception (2010).mkv");
        entry.Should().NotBeNull();
        entry!.OperationType.Should().Be(VirtualOperationType.Saved);
        entry.Metadata["Category"].Should().Be("Películas");
        entry.FileSizeBytes.Should().Be(1073741824);
    }

    [Fact]
    public async Task DestinationSinkNode_WithVirtualCollision_PerformsIncrementalRenameInVfs()
    {
        var sinkNode = new DestinationSinkNode();
        sinkNode.Parameters["DestinationRoot"] = @"C:\VirtualOutput";
        sinkNode.Parameters["ConflictStrategy"] = "RenameIncremental";

        var vfsStore = new VirtualFileSystemStore();
        // Registrar previamente un archivo con el mismo nombre en VFS
        vfsStore.AddOrUpdateFile(new VirtualFileEntry(
            @"C:\VirtualOutput\Sample.mkv", @"C:\Sample.mkv", "Sample.mkv", ".mkv", @"C:\VirtualOutput", 500,
            VirtualOperationType.Saved, "PriorSink", "node-0", new Dictionary<string, object?>(), [], DateTime.UtcNow));

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.VirtualFileSystem).Returns(vfsStore);
        mockContext.SetupGet(c => c.IsVirtualFileSystemEnabled).Returns(true);

        var emittedItems = new List<FileItemContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, it) => emittedItems.Add(it))
            .Returns(Task.CompletedTask);

        var newItem = new FileItemContext(@"C:\Muestras\Sample.mkv")
        {
            FileSizeBytes = 1000
        };
        newItem.Metadata["VirtualSample"] = true;

        await sinkNode.ExecuteAsync("In", newItem, mockContext.Object, CancellationToken.None);

        emittedItems.Should().ContainSingle();
        vfsStore.TotalFiles.Should().Be(2);
        vfsStore.FileExists(@"C:\VirtualOutput\Sample.mkv").Should().BeTrue();
        vfsStore.FileExists(@"C:\VirtualOutput\Sample_1.mkv").Should().BeTrue();

        var renamedEntry = vfsStore.GetFile(@"C:\VirtualOutput\Sample_1.mkv");
        renamedEntry.Should().NotBeNull();
        renamedEntry!.OperationType.Should().Be(VirtualOperationType.ConflictRenamed);
    }

    [Fact]
    public async Task FileRelocatorNode_WithVirtualItem_MovesFileInVirtualFileSystemStore()
    {
        var relocatorNode = new FileRelocatorNode();
        relocatorNode.Parameters["Operation"] = "Move";
        relocatorNode.Parameters["DestinationDirectory"] = @"C:\Organized\Movies";

        var vfsStore = new VirtualFileSystemStore();
        string initialVfsPath = @"C:\VirtualOutput\Inception.mkv";
        vfsStore.AddOrUpdateFile(new VirtualFileEntry(
            initialVfsPath, initialVfsPath, "Inception.mkv", ".mkv", @"C:\VirtualOutput", 1024,
            VirtualOperationType.Saved, "Sink", "1", new Dictionary<string, object?>(), [], DateTime.UtcNow));

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.VirtualFileSystem).Returns(vfsStore);
        mockContext.SetupGet(c => c.IsVirtualFileSystemEnabled).Returns(true);

        var emittedItems = new List<FileItemContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, it) => emittedItems.Add(it))
            .Returns(Task.CompletedTask);

        var item = new FileItemContext(initialVfsPath);
        item.Metadata["VirtualSample"] = true;

        await relocatorNode.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        emittedItems.Should().ContainSingle();
        vfsStore.FileExists(initialVfsPath).Should().BeFalse();
        vfsStore.FileExists(@"C:\Organized\Movies\Inception.mkv").Should().BeTrue();

        var moved = vfsStore.GetFile(@"C:\Organized\Movies\Inception.mkv");
        moved.Should().NotBeNull();
        moved!.OperationType.Should().Be(VirtualOperationType.Moved);
    }

    [Fact]
    public async Task SafeRecycleDeleteNode_WithVirtualItem_RecyclesInVirtualFileSystemStore()
    {
        var recycleNode = new SafeRecycleDeleteNode();
        recycleNode.Parameters["DeleteOriginal"] = false;

        var vfsStore = new VirtualFileSystemStore();
        string targetPath = @"C:\Organized\DeleteMe.tmp";
        vfsStore.AddOrUpdateFile(new VirtualFileEntry(
            targetPath, targetPath, "DeleteMe.tmp", ".tmp", @"C:\Organized", 200,
            VirtualOperationType.Saved, "Sink", "1", new Dictionary<string, object?>(), [], DateTime.UtcNow));

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.VirtualFileSystem).Returns(vfsStore);
        mockContext.SetupGet(c => c.IsVirtualFileSystemEnabled).Returns(true);

        var item = new FileItemContext(targetPath);
        item.Metadata["VirtualSample"] = true;

        await recycleNode.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        vfsStore.TotalFiles.Should().Be(0);
        vfsStore.FileExists(targetPath).Should().BeFalse();

        var recycled = vfsStore.GetFile(targetPath);
        recycled.Should().NotBeNull();
        recycled!.OperationType.Should().Be(VirtualOperationType.Recycled);
    }
}
