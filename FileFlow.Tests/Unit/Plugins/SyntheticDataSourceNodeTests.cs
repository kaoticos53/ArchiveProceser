using System.Diagnostics;
using System.IO;
using FluentAssertions;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.FileSystem.UI.Services;
using FileFlow.Sdk;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

[Collection("RenamerSampleDataTests")]
public class SyntheticDataSourceNodeTests : IDisposable
{
    private readonly string _tempDirectory;

    public SyntheticDataSourceNodeTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FileFlow_SyntheticSourceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
        RenamerSampleDataProvider.ClearCustomSamples();
    }

    [Fact]
    public async Task SyntheticDataSourceNode_VirtualMode_EmitsMoviesCategoryWithLimit()
    {
        // Arrange
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Películas";
        node.Parameters["EmissionMode"] = "Virtual";
        node.Parameters["MaxItems"] = 5;

        var mockContext = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<FileItemContext>();

        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => emittedItems.Add(item))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", new FileItemContext("dummy"), mockContext.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(5);
        foreach (var item in emittedItems)
        {
            item.Metadata.Should().ContainKey("Category");
            item.Metadata["Category"].Should().Be("Películas");
        }
    }

    [Fact]
    public async Task SyntheticDataSourceNode_VirtualMode_EmitsSeriesCategory()
    {
        // Arrange
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Series";
        node.Parameters["EmissionMode"] = "Virtual";
        node.Parameters["MaxItems"] = 10;

        var mockContext = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<FileItemContext>();

        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => emittedItems.Add(item))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", new FileItemContext("dummy"), mockContext.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(10);
        emittedItems.Should().Contain(i => i.CurrentPath.Contains("Breaking.Bad") || i.CurrentPath.Contains("Stranger.Things"));
    }

    [Fact]
    public async Task SyntheticDataSourceNode_VirtualMode_EmitsMusicCategoryWithRichAudioMetadata()
    {
        // Arrange
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Música";
        node.Parameters["EmissionMode"] = "Virtual";
        node.Parameters["MaxItems"] = 5;

        var mockContext = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<FileItemContext>();

        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => emittedItems.Add(item))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", new FileItemContext("dummy"), mockContext.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(5);
        foreach (var item in emittedItems)
        {
            item.Metadata.Should().ContainKey("Category");
            item.Metadata["Category"].Should().Be("Música");
            item.Metadata.Should().ContainKey("Audio:Artist");
            item.Metadata.Should().ContainKey("Audio:Title");
            item.Metadata.Should().ContainKey("Audio:Album");
        }
    }

    [Fact]
    public async Task SyntheticDataSourceNode_VirtualMode_EmitsPhotosCategoryWithExifMetadata()
    {
        // Arrange
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Fotos";
        node.Parameters["EmissionMode"] = "Virtual";
        node.Parameters["MaxItems"] = 5;

        var mockContext = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<FileItemContext>();

        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => emittedItems.Add(item))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", new FileItemContext("dummy"), mockContext.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(5);
        foreach (var item in emittedItems)
        {
            item.Metadata.Should().ContainKey("Category");
            item.Metadata["Category"].Should().Be("Fotos");
            item.Metadata.Should().ContainKey("Exif:CameraModel");
            item.Metadata.Should().ContainKey("Img:Width");
            item.Metadata.Should().ContainKey("Img:Height");
        }
    }

    [Fact]
    public async Task SyntheticDataSourceNode_VirtualMode_EmitsDocumentsCategoryWithDocMetadata()
    {
        // Arrange
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Documentos";
        node.Parameters["EmissionMode"] = "Virtual";
        node.Parameters["MaxItems"] = 5;

        var mockContext = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<FileItemContext>();

        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => emittedItems.Add(item))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", new FileItemContext("dummy"), mockContext.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(5);
        foreach (var item in emittedItems)
        {
            item.Metadata.Should().ContainKey("Category");
            item.Metadata["Category"].Should().Be("Documentos");
            item.Metadata.Should().ContainKey("Doc:Author");
            item.Metadata.Should().ContainKey("Doc:Title");
            item.Metadata.Should().ContainKey("Doc:PageCount");
        }
    }

    [Fact]
    public async Task SyntheticDataSourceNode_CustomItems_EmitsCustomTextList()
    {
        // Arrange
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Personalizada";
        node.Parameters["EmissionMode"] = "Virtual";
        node.Parameters["CustomItems"] = "MiArchivoEspecial.2024.1080p.mkv\nOtroArchivoDePrueba.mp4";

        var mockContext = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<FileItemContext>();

        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => emittedItems.Add(item))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", new FileItemContext("dummy"), mockContext.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(2);
        emittedItems[0].CurrentPath.Should().EndWith("MiArchivoEspecial.2024.1080p.mkv");
        emittedItems[1].CurrentPath.Should().EndWith("OtroArchivoDePrueba.mp4");
        emittedItems[0].Metadata["Category"].Should().Be("Personalizada");
    }

    [Fact]
    public async Task SyntheticDataSourceNode_PhysicalMockMode_CreatesPhysicalFilesOnDisk()
    {
        // Arrange
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Cómics y Manga";
        node.Parameters["EmissionMode"] = "PhysicalMock";
        node.Parameters["OutputFolder"] = _tempDirectory;
        node.Parameters["MaxItems"] = 3;

        var mockContext = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<FileItemContext>();

        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, item) => emittedItems.Add(item))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", new FileItemContext("dummy"), mockContext.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(3);
        foreach (var item in emittedItems)
        {
            File.Exists(item.CurrentPath).Should().BeTrue();
            item.CurrentPath.Should().StartWith(_tempDirectory);
        }
    }

    [Fact]
    public async Task SyntheticDataSourceNode_EmissionLatency_ShouldPaceEveryEmission()
    {
        // La latencia del origen sintético (EmissionDelayMs) simula un origen lento. Es la tercera espera que
        // sale del «tiempo real» del inventario de trabajo aplazado (hito 175) por la vía barata: el nodo no
        // tiene reloj inyectable —su fábrica lo construye sin dependencias—, pero una latencia de milisegundos
        // sí se la puede permitir una prueba.
        const int LatencyMs = 5;

        // Arrange
        var node = new SyntheticDataSourceNode();
        node.Parameters["Category"] = "Películas";
        node.Parameters["EmissionMode"] = "Virtual";
        node.Parameters["MaxItems"] = 3;
        node.Parameters["EmissionDelayMs"] = LatencyMs;

        var mockContext = new Mock<IFlowExecutionContext>();
        var emittedAt = new List<long>();

        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, _) => emittedAt.Add(Stopwatch.GetTimestamp()))
            .Returns(Task.CompletedTask);

        // Act
        var total = Stopwatch.StartNew();
        await node.ExecuteAsync("In", new FileItemContext("dummy"), mockContext.Object, CancellationToken.None);
        total.Stop();

        // Assert
        emittedAt.Should().HaveCount(3, "la latencia retrasa cada muestra, no descarta ninguna");

        // Task.Delay garantiza esperar al menos lo pedido, así que cada hueco entre dos emisiones contiene el
        // retardo declarado. El hueco —y no el tiempo total— es lo que lo prueba: el armado de las muestras
        // ocurre entero antes de la primera emisión, así que no puede inflarlo. Si el aplazamiento desapareciera,
        // los huecos caerían a microsegundos y esta aserción falla nombrando la latencia que el nodo dice respetar.
        var gaps = emittedAt
            .Zip(emittedAt.Skip(1), (before, after) => Stopwatch.GetElapsedTime(before, after))
            .ToList();

        gaps.Should().OnlyContain(
            gap => gap >= TimeSpan.FromMilliseconds(LatencyMs),
            $"cada emisión espera los {LatencyMs} ms de EmissionDelayMs antes de salir");

        total.Elapsed.Should().BeGreaterThanOrEqualTo(
            TimeSpan.FromMilliseconds(LatencyMs * emittedAt.Count),
            "hay un retardo por muestra, incluida la primera");
    }

    [Fact]
    public void RenamerSampleDataProvider_ManualSamples_AddAndFilterByCategory()
    {
        // Arrange
        RenamerSampleDataProvider.ClearCustomSamples();
        RenamerSampleDataProvider.AddCustomSample("Manual_Custom_Video_2026.mkv");

        // Act
        var customSamples = RenamerSampleDataProvider.GetSampleItemsByCategory("Personalizada", out var desc);
        var allSamples = RenamerSampleDataProvider.GetSampleItemsByCategory("Todas", out _);
        var movieSamples = RenamerSampleDataProvider.GetSampleItemsByCategory("Películas", out _);

        // Assert
        customSamples.Should().HaveCount(1);
        customSamples[0].CurrentPath.Should().EndWith("Manual_Custom_Video_2026.mkv");
        desc.Should().Contain("manuales");

        allSamples.Should().Contain(i => i.CurrentPath.EndsWith("Manual_Custom_Video_2026.mkv"));
        movieSamples.Should().NotContain(i => i.CurrentPath.EndsWith("Manual_Custom_Video_2026.mkv"));

        // Cleanup
        RenamerSampleDataProvider.ClearCustomSamples();
        RenamerSampleDataProvider.GetCustomSamples().Should().BeEmpty();
    }
}
