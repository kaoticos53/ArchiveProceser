using System.IO;
using FileFlow.Plugin.Data;
using FileFlow.Sdk;
using FluentAssertions;
using MiniExcelLibs;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins.Data;

public class DataReadersTests : IDisposable
{
    private readonly string _tempDir;

    public DataReadersTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_DataReaders_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task CsvReaderNode_ValidCsv_EmitsAllRowsWithMetadata()
    {
        // Arrange
        string csvPath = Path.Combine(_tempDir, "clientes.csv");
        string csvContent = "Id,Nombre,Ciudad,Saldo\r\n101,Juan Pérez,Madrid,1500.50\r\n102,María Gómez,Barcelona,2300.00\r\n103,Carlos Díaz,Valencia,850.25";
        await File.WriteAllTextAsync(csvPath, csvContent);

        var node = new CsvReaderNode();
        node.Parameters["FilePath"] = csvPath;
        node.Parameters["Delimiter"] = "Auto";
        node.Parameters["HasHeader"] = true;

        var emittedItems = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) => emittedItems.Add(item))
            .Returns(Task.CompletedTask);

        var inputItem = new FileItemContext(csvPath);

        // Act
        await node.ExecuteAsync("In", inputItem, mockContext.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(3);
        emittedItems[0].Metadata["Id"].Should().Be("101");
        emittedItems[0].Metadata["Nombre"].Should().Be("Juan Pérez");
        emittedItems[0].Metadata["Ciudad"].Should().Be("Madrid");
        emittedItems[0].Metadata["Saldo"].Should().Be("1500.50");

        emittedItems[1].Metadata["Id"].Should().Be("102");
        emittedItems[1].Metadata["Nombre"].Should().Be("María Gómez");

        emittedItems[2].Metadata["Id"].Should().Be("103");
        emittedItems[2].Metadata["Ciudad"].Should().Be("Valencia");
    }

    [Fact]
    public async Task ExcelReaderNode_ValidXlsx_EmitsRowsWithMetadata()
    {
        // Arrange
        string xlsxPath = Path.Combine(_tempDir, "pedidos.xlsx");
        var sampleData = new[]
        {
            new { PedidoId = "PED-001", Cliente = "Acme Corp", Total = 1200 },
            new { PedidoId = "PED-002", Cliente = "Beta LLC", Total = 4500 }
        };
        await MiniExcel.SaveAsAsync(xlsxPath, sampleData);

        var node = new ExcelReaderNode();
        node.Parameters["FilePath"] = xlsxPath;

        var emittedItems = new List<FileItemContext>();
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) => emittedItems.Add(item))
            .Returns(Task.CompletedTask);

        var inputItem = new FileItemContext(xlsxPath);

        // Act
        await node.ExecuteAsync("In", inputItem, mockContext.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(2);
        emittedItems[0].Metadata["PedidoId"].Should().Be("PED-001");
        emittedItems[0].Metadata["Cliente"].Should().Be("Acme Corp");
        emittedItems[1].Metadata["PedidoId"].Should().Be("PED-002");
        emittedItems[1].Metadata["Cliente"].Should().Be("Beta LLC");
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
