using FileFlow.App.Collections;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using FileFlow.Tests.TestHelpers;
using Xunit;

namespace FileFlow.Tests.Unit;

public class AsyncVirtualizingListTests : IAsyncLifetime
{
    private SqliteLogStore _store = null!;

    public Task InitializeAsync()
    {
        _store = new SqliteLogStore($"TestVirtualList_{Guid.NewGuid():N}");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
    }

    [Fact]
    public async Task AsyncVirtualizingList_Count_Reflects_Database()
    {
        var list = new AsyncVirtualizingList(_store);
        Assert.Empty(list);

        for (int i = 0; i < 250; i++)
        {
            _store.EnqueueLog(StructuredLogRecord.Create("test-exec", LogLevel.Information, $"Log message {i}", "NodeA", "Node A", $"/tmp/file_{i}.txt", 10.0 + i));
        }
        await _store.FlushPendingLogsAsync();

        await list.RefreshAsync();
        Assert.Equal(250, list.Count);
    }

    [Fact]
    public async Task AsyncVirtualizingList_Index_Access_Fetches_Data()
    {
        var list = new AsyncVirtualizingList(_store);
        for (int i = 0; i < 150; i++)
        {
            _store.EnqueueLog(StructuredLogRecord.Create("test-exec", LogLevel.Information, $"Log message {i:D3}", "NodeA", "Node A", $"/tmp/file_{i}.txt", 10.0 + i));
        }
        await _store.FlushPendingLogsAsync();
        await list.RefreshAsync();

        // Primer acceso solicita página en segundo plano
        var itemImmediate = list[50];
        Assert.NotNull(itemImmediate);

        await AsyncTestWaiter.WaitForAsync(
            () => list[50]?.Message?.Contains("Log message 050", StringComparison.Ordinal) == true,
            TimeSpan.FromSeconds(2),
            description: "the virtualized log item at index 50 to finish loading");

        var loadedItem = list[50];
        Assert.NotNull(loadedItem);
        Assert.Equal("test-exec", loadedItem.ExecutionId);
        Assert.Contains("Log message 050", loadedItem.Message);
    }

    [Fact]
    public async Task AsyncVirtualizingList_Sorting_DurationMs_Works_Correctly()
    {
        var list = new AsyncVirtualizingList(_store);
        _store.EnqueueLog(StructuredLogRecord.Create("test-exec", LogLevel.Information, "Fast Op", durationMs: 5.0));
        _store.EnqueueLog(StructuredLogRecord.Create("test-exec", LogLevel.Information, "Slow Op", durationMs: 500.0));
        _store.EnqueueLog(StructuredLogRecord.Create("test-exec", LogLevel.Information, "Medium Op", durationMs: 50.0));
        await _store.FlushPendingLogsAsync();

        var filter = new LogFilterCriteria(SortColumn: "DurationMs", IsAscending: false);
        await list.RefreshAsync(filter);
        Assert.Equal(3, list.Count);

        // Disparar carga
        _ = list[0];
        await AsyncTestWaiter.WaitForAsync(
            () => list[0]?.Message == "Slow Op" && list[0]?.DurationMs == 500.0,
            TimeSpan.FromSeconds(2),
            description: "the sorted slowest log item to finish loading");

        var slowest = list[0];
        Assert.Equal("Slow Op", slowest.Message);
        Assert.Equal(500.0, slowest.DurationMs);
    }

    [Fact]
    public void AsyncVirtualizingList_UpdateCount_Expands_Smoothly()
    {
        var list = new AsyncVirtualizingList(_store);
        Assert.Empty(list);

        list.UpdateCount(5000);
        Assert.Equal(5000, list.Count);

        list.UpdateCount(12000);
        Assert.Equal(12000, list.Count);
    }
}
