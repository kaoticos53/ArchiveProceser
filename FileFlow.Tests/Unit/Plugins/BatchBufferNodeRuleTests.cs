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

    [Fact]
    public async Task OnWorkflowCompleted_WhenTheBatchDidNotFill_ShouldDeliverItAndCloseItWithItsMarker()
    {
        // Arrange: un lote de tres con dos elementos dentro. El que no se llena es el caso normal —seis archivos
        // con un lote de diez—, así que si el pendiente se quedara ahí el flujo terminaría en verde sin entregar
        // nada, que es lo que hacía antes de este gancho.
        var node = new BatchBufferNode();
        node.Parameters["BatchSize"] = 3;

        var contextMock = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<(string Port, FileItemContext Item)>();

        contextMock
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) => emittedItems.Add((port, item)))
            .Returns(Task.CompletedTask);

        await node.ExecuteAsync("ItemIn", new FileItemContext("C:\\lote1.txt"), contextMock.Object, CancellationToken.None);
        await node.ExecuteAsync("ItemIn", new FileItemContext("C:\\lote2.txt"), contextMock.Object, CancellationToken.None);
        emittedItems.Should().BeEmpty("el umbral no se alcanzó: el lote sigue dentro del nodo");

        // Act: termina la ejecución sin que el lote llegue a llenarse.
        await node.OnWorkflowCompletedAsync(contextMock.Object, CancellationToken.None);

        // Assert: sale el lote entero —los dos elementos y su marcador—, con la misma forma que un lote completo.
        emittedItems.Select(e => e.Port).Should().Equal("ItemOut", "ItemOut", "BatchCompleted");
        emittedItems.Where(e => e.Port == "ItemOut").Select(e => e.Item.CurrentPath)
            .Should().Equal("C:\\lote1.txt", "C:\\lote2.txt");
        emittedItems.Where(e => e.Port == "ItemOut").Select(e => e.Item.Metadata["BatchSize"])
            .Should().AllBeEquivalentTo(2);

        var marker = emittedItems.Last().Item;
        marker.Metadata["BatchSize"].Should().Be(2);
        marker.Metadata["BatchIncomplete"].Should().Be(true,
            "el marcador tiene que distinguir un lote cerrado por umbral de uno cerrado por fin de ejecución");

        // Y el búfer queda vacío: lo entregado no vuelve a salir en un segundo cierre ni llega a la ejecución
        // siguiente.
        emittedItems.Clear();
        await node.OnWorkflowCompletedAsync(contextMock.Object, CancellationToken.None);
        emittedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task OnWorkflowCompleted_WhenNothingIsPending_ShouldNotEmitAnything()
    {
        // El caso simétrico: el único elemento llenó su lote (tamaño uno), así que al terminar la ejecución no
        // queda nada y no puede salir un lote vacío con su marcador.
        var node = new BatchBufferNode();
        node.Parameters["BatchSize"] = 1;

        var contextMock = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<(string Port, FileItemContext Item)>();

        contextMock
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) => emittedItems.Add((port, item)))
            .Returns(Task.CompletedTask);

        await node.ExecuteAsync("ItemIn", new FileItemContext("C:\\unico.txt"), contextMock.Object, CancellationToken.None);
        emittedItems.Should().HaveCount(2, "el lote de uno sale en el acto: 1 ItemOut + 1 BatchCompleted");
        emittedItems.Clear();

        await node.OnWorkflowCompletedAsync(contextMock.Object, CancellationToken.None);

        emittedItems.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheSameInstanceSeesAnotherExecution_ShouldNotMixThePendingItemsOfThePreviousOne()
    {
        // El motor reconstruye cada nodo en cada ejecución, así que el búfer nace vacío de por sí; el
        // WorkflowExecutionId es la segunda línea de defensa para un anfitrión que reutilice la instancia (o para
        // una ejecución que se corta antes del gancho de fin de flujo y deja su pendiente dentro). Aquí se
        // reutiliza la instancia a propósito: es la única forma de que la defensa sea observable.
        var node = new BatchBufferNode();
        node.Parameters["BatchSize"] = 3;

        var contextMock = new Mock<IFlowExecutionContext>();
        var emittedItems = new List<(string Port, FileItemContext Item)>();

        contextMock
            .Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, item) => emittedItems.Add((port, item)))
            .Returns(Task.CompletedTask);

        static FileItemContext Item(string executionId, string path)
        {
            var item = new FileItemContext(path);
            item.Metadata["WorkflowExecutionId"] = executionId;
            return item;
        }

        // Ejecución anterior: dos elementos que no llenan el lote, y la ejecución no llega a cerrarlo.
        await node.ExecuteAsync("ItemIn", Item("eje-1", "C:\\uno.txt"), contextMock.Object, CancellationToken.None);
        await node.ExecuteAsync("ItemIn", Item("eje-1", "C:\\dos.txt"), contextMock.Object, CancellationToken.None);
        emittedItems.Should().BeEmpty();

        // Ejecución nueva, mismo nodo: sus tres elementos llenan el lote de tres.
        await node.ExecuteAsync("ItemIn", Item("eje-2", "C:\\tres.txt"), contextMock.Object, CancellationToken.None);
        await node.ExecuteAsync("ItemIn", Item("eje-2", "C:\\cuatro.txt"), contextMock.Object, CancellationToken.None);
        await node.ExecuteAsync("ItemIn", Item("eje-2", "C:\\cinco.txt"), contextMock.Object, CancellationToken.None);

        emittedItems.Where(e => e.Port == "ItemOut").Select(e => Path.GetFileName(e.Item.CurrentPath))
            .Should().Equal(new[] { "tres.txt", "cuatro.txt", "cinco.txt" },
                "los elementos que quedaron esperando en la ejecución anterior no se suman al lote de ésta");
        emittedItems.Count(e => e.Port == "BatchCompleted").Should().Be(1);
    }
}
