using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Plugin.Archives;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class SyntheticArchiveSimulationTests : IDisposable
{
    private readonly string _tempFolder;

    public SyntheticArchiveSimulationTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "FileFlow_ArchiveSimTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
        {
            try { Directory.Delete(_tempFolder, true); } catch { }
        }
    }

    [Fact]
    public async Task VirtualArchive_WhenProcessedBySmartUnpack_ShouldExtractToVfsWithoutErrors()
    {
        // 1. Synthetic Source emite un paquete virtual con entradas simuladas
        var sourceNode = new SyntheticDataSourceNode();
        sourceNode.Parameters["Category"] = "Personalizada";
        sourceNode.Parameters["EmissionMode"] = "Virtual";
        sourceNode.Parameters["CustomItems"] = """
        Backups/
            paquete.zip | size=10MB [archive: documentos/informe.pdf | size=2MB; datos.csv | size=15KB]
        """;

        var vfs = new VirtualFileSystemStore();
        var context = new TestFlowExecutionContext(vfs);

        await sourceNode.ExecuteAsync("In", new FileItemContext("start", false), context, CancellationToken.None);

        context.EmittedItems.Should().HaveCount(1);
        var zipItem = context.EmittedItems[0];
        zipItem.IsVirtual.Should().BeTrue();
        zipItem.Metadata["IsArchive"]?.ToString().Should().Be("True");

        // 2. Conectar a SmartUnpackNode
        var unpackNode = new SmartUnpackNode();
        unpackNode.Parameters["DestinationFolder"] = @"C:\Muestras\Extracted";

        var unpackContext = new TestFlowExecutionContext(vfs);

        await unpackNode.ExecuteAsync("In", zipItem, unpackContext, CancellationToken.None);

        // SmartUnpack emite por 'Out' la carpeta extraída
        unpackContext.EmittedItems.Should().HaveCount(1);
        var unpackedDir = unpackContext.EmittedItems[0];
        unpackedDir.IsDirectory.Should().BeTrue();
        unpackedDir.Metadata["UnpackedFileCount"]?.ToString().Should().Be("2");

        // Verificar que las entradas simuladas se registraron en el VFS
        var allVfsFiles = vfs.GetAllFiles();
        allVfsFiles.Should().Contain(f => f.FileName == "informe.pdf" && f.FileSizeBytes == 2L * 1024 * 1024);
        allVfsFiles.Should().Contain(f => f.FileName == "datos.csv" && f.FileSizeBytes == 15L * 1024);
    }

    [Fact]
    public async Task PhysicalMock_ShouldGenerateRealZipFileOnDisk()
    {
        var sourceNode = new SyntheticDataSourceNode();
        sourceNode.Parameters["Category"] = "Personalizada";
        sourceNode.Parameters["EmissionMode"] = "PhysicalMock";
        sourceNode.Parameters["OutputFolder"] = _tempFolder;
        sourceNode.Parameters["CustomItems"] = """
        paquete_real.zip [archive: sub/archivo_mock.txt | size=500]
        """;

        var context = new TestFlowExecutionContext();

        await sourceNode.ExecuteAsync("In", new FileItemContext("start", false), context, CancellationToken.None);

        context.EmittedItems.Should().HaveCount(1);
        var emittedZip = context.EmittedItems[0];
        File.Exists(emittedZip.CurrentPath).Should().BeTrue();

        // Verificar que es un ZIP válido que System.IO.Compression puede abrir
        using var fs = new FileStream(emittedZip.CurrentPath, FileMode.Open);
        using var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read);
        zip.Entries.Should().HaveCount(1);
        zip.Entries[0].FullName.Should().Be("sub/archivo_mock.txt");
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
