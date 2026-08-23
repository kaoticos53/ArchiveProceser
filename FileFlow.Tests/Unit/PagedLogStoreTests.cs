using FileFlow.App.Collections;
using FileFlow.App.Models;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

public class PagedLogStoreTests
{
    [Fact]
    public void PagedLogStore_AddAndIndex_ShouldPreserveExactOrderAcrossChunkBoundaries()
    {
        var store = new PagedLogStore<int>();
        const int totalItems = 10000;

        for (int i = 0; i < totalItems; i++)
        {
            store.Add(i);
        }

        store.Count.Should().Be(totalItems);
        for (int i = 0; i < totalItems; i++)
        {
            store[i].Should().Be(i);
        }

        // Test boundary index
        store[2047].Should().Be(2047);
        store[2048].Should().Be(2048);
        store[2049].Should().Be(2049);
        store[4095].Should().Be(4095);
        store[4096].Should().Be(4096);
    }

    [Fact]
    public void PagedLogStore_AddRange_ShouldCorrectlyPopulateStore()
    {
        var store = new PagedLogStore<string>();
        var items = Enumerable.Range(0, 5000).Select(i => $"Log entry #{i}").ToList();

        store.AddRange(items);

        store.Count.Should().Be(5000);
        store[0].Should().Be("Log entry #0");
        store[4999].Should().Be("Log entry #4999");
    }

    [Fact]
    public void PagedLogStore_Clear_ShouldResetCountAndChunks()
    {
        var store = new PagedLogStore<LogEntry>();
        for (int i = 0; i < 3000; i++)
        {
            store.Add(new LogEntry(DateTime.Now, LogLevel.Information, $"Message {i}"));
        }

        store.Count.Should().Be(3000);
        store.Clear();
        store.Count.Should().Be(0);
    }

    [Fact]
    public void PagedLogStore_ConcurrentAdd_ShouldBeThreadSafe()
    {
        var store = new PagedLogStore<int>();
        const int threads = 8;
        const int itemsPerThread = 2000;

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < itemsPerThread; i++)
            {
                store.Add(i);
            }
        });

        store.Count.Should().Be(threads * itemsPerThread);
    }
}
