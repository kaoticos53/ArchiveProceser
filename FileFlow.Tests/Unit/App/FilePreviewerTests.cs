using System.IO;
using FileFlow.App.Preview.Core;
using FileFlow.App.Preview.Providers;
using FileFlow.App.Preview.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class FilePreviewerTests : IDisposable
{
    private readonly string _tempDir;

    public FilePreviewerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_Preview_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void FilePreviewRegistry_DefaultProviders_ShouldResolveImageAndText()
    {
        var registry = FilePreviewRegistry.Instance;
        registry.AllProviders.Should().NotBeEmpty();

        var imgCtx = new FilePreviewContext("foto.jpg");
        var imgProvider = registry.GetProvider(imgCtx);
        imgProvider.Should().NotBeNull();
        imgProvider.Should().BeOfType<ImagePreviewProvider>();

        var txtCtx = new FilePreviewContext("codigo.cs");
        var txtProvider = registry.GetProvider(txtCtx);
        txtProvider.Should().NotBeNull();
        txtProvider.Should().BeOfType<TextCodePreviewProvider>();

        var xlsxCtx = new FilePreviewContext("datos.xlsx");
        var xlsxProvider = registry.GetProvider(xlsxCtx);
        xlsxProvider.Should().NotBeNull();
        xlsxProvider.Should().BeOfType<SpreadsheetPreviewProvider>();

        var mp3Ctx = new FilePreviewContext("audio.mp3");
        var mp3Provider = registry.GetProvider(mp3Ctx);
        mp3Provider.Should().NotBeNull();
        mp3Provider.Should().BeOfType<AudioPreviewProvider>();

        var zipCtx = new FilePreviewContext("paquete.zip");
        var zipProvider = registry.GetProvider(zipCtx);
        zipProvider.Should().NotBeNull();
        zipProvider.Should().BeOfType<ArchiveTreePreviewProvider>();
    }

    [Fact]
    public async Task FilePreviewerViewModel_LoadContext_ShouldPopulateMetadataAndNavigation()
    {
        string sampleFile1 = Path.Combine(_tempDir, "documento_1.txt");
        string sampleFile2 = Path.Combine(_tempDir, "documento_2.txt");

        await File.WriteAllTextAsync(sampleFile1, "Contenido 1");
        await File.WriteAllTextAsync(sampleFile2, "Contenido 2");

        var ctx1 = new FilePreviewContext(sampleFile1);
        ctx1.Metadata["AI:Category"] = "Documento";
        var ctx2 = new FilePreviewContext(sampleFile2);

        var vm = new FilePreviewerViewModel();
        await vm.LoadContextAsync(ctx1, [ctx1, ctx2]);

        vm.CurrentContext.Should().Be(ctx1);
        vm.CurrentIndex.Should().Be(0);
        vm.TotalItemsCount.Should().Be(2);
        vm.CanNavigateNext.Should().BeTrue();
        vm.CanNavigatePrevious.Should().BeFalse();

        vm.MetadataItems.Should().Contain(m => m.Key == "Archivo" && m.Value == "documento_1.txt");
        vm.MetadataItems.Should().Contain(m => m.Key == "AI:Category" && m.Value == "Documento" && m.IsAi);

        // Navegar siguiente
        await vm.NavigateNextAsync();
        vm.CurrentContext.Should().Be(ctx2);
        vm.CurrentIndex.Should().Be(1);
        vm.CanNavigateNext.Should().BeFalse();
        vm.CanNavigatePrevious.Should().BeTrue();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch { }
    }
}
