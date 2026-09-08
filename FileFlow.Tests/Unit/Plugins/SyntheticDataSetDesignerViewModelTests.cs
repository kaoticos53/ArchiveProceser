using System.IO;
using FileFlow.Plugin.FileSystem.Services;
using FileFlow.Plugin.FileSystem.UI.ViewModels;
using FileFlow.Sdk.SyntheticData;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class SyntheticDataSetDesignerViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SyntheticDataSetStorageService _storageService;

    public SyntheticDataSetDesignerViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_DesignerVMTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _storageService = new SyntheticDataSetStorageService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void ViewModel_ShouldInitializeWithDataSetsAndSelectFirst()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.FilteredDataSets.Should().NotBeEmpty();
        vm.SelectedDataSet.Should().NotBeNull();
        vm.DataSetName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ViewModel_SearchText_ShouldFilterDataSets()
    {
        var ds1 = new SyntheticDataSet("Documentos Facturas 2024", "Documentos");
        var ds2 = new SyntheticDataSet("Anime Bluray 1080p", "Series");
        _storageService.SaveDataSet(ds1);
        _storageService.SaveDataSet(ds2);

        var vm = new SyntheticDataSetDesignerViewModel(_storageService)
        {
            SearchText = "Facturas"
        };

        vm.FilteredDataSets.Should().Contain(d => d.Name == "Documentos Facturas 2024");
        vm.FilteredDataSets.Should().NotContain(d => d.Name == "Anime Bluray 1080p");
    }

    [Fact]
    public void ViewModel_NewDataSetCommand_ShouldCreateAndSelect()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        int initialCount = vm.FilteredDataSets.Count;

        vm.NewDataSetCommand.Execute(null);

        vm.FilteredDataSets.Count.Should().Be(initialCount + 1);
        vm.SelectedDataSet.Should().NotBeNull();
        vm.SelectedDataSet!.Name.Should().Be("Nuevo Conjunto de Pruebas");
        vm.EditableItems.Should().NotBeEmpty();
    }

    [Fact]
    public void ViewModel_ApplyDslCommand_ShouldParseAndPopulateItems()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService)
        {
            DslText = """
            Fotos/
                playa.jpg | size=3MB | camera=Nikon
                montaña.jpg | size=4MB | camera=Sony
            """
        };

        vm.ApplyDslToItemsCommand.Execute(null);

        vm.EditableItems.Should().HaveCount(3); // 1 carpeta + 2 fotos
        vm.TotalFiles.Should().Be(2);
        vm.TotalDirectories.Should().Be(1);
    }

    [Fact]
    public void ViewModel_TabSwitching_ShouldSynchronizeDsl()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.EditableItems.Clear();
        vm.EditableItems.Add(new SyntheticFileDefinition("Musica/cancion.mp3", 5000000));

        vm.SelectedTabIndex = 1; // Cambiar a pestaña DSL

        vm.DslText.Should().Contain("cancion.mp3");
    }
}
