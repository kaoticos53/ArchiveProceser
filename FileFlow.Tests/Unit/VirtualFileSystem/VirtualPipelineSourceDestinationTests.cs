using System.IO;
using FluentAssertions;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FileFlow.Sdk.VirtualFileSystem;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.VirtualFileSystem;

public class VirtualPipelineSourceDestinationTests
{
    [Fact]
    public async Task SyntheticDataSourceNode_InVirtualMode_RegistersSourceFilesWithOriginalOperation()
    {
        // Arrange
        var vfsStore = new VirtualFileSystemStore();
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Películas";
        node.Parameters["EmissionMode"] = "Virtual";
        node.Parameters["MaxItems"] = 3;

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.VirtualFileSystem).Returns(vfsStore);
        mockContext.SetupGet(c => c.IsVirtualFileSystemEnabled).Returns(true);

        var emittedItems = new List<FileItemContext>();
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => emittedItems.Add(item))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", new FileItemContext("dummy"), mockContext.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(3);
        emittedItems.Should().OnlyContain(i => i.IsVirtual);

        var sourceFiles = vfsStore.GetSourceFiles();
        sourceFiles.Should().HaveCount(3);
        sourceFiles.Should().OnlyContain(f => f.Role == VirtualFileRole.Source);
        sourceFiles.Should().OnlyContain(f => f.OperationType == VirtualOperationType.Original);
    }

    [Fact]
    public async Task Pipeline_Synthetic_To_Renamer_To_DestinationSink_MaintainsSourceAndDestinationInVFS()
    {
        // Arrange
        var vfsStore = new VirtualFileSystemStore();

        // 1. Synthetic Source
        var sourceNode = new SyntheticDataSourceNode();
        sourceNode.Parameters["Category"] = "Películas";
        sourceNode.Parameters["EmissionMode"] = "Virtual";
        sourceNode.Parameters["MaxItems"] = 2;

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.VirtualFileSystem).Returns(vfsStore);
        mockContext.SetupGet(c => c.IsVirtualFileSystemEnabled).Returns(true);

        var sourceItems = new List<FileItemContext>();
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => sourceItems.Add(item))
            .Returns(Task.CompletedTask);

        await sourceNode.ExecuteAsync("In", new FileItemContext("dummy"), mockContext.Object, CancellationToken.None);
        sourceItems.Should().HaveCount(2);

        // 2. Advanced Renamer (Virtual mode)
        var renamerNode = new AdvancedRenamerNode();
        renamerNode.Parameters["RenameMode"] = "Virtual";
        renamerNode.Parameters["Steps"] = "[{\"Type\":\"Prefix\",\"Value\":\"2026_\"}]";

        var renamedItems = new List<FileItemContext>();
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => renamedItems.Add(item))
            .Returns(Task.CompletedTask);

        foreach (var item in sourceItems)
        {
            await renamerNode.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);
        }
        renamedItems.Should().HaveCount(2);
        renamedItems.Should().OnlyContain(i => i.CurrentPath != i.OriginalPath);
        renamedItems.Should().OnlyContain(i => i.FileName.StartsWith("Películas_"));

        // 3. Destination Sink (Virtual output to D:\PeliculasFinal)
        var sinkNode = new DestinationSinkNode();
        sinkNode.Parameters["DestinationRoot"] = @"D:\PeliculasFinal";
        sinkNode.Parameters["CollisionStrategy"] = "Overwrite";

        var sinkItems = new List<FileItemContext>();
        mockContext.Setup(c => c.EmitAsync("Done", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => sinkItems.Add(item))
            .Returns(Task.CompletedTask);

        foreach (var item in renamedItems)
        {
            await sinkNode.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);
        }
        sinkItems.Should().HaveCount(2);

        // Assert VFS Separation
        var sourceFiles = vfsStore.GetSourceFiles();
        var destinationFiles = vfsStore.GetDestinationFiles();

        sourceFiles.Should().HaveCount(2);
        sourceFiles.Should().OnlyContain(f => f.Role == VirtualFileRole.Source);
        // Los archivos de origen deben reflejar que fueron renombrados y su destino vinculado
        sourceFiles.Should().OnlyContain(f => f.OperationType == VirtualOperationType.Renamed);
        sourceFiles.Should().OnlyContain(f => !string.IsNullOrEmpty(f.DestinationPath));

        destinationFiles.Should().HaveCount(2);
        destinationFiles.Should().OnlyContain(f => f.Role == VirtualFileRole.Destination);
        destinationFiles.Should().OnlyContain(f => f.DirectoryPath.StartsWith(@"D:\PeliculasFinal", StringComparison.OrdinalIgnoreCase));
        destinationFiles.Should().OnlyContain(f => !string.IsNullOrEmpty(f.RelatedSourcePath));

        // Ambos directorios existen en el VFS
        vfsStore.DirectoryExists(@"D:\PeliculasFinal").Should().BeTrue();

        // El árbol ASCII contiene ambas secciones
        string tree = vfsStore.GenerateAsciiTree();
        tree.Should().Contain("[Carpetas de Origen]");
        tree.Should().Contain("[Carpetas de Destino]");
    }

    [Fact]
    public void VirtualFileSystemStore_MoveFile_PreservesSourceAndCreatesDestination()
    {
        // Arrange
        var store = new VirtualFileSystemStore();
        string sourcePath = @"C:\Input\Documento.pdf";
        string destPath = @"D:\Output\Documento.pdf";

        store.AddOrUpdateFile(new VirtualFileEntry(
            VirtualPath: sourcePath,
            OriginalPath: sourcePath,
            FileName: "Documento.pdf",
            Extension: ".pdf",
            DirectoryPath: @"C:\Input",
            FileSizeBytes: 5000,
            OperationType: VirtualOperationType.Original,
            Role: VirtualFileRole.Source,
            SourceNodeName: "TestNode",
            SourceNodeId: "1",
            TimestampUtc: DateTime.UtcNow
        ));

        // Act
        bool moved = store.MoveFile(sourcePath, destPath, "RelocatorNode", "2");

        // Assert
        moved.Should().BeTrue();

        var sourceFile = store.GetFile(sourcePath);
        sourceFile.Should().NotBeNull();
        sourceFile!.OperationType.Should().Be(VirtualOperationType.Moved);
        sourceFile.Role.Should().Be(VirtualFileRole.Source);
        sourceFile.DestinationPath.Should().Be(destPath);

        var destFile = store.GetFile(destPath);
        destFile.Should().NotBeNull();
        destFile!.OperationType.Should().Be(VirtualOperationType.Moved);
        destFile.Role.Should().Be(VirtualFileRole.Destination);
        destFile.RelatedSourcePath.Should().Be(sourcePath);

        store.GetSourceFiles().Should().HaveCount(1);
        store.GetDestinationFiles().Should().HaveCount(1);
    }

    [Fact]
    public void VirtualFileSystemExplorerViewModel_PartitionsTreeAndFiltersByRole()
    {
        // Arrange
        var store = new VirtualFileSystemStore();
        store.AddOrUpdateFile(new VirtualFileEntry(
            VirtualPath: @"C:\Origen\F1.txt",
            OriginalPath: @"C:\Origen\F1.txt",
            FileName: "F1.txt",
            Extension: ".txt",
            DirectoryPath: @"C:\Origen",
            FileSizeBytes: 100,
            OperationType: VirtualOperationType.Original,
            Role: VirtualFileRole.Source,
            SourceNodeName: "SourceNode",
            SourceNodeId: "1",
            TimestampUtc: DateTime.UtcNow
        ));

        store.AddOrUpdateFile(new VirtualFileEntry(
            VirtualPath: @"D:\Destino\F1_Renombrado.txt",
            OriginalPath: @"C:\Origen\F1.txt",
            FileName: "F1_Renombrado.txt",
            Extension: ".txt",
            DirectoryPath: @"D:\Destino",
            FileSizeBytes: 100,
            OperationType: VirtualOperationType.Saved,
            Role: VirtualFileRole.Destination,
            RelatedSourcePath: @"C:\Origen\F1.txt",
            SourceNodeName: "SinkNode",
            SourceNodeId: "2",
            TimestampUtc: DateTime.UtcNow
        ));

        var vm = new VirtualFileSystemExplorerViewModel(store);

        // Assert Initial Tree Structure
        vm.DirectoryTreeNodes.Should().HaveCount(1);
        var rootNode = vm.DirectoryTreeNodes[0];
        rootNode.Children.Should().HaveCount(2); // Origen and Destino
        rootNode.Children.Should().Contain(c => c.Role == VirtualFileRole.Source);
        rootNode.Children.Should().Contain(c => c.Role == VirtualFileRole.Destination);

        vm.TotalSourceFiles.Should().Be(1);
        vm.TotalDestinationFiles.Should().Be(1);
        vm.FilteredFiles.Should().HaveCount(2);

        // Filter by Origen
        vm.SelectedRoleFilter = "📥 Origen";
        vm.FilteredFiles.Should().HaveCount(1);
        vm.FilteredFiles[0].FileName.Should().Be("F1.txt");

        // Filter by Destino
        vm.SelectedRoleFilter = "📤 Destino";
        vm.FilteredFiles.Should().HaveCount(1);
        vm.FilteredFiles[0].FileName.Should().Be("F1_Renombrado.txt");

        // Reset to Todos
        vm.SelectedRoleFilter = "Todos";
        vm.FilteredFiles.Should().HaveCount(2);
    }
}
