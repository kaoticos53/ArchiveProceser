using System.IO;
using FileFlow.Plugin.Data;
using FileFlow.Sdk;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins.Data;

public class SqliteSinkTests : IDisposable
{
    private readonly string _tempDir;

    public SqliteSinkTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FileFlow_SqliteSink_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task SqliteDatabaseSinkNode_InsertsRecordsAndCreatesSchemaAutomatically()
    {
        // Arrange
        string dbPath = Path.Combine(_tempDir, "audit.db");
        var node = new SqliteDatabaseSinkNode();
        node.Parameters["DatabasePath"] = dbPath;
        node.Parameters["TableName"] = "AuditTrail";
        node.Parameters["AutoCreateTable"] = true;
        node.Parameters["StoreMetadataAsJson"] = true;

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        var item = new FileItemContext(Path.Combine(_tempDir, "documento_importante.pdf"))
        {
            FileSizeBytes = 4096
        };
        item.Metadata["HashSHA256"] = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        item.Metadata["Status"] = "Encrypted";
        item.Metadata["Author"] = "Departamento Legal";

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        File.Exists(dbPath).Should().BeTrue();

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();

        using var cmd = new SqliteCommand("SELECT FileName, FileSizeBytes, HashSHA256, Status, MetadataJson FROM AuditTrail WHERE FileName = @name", conn);
        cmd.Parameters.AddWithValue("@name", "documento_importante.pdf");

        using var reader = await cmd.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();

        reader.GetString(0).Should().Be("documento_importante.pdf");
        reader.GetInt64(1).Should().Be(4096);
        reader.GetString(2).Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        reader.GetString(3).Should().Be("Encrypted");
        reader.GetString(4).Should().Contain("Departamento Legal");
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
