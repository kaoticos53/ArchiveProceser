using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Plugin.Images;
using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class TemporaryDirectoryAndSizeVariablesTests : IDisposable
{
    private readonly string _testTempDir;

    public TemporaryDirectoryAndSizeVariablesTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "FileFlow_TempDirTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
            // Limpieza pasiva
        }
    }

    [Fact]
    public void SystemVariablesResolver_ShouldResolveTempDirAndRandomId()
    {
        // Arrange
        var item = new FileItemContext(Path.Combine(_testTempDir, "test.txt"));
        item.Metadata["TemporaryDirectory"] = _testTempDir;

        // Act
        string tempDirVal = VariableTemplateResolver.Resolve("{TempDir}", item);
        string tempWorkingDirVal = VariableTemplateResolver.Resolve("{TempWorkingDir}", item);
        string randomId1 = VariableTemplateResolver.Resolve("{RandomId}", item);
        string randomId2 = VariableTemplateResolver.Resolve("{RandomId}", item);
        string guidVal = VariableTemplateResolver.Resolve("{Guid}", item);

        // Assert
        tempDirVal.Should().Be(_testTempDir);
        tempWorkingDirVal.Should().Be(_testTempDir);
        randomId1.Should().NotBeNullOrWhiteSpace().And.HaveLength(8);
        randomId2.Should().NotBeNullOrWhiteSpace().And.HaveLength(8);
        randomId1.Should().NotBe(randomId2, "cada llamada a {RandomId} debe generar un id aleatorio único");
        Guid.TryParse(guidVal, out _).Should().BeTrue("debe tener formato de GUID válido");
    }

    [Fact]
    public void SystemVariablesResolver_ShouldResolveSizeVariables()
    {
        // Arrange
        var item = new FileItemContext(Path.Combine(_testTempDir, "image.webp"));
        item.FileSizeBytes = 50000;
        item.Metadata["OriginalFileSizeBytes"] = 100000L;
        item.Metadata["OutputFileSizeBytes"] = 50000L;
        item.Metadata["SavedBytes"] = 50000L;
        item.Metadata["SavedPercent"] = 50.0;
        item.Metadata["CompressionRatio"] = 0.5;

        // Act
        string origBytes = VariableTemplateResolver.Resolve("{OriginalFileSize}", item);
        string origKb = VariableTemplateResolver.Resolve("{OriginalFileSizeKB}", item);
        string origMb = VariableTemplateResolver.Resolve("{OriginalFileSizeMB}", item);
        string outBytes = VariableTemplateResolver.Resolve("{OutputFileSize}", item);
        string savedBytes = VariableTemplateResolver.Resolve("{SavedBytes}", item);
        string savedPct = VariableTemplateResolver.Resolve("{SavedPercent}", item);
        string ratio = VariableTemplateResolver.Resolve("{CompressionRatio}", item);

        // Assert
        origBytes.Should().Be("100000");
        origKb.Should().Be((100000 / 1024.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        outBytes.Should().Be("50000");
        savedBytes.Should().Be("50000");
        savedPct.Should().Be("50");
        ratio.Should().Be("0.5");
    }

    [Fact]
    public async Task ImageOptimizerNode_WhenOutputDirectoryEmpty_ShouldUseTemporaryDirectoryWithRandomSubfolder()
    {
        // Arrange: Crear imagen de prueba 50x50
        string inputImage = Path.Combine(_testTempDir, "sample.png");
        using (var img = new Image<Rgba32>(50, 50))
        {
            await img.SaveAsPngAsync(inputImage);
        }

        var node = new ImageOptimizerNode();
        node.Parameters["OutputDirectory"].Should().Be(string.Empty, "por defecto OutputDirectory debe estar vacío");

        var item = new FileItemContext(inputImage)
        {
            OriginalPath = inputImage,
            FileSizeBytes = new FileInfo(inputImage).Length
        };

        FileItemContext? emittedItem = null;
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.TemporaryDirectory).Returns(_testTempDir);
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, itm) => emittedItem = itm)
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedItem.Should().NotBeNull();
        emittedItem!.CurrentPath.Should().StartWith(_testTempDir);
        Path.GetDirectoryName(emittedItem.CurrentPath).Should().NotBe(_testTempDir, "debe guardarse en una subcarpeta aleatoria anti-colisiones");
        File.Exists(emittedItem.CurrentPath).Should().BeTrue();

        // Validar metadatos de tamaño emitidos
        emittedItem.Metadata.Should().ContainKey("OriginalFileSize");
        emittedItem.Metadata.Should().ContainKey("OutputFileSize");
        emittedItem.Metadata.Should().ContainKey("SavedBytes");
        emittedItem.Metadata.Should().ContainKey("CompressionRatio");

        long origSize = Convert.ToInt64(emittedItem.Metadata["OriginalFileSize"]);
        long outputSize = Convert.ToInt64(emittedItem.Metadata["OutputFileSize"]);
        origSize.Should().Be(item.FileSizeBytes);
        outputSize.Should().Be(emittedItem.FileSizeBytes);
    }

    [Fact]
    public async Task ImageOptimizerNode_CollisionAvoidance_TwoRunsProduceDifferentPaths()
    {
        // Arrange
        string inputImage = Path.Combine(_testTempDir, "photo.png");
        using (var img = new Image<Rgba32>(30, 30))
        {
            await img.SaveAsPngAsync(inputImage);
        }

        var node1 = new ImageOptimizerNode();
        var node2 = new ImageOptimizerNode();

        var item1 = new FileItemContext(inputImage) { OriginalPath = inputImage, FileSizeBytes = new FileInfo(inputImage).Length };
        var item2 = new FileItemContext(inputImage) { OriginalPath = inputImage, FileSizeBytes = new FileInfo(inputImage).Length };

        string? outPath1 = null;
        string? outPath2 = null;

        var mockContext1 = new Mock<IFlowExecutionContext>();
        mockContext1.Setup(c => c.TemporaryDirectory).Returns(_testTempDir);
        mockContext1.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, itm) => outPath1 = itm.CurrentPath)
            .Returns(Task.CompletedTask);

        var mockContext2 = new Mock<IFlowExecutionContext>();
        mockContext2.Setup(c => c.TemporaryDirectory).Returns(_testTempDir);
        mockContext2.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((_, itm) => outPath2 = itm.CurrentPath)
            .Returns(Task.CompletedTask);

        // Act
        await node1.ExecuteAsync("In", item1, mockContext1.Object, CancellationToken.None);
        await node2.ExecuteAsync("In", item2, mockContext2.Object, CancellationToken.None);

        // Assert
        outPath1.Should().NotBeNullOrWhiteSpace();
        outPath2.Should().NotBeNullOrWhiteSpace();
        outPath1.Should().NotBe(outPath2, "dos ejecuciones sucesivas o paralelas no deben sobreescribir ni colisionar el archivo");
        Path.GetDirectoryName(outPath1).Should().NotBe(Path.GetDirectoryName(outPath2));
    }

    [Theory]
    [InlineData(2000, 1000, ">", true)]   // Output mayor que original -> True
    [InlineData(800, 1000, ">", false)]   // Output menor que original -> False
    [InlineData(800, 1000, "<", true)]    // Output menor que original -> True
    public async Task ExpressionFilterNode_ShouldCompareOutputFileSizeWithOriginalFileSize(
        long outputSize,
        long originalSize,
        string op,
        bool expectTrue)
    {
        // Arrange
        var filterNode = new ExpressionFilterNode();
        filterNode.Parameters["Property"] = "{OutputFileSize}";
        filterNode.Parameters["Operator"] = op;
        filterNode.Parameters["ComparisonValue"] = "{OriginalFileSize}";

        var item = new FileItemContext(Path.Combine(_testTempDir, "image_opt.webp"));
        item.Metadata["OutputFileSize"] = outputSize;
        item.Metadata["OriginalFileSize"] = originalSize;

        string? emittedPort = null;
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        // Act
        await filterNode.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        string expectedPort = expectTrue ? "True" : "False";
        emittedPort.Should().Be(expectedPort);
    }
}
