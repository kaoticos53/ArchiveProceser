using System.Runtime.InteropServices;
using FluentAssertions;
using FileFlow.Plugin.Integrations;
using FileFlow.Sdk;
using FileFlow.Sdk.Platform;
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

    [Fact]
    public async Task CliExecutionNode_WhenExitCodeNonZero_ShouldEmitFailedAndCaptureStdErr()
    {
        var node = new CliExecutionNode();
        node.Parameters["ExecutablePath"] = "mock-shell";
        node.Parameters["ArgumentsTemplate"] = "run something";
        node.Parameters["CaptureOutputToMetadata"] = true;

        var item = new FileItemContext(@"C:\Temp\dummy.txt");

        var platform = new Mock<IOsPlatformService>();
        platform.SetupGet(p => p.Platform).Returns(OSPlatform.Windows);
        platform.Setup(p => p.GetDefaultShellExecutable()).Returns("cmd.exe");

        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 7,
                StandardOutput = "",
                StandardError = "fatal error",
                Duration = TimeSpan.FromMilliseconds(20),
                TimedOut = false
            });

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.Platform).Returns(platform.Object);
        mockContext.SetupGet(c => c.ProcessRunner).Returns(runner.Object);

        string? emittedPin = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((pin, _) => emittedPin = pin)
            .Returns(Task.CompletedTask);

        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        emittedPin.Should().Be("Failed");
        item.Metadata["Cli:ExitCode"].Should().Be(7);
        item.Metadata["Cli:StdErr"]?.ToString().Should().Contain("fatal error");
    }

    [Fact]
    public async Task CliExecutionNode_WhenRunnerTimesOut_ShouldEmitFailed()
    {
        var node = new CliExecutionNode();
        node.Parameters["ExecutablePath"] = "mock-shell";
        node.Parameters["ArgumentsTemplate"] = "run timeout";

        var item = new FileItemContext(@"C:\Temp\dummy.txt");

        var platform = new Mock<IOsPlatformService>();
        platform.SetupGet(p => p.Platform).Returns(OSPlatform.Windows);
        platform.Setup(p => p.GetDefaultShellExecutable()).Returns("cmd.exe");

        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = -1,
                StandardOutput = "",
                StandardError = "",
                Duration = TimeSpan.FromSeconds(1),
                TimedOut = true
            });

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.Platform).Returns(platform.Object);
        mockContext.SetupGet(c => c.ProcessRunner).Returns(runner.Object);

        string? emittedPin = null;
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
            .Callback<string, FileItemContext>((pin, _) => emittedPin = pin)
            .Returns(Task.CompletedTask);

        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        emittedPin.Should().Be("Failed");
    }

    [Fact]
    public async Task CliExecutionNode_OnNonWindows_ShouldTranslateCmdSwitchToShellC()
    {
        var node = new CliExecutionNode();
        node.Parameters["ExecutablePath"] = "cmd.exe";
        node.Parameters["ArgumentsTemplate"] = "/c echo hello";

        var item = new FileItemContext(@"/tmp/dummy.txt");

        var platform = new Mock<IOsPlatformService>();
        platform.SetupGet(p => p.Platform).Returns(OSPlatform.Linux);
        platform.Setup(p => p.GetDefaultShellExecutable()).Returns("/bin/bash");

        ProcessExecutionRequest? captured = null;
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessExecutionRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = "ok",
                StandardError = "",
                Duration = TimeSpan.FromMilliseconds(10),
                TimedOut = false
            });

        var mockContext = new Mock<IFlowExecutionContext>();
        mockContext.SetupGet(c => c.Platform).Returns(platform.Object);
        mockContext.SetupGet(c => c.ProcessRunner).Returns(runner.Object);
        mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>())).Returns(Task.CompletedTask);

        await node.ExecuteAsync("In", item, mockContext.Object, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.FileName.Should().Be("/bin/bash");
        captured.Arguments.Should().StartWith("-c \"");
        captured.Arguments.Should().Contain("echo hello");
    }
}
