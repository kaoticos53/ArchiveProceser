using System.IO;
using FileFlow.Plugin.Data;
using FileFlow.Sdk;
using FluentAssertions;
using MiniExcelLibs;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins.Data;

public class DataLookupTests : IDisposable
{
    private readonly string _tempDir;

    public DataLookupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_DataLookup_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task DataLookupNode_MatchedKeyInExcel_EnrichesItemAndEmitsToMatched()
    {
        // Arrange: Crear tabla de clientes en Excel
        string xlsxPath = Path.Combine(_tempDir, "clientes.xlsx");
        var clients = new[]
        {
            new { Codigo = "101", RazonSocial = "Acme S.A.", Email = "contacto@acme.com", Zona = "Norte" },
            new { Codigo = "102", RazonSocial = "Globex Corp", Email = "ventas@globex.com", Zona = "Sur" }
        };
        await MiniExcel.SaveAsAsync(xlsxPath, clients);

        var node = new DataLookupNode();
        node.Parameters["DataSourcePath"] = xlsxPath;
        node.Parameters["LookupKeyColumn"] = "Codigo";
        node.Parameters["MatchExpression"] = "{FileNameWithoutExtension}";
        node.Parameters["PrefixColumns"] = "Cliente_";

        var matchedItems = new List<FileItemContext>();
        var unmatchedItems = new List<FileItemContext>();

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) =>
            {
                if (port == "Matched") matchedItems.Add(item);
                else if (port == "Unmatched") unmatchedItems.Add(item);
            })
            .Returns(Task.CompletedTask);

        // Archivo que coincide con el código 101
        var item101 = new FileItemContext(Path.Combine(_tempDir, "101.pdf"));

        // Act
        await node.ExecuteAsync("In", item101, mockContext.Object, CancellationToken.None);

        // Assert
        matchedItems.Should().HaveCount(1);
        unmatchedItems.Should().BeEmpty();

        var enriched = matchedItems[0];
        enriched.Metadata["Cliente_RazonSocial"].Should().Be("Acme S.A.");
        enriched.Metadata["Cliente_Email"].Should().Be("contacto@acme.com");
        enriched.Metadata["Cliente_Zona"].Should().Be("Norte");
        enriched.Metadata["LookupMatched"].Should().Be(true);
    }

    [Fact]
    public async Task DataLookupNode_UnmatchedKey_EmitsToUnmatched()
    {
        // Arrange: Crear tabla en CSV
        string csvPath = Path.Combine(_tempDir, "lookup.csv");
        await File.WriteAllTextAsync(csvPath, "Id,Valor\r\nAAA,100\r\nBBB,200");

        var node = new DataLookupNode();
        node.Parameters["DataSourcePath"] = csvPath;
        node.Parameters["LookupKeyColumn"] = "Id";
        node.Parameters["MatchExpression"] = "{FileNameWithoutExtension}";

        var matchedItems = new List<FileItemContext>();
        var unmatchedItems = new List<FileItemContext>();

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) =>
            {
                if (port == "Matched") matchedItems.Add(item);
                else if (port == "Unmatched") unmatchedItems.Add(item);
            })
            .Returns(Task.CompletedTask);

        // Archivo con clave inexistente CCC
        var itemCCC = new FileItemContext(Path.Combine(_tempDir, "CCC.txt"));

        // Act
        await node.ExecuteAsync("In", itemCCC, mockContext.Object, CancellationToken.None);

        // Assert
        matchedItems.Should().BeEmpty();
        unmatchedItems.Should().HaveCount(1);
        unmatchedItems[0].Metadata["LookupMatched"].Should().Be(false);
    }

    public void Dispose()
    {
        try
        {
            DataLookupTableLoader.ClearCache();
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch { }
    }
}
