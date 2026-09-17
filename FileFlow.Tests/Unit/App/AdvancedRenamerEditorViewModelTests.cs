using System.IO;
using Avalonia;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.FileSystem.UI.Services;
using FileFlow.Plugin.FileSystem.UI.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

[Collection("RenamerSampleDataTests")]
public class AdvancedRenamerEditorViewModelTests : IDisposable
{
    private readonly string _testDir;

    public AdvancedRenamerEditorViewModelTests()
    {
        RenamerSampleDataProvider.ClearCustomSamples();
        _testDir = Path.Combine(Path.GetTempPath(), "FileFlow_RenamerPreview_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        RenamerSampleDataProvider.ClearCustomSamples();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void Constructor_WithoutFolderSource_ShouldUseSyntheticSamplesAndDefaultDescription()
    {
        // Arrange
        var renamerNode = new AdvancedRenamerNode();

        // Act
        var vm = new AdvancedRenamerEditorViewModel(renamerNode);

        // Assert
        vm.PreviewItems.Should().NotBeEmpty();
        vm.PreviewItems.Count.Should().BeGreaterThanOrEqualTo(18);
        vm.PreviewSourceDescription.Should().Contain("sintéticas");
        vm.SampleCategories.Should().Contain("Películas");

        // Act - Switch category to Películas
        vm.SelectedSampleCategory = "Películas";
        vm.PreviewItems.Should().HaveCount(40);

        // Act - Switch category to Fotos
        vm.SelectedSampleCategory = "Fotos";
        vm.PreviewItems.Should().HaveCount(20);
        vm.PreviewItems.Should().Contain(p => p.OriginalName.Contains("DSC_0042") || p.OriginalName.Contains("IMG_"));
        var photoSamples = RenamerSampleDataProvider.GetSampleItemsByCategory("Fotos", out _);
        photoSamples.Should().AllSatisfy(p => p.Metadata.Should().ContainKey("Exif:CameraModel"));

        // Act - Switch category to Música
        vm.SelectedSampleCategory = "Música";
        vm.PreviewItems.Should().HaveCount(40);
        vm.PreviewItems.Should().Contain(p => p.OriginalName.Contains("Daft Punk") || p.OriginalName.Contains("Queen"));
        var musicSamples = RenamerSampleDataProvider.GetSampleItemsByCategory("Música", out _);
        musicSamples.Should().AllSatisfy(p => p.Metadata.Should().ContainKey("Audio:Artist"));

        // Act - Switch category to Documentos
        vm.SelectedSampleCategory = "Documentos";
        vm.PreviewItems.Should().HaveCount(20);
        vm.PreviewItems.Should().Contain(p => p.OriginalName.Contains("Factura") || p.OriginalName.Contains("Informe") || p.OriginalName.Contains("FAC-"));
        var docSamples = RenamerSampleDataProvider.GetSampleItemsByCategory("Documentos", out _);
        docSamples.Should().AllSatisfy(p => p.Metadata.Should().ContainKey("Doc:Author"));
    }

    [Fact]
    public void Constructor_ShouldInitializeMethodsAndPresetsCorrectly()
    {
        // Arrange
        var renamerNode = new AdvancedRenamerNode();

        // Act
        var vm = new AdvancedRenamerEditorViewModel(renamerNode);

        // Assert
        vm.PipelineName.Should().Be("Pipeline Predeterminado");
        vm.AvailablePresets.Should().HaveCountGreaterThanOrEqualTo(12);
        vm.AvailableTags.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WithPresetSelected_ShouldLoadPresetStepsAndSelectPreset()
    {
        // Arrange
        var renamerNode = new AdvancedRenamerNode();
        renamerNode.Parameters["PipelineName"] = "0️⃣1️⃣ Rellenar Números (1, 2... 10 -> 01, 02... 10)";

        // Act
        var vm = new AdvancedRenamerEditorViewModel(renamerNode);

        // Assert
        vm.PipelineName.Should().Be("0️⃣1️⃣ Rellenar Números (1, 2... 10 -> 01, 02... 10)");
        vm.SelectedPreset.Should().NotBeNull();
        vm.SelectedPreset!.Name.Should().Be("0️⃣1️⃣ Rellenar Números (1, 2... 10 -> 01, 02... 10)");
        vm.Steps.Should().HaveCount(2);
        vm.Steps[0].MethodType.Should().Be(FileFlow.Sdk.Renaming.RenameMethodType.NormalizeNumbers);
    }

    [Fact]
    public void RenamerSampleDataProvider_ShouldLoadFromJsonSuccessfully()
    {
        // Arrange
        string sampleJson = """
        [
          {
            "Directory": "C:\\Muestras\\Test",
            "FileName": "sample1.pdf",
            "FileSizeBytes": 2048,
            "IsDirectory": false,
            "Metadata": { "CustomTag": "Demo" }
          }
        ]
        """;
        string tempJsonFile = Path.Combine(_testDir, "test_samples.json");
        File.WriteAllText(tempJsonFile, sampleJson);

        // Act
        var loaded = FileFlow.Plugin.FileSystem.UI.Services.RenamerSampleDataProvider.TryLoadFromFile(tempJsonFile);

        // Assert
        loaded.Should().NotBeNull();
        loaded.Should().HaveCount(1);
        loaded![0].FileName.Should().Be("sample1.pdf");
        loaded[0].Metadata["CustomTag"].Should().Be("Demo");
    }

    [Fact]
    public void RenamerPresetService_ShouldLoadFromJsonSuccessfully()
    {
        // Arrange
        string presetJson = """
        [
          {
            "Name": "Preset de Prueba",
            "Category": "Pruebas",
            "Description": "Preset para verificar carga JSON",
            "Steps": []
          }
        ]
        """;
        string tempJsonFile = Path.Combine(_testDir, "test_presets.json");
        File.WriteAllText(tempJsonFile, presetJson);

        // Act
        var loaded = FileFlow.Sdk.Renaming.RenamerPresetService.TryLoadPresetsFromFile(tempJsonFile);

        // Assert
        loaded.Should().NotBeNull();
        loaded.Should().HaveCount(1);
        loaded![0].Name.Should().Be("Preset de Prueba");
    }

    [Fact]
    public void RegexLibrary_And_ScriptLibrary_ShouldLoadBuiltinsWithoutExceptions()
    {
        // Act
        var regexes = FileFlow.Plugin.FileSystem.UI.Services.RegexLibraryService.Instance.GetBuiltInPatterns();
        var scripts = FileFlow.Plugin.Scripting.Services.ScriptLibraryService.Instance.GetBuiltInScripts();

        // Assert
        regexes.Should().NotBeNullOrEmpty();
        scripts.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SelectedPreset_WhenChanged_ShouldUpdatePipelineNameAndSteps()
    {
        // Arrange
        var renamerNode = new AdvancedRenamerNode();
        var vm = new AdvancedRenamerEditorViewModel(renamerNode);

        var targetPreset = vm.AvailablePresets.FirstOrDefault(p => p.Name.Contains("Fotografía"));
        targetPreset.Should().NotBeNull();

        // Act
        vm.SelectedPreset = targetPreset;

        // Assert
        vm.PipelineName.Should().Be(targetPreset!.Name);
        vm.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public void ExecuteCustomAction_WithNodeCustomActionContext_ShouldAcceptContext()
    {
        // Arrange
        var renamerNode = new AdvancedRenamerNode();
        bool callbackFired = false;
        var context = new FileFlow.Sdk.NodeCustomActionContext(null, () => callbackFired = true);

        // Act - CustomAction should accept context without exception
        renamerNode.Invoking(n => n.ExecuteCustomAction("UnknownAction", context)).Should().NotThrow();
        callbackFired.Should().BeFalse();
    }
}
