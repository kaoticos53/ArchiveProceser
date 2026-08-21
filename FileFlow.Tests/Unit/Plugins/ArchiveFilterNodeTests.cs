using FileFlow.Plugin.Archives;
using FileFlow.Sdk;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class ArchiveFilterNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldEmitToArchivePort_WhenPrimaryZipFile()
    {
        // Arrange
        var node = new ArchiveFilterNode();
        var item = new FileItemContext(@"C:\origen\Fichero.zip");
        var contextMock = new Mock<IFlowExecutionContext>();
        string? emittedPort = null;

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        // Assert
        emittedPort.Should().Be("Archive");
        item.Metadata.Should().ContainKey("IsPrimaryArchive");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitToSecondaryVolumePort_WhenSplitRarVolume()
    {
        // Arrange
        var node = new ArchiveFilterNode();
        var item = new FileItemContext(@"C:\origen\directorio1\Fichero2.r01");
        var contextMock = new Mock<IFlowExecutionContext>();
        string? emittedPort = null;

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        // Assert
        emittedPort.Should().Be("SecondaryVolume");
        item.Metadata.Should().ContainKey("IsSecondaryArchiveVolume");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitToRegularFilePort_WhenDocFile()
    {
        // Arrange
        var node = new ArchiveFilterNode();
        var item = new FileItemContext(@"C:\origen\directorio2\fichero3.doc");
        var contextMock = new Mock<IFlowExecutionContext>();
        string? emittedPort = null;

        contextMock.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((port, _) => emittedPort = port)
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, contextMock.Object, CancellationToken.None);

        // Assert
        emittedPort.Should().Be("RegularFile");
    }
}
