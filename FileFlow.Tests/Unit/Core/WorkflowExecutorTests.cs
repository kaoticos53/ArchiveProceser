using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class WorkflowExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldExecuteSingleNode_WhenGraphContainsOneNode()
    {
        // Arrange
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);

        string tempSource = Path.Combine(Path.GetTempPath(), "FF_Exec_Single_" + Guid.NewGuid());
        Directory.CreateDirectory(tempSource);

        try
        {
            var executor = new WorkflowExecutor();
            var graph = new WorkflowGraph { Name = "Test Single Node Workflow" };
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "node-1",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = tempSource }
            });

            // Act & Assert
            Func<Task> act = async () => await executor.ExecuteAsync(graph, loader, CancellationToken.None);

            await act.Should().NotThrowAsync();
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
        }
    }

    [Fact]
    public void WorkflowExecutor_MaxDegreeOfParallelism_ShouldUpdateThrottleSemaphore()
    {
        var executor = new WorkflowExecutor();
        executor.MaxDegreeOfParallelism = 4;
        Assert.Equal(4, executor.MaxDegreeOfParallelism);

        // Disposing old semaphore and assigning new one multiple times
        executor.MaxDegreeOfParallelism = 8;
        Assert.Equal(8, executor.MaxDegreeOfParallelism);

        executor.MaxDegreeOfParallelism = 1;
        Assert.Equal(1, executor.MaxDegreeOfParallelism);
    }

    [Fact]
    public void WorkflowExecutor_PauseAndResume_ShouldToggleState()
    {
        var executor = new WorkflowExecutor();
        Assert.False(executor.IsPaused);

        executor.Pause();
        Assert.True(executor.IsPaused);

        executor.Resume();
        Assert.False(executor.IsPaused);
    }

    [Fact]
    public void WorkflowExecutor_DryRun_ShouldToggleDryRunState()
    {
        var executor = new WorkflowExecutor();
        executor.IsDryRun = true;
        Assert.True(executor.IsDryRun);

        executor.IsDryRun = false;
        Assert.False(executor.IsDryRun);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCancel_WhenCancellationTokenSignaled()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);

        string tempSource = Path.Combine(Path.GetTempPath(), "FF_Exec_Cancel_" + Guid.NewGuid());
        Directory.CreateDirectory(tempSource);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        try
        {
            var executor = new WorkflowExecutor();
            var graph = new WorkflowGraph { Name = "Test Cancel Workflow" };
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "node-1",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = tempSource }
            });

            Func<Task> act = async () => await executor.ExecuteAsync(graph, loader, cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
        }
    }
}
