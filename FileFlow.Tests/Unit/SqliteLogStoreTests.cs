using FileFlow.Core.Telemetry;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

public class SqliteLogStoreTests : IAsyncLifetime
{
    private SqliteLogStore _store = null!;

    public Task InitializeAsync()
    {
        _store = new SqliteLogStore($"TestDb_{Guid.NewGuid():N}");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
    }

    [Fact]
    public async Task SqliteLogStore_BatchIngestion_ShouldStoreAndCountAllRecords()
    {
        const int totalRecords = 5000;
        var records = new List<StructuredLogRecord>(totalRecords);

        for (int i = 0; i < totalRecords; i++)
        {
            records.Add(StructuredLogRecord.Create(
                executionId: "exec-123",
                level: (i % 10 == 0) ? LogLevel.Error : LogLevel.Information,
                message: $"Processing item #{i}",
                nodeId: $"node-{i % 5}",
                nodeName: $"Node Name #{i % 5}",
                filePath: $@"D:\Data\file_{i}.txt",
                durationMs: i * 0.5
            ));
        }

        _store.EnqueueLogs(records);
        await _store.FlushPendingLogsAsync();

        int totalCount = await _store.GetTotalCountAsync();
        totalCount.Should().Be(totalRecords);

        int errorCount = await _store.GetTotalCountAsync(new LogFilterCriteria(MinLevel: LogLevel.Error));
        errorCount.Should().Be(totalRecords / 10);
    }

    [Fact]
    public async Task SqliteLogStore_GetLogsWindow_ShouldReturnAccuratePages()
    {
        for (int i = 0; i < 250; i++)
        {
            _store.EnqueueLog(StructuredLogRecord.Create("exec-1", LogLevel.Information, $"Message #{i:D3}"));
        }
        await _store.FlushPendingLogsAsync();

        var page1 = await _store.GetLogsWindowAsync(0, 50, newestFirst: false);
        page1.Should().HaveCount(50);
        page1[0].Message.Should().Be("Message #000");
        page1[49].Message.Should().Be("Message #049");

        var page2 = await _store.GetLogsWindowAsync(50, 50, newestFirst: false);
        page2.Should().HaveCount(50);
        page2[0].Message.Should().Be("Message #050");
        page2[49].Message.Should().Be("Message #099");
    }

    [Fact]
    public async Task SqliteLogStore_GetFileTrace_ShouldReturnAllOperationsForSpecificFile()
    {
        _store.EnqueueLog(StructuredLogRecord.Create("exec-1", LogLevel.Information, "Scanned", nodeId: "FolderSource", filePath: @"C:\in\test.jpg"));
        _store.EnqueueLog(StructuredLogRecord.Create("exec-1", LogLevel.Information, "Resized", nodeId: "ImageResize", filePath: @"C:\in\test.jpg", durationMs: 15.2));
        _store.EnqueueLog(StructuredLogRecord.Create("exec-1", LogLevel.Information, "Other file scanned", nodeId: "FolderSource", filePath: @"C:\in\other.jpg"));
        _store.EnqueueLog(StructuredLogRecord.Create("exec-1", LogLevel.Information, "Saved", nodeId: "FolderSink", filePath: @"C:\out\test.jpg"));
        await _store.FlushPendingLogsAsync();

        var trace = await _store.GetFileTraceAsync("test.jpg");
        trace.Should().HaveCount(3);
        trace.Select(t => t.NodeId).Should().ContainInOrder("FolderSource", "ImageResize", "FolderSink");
    }

    [Fact]
    public async Task SqliteLogStore_GetNodeExecutionMetrics_ShouldAggregatePerformanceAccurately()
    {
        _store.EnqueueLog(StructuredLogRecord.Create("exec-1", LogLevel.Information, "Op 1", nodeId: "NodeA", durationMs: 10.0));
        _store.EnqueueLog(StructuredLogRecord.Create("exec-1", LogLevel.Information, "Op 2", nodeId: "NodeA", durationMs: 20.0));
        _store.EnqueueLog(StructuredLogRecord.Create("exec-1", LogLevel.Error, "Op 3 Failed", nodeId: "NodeA", durationMs: 30.0));
        _store.EnqueueLog(StructuredLogRecord.Create("exec-1", LogLevel.Information, "Op B", nodeId: "NodeB", durationMs: 50.0));
        await _store.FlushPendingLogsAsync();

        var metrics = await _store.GetNodeExecutionMetricsAsync("exec-1");
        metrics.Should().HaveCount(2);

        var nodeAMetrics = metrics.First(m => m.NodeId == "NodeA");
        nodeAMetrics.TotalExecutions.Should().Be(3);
        nodeAMetrics.AvgDurationMs.Should().BeApproximately(20.0, 0.01);
        nodeAMetrics.MaxDurationMs.Should().Be(30.0);
        nodeAMetrics.MinDurationMs.Should().Be(10.0);
        nodeAMetrics.ErrorCount.Should().Be(1);
    }
}
