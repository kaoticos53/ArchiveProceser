using FileFlow.App.Collections;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

public class FastObservableRingBufferTests
{
    [Fact]
    public void RingBuffer_ShouldStoreAndRetrieveItemsInFifoOrder()
    {
        var buffer = new FastObservableRingBuffer<string>(3);
        buffer.Add("A");
        buffer.Add("B");
        buffer.Add("C");

        buffer.Count.Should().Be(3);
        buffer[0].Should().Be("A");
        buffer[1].Should().Be("B");
        buffer[2].Should().Be("C");
    }

    [Fact]
    public void RingBuffer_WhenCapacityExceeded_ShouldOverwriteOldestElements()
    {
        var buffer = new FastObservableRingBuffer<string>(3);
        buffer.Add("1");
        buffer.Add("2");
        buffer.Add("3");
        buffer.Add("4"); // Overwrites "1"
        buffer.Add("5"); // Overwrites "2"

        buffer.Count.Should().Be(3);
        buffer[0].Should().Be("3");
        buffer[1].Should().Be("4");
        buffer[2].Should().Be("5");
    }

    [Fact]
    public void RingBuffer_AddRange_ShouldAddMultipleElementsInBatch()
    {
        var buffer = new FastObservableRingBuffer<int>(5);
        buffer.AddRange([10, 20, 30, 40, 50, 60]); // 6 items into capacity 5 -> [20, 30, 40, 50, 60]

        buffer.Count.Should().Be(5);
        buffer[0].Should().Be(20);
        buffer[4].Should().Be(60);
    }

    [Fact]
    public void RingBuffer_Clear_ShouldResetCountAndBuffer()
    {
        var buffer = new FastObservableRingBuffer<string>(4);
        buffer.AddRange(["A", "B", "C"]);
        buffer.Count.Should().Be(3);

        buffer.Clear();
        buffer.Count.Should().Be(0);
        buffer.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void RingBuffer_ResizingCapacity_ShouldPreserveMostRecentElements()
    {
        var buffer = new FastObservableRingBuffer<int>(5);
        buffer.AddRange([1, 2, 3, 4, 5]);

        buffer.Capacity = 3;
        buffer.Count.Should().Be(3);
        buffer[0].Should().Be(3);
        buffer[1].Should().Be(4);
        buffer[2].Should().Be(5);
    }

    [Fact]
    public void RingBuffer_Notification_ShouldFireCollectionChangedReset()
    {
        var buffer = new FastObservableRingBuffer<string>(10);
        bool resetFired = false;
        buffer.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                resetFired = true;
            }
        };

        buffer.Add("Item");
        resetFired.Should().BeTrue();
    }
}
