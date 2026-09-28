using System.IO;
using FileFlow.Plugin.FileSystem.Services;
using FileFlow.Plugin.FileSystem.UI.ViewModels;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.SyntheticData;
using FluentAssertions;
using Material.Icons;
using Moq;
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
        vm.BuildTreeFromItems();

        vm.SelectedTabIndex = 1; // Cambiar a pestaña DSL

        vm.DslText.Should().Contain("cancion.mp3");
    }

    [Fact]
    public void BuildTreeFromItems_ShouldCreateNestedHierarchy()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.EditableItems.Clear();
        vm.EditableItems.Add(new SyntheticFileDefinition("Series/Anime/DeathNote/Cap1.mkv", 500000000));
        vm.EditableItems.Add(new SyntheticFileDefinition("Series/Anime/DeathNote/Cap2.mkv", 500000000));
        vm.EditableItems.Add(new SyntheticFileDefinition("Documentos/nota.txt", 1024));

        vm.BuildTreeFromItems();

        vm.RootTreeNodes.Should().HaveCount(2); // "Documentos" and "Series"
        var seriesNode = vm.RootTreeNodes.FirstOrDefault(n => n.Name == "Series");
        seriesNode.Should().NotBeNull();
        seriesNode!.IsDirectory.Should().BeTrue();
        seriesNode.Children.Should().HaveCount(1); // "Anime"

        var animeNode = seriesNode.Children[0];
        animeNode.Name.Should().Be("Anime");
        animeNode.Children.Should().HaveCount(1); // "DeathNote"

        var dnNode = animeNode.Children[0];
        dnNode.Name.Should().Be("DeathNote");
        dnNode.Children.Should().HaveCount(2); // "Cap1.mkv", "Cap2.mkv"

        var cap1 = dnNode.Children.FirstOrDefault(c => c.Name == "Cap1.mkv");
        cap1.Should().NotBeNull();
        cap1!.IsDirectory.Should().BeFalse();
        cap1.IconKind.Should().Be(MaterialIconKind.Movie, "el icono del nodo es vectorial, no un emoji dependiente de las fuentes del sistema");
        cap1.RelativePath.Should().Be("Series/Anime/DeathNote/Cap1.mkv");
        cap1.FileSizeBytes.Should().Be(500000000);

        seriesNode.CalculateTotalRecursiveFiles().Should().Be(2);
        seriesNode.CalculateTotalRecursiveBytes().Should().Be(1000000000);
    }

    [Fact]
    public void AddFileToTree_WithSelectedFolder_ShouldNestCorrectly()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.EditableItems.Clear();
        vm.EditableItems.Add(new SyntheticFileDefinition("Videos/Pelis/", 0, isDirectory: true));
        vm.BuildTreeFromItems();

        var videosNode = vm.RootTreeNodes.FirstOrDefault(n => n.Name == "Videos");
        videosNode.Should().NotBeNull();
        var pelisNode = videosNode!.Children.FirstOrDefault(n => n.Name == "Pelis");
        pelisNode.Should().NotBeNull();

        vm.SelectedTreeNode = pelisNode;
        vm.AddFileToTreeCommand.Execute(null);

        pelisNode!.Children.Should().HaveCount(1);
        var newFile = pelisNode.Children[0];
        newFile.Name.Should().Be("nuevo_archivo.dat");
        newFile.RelativePath.Should().Be("Videos/Pelis/nuevo_archivo.dat");
        vm.SelectedTreeNode.Should().Be(newFile);
    }

    [Fact]
    public void AddFolderToTree_AtRoot_ShouldAddToRootTreeNodes()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.EditableItems.Clear();
        vm.BuildTreeFromItems();
        vm.SelectedTreeNode = null;

        vm.AddFolderToTreeCommand.Execute(null);

        vm.RootTreeNodes.Should().ContainSingle();
        var rootFolder = vm.RootTreeNodes[0];
        rootFolder.Name.Should().Be("Nueva_Carpeta");
        rootFolder.IsDirectory.Should().BeTrue();
        rootFolder.RelativePath.Should().Be("Nueva_Carpeta");
    }

    [Fact]
    public void AddArchiveToTree_ShouldIncludeSimulatedArchiveEntries()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.EditableItems.Clear();
        vm.BuildTreeFromItems();
        vm.SelectedTreeNode = null;

        vm.AddArchiveToTreeCommand.Execute(null);

        vm.RootTreeNodes.Should().ContainSingle();
        var archiveNode = vm.RootTreeNodes[0];
        archiveNode.Name.Should().Be("paquete_simulado.zip");
        archiveNode.IsArchive.Should().BeTrue();
        archiveNode.SimulatedArchiveEntries.Should().NotBeEmpty();
        archiveNode.IconKind.Should().Be(MaterialIconKind.ZipBox);
    }

    [Fact]
    public void RemoveTreeNode_ShouldRemoveSubtreeAndSync()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.EditableItems.Clear();
        vm.EditableItems.Add(new SyntheticFileDefinition("Sub/child1.txt", 100));
        vm.EditableItems.Add(new SyntheticFileDefinition("Sub/child2.txt", 200));
        vm.BuildTreeFromItems();

        var subNode = vm.RootTreeNodes.FirstOrDefault(n => n.Name == "Sub");
        subNode.Should().NotBeNull();

        vm.SelectedTreeNode = subNode;
        vm.RemoveTreeNodeCommand.Execute(null);

        vm.RootTreeNodes.Should().BeEmpty();
        vm.EditableItems.Should().BeEmpty();
    }

    [Fact]
    public void BuildTreeFromItems_WithArchive_ShouldPopulateArchiveChildNodes()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.EditableItems.Clear();
        var zipDef = new SyntheticFileDefinition("Archivos/paquete.zip", 1000000);
        zipDef.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("doc.pdf", 500000));
        zipDef.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("foto.jpg", 400000));
        vm.EditableItems.Add(zipDef);

        vm.BuildTreeFromItems();

        var archivosFolder = vm.RootTreeNodes.FirstOrDefault(n => n.Name == "Archivos");
        archivosFolder.Should().NotBeNull();
        var zipNode = archivosFolder!.Children.FirstOrDefault(n => n.Name == "paquete.zip");
        zipNode.Should().NotBeNull();
        zipNode!.IsArchive.Should().BeTrue();
        zipNode.Children.Should().HaveCount(2);

        var docEntry = zipNode.Children.FirstOrDefault(c => c.Name == "doc.pdf");
        docEntry.Should().NotBeNull();
        docEntry!.IsArchiveEntry.Should().BeTrue();
        docEntry.IconKind.Should().Be(MaterialIconKind.FileDocument);

        var fotoEntry = zipNode.Children.FirstOrDefault(c => c.Name == "foto.jpg");
        fotoEntry.Should().NotBeNull();
        fotoEntry!.IsArchiveEntry.Should().BeTrue();
        fotoEntry.IconKind.Should().Be(MaterialIconKind.Image);
    }

    [Fact]
    public void AddFileToTree_WithSelectedArchive_ShouldAddInternalEntry()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.EditableItems.Clear();
        var zipDef = new SyntheticFileDefinition("paquete.zip", 1000000);
        vm.EditableItems.Add(zipDef);
        vm.BuildTreeFromItems();

        var zipNode = vm.RootTreeNodes.FirstOrDefault(n => n.Name == "paquete.zip");
        zipNode.Should().NotBeNull();

        vm.SelectedTreeNode = zipNode;
        int initialChildCount = zipNode!.Children.Count;

        vm.AddFileToTreeCommand.Execute(null);

        zipNode.Children.Count.Should().Be(initialChildCount + 1);
        var addedEntry = zipNode.Children.Last();
        addedEntry.IsArchiveEntry.Should().BeTrue();
        addedEntry.Parent.Should().Be(zipNode);
        vm.SelectedTreeNode.Should().Be(addedEntry);
    }

    [Fact]
    public void AddArchiveEntryCommand_And_RemoveArchiveEntryCommand_ShouldWorkCorrectly()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.EditableItems.Clear();
        var zipDef = new SyntheticFileDefinition("test.zip", 500000);
        vm.EditableItems.Add(zipDef);
        vm.BuildTreeFromItems();

        var zipNode = vm.RootTreeNodes.First();
        vm.SelectedTreeNode = zipNode;

        vm.AddArchiveEntryCommand.Execute(null);

        var addedEntryDef = zipNode.SimulatedArchiveEntries.LastOrDefault();
        addedEntryDef.Should().NotBeNull();

        vm.RemoveArchiveEntryCommand.Execute(addedEntryDef);
        zipNode.SimulatedArchiveEntries.Should().NotContain(addedEntryDef!);
    }

    [Fact]
    public void ExpandAllTree_And_CollapseAllTree_ShouldToggleExpansionRecursively()
    {
        var vm = new SyntheticDataSetDesignerViewModel(_storageService);
        vm.EditableItems.Clear();
        vm.EditableItems.Add(new SyntheticFileDefinition("A/B/C/file.txt", 50));
        vm.BuildTreeFromItems();

        vm.CollapseAllTreeCommand.Execute(null);
        var aNode = vm.RootTreeNodes[0];
        aNode.IsExpanded.Should().BeFalse();
        aNode.Children[0].IsExpanded.Should().BeFalse();

        vm.ExpandAllTreeCommand.Execute(null);
        aNode.IsExpanded.Should().BeTrue();
        aNode.Children[0].IsExpanded.Should().BeTrue();
    }

    // ── La orden DESTRUCTIVA del diseñador: borrar un dataset propio ─────────────────────────────────────

    /// <summary>Un dataset propio, guardado y elegido: lo único que el borrado puede borrar (los del sistema no).</summary>
    private SyntheticDataSetDesignerViewModel WithCustomDataSetSelected(Mock<IDialogService> dialogs)
    {
        var custom = new SyntheticDataSet("Dataset Propio", "General")
        {
            Items = [new SyntheticFileDefinition("a.txt", 10)],
        };

        _storageService.SaveDataSet(custom);

        var vm = new SyntheticDataSetDesignerViewModel(_storageService, dialogs.Object);
        vm.SelectedDataSet = vm.FilteredDataSets.First(d => d.Id == custom.Id);
        vm.SelectedDataSet!.IsBuiltIn.Should().BeFalse("la prueba necesita un dataset propio: los del sistema no se borran");
        return vm;
    }

    [Fact]
    public async Task DeleteDataSetCommand_WhenConfirmed_ShouldRemoveTheCustomDataSet()
    {
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = WithCustomDataSetSelected(dialogs);
        string id = vm.SelectedDataSet!.Id;

        await vm.DeleteDataSetCommand.ExecuteAsync(null);

        vm.FilteredDataSets.Should().NotContain(d => d.Id == id);
        _storageService.GetDataSetById(id).Should().BeNull("confirmado el borrado, el almacén lo pierde de verdad");
    }

    [Fact]
    public async Task DeleteDataSetCommand_WhenRefused_ShouldLeaveTheDataSetAlone()
    {
        // La variante SÍNCRONA del contrato contesta «sí»: es la que contestaba el doble nulo (y en un host
        // WinUI devuelve «no» sin preguntar). Si el diseñador volviera a preguntar por ahí, o si la puerta le
        // entregara otra vez el doble nulo, el dataset desaparecería sin que nadie lo autorice y esta prueba
        // lo dice.
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        var vm = WithCustomDataSetSelected(dialogs);
        string id = vm.SelectedDataSet!.Id;

        await vm.DeleteDataSetCommand.ExecuteAsync(null);

        vm.FilteredDataSets.Should().Contain(d => d.Id == id);
        _storageService.GetDataSetById(id).Should().NotBeNull();
        dialogs.Verify(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once,
            "la pregunta tiene que haber salido por la vía asíncrona del contrato");
        dialogs.Verify(d => d.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()), Times.Never,
            "la vía síncrona no contesta por el usuario en ninguna rama");
    }
}


