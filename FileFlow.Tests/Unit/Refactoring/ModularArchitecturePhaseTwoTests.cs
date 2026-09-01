using System.Globalization;
using System.Windows;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Telemetry;
using FileFlow.Plugin.Archives.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FileFlow.Sdk.TemplateEngine.Resolvers;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FileFlow.Tests.Unit.Refactoring;

public class ModularArchitecturePhaseTwoTests
{
    [Fact]
    public void PathRelativeCalculator_ComputesCorrectRelativePaths()
    {
        string root = @"C:\Source\Files";
        string file = @"C:\Source\Files\SubFolder\document.txt";

        string relDir = PathRelativeCalculator.CalculateRelativeDirectory(file, root);
        string relFile = PathRelativeCalculator.CalculateRelativeFilePath(file, root);

        Assert.Equal("SubFolder", relDir);
        Assert.Equal(@"SubFolder\document.txt", relFile);
    }

    [Fact]
    public void DomainVariableResolver_ResolvesEnvAndDateAndSize()
    {
        var item = new FileItemContext(@"C:\test\sample.dat")
        {
            FileSizeBytes = 1048576 * 5 // 5 MB
        };

        bool envResolved = DomainVariableResolver.TryResolve("env", "PATH", null, item, item.CurrentPath, out string envVal);
        Assert.True(envResolved);
        Assert.False(string.IsNullOrEmpty(envVal));

        bool sizeResolved = DomainVariableResolver.TryResolve("size", "mb", null, item, item.CurrentPath, out string sizeVal);
        Assert.True(sizeResolved);
        Assert.Equal("5.00", sizeVal);

        bool dateResolved = DomainVariableResolver.TryResolve("now", "yyyy", null, item, item.CurrentPath, out string dateVal);
        Assert.True(dateResolved);
        Assert.Equal(DateTime.Now.Year.ToString(CultureInfo.InvariantCulture), dateVal);
    }

    [Fact]
    public void NodeCategoryStyling_ReturnsDistinctAccentsForCategories()
    {
        var (fsH, fsA) = NodeCategoryStyling.GetColorsForCategory("filesystem");
        var (arH, arA) = NodeCategoryStyling.GetColorsForCategory("archives");
        var (imH, imA) = NodeCategoryStyling.GetColorsForCategory("images");

        Assert.Equal("#10B981", fsA);
        Assert.Equal("#F59E0B", arA);
        Assert.Equal("#A855F7", imA);
        Assert.NotEqual(fsH, arH);
    }

    [Fact]
    public void EditorViewportCalculator_CalculatesGeometryGracefully()
    {
        var (emptyZoom, emptyLoc) = EditorViewportCalculator.CalculateFitToScreen([]);
        Assert.Equal(1.0, emptyZoom);
        Assert.Equal(new Point(0, 0), emptyLoc);
    }

    [Fact]
    public async Task SqliteLogSchemaAndMetricsReader_WorkEndToEnd()
    {
        using var conn = new SqliteConnection("Data Source=InMemoryPhaseTwoTest;Mode=Memory;Cache=Shared");
        conn.Open();

        var lockObj = new Lock();
        SqliteLogSchema.Initialize(conn, lockObj);

        // Insert sample record
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO ExecutionLogs (ExecutionId, Timestamp, Level, NodeId, NodeName, DurationMs, Message)
                VALUES ('exec-1', 1700000000000, 1, 'node-1', 'Renamer', 42.5, 'Success');
            """;
            cmd.ExecuteNonQuery();
        }

        var metrics = await SqliteLogMetricsReader.GetNodeExecutionMetricsAsync(
            "Data Source=InMemoryPhaseTwoTest;Mode=Memory;Cache=Shared", 
            "exec-1");

        Assert.Single(metrics);
        Assert.Equal("node-1", metrics[0].NodeId);
        Assert.Equal("Renamer", metrics[0].NodeName);
        Assert.Equal(1, metrics[0].TotalExecutions);
        Assert.Equal(42.5, metrics[0].AvgDurationMs);
    }

    [Fact]
    public void SafeArchiveExtractor_PasswordCandidates_MergesProperly()
    {
        var item = new FileItemContext(@"C:\test\sample.zip");
        item.Metadata["Secret"] = "MyPass123";

        var candidates = SafeArchiveExtractor.GetPasswordCandidates("{Secret};admin", "", item);
        Assert.Contains("MyPass123", candidates);
        Assert.Contains("admin", candidates);
    }
}
