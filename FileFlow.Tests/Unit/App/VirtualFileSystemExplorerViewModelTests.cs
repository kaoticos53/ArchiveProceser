using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.VirtualFileSystem;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class VirtualFileSystemExplorerViewModelTests
{
    private readonly VirtualFileSystemStore _store = new();
    private readonly Mock<IProcessLauncherService> _mockLauncher = new();
    private readonly Mock<IDialogService> _mockDialog = new();

    public VirtualFileSystemExplorerViewModelTests()
    {
        _store.AddOrUpdateFile(new VirtualFileEntry(
            VirtualPath: @"C:\Output\Películas\2010\Inception.1080p.mkv",
            OriginalPath: @"C:\Muestras\Películas\Inception (2010).mkv",
            FileName: "Inception.1080p.mkv",
            Extension: ".mkv",
            DirectoryPath: @"C:\Output\Películas\2010",
            FileSizeBytes: 1073741824,
            OperationType: VirtualOperationType.Saved,
            SourceNodeName: "Destination Sink",
            SourceNodeId: "node-1",
            Metadata: new Dictionary<string, object?>
            {
                ["Category"] = "Películas",
                ["Video:Resolution"] = "1080p",
                ["Audio:Codec"] = "DTS-HD"
            },
            ExecutionLog: ["Renamed by AdvancedRenamer", "Saved by DestinationSink"],
            TimestampUtc: DateTime.UtcNow
        ));

        _store.AddOrUpdateFile(new VirtualFileEntry(
            VirtualPath: @"C:\Output\Música\Daft Punk\Discovery.mp3",
            OriginalPath: @"C:\Muestras\Música\Discovery.mp3",
            FileName: "Discovery.mp3",
            Extension: ".mp3",
            DirectoryPath: @"C:\Output\Música\Daft Punk",
            FileSizeBytes: 8388608,
            OperationType: VirtualOperationType.Saved,
            SourceNodeName: "Destination Sink",
            SourceNodeId: "node-1",
            Metadata: new Dictionary<string, object?>
            {
                ["Category"] = "Música",
                ["Audio:Artist"] = "Daft Punk",
                ["Audio:Album"] = "Discovery"
            },
            ExecutionLog: ["Saved by DestinationSink"],
            TimestampUtc: DateTime.UtcNow
        ));
    }

    [Fact]
    public void Constructor_InitializesTreeAndFilteredFiles()
    {
        var vm = new VirtualFileSystemExplorerViewModel(_store, _mockLauncher.Object, _mockDialog.Object);

        vm.TotalFiles.Should().Be(2);
        vm.FilteredFiles.Should().HaveCount(2);
        vm.DirectoryTreeNodes.Should().NotBeEmpty();
        vm.SelectedFile.Should().NotBeNull();
        vm.SelectedFileMetadata.Should().NotBeEmpty();
    }

    [Fact]
    public void SearchText_FiltersFilesAccurately()
    {
        var vm = new VirtualFileSystemExplorerViewModel(_store, _mockLauncher.Object, _mockDialog.Object);

        vm.SearchText = "Inception";
        vm.FilteredFiles.Should().ContainSingle();
        vm.FilteredFiles[0].FileName.Should().Be("Inception.1080p.mkv");

        vm.SearchText = ".mp3";
        vm.FilteredFiles.Should().ContainSingle();
        vm.FilteredFiles[0].FileName.Should().Be("Discovery.mp3");

        vm.SearchText = "NonExistentTerm";
        vm.FilteredFiles.Should().BeEmpty();
    }

    [Fact]
    public void CategoryFilter_FiltersFilesByMetadataCategory()
    {
        var vm = new VirtualFileSystemExplorerViewModel(_store, _mockLauncher.Object, _mockDialog.Object);

        vm.SelectedCategoryFilter = "Música";
        vm.FilteredFiles.Should().ContainSingle();
        vm.FilteredFiles[0].FileName.Should().Be("Discovery.mp3");

        vm.SelectedCategoryFilter = "Películas";
        vm.FilteredFiles.Should().ContainSingle();
        vm.FilteredFiles[0].FileName.Should().Be("Inception.1080p.mkv");

        vm.SelectedCategoryFilter = "Todas";
        vm.FilteredFiles.Should().HaveCount(2);
    }

    [Fact]
    public void MetadataInspector_CategorizesGroupsProperly()
    {
        var vm = new VirtualFileSystemExplorerViewModel(_store, _mockLauncher.Object, _mockDialog.Object);
        var musicFile = _store.GetFile(@"C:\Output\Música\Daft Punk\Discovery.mp3");
        vm.SelectedFile = musicFile;

        vm.SelectedFileMetadata.Should().Contain(m => m.Category == "Música (ID3)" && m.Key == "Audio:Artist" && m.Value == "Daft Punk");
        vm.SelectedFileMetadata.Should().Contain(m => m.Category == "Archivo" && m.Key == "Nombre Virtual" && m.Value == "Discovery.mp3");
    }

    [Fact]
    public void ClearVirtualFileSystemCommand_WhenConfirmed_ClearsStore()
    {
        _mockDialog.Setup(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var vm = new VirtualFileSystemExplorerViewModel(_store, _mockLauncher.Object, _mockDialog.Object);

        vm.ClearVirtualFileSystemCommand.Execute(null);

        vm.TotalFiles.Should().Be(0);
        vm.FilteredFiles.Should().BeEmpty();
    }
}
