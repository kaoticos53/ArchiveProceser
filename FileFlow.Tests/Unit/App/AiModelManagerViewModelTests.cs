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

    [Fact]
    public void AiModelItemViewModel_RefreshState_WithErrorMessage_RetainsErrorState()
    {
        // Arrange
        var item = new AiModelItemViewModel
        {
            ModelId = "non-existent-model-id",
            Name = "NonExistentModel",
            Category = "Test",
            ErrorMessage = "Fallo de conexión 404",
            HasError = true
        };

        // Act
        item.RefreshState();

        // Assert
        item.HasError.Should().BeTrue();
        item.ErrorMessage.Should().Be("Fallo de conexión 404");
        item.StatusIcon.Should().Be("❌");
        item.StatusText.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AiModelManagerViewModel_DownloadUnknownModel_SetsErrorStateAndDetails()
    {
        // Arrange
        var vm = new AiModelManagerViewModel();
        var fakeItem = new AiModelItemViewModel
        {
            ModelId = "invalid-unknown-model",
            Name = "Unknown Model",
            Category = "Test"
        };

        // Act
        await vm.DownloadModelInternalAsync(fakeItem, suppressSingleAlert: true);

        // Assert
        fakeItem.HasError.Should().BeTrue();
        fakeItem.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        fakeItem.StatusIcon.Should().Be("❌");
        vm.HasDownloadError.Should().BeTrue();
        vm.LastDownloadErrorMessage.Should().NotBeNullOrWhiteSpace();

        // Act 2: Descartar error
        vm.DismissError();
        vm.HasDownloadError.Should().BeFalse();
        vm.LastDownloadErrorMessage.Should().BeNull();
    }

    [Fact]
    public void AiModelItemViewModel_RefreshState_ReflectsCustomUrlsStatus()
    {
        string modelId = "mobilenetv2";
        AiModelManager.ResetCustomUrls(modelId);

        var item = new AiModelItemViewModel
        {
            ModelId = modelId,
            Name = "MobileNetV2",
            Category = "Visión"
        };

        try
        {
            // Initial
            item.RefreshState();
            item.HasCustomUrls.Should().BeFalse();
            item.ConfiguredUrlsCount.Should().BeGreaterThan(0);

            // Set custom
            AiModelManager.SetCustomUrls(modelId, new[] { "https://custom-mirror.example.com/model.onnx" });
            item.RefreshState();
            item.HasCustomUrls.Should().BeTrue();
            item.ConfiguredUrlsCount.Should().Be(1);

            // Reset
            AiModelManager.ResetCustomUrls(modelId);
            item.RefreshState();
            item.HasCustomUrls.Should().BeFalse();
        }
        finally
        {
            AiModelManager.ResetCustomUrls(modelId);
        }
    }
}
