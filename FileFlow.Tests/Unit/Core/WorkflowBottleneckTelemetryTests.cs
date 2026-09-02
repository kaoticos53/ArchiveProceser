using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class WorkflowBottleneckTelemetryTests
{
    [Fact]
    public void TelemetryTracker_SingleNodeExecutions_ComputesAccurateAverages()
    {
        // Arrange
        var tracker = new WorkflowTelemetryTracker();
        tracker.Reset();

        // Act
        tracker.RecordNodeExecution("node-a", 10.0);
        tracker.RecordNodeExecution("node-a", 20.0);
        tracker.RecordNodeExecution("node-a", 30.0);

        var stats = tracker.GetNodeStats();

        // Assert
        stats.Should().ContainKey("node-a");
        var nodeA = stats["node-a"];
        nodeA.ProcessedCount.Should().Be(3);
        nodeA.TotalTimeMs.Should().Be(60.0);
        nodeA.AverageTimeMs.Should().Be(20.0);
        nodeA.HeatLevel.Should().Be(LatencyHeatLevel.Low);
        nodeA.IsBottleneck.Should().BeFalse(); // Single node is not flagged as bottleneck relative to other nodes
    }

    [Fact]
    public void TelemetryTracker_MultipleNodes_FlagsHeaviestNodeAsBottleneck()
    {
        // Arrange
        var tracker = new WorkflowTelemetryTracker();
        tracker.Reset();

        // Act: Fast node takes 10ms total, Slow bottleneck node takes 900ms total
        tracker.RecordNodeExecution("node-fast", 5.0);
        tracker.RecordNodeExecution("node-fast", 5.0);

        tracker.RecordNodeExecution("node-heavy", 450.0);
        tracker.RecordNodeExecution("node-heavy", 450.0);

        var stats = tracker.GetNodeStats();

        // Assert
        stats.Should().ContainKey("node-fast");
        stats.Should().ContainKey("node-heavy");

        var fast = stats["node-fast"];
        var heavy = stats["node-heavy"];

        fast.IsBottleneck.Should().BeFalse();
        fast.HeatLevel.Should().Be(LatencyHeatLevel.Low);

        heavy.IsBottleneck.Should().BeTrue();
        heavy.HeatLevel.Should().Be(LatencyHeatLevel.High);
        heavy.RelativeBottleneckRatio.Should().BeGreaterThan(0.90);
    }

    [Fact]
    public async Task WorkflowExecutor_Execution_RecordsNodeTelemetryStats()
    {
        // Arrange
        var executor = new WorkflowExecutor { IsDryRun = true };
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(ThrottleDelayNode).Assembly);

        string tempFolder = Path.Combine(Path.GetTempPath(), "FileFlow_Bottleneck_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        await File.WriteAllTextAsync(Path.Combine(tempFolder, "sample.txt"), "sample data");

        try
        {
            var graph = new WorkflowGraph { Name = "Telemetry Bottleneck Test Flow" };
            var src = new WorkflowNode
            {
                Id = "source-1",
                NodeTypeName = typeof(FolderSourceNode).FullName!,
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = tempFolder }
            };
            var delay = new WorkflowNode
            {
                Id = "delay-1",
                NodeTypeName = typeof(ThrottleDelayNode).FullName!,
                Parameters = new Dictionary<string, object?> { ["DelayMilliseconds"] = 50 }
            };

            graph.Nodes.Add(src);
            graph.Nodes.Add(delay);
            graph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = "source-1",
                SourcePortName = "Out",
                TargetNodeId = "delay-1",
                TargetPortName = "In"
            });

            // Act
            await executor.ExecuteAsync(graph, loader, CancellationToken.None);
            var stats = executor.GetNodeTelemetryStats();

            // Assert
            stats.Should().ContainKey("source-1");
            stats.Should().ContainKey("delay-1");
            stats["source-1"].ProcessedCount.Should().BeGreaterThanOrEqualTo(1);
            stats["delay-1"].ProcessedCount.Should().BeGreaterThanOrEqualTo(1);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }
    }
}
