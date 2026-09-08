using System.IO;
using FileFlow.Plugin.FileSystem.Services;
using FileFlow.Sdk.SyntheticData;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class SyntheticDataSetStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SyntheticDataSetStorageService _service;

    public SyntheticDataSetStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_DataSetStorageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service = new SyntheticDataSetStorageService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void StorageService_ShouldLoadBuiltInDataSets_Initially()
    {
        var all = _service.GetAllDataSets();
        all.Should().NotBeEmpty();
        all.Should().Contain(d => d.IsBuiltIn);
    }

    [Fact]
    public void StorageService_SaveAndGetDataSet_ShouldPersistToDisk()
    {
        var ds = new SyntheticDataSet("Test Series 4K", "Series", "Descripción de prueba")
        {
            Items =
            [
                new SyntheticFileDefinition("Temporada 01/S01E01.mkv", 1024 * 1024 * 500, false),
                new SyntheticFileDefinition("Temporada 01/S01E02.mkv", 1024 * 1024 * 500, false)
            ]
        };

        _service.SaveDataSet(ds);

        var retrieved = _service.GetDataSetById(ds.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Series 4K");
        retrieved.Items.Should().HaveCount(2);
        retrieved.Items[0].RelativePath.Should().Be("Temporada 01/S01E01.mkv");

        string expectedFile = Path.Combine(_tempDir, $"{ds.Id}.json");
        File.Exists(expectedFile).Should().BeTrue();
    }

    [Fact]
    public void StorageService_DeleteDataSet_ShouldRemoveCustomAndProtectBuiltIn()
    {
        var custom = new SyntheticDataSet("Custom Borrable", "General");
        _service.SaveDataSet(custom);

        bool deleted = _service.DeleteDataSet(custom.Id);
        deleted.Should().BeTrue();
        _service.GetDataSetById(custom.Id).Should().BeNull();

        // Probar protección de built-in
        var builtIn = _service.GetAllDataSets().First(d => d.IsBuiltIn);
        bool deletedBuiltIn = _service.DeleteDataSet(builtIn.Id);
        deletedBuiltIn.Should().BeFalse();
        _service.GetDataSetById(builtIn.Id).Should().NotBeNull();
    }

    [Fact]
    public void StorageService_CloneDataSet_ShouldCreateIndependentCopy()
    {
        var original = new SyntheticDataSet("Base Dataset", "Fotos")
        {
            Items = [new SyntheticFileDefinition("Vacaciones/foto1.jpg", 2048)]
        };
        _service.SaveDataSet(original);

        var clone = _service.CloneDataSet(original.Id, "Base Dataset Copia");
        clone.Should().NotBeNull();
        clone.Id.Should().NotBe(original.Id);
        clone.Name.Should().Be("Base Dataset Copia");
        clone.Items.Should().HaveCount(1);
    }

    [Fact]
    public void StorageService_ExportAndImport_ShouldRoundTripCorrectly()
    {
        var ds = new SyntheticDataSet("Exportable", "Música")
        {
            Items =
            [
                new SyntheticFileDefinition("Album/track01.flac", 40000000, false, new Dictionary<string, object?>
                {
                    ["Audio:Artist"] = "Queen",
                    ["Audio:Title"] = "Bohemian Rhapsody"
                })
            ]
        };

        string json = _service.ExportDataSetToJson(ds);
        json.Should().Contain("Bohemian Rhapsody");

        var imported = _service.ImportDataSetFromJson(json, autoSave: true);
        imported.Should().NotBeNull();
        imported.Name.Should().Be("Exportable");
        imported.Items.Should().HaveCount(1);
        imported.Items[0].Metadata["Audio:Artist"]?.ToString().Should().Be("Queen");
    }
}
