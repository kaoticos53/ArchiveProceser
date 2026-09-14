using FileFlow.App.ViewModels;
using FileFlow.Plugin.AI;
using FileFlow.Sdk.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class AiModelUrlsConfigViewModelTests
{
    private readonly Mock<IDialogService> _dialogMock = new();

    [Fact]
    public void Constructor_InitializesWithCatalogInfoAndDefaultUrls()
    {
        string modelId = "mobilenetv2";
        var vm = new AiModelUrlsConfigViewModel(modelId, _dialogMock.Object);

        vm.ModelId.Should().Be(modelId);
        vm.ModelName.Should().NotBeNullOrWhiteSpace();
        vm.Category.Should().NotBeNullOrWhiteSpace();
        vm.UrlsText.Should().NotBeNullOrWhiteSpace();
        vm.ParseUrls().Should().NotBeEmpty();
    }

    [Fact]
    public void ResetToDefaults_RestoresOriginalUrls()
    {
        string modelId = "mobilenetv2";
        var vm = new AiModelUrlsConfigViewModel(modelId, _dialogMock.Object);

        vm.UrlsText = "https://example.com/custom_model.onnx";
        vm.ParseUrls().Should().ContainSingle().Which.Should().Be("https://example.com/custom_model.onnx");

        vm.ResetToDefaults();
        var defaults = AiModelManager.GetDefaultUrls(modelId);
        vm.ParseUrls().Should().BeEquivalentTo(defaults);
    }

    [Fact]
    public void Save_WhenUrlsEmpty_ShowsWarningAndReturnsFalse()
    {
        string modelId = "mobilenetv2";
        var vm = new AiModelUrlsConfigViewModel(modelId, _dialogMock.Object)
        {
            UrlsText = ""
        };

        bool saved = vm.Save();

        saved.Should().BeFalse();
        _dialogMock.Verify(d => d.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Save_WhenValidUrls_UpdatesConfigAndReturnsTrue()
    {
        string modelId = "mobilenetv2";
        var vm = new AiModelUrlsConfigViewModel(modelId, _dialogMock.Object)
        {
            UrlsText = "https://custom.mirror.org/tiny_yolo.onnx"
        };

        try
        {
            bool saved = vm.Save();
            saved.Should().BeTrue();
            vm.IsCustomConfig.Should().BeTrue();
            AiModelManager.GetConfiguredUrls(modelId).Should().Contain("https://custom.mirror.org/tiny_yolo.onnx");
        }
        finally
        {
            AiModelManager.ResetCustomUrls(modelId);
        }
    }
}
