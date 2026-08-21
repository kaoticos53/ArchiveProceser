using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class WorkflowDebugSessionTests
{
    [Fact]
    public void WorkflowGraph_ShouldPersistAndRestore_BreakpointsInJson()
    {
        // Arrange
        var graph = new WorkflowGraph { Name = "Debug Graph" };
        graph.Nodes.Add(new WorkflowNode { Id = "node-1", HasBreakpoint = true });
        graph.Nodes.Add(new WorkflowNode { Id = "node-2", HasBreakpoint = false });
        graph.BreakpointNodeIds.Add("node-1");

        // Act
        string json = graph.ToJson();
        var restored = WorkflowGraph.FromJson(json);

        // Assert
        restored.BreakpointNodeIds.Should().Contain("node-1");
        restored.BreakpointNodeIds.Should().NotContain("node-2");
        restored.Nodes.First(n => n.Id == "node-1").HasBreakpoint.Should().BeTrue();
    }

    [Fact]
    public void WorkflowDebugSession_ShouldToggleAndManageBreakpoints()
    {
        // Arrange
        var session = new WorkflowDebugSession();

        // Act
        session.ToggleBreakpoint("node-123");

        // Assert
        session.HasBreakpoint("node-123").Should().BeTrue();

        // Toggle off
        session.ToggleBreakpoint("node-123");
        session.HasBreakpoint("node-123").Should().BeFalse();
    }

    [Fact]
    public void WorkflowDebugSession_ShouldRecordSnapshotsCorrectly()
    {
        // Arrange
        var session = new WorkflowDebugSession();
        var item = new FileItemContext(@"C:\test\sample.txt");
        item.Metadata["CustomTag"] = "Value123";

        // Act
        var snapIn = NodeDataSnapshot.CreateInput("node-1", "Input", item);
        session.RecordSnapshot(snapIn);

        var snapOut = NodeDataSnapshot.CreateOutput("node-1", "Output", item);
        session.RecordSnapshot(snapOut);

        // Assert
        var snapshots = session.GetSnapshotsForNode("node-1");
        snapshots.Should().HaveCount(2);
        snapshots[0].IsInput.Should().BeTrue();
        snapshots[0].ItemSnapshot.Metadata["CustomTag"].Should().Be("Value123");
        snapshots[1].IsInput.Should().BeFalse();
    }

    [Fact]
    public async Task WorkflowExecutor_WithDebugSession_ShouldPauseAtBreakpointAndResume()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);

        string tempSource = Path.Combine(Path.GetTempPath(), "FF_Debug_Test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempSource);
        File.WriteAllText(Path.Combine(tempSource, "test.txt"), "Hello Debugger");

        try
        {
            var session = new WorkflowDebugSession();
            session.ToggleBreakpoint("node-1");

            var executor = new WorkflowExecutor
            {
                DebugSession = session
            };

            var graph = new WorkflowGraph { Name = "Test Breakpoint Execution" };
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "node-1",
                NodeTypeName = "FolderSourceNode",
                HasBreakpoint = true,
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = tempSource }
            });
            graph.BreakpointNodeIds.Add("node-1");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            bool breakpointHit = false;
            session.NodeStatusChanged += (nodeId, status, details) =>
            {
                if (status == NodeExecutionStatus.PausedAtBreakpoint && nodeId == "node-1")
                {
                    breakpointHit = true;
                    // Resume execution after a short moment
                    Task.Delay(50).ContinueWith(_ => session.Continue());
                }
            };

            // Act
            await executor.ExecuteAsync(graph, loader, cts.Token);

            // Assert
            breakpointHit.Should().BeTrue();
            session.GetSnapshotsForNode("node-1").Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
        }
    }
}
