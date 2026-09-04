using FileFlow.Core.Engine;
using FileFlow.Sdk.Telemetry;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit;

public class RollingNodeMetricsTrackerTests
{
    [Fact]
    public void RollingNodeMetricsTracker_EmptyState_ShouldReturnZeroes()
    {
        var tracker = new RollingNodeMetricsTracker();
        var (rollingAvgMs, rollingAvgBytes, peakBytes, avgCpu, isGpu, samples) = tracker.GetRollingMetrics("node-1");

        rollingAvgMs.Should().Be(0);
        rollingAvgBytes.Should().Be(0);
        peakBytes.Should().Be(0);
        avgCpu.Should().Be(0);
        isGpu.Should().BeFalse();
        samples.Should().BeEmpty();
    }

    [Fact]
    public void RollingNodeMetricsTracker_AddSamplesUnderWindow_ShouldComputeAccurateAverages()
    {
        var tracker = new RollingNodeMetricsTracker();
        tracker.RecordSample("node-1", 10.0, 1000, 20.0, false);
        tracker.RecordSample("node-1", 30.0, 3000, 40.0, true);

        var (rollingAvgMs, rollingAvgBytes, peakBytes, avgCpu, isGpu, samples) = tracker.GetRollingMetrics("node-1");

        rollingAvgMs.Should().Be(20.0);
        rollingAvgBytes.Should().Be(2000);
        peakBytes.Should().Be(3000);
        avgCpu.Should().Be(30.0);
        isGpu.Should().BeTrue();
        samples.Should().HaveCount(2);
        samples[0].DurationMs.Should().Be(10.0);
        samples[1].DurationMs.Should().Be(30.0);
    }

    [Fact]
    public void RollingNodeMetricsTracker_ExceedingWindowSize_ShouldEvictOldestInFifoOrder()
    {
        var tracker = new RollingNodeMetricsTracker();
        
        // Add 10 samples: 10, 20, 30, 40, 50, 60, 70, 80, 90, 100
        for (int i = 1; i <= 10; i++)
        {
            tracker.RecordSample("node-1", i * 10.0, i * 1024, i * 5.0, i % 2 == 0);
        }

        var (rollingAvgMs, rollingAvgBytes, peakBytes, avgCpu, isGpu, samples) = tracker.GetRollingMetrics("node-1");

        // The last 8 samples are 30, 40, 50, 60, 70, 80, 90, 100
        // Sum = 30+40+50+60+70+80+90+100 = 520. Avg = 520 / 8 = 65.0
        rollingAvgMs.Should().Be(65.0);
        peakBytes.Should().Be(10 * 1024);
        samples.Should().HaveCount(8);
        samples[0].DurationMs.Should().Be(30.0);
        samples[7].DurationMs.Should().Be(100.0);
        isGpu.Should().BeTrue();
    }

    [Fact]
    public void RollingNodeMetricsTracker_Reset_ShouldClearAllNodeMetrics()
    {
        var tracker = new RollingNodeMetricsTracker();
        tracker.RecordSample("node-1", 50.0, 2048, 15.0, false);
        tracker.RecordSample("node-2", 100.0, 4096, 25.0, true);

        tracker.Reset();

        var (rollingAvgMs, _, _, _, _, samples) = tracker.GetRollingMetrics("node-1");
        rollingAvgMs.Should().Be(0);
        samples.Should().BeEmpty();
    }

    [Fact]
    public void WorkflowTelemetryTracker_RecordNodeExecution_ShouldIntegrateRollingMetrics()
    {
        var tracker = new WorkflowTelemetryTracker();
        
        tracker.RecordNodeExecution("node-a", 100.0, allocatedBytes: 1024 * 1024, cpuPercentage: 15.0, gpuAccelerated: true);
        tracker.RecordNodeExecution("node-a", 200.0, allocatedBytes: 2 * 1024 * 1024, cpuPercentage: 25.0, gpuAccelerated: true);

        var nodeStats = tracker.GetNodeStats();
        nodeStats.Should().ContainKey("node-a");
        var stats = nodeStats["node-a"];
        stats.ProcessedCount.Should().Be(2);
        stats.AverageTimeMs.Should().Be(150.0);
        stats.RollingAvgDurationMs.Should().Be(150.0);
        stats.RollingAvgAllocatedBytes.Should().Be((long)(1.5 * 1024 * 1024));
        stats.PeakAllocatedBytes.Should().Be(2 * 1024 * 1024);
        stats.AvgCpuPercentage.Should().Be(20.0);
        stats.IsGpuAccelerated.Should().BeTrue();
        stats.RecentSamples.Should().NotBeNull();
        stats.RecentSamples!.Should().HaveCount(2);
    }
}
