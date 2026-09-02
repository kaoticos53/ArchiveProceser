using System.IO;
using System.Text.Json;
using FileFlow.Plugin.Data;
using FileFlow.Sdk;
using FluentAssertions;
using MiniExcelLibs;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins.Data;

public class DataExportersTests : IDisposable
{
    private readonly string _tempDir;

    public DataExportersTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_DataExporters_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task ExcelReportGeneratorNode_CollectsItemsAndEmitsXlsxOnCompletion()
    {
        // Arrange
        var node = new ExcelReportGeneratorNode();
        node.Parameters["OutputDirectory"] = _tempDir;
        node.Parameters["ReportFileName"] = "Resumen_{Date}.xlsx";
        node.Parameters["ColumnsToExport"] = "FileName, FileSizeBytes, Status";

        var outItems = new List<FileItemContext>();
        var reportItems = new List<FileItemContext>();

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) =>
            {
                if (port == "Out") outItems.Add(item);
                else if (port == "Report") reportItems.Add(item);
            })
            .Returns(Task.CompletedTask);

        var item1 = new FileItemContext(Path.Combine(_tempDir, "foto1.jpg")) { FileSizeBytes = 1024 };
        item1.Metadata["Status"] = "Optimized";
        item1.Metadata["GlobalOutputDir"] = _tempDir;

        var item2 = new FileItemContext(Path.Combine(_tempDir, "doc1.pdf")) { FileSizeBytes = 2048 };
        item2.Metadata["Status"] = "Converted";
        item2.Metadata["GlobalOutputDir"] = _tempDir;

        // Act: Enviar 2 archivos por el pipeline
        await node.ExecuteAsync("In", item1, mockContext.Object, CancellationToken.None);
        await node.ExecuteAsync("In", item2, mockContext.Object, CancellationToken.None);

        // Completar el flujo
        await node.OnWorkflowCompletedAsync(mockContext.Object, CancellationToken.None);

        // Assert
        outItems.Should().HaveCount(2);
        reportItems.Should().HaveCount(1);

        string reportPath = reportItems[0].CurrentPath;
        File.Exists(reportPath).Should().BeTrue();

        // Leer el reporte generado con MiniExcel
        await using var stream = File.OpenRead(reportPath);
        var rows = (await stream.QueryAsync(useHeaderRow: true)).ToList();
        rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task CsvExportNode_WritesAndAppendsRowsCorrectly()
    {
        // Arrange
        string csvDest = Path.Combine(_tempDir, "registros.csv");
        var node = new CsvExportNode();
        node.Parameters["DestinationPath"] = csvDest;
        node.Parameters["Delimiter"] = ";";
        node.Parameters["Columns"] = "FileName, FileSizeBytes, Estado";
        node.Parameters["AppendMode"] = true;

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        var itemA = new FileItemContext(Path.Combine(_tempDir, "archivoA.zip")) { FileSizeBytes = 500 };
        itemA.Metadata["Estado"] = "Comprimido";

        var itemB = new FileItemContext(Path.Combine(_tempDir, "archivoB.zip")) { FileSizeBytes = 1200 };
        itemB.Metadata["Estado"] = "Verificado";

        // Act
        await node.ExecuteAsync("In", itemA, mockContext.Object, CancellationToken.None);
        await node.ExecuteAsync("In", itemB, mockContext.Object, CancellationToken.None);

        // Assert
        File.Exists(csvDest).Should().BeTrue();
        string[] lines = await File.ReadAllLinesAsync(csvDest);
        lines.Should().HaveCount(3); // Cabecera + 2 filas
        lines[0].Should().Be("FileName;FileSizeBytes;Estado");
        lines[1].Should().Contain("archivoA.zip;500;Comprimido");
        lines[2].Should().Contain("archivoB.zip;1200;Verificado");
    }

    [Fact]
    public async Task DataFormatConverterNode_CsvToJson_ConvertsCorrectly()
    {
        // Arrange: Crear archivo CSV
        string csvSource = Path.Combine(_tempDir, "datos.csv");
        await File.WriteAllTextAsync(csvSource, "Id,Producto,Precio\r\n1,Teclado,45.99\r\n2,Raton,19.50");

        var node = new DataFormatConverterNode();
        node.Parameters["TargetFormat"] = "JSON";
        node.Parameters["OutputDirectory"] = _tempDir;

        FileItemContext? converted = null;
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) => converted = item)
            .Returns(Task.CompletedTask);

        var inputItem = new FileItemContext(csvSource);

        // Act
        await node.ExecuteAsync("In", inputItem, mockContext.Object, CancellationToken.None);

        // Assert
        converted.Should().NotBeNull();
        File.Exists(converted!.CurrentPath).Should().BeTrue();
        converted.CurrentPath.Should().EndWith(".json");

        string jsonContent = await File.ReadAllTextAsync(converted.CurrentPath);
        using var doc = JsonDocument.Parse(jsonContent);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(2);
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
