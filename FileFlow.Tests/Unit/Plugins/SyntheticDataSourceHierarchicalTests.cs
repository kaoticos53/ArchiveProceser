using FileFlow.Core.Engine;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.FileSystem.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.SyntheticData;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class SyntheticDataSourceHierarchicalTests
{
    [Fact]
    public async Task SyntheticDataSourceNode_ShouldEmitHierarchicalRelativePaths()
    {
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Personalizada";
        node.Parameters["EmissionMode"] = "Virtual";
        node.Parameters["CustomItems"] = """
        Series/Anime/DeathNote/
            Capitulo_01.mkv | size=350MB | res=1080p
            Capitulo_02.mkv | size=350MB | res=1080p
        """;

        var vfs = new VirtualFileSystemStore();
        var context = new TestFlowExecutionContext(vfs);

        await node.ExecuteAsync("In", new FileItemContext("start", false), context, CancellationToken.None);

        context.EmittedItems.Should().HaveCount(2);

        var ep1 = context.EmittedItems[0];
        ep1.IsVirtual.Should().BeTrue();
        ep1.Metadata["RelativePath"]?.ToString().Should().Be("Series/Anime/DeathNote/Capitulo_01.mkv");
        ep1.Metadata["RelativeDir"]?.ToString().Should().Be("Series/Anime/DeathNote");
        ep1.Metadata["res"]?.ToString().Should().Be("1080p");
        ep1.FileSizeBytes.Should().Be(350L * 1024 * 1024);
    }

    [Fact]
    public async Task SyntheticDataSourceNode_EmitDirectoriesTrue_ShouldEmitFolderItems()
    {
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Personalizada";
        node.Parameters["EmissionMode"] = "Virtual";
        node.Parameters["EmitDirectories"] = true;
        node.Parameters["CustomItems"] = """
        CarpetaA/
            archivo.txt
        """;

        var vfs = new VirtualFileSystemStore();
        var context = new TestFlowExecutionContext(vfs);

        await node.ExecuteAsync("In", new FileItemContext("start", false), context, CancellationToken.None);

        context.EmittedItems.Should().HaveCount(2);
        context.EmittedItems.Should().Contain(i => i.IsDirectory && i.FileName == "CarpetaA");
        context.EmittedItems.Should().Contain(i => !i.IsDirectory && i.FileName == "archivo.txt");
    }

    [Fact]
    public async Task SyntheticDataSourceNode_ShouldLoadCustomDataSetByName()
    {
        string dsName = "CustomTestDS_" + Guid.NewGuid().ToString("N")[..8];
        var customDs = new SyntheticDataSet(dsName, "Personalizada")
        {
            Items =
            [
                new SyntheticFileDefinition("Documentos/Informe.pdf", 50000),
                new SyntheticFileDefinition("Documentos/Datos.xlsx", 20000)
            ]
        };
        SyntheticDataSetStorageService.Instance.SaveDataSet(customDs);

        try
        {
            var node = new SyntheticDataSourceNode();
            node.Parameters["Category"] = dsName;
            node.Parameters["EmissionMode"] = "Virtual";

            var vfs = new VirtualFileSystemStore();
            var context = new TestFlowExecutionContext(vfs);

            await node.ExecuteAsync("In", new FileItemContext("start", false), context, CancellationToken.None);

            context.EmittedItems.Should().HaveCount(2);
            context.EmittedItems.Should().Contain(i => i.FileName == "Informe.pdf");
            context.EmittedItems.Should().Contain(i => i.FileName == "Datos.xlsx");
        }
        finally
        {
            SyntheticDataSetStorageService.Instance.DeleteDataSet(customDs.Id);
        }
    }

    private class TestFlowExecutionContext : IFlowExecutionContext
    {
        public bool IsDryRun => false;
        public string ExecutionId { get; } = Guid.NewGuid().ToString();
        public FileFlow.Sdk.VirtualFileSystem.IVirtualFileSystemStore? VirtualFileSystem { get; }
        public bool IsVirtualFileSystemEnabled => VirtualFileSystem != null;

        public List<FileItemContext> EmittedItems { get; } = [];

        public TestFlowExecutionContext(FileFlow.Sdk.VirtualFileSystem.IVirtualFileSystemStore? vfs = null)
        {
            VirtualFileSystem = vfs;
        }

        public Task EmitAsync(string outputPortName, FileItemContext item)
        {
            EmittedItems.Add(item);
            return Task.CompletedTask;
        }

        public void Log(string message, LogLevel level) { }
        public void Log(string message, LogLevel level, FileItemContext? item = null, double? durationMs = null, string? detailsJson = null) { }
        public void ReportProgress(double percentage, string statusMessage) { }
        public void RegisterPlannedAction(PlannedAction action) { }
        public void RecordJournalEntry(JournalEntry entry) { }
        public void BreakpointHit(string nodeId, FileItemContext item) { }
    }
}
