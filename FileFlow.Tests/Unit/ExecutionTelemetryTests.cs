using FileFlow.Core.Engine;
using FileFlow.Sdk.Telemetry;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

public class ExecutionTelemetryTests
{
    [Fact]
    public void TelemetrySnapshot_InitialState_ShouldBeConsistent()
    {
        var executor = new WorkflowExecutor();
        var snapshot = executor.GetTelemetrySnapshot();

        snapshot.ProcessedItems.Should().Be(0);
        snapshot.TotalItems.Should().Be(0);
        snapshot.Percentage.Should().Be(0.0);
        snapshot.MegabytesPerSecond.Should().Be(0.0);
    }

    [Fact]
    public void TelemetrySnapshot_CustomStatus_ShouldReflectInSnapshot()
    {
        var executor = new WorkflowExecutor();
        executor.SetCustomStatusMessage("⚡ Escaneando 500 archivos...");

        var snapshot = executor.GetTelemetrySnapshot();
        snapshot.StatusMessage.Should().Be("⚡ Escaneando 500 archivos...");
    }

    [Fact]
    public void TelemetrySnapshot_EmptyStatic_ShouldProvideDefaultValues()
    {
        var empty = TelemetrySnapshot.Empty;
        empty.ProcessedItems.Should().Be(0);
        empty.TotalItems.Should().Be(0);
        empty.Percentage.Should().Be(0.0);
        empty.Elapsed.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TelemetrySnapshot_ExpectedTotalItems_ShouldCalculateAccuratePercentage()
    {
        var executor = new WorkflowExecutor();
        executor.SetTotalExpectedItems(1000);

        var snapshot = executor.GetTelemetrySnapshot();
        snapshot.TotalItems.Should().Be(1000);
        snapshot.Percentage.Should().Be(0.0);
    }
}
