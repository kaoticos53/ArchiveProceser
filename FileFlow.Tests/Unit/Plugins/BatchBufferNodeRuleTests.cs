using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class BatchBufferNodeRuleTests
{
    [Fact]
    public async Task ExecuteAsync_WhenBatchSizeReached_ShouldEmitBufferedItemsAndCompletionMarker()
    {
        // Arrange (AAA)
        var node = new BatchBufferNode();
        node.Parameters["BatchSize"] = 3;

        var contextMock = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<(string Port, FileItemContext Item)>();

        contextMock
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) => emittedItems.Add((port, item)))
            .Returns(Task.CompletedTask);

        var item1 = new FileItemContext("C:\\file1.txt");
        var item2 = new FileItemContext("C:\\file2.txt");
        var item3 = new FileItemContext("C:\\file3.txt");

        // Act
        await node.ExecuteAsync("ItemIn", item1, contextMock.Object, CancellationToken.None);
        emittedItems.Should().BeEmpty(); // Buffer not full yet

        await node.ExecuteAsync("ItemIn", item2, contextMock.Object, CancellationToken.None);
        emittedItems.Should().BeEmpty(); // Buffer not full yet

        await node.ExecuteAsync("ItemIn", item3, contextMock.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(4); // 3 ItemOut + 1 BatchCompleted marker
        emittedItems.Where(e => e.Port == "ItemOut").Should().HaveCount(3);
        emittedItems.Last().Port.Should().Be("BatchCompleted");
        emittedItems.Last().Item.Metadata["BatchSize"].Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenForceFlushPortReceivesInput_ShouldEmitPartialBufferImmediately()
    {
        // Arrange
        var node = new BatchBufferNode();
        node.Parameters["BatchSize"] = 10; // Large threshold

        var contextMock = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<(string Port, FileItemContext Item)>();

        contextMock
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) => emittedItems.Add((port, item)))
            .Returns(Task.CompletedTask);

        var item1 = new FileItemContext("C:\\file1.txt");
        var flushItem = new FileItemContext(string.Empty);

        // Act
        await node.ExecuteAsync("ItemIn", item1, contextMock.Object, CancellationToken.None);
        emittedItems.Should().BeEmpty();

        await node.ExecuteAsync("ForceFlush", flushItem, contextMock.Object, CancellationToken.None);

        // Assert
        emittedItems.Should().HaveCount(2); // 1 ItemOut + 1 BatchCompleted marker
        emittedItems[0].Item.CurrentPath.Should().Be("C:\\file1.txt");
        emittedItems[1].Port.Should().Be("BatchCompleted");
    }

    [Fact]
    public async Task ExecuteAsync_WhenForceFlushPortReceivesInputOnEmptyBuffer_ShouldNotEmitAnything()
    {
        // Arrange
        var node = new BatchBufferNode();
        var contextMock = new Mock<IFlowExecutionContext>();
        var flushItem = new FileItemContext(string.Empty);

        // Act
        await node.ExecuteAsync("ForceFlush", flushItem, contextMock.Object, CancellationToken.None);

        // Assert
        contextMock.Verify(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()), Times.Never);
    }
}
