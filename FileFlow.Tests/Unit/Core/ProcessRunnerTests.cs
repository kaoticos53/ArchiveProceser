using System.Runtime.InteropServices;
using FileFlow.Core.Platform;
using FileFlow.Sdk.Platform;
using FluentAssertions;
using Moq;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class ProcessRunnerTests
{
    [Fact]
    public async Task ProcessRunner_ExecutesBasicCommand_AndCapturesOutput()
    {
        var runner = ProcessRunner.Instance;
        string exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/sh";
        string args = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/c echo HelloProcessRunner" : "-c \"echo HelloProcessRunner\"";

        var result = await runner.RunAsync(exe, args, timeout: TimeSpan.FromSeconds(5));

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Contain("HelloProcessRunner");
        result.TimedOut.Should().BeFalse();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ProcessRunner_HandlesStreamingCallbacks()
    {
        var runner = ProcessRunner.Instance;
        string exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/sh";
        string args = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "/c echo LineOne & echo LineTwo" : "-c \"echo LineOne; echo LineTwo\"";

        var lines = new List<string>();

        var result = await runner.RunAsync(new ProcessExecutionRequest
        {
            FileName = exe,
            Arguments = args,
            Timeout = TimeSpan.FromSeconds(5),
            OnStandardOutputLine = line => lines.Add(line)
        });

        result.Success.Should().BeTrue();
        lines.Should().Contain(l => l.Contains("LineOne"));
    }

    [Fact]
    public async Task ProcessRunner_HandlesTimeout_Gracefully()
    {
        var runner = ProcessRunner.Instance;
        string exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/sh";
        // Ping or sleep to simulate delay
        string args = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "/c ping -n 5 127.0.0.1 > nul"
            : "-c \"sleep 5\"";

        var result = await runner.RunAsync(exe, args, timeout: TimeSpan.FromMilliseconds(500));

        result.TimedOut.Should().BeTrue();
        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
    }

    [Fact]
    public async Task NullProcessRunner_ProvidesSafeNoOpExecution()
    {
        var nullRunner = NullProcessRunner.Instance;

        var result = await nullRunner.RunAsync("fake.exe", "--version");

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task MockProcessRunner_CanBeInjectedInContext()
    {
        var mockRunner = new Mock<IProcessRunner>();
        mockRunner
            .Setup(r => r.RunAsync(It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = "Mock output from injected runner",
                StandardError = string.Empty,
                TimedOut = false,
                Duration = TimeSpan.FromMilliseconds(42)
            });

        var result = await mockRunner.Object.RunAsync(new ProcessExecutionRequest
        {
            FileName = "any_tool.exe"
        });

        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Be("Mock output from injected runner");
    }
}
