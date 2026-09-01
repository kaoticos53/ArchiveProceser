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
        vm.PreviewItems.Count.Should().Be(6);
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
        vm.AvailablePresets.Should().NotBeEmpty();
        vm.AvailableTags.Should().NotBeEmpty();
    }
}
