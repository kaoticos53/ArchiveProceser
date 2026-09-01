using System.IO;
using System.Windows;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.FileSystem.UI.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class AdvancedRenamerEditorViewModelTests : IDisposable
{
    private readonly string _testDir;

    public AdvancedRenamerEditorViewModelTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileFlow_RenamerPreview_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
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
        vm.PreviewItems.Count.Should().Be(18);
        vm.PreviewSourceDescription.Should().Contain("sintéticas");
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
        vm.AvailablePresets.Should().HaveCount(12);
        vm.AvailableTags.Should().NotBeEmpty();
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
}
