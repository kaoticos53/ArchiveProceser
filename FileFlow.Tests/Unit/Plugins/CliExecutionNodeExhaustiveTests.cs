using FluentAssertions;
using FileFlow.Plugin.Integrations;
using FileFlow.Sdk;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class CliExecutionNodeExhaustiveTests
{
    [Fact]
    public async Task CliExecutionNode_DryRun_ShouldRegisterPlannedActionWithoutExecuting()
    {
        // Arrange
        var node = new CliExecutionNode();
        node.Parameters["ExecutablePath"] = "cmd.exe";
        node.Parameters["ArgumentsTemplate"] = "/c echo test";

        var item = new FileItemContext(@"C:\Temp\file.txt");
        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.Setup(c => c.IsDryRun).Returns(true);

        PlannedAction? recordedAction = null;
        mockContext.Setup(c => c.RegisterPlannedAction(It.IsAny<PlannedAction>()))
            .Callback<PlannedAction>(a => recordedAction = a);

        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        recordedAction.Should().NotBeNull();
        recordedAction!.OperationType.Should().Be(PlannedOperationType.ExecuteCommand);
        recordedAction.Description.Should().Contain("Run command: cmd.exe");
    }

    [Fact]
    public async Task CliExecutionNode_SimpleExecution_ShouldCaptureOutputSuccessfully()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Arrange
        var node = new CliExecutionNode();
        node.Parameters["ExecutablePath"] = "cmd.exe";
        node.Parameters["ArgumentsTemplate"] = "/c echo Hello_FileFlow";
        node.Parameters["CaptureOutputToMetadata"] = true;
        node.Parameters["TimeoutSeconds"] = 10;

        var item = new FileItemContext(@"C:\Temp\dummy.txt");
        var mockContext = new Mock<IFlowExecutionContext>();
        string? emittedPin = null;

        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((pin, _) => emittedPin = pin)
            .Returns(Task.CompletedTask);

        // Act
        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        // Assert
        emittedPin.Should().Be("Success");
        item.Metadata.Should().ContainKey("Cli:StdOut");
        item.Metadata["Cli:StdOut"]!.ToString().Should().Contain("Hello_FileFlow");
        item.Metadata["Cli:ExitCode"].Should().Be(0);
    }
}
