using System.IO;
using FluentAssertions;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class SafeRecycleDeleteNodeExhaustiveTests
{
    [Fact]
    public async Task SafeRecycleDeleteNode_NonExistentFile_ShouldEmitToErrorPin()
    {
        // Arrange
        var node = new SafeRecycleDeleteNode();
        var item = new FileItemContext(@"C:\Inexistente_12345\no_file.tmp");
        var mockContext = new Mock<IFlowExecutionContext>();
        string? emittedPin = null;

        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((pin, _) => emittedPin = pin)
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedPin.Should().Be("Error");
    }

    [Fact]
    public async Task SafeRecycleDeleteNode_DryRun_ShouldRegisterPlannedActionWithoutDeleting()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            var node = new SafeRecycleDeleteNode();
            var item = new FileItemContext(tempFile);
            var mockContext = new Mock<IFlowExecutionContext>();
            mockContext.Setup(c => c.IsDryRun).Returns(true);

            PlannedAction? recordedAction = null;
            mockContext.Setup(c => c.RegisterPlannedAction(It.IsAny<PlannedAction>()))
                .Callback<PlannedAction>(a => recordedAction = a);

            string? emittedPin = null;
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                .Callback<string, FileItemContext>((pin, _) => emittedPin = pin)
                .Returns(Task.CompletedTask);

            // Act
            await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

            // Assert
            emittedPin.Should().Be("Deleted");
            recordedAction.Should().NotBeNull();
            recordedAction!.OperationType.Should().Be(PlannedOperationType.Recycle);
            File.Exists(tempFile).Should().BeTrue(); // No debe haberse borrado en disco
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
