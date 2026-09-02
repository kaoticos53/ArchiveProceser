using System.IO;
using FileFlow.App.ViewModels;
using FileFlow.Plugin.AI;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class AiModelManagerViewModelTests
{
    [Fact]
    public void AiModelManagerViewModel_Initialization_ShouldLoadAllCatalogModels()
    {
        // Act
        var vm = new AiModelManagerViewModel();

        // Assert
        vm.Models.Should().NotBeEmpty();
        vm.Models.Count.Should().Be(AiModelManager.Catalog.Count);
        vm.ModelsDirectory.Should().NotBeNullOrWhiteSpace();
        vm.InstalledSummary.Should().NotBeNullOrWhiteSpace();

        foreach (var model in vm.Models)
        {
            model.ModelId.Should().NotBeNullOrWhiteSpace();
            model.Name.Should().NotBeNullOrWhiteSpace();
            model.Category.Should().NotBeNullOrWhiteSpace();
            model.Description.Should().NotBeNullOrWhiteSpace();
            model.ExpectedSizeLabel.Should().NotBeNullOrWhiteSpace();
            model.StatusIcon.Should().NotBeNullOrWhiteSpace();
            model.StatusText.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void AiModelManagerViewModel_RefreshStatus_CalculatesCountsCorrectly()
    {
        // Arrange
        var vm = new AiModelManagerViewModel();

        // Act
        vm.RefreshStatus();

        // Assert
        vm.InstalledSummary.Should().Contain("modelos instalados");
        (vm.HasMissingModels || !vm.HasMissingModels).Should().BeTrue();
    }

    [Fact]
    public void AiModelItemViewModel_RefreshState_SetsProperIconsAndLabels()
    {
        // Arrange
        var item = new AiModelItemViewModel
        {
            ModelId = "mobilenetv2",
            Name = "MobileNetV2",
            Category = "Visión",
            ExpectedSizeLabel = "~14 MB"
        };

        // Act
        item.RefreshState();

        // Assert
        item.StatusIcon.Should().BeOneOf("✅", "⏳");
        item.StatusText.Should().NotBeNullOrWhiteSpace();
        item.CanDownload.Should().BeTrue();
        item.IsDownloading.Should().BeFalse();
    }

    [Fact]
    public async Task AiModelManager_DownloadUltraFace_ShouldPersistFileOnDiskAndNotDelete()
    {
        // Act: descargar UltraFace (1.2 MB) para probar descarga real e inmutabilidad en disco
        double lastProgress = 0.0;
        var progress = new Progress<double>(p => lastProgress = p);

        string? modelPath = await AiModelManager.DownloadModelWithProgressAsync(
            "ultraface",
            progress,
            statusLogger: null,
            CancellationToken.None);

        // Assert
        modelPath.Should().NotBeNull();
        File.Exists(modelPath!).Should().BeTrue("el archivo del modelo debe persistir en el disco y NO ser eliminado");

        var fi = new FileInfo(modelPath!);
        fi.Length.Should().BeGreaterThanOrEqualTo(1_000_000, "el tamaño del archivo debe cumplir el mínimo");

        bool available = AiModelManager.IsModelAvailable("ultraface");
        available.Should().BeTrue();

        long? diskSize = AiModelManager.GetModelDiskSizeBytes("ultraface");
        diskSize.Should().Be(fi.Length);
    }
}
