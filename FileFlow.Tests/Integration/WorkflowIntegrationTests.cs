using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.Archives;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Images;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Integration;

public class WorkflowIntegrationTests
{
    [Fact]
    public async Task EndToEnd_FolderSourceToInjectorToDestinationSink_ShouldProcessPipeline()
    {
        // Arrange
        string tempSource = Path.Combine(Path.GetTempPath(), "FF_Int_Source_" + Guid.NewGuid());
        string tempDest = Path.Combine(Path.GetTempPath(), "FF_Int_Dest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempSource);
        File.WriteAllText(Path.Combine(tempSource, "sample.txt"), "Integration Test Content");

        try
        {
            var loader = new PluginLoader();
            loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
            loader.RegisterNodeTypesFromAssembly(typeof(SmartUnpackNode).Assembly);
            loader.RegisterNodeTypesFromAssembly(typeof(ExifMetadataNode).Assembly);

            var graph = new WorkflowGraph { Name = "E2E Pipeline" };
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "node-1",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = tempSource }
            });
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "node-2",
                NodeTypeName = "VariableInjectorNode",
                Parameters = new Dictionary<string, object?> { ["VariableName"] = "ProcessedCategory", ["ExpressionValue"] = "TestCategory" }
            });
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "node-3",
                NodeTypeName = "DestinationSinkNode",
                Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = tempDest }
            });

            graph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = "node-1",
                SourcePortName = "Out",
                TargetNodeId = "node-2",
                TargetPortName = "In"
            });

            graph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = "node-2",
                SourcePortName = "Out",
                TargetNodeId = "node-3",
                TargetPortName = "In"
            });

            var executor = new WorkflowExecutor();

            // Act
            await executor.ExecuteAsync(graph, loader, cancellationToken: CancellationToken.None);

            // Assert
            string expectedOutputFile = Path.Combine(tempDest, "sample.txt");
            File.Exists(expectedOutputFile).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
            if (Directory.Exists(tempDest)) Directory.Delete(tempDest, true);
        }
    }
}
