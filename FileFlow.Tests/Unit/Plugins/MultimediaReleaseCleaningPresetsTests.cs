using System.IO;
using FluentAssertions;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.FileSystem.UI.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Renaming;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class MultimediaReleaseCleaningPresetsTests : IDisposable
{
    private readonly string _tempDirectory;

    public MultimediaReleaseCleaningPresetsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FileFlow_MediaCleanTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
    }

    [Fact]
    public void RenamerPresetService_BuiltInPresets_ShouldContainMultimediaPipelineAndPhases()
    {
        // Act
        var presets = RenamerPresetService.GetBuiltinPresets();

        // Assert
        var fullPipeline = presets.FirstOrDefault(p => p.Name.Contains("Pipeline Limpieza Multimedia"));
        fullPipeline.Should().NotBeNull();
        fullPipeline!.Category.Should().Be("Multimedia");
        fullPipeline.Steps.Should().HaveCount(10);

        var cleanNamePreset = presets.FirstOrDefault(p => p.Name == "🧹 Limpiar Nombre");
        cleanNamePreset.Should().NotBeNull();
        cleanNamePreset!.Category.Should().Be("Limpieza");
        cleanNamePreset.Steps.Should().HaveCount(10);

        presets.Should().Contain(p => p.Name.Contains("Fase 1"));
        presets.Should().Contain(p => p.Name.Contains("Fase 2"));
        presets.Should().Contain(p => p.Name.Contains("Fase 3"));
        presets.Should().Contain(p => p.Name.Contains("Fase 4"));
        presets.Should().Contain(p => p.Name.Contains("Fase 5"));
        presets.Should().Contain(p => p.Name.Contains("Fase 6"));
    }

    [Fact]
    public void RegexLibraryService_BuiltInPatterns_ShouldContainMultimediaReleasePatterns()
    {
        // Act
        var patterns = RegexLibraryService.Instance.GetBuiltInPatterns();

        // Assert
        var mediaPatterns = patterns.Where(p => p.Category == "Releases y Multimedia").ToList();
        mediaPatterns.Should().NotBeEmpty();
        mediaPatterns.Should().Contain(p => p.Name.Contains("Fase 1"));
        mediaPatterns.Should().Contain(p => p.Name.Contains("Fase 2"));
        mediaPatterns.Should().Contain(p => p.Name.Contains("Fase 3"));
        mediaPatterns.Should().Contain(p => p.Name.Contains("Fase 4"));
        mediaPatterns.Should().Contain(p => p.Name.Contains("Fase 5A"));
        mediaPatterns.Should().Contain(p => p.Name.Contains("Fase 5B"));
        mediaPatterns.Should().Contain(p => p.Name.Contains("Fase 6A"));
        mediaPatterns.Should().Contain(p => p.Name.Contains("Fase 6B"));
        mediaPatterns.Should().Contain(p => p.Name.Contains("Fase 6C"));
    }

    [Theory]
    [InlineData("Pelicula.2024.www.TorrentSite.to.1080p.WEBRip.x265.10bit.DTS-HD.MA.AMZN.Dual.Audio.Castellano.sub-espanol-FLUX[TGx].mkv", "Pelicula 2024.mkv")]
    [InlineData("The.Show.S02E05.1080p.HEVC.AAC-MeGusta.mp4", "The Show S02E05.mp4")]
    [InlineData("Anime.Episodio.04.1080p.CR.Multi-Audio.sub-espanol[Erai-raws].mkv", "Anime Episodio 04.mkv")]
    [InlineData("Gran_Pelicula.De.Aventuras.2022.4K.UHD.HDR.Remux-CiNEFiLE.mkv", "Gran Pelicula De Aventuras 2022.mkv")]
    public async Task AdvancedRenamerNode_WithMultimediaPipeline_ShouldCleanDirtyReleaseNamesProperly(string inputName, string expectedCleanName)
    {
        // Arrange
        string testFile = Path.Combine(_tempDirectory, inputName);
        await File.WriteAllTextAsync(testFile, "dummy video data");

        var presets = RenamerPresetService.GetBuiltinPresets();
        var fullPipeline = presets.First(p => p.Name.Contains("Pipeline Limpieza Multimedia"));

        var node = new AdvancedRenamerNode();
        node.Parameters["RenameMode"] = "DirectInPlace";
        node.Parameters["MethodSteps"] = RenamerPresetService.SerializeSteps(fullPipeline.Steps);

        var item = new FileItemContext(testFile);
        var mockContext = new Mock<IFlowExecutionContext>();
        string? emittedPin = null;
        FileItemContext? emittedItem = null;

        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((pin, it) =>
            {
                emittedPin = pin;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedPin.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        Path.GetFileName(emittedItem!.CurrentPath).Should().Be(expectedCleanName);
        File.Exists(Path.Combine(_tempDirectory, expectedCleanName)).Should().BeTrue();
    }

    [Fact]
    public async Task AdvancedRenamerNode_PipelineNameResolution_ShouldExecutePresetWhenMethodStepsEmpty()
    {
        // Arrange
        string inputName = "Blockbuster.Movie.2025.2160p.UHD.TrueHD.Atmos.NF-ROVERS.mkv";
        string testFile = Path.Combine(_tempDirectory, inputName);
        await File.WriteAllTextAsync(testFile, "dummy video data");

        var node = new AdvancedRenamerNode();
        node.Parameters["RenameMode"] = "DirectInPlace";
        // MethodSteps queda vacío a propósito, pero se especifica el PipelineName que coincide con el preset
        node.Parameters["MethodSteps"] = string.Empty;
        node.Parameters["PipelineName"] = "Pipeline Limpieza Multimedia (Scene, Rips, Códecs y URLs)";

        var item = new FileItemContext(testFile);
        var mockContext = new Mock<IFlowExecutionContext>();
        string? emittedPin = null;
        FileItemContext? emittedItem = null;

        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((pin, it) =>
            {
                emittedPin = pin;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedPin.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        Path.GetFileName(emittedItem!.CurrentPath).Should().Be("Blockbuster Movie 2025.mkv");
    }

    [Theory]
    [InlineData("🧹 Limpiar Nombre")]
    [InlineData("Limpiar Nombre")]
    public async Task AdvancedRenamerNode_LimpiarNombrePreset_ShouldExecutePresetByName(string pipelineName)
    {
        // Arrange
        string inputName = "Series.Show.S01E01.1080p.WEBRip.x264.AAC.AMZN.Dual.Audio.Castellano[TorrentSite].mkv";
        string testFile = Path.Combine(_tempDirectory, inputName);
        await File.WriteAllTextAsync(testFile, "dummy video data");

        var node = new AdvancedRenamerNode();
        node.Parameters["RenameMode"] = "DirectInPlace";
        node.Parameters["MethodSteps"] = string.Empty;
        node.Parameters["PipelineName"] = pipelineName;

        var item = new FileItemContext(testFile);
        var mockContext = new Mock<IFlowExecutionContext>();
        string? emittedPin = null;
        FileItemContext? emittedItem = null;

        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((pin, it) =>
            {
                emittedPin = pin;
                emittedItem = it;
            })
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedPin.Should().Be("Out");
        emittedItem.Should().NotBeNull();
        Path.GetFileName(emittedItem!.CurrentPath).Should().Be("Series Show S01E01.mkv");
    }
}
