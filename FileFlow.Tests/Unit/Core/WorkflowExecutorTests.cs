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
}
