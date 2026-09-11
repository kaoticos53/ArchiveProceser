using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Integration;

public class WorkflowWorkspaceCleanupIntegrationTests
{
    public class DummyIntermediateProducerNode : FlowNodeBase
    {
        public static string LastCreatedWorkspacePath = string.Empty;

        public override string Name => "Dummy Intermediate Producer";
        public override string Category => "Testing";
        public override string Description => "Creates a temp file in TempWorkspace and registers it";

        public DummyIntermediateProducerNode()
        {
            Inputs = [new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")];
            Outputs = [new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")];
        }

        public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
        {
            string tempDir = context.TempWorkspace.CreateSubdirectory("intermediate_test");
            string tempFile = Path.Combine(tempDir, "temp_data.bin");
            await File.WriteAllTextAsync(tempFile, "temporary binary payload", cancellationToken);

            LastCreatedWorkspacePath = context.TempWorkspace.ExecutionTempDirectory;
            item.Metadata["ProducedTempFile"] = tempFile;
            item.Metadata["WorkspacePath"] = LastCreatedWorkspacePath;
            await context.EmitAsync("Out", item);
        }
    }

    public class DummyFailingNode : FlowNodeBase
    {
        public override string Name => "Dummy Failing Node";
        public override string Category => "Testing";
        public override string Description => "Throws an exception to simulate workflow error";

        public DummyFailingNode()
        {
            Inputs = [new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")];
            Outputs = [new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")];
        }

        public override Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated catastrophic step error.");
        }
    }

    [Fact]
    public async Task WorkflowExecutor_WithAutoClean_ShouldPurgeTemporaryWorkspaceOnSuccess()
    {
        // Arrange
        string tempSource = Path.Combine(Path.GetTempPath(), "FF_Cleanup_Source_" + Guid.NewGuid().ToString("N"));
        string tempDest = Path.Combine(Path.GetTempPath(), "FF_Cleanup_Dest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempSource);
        File.WriteAllText(Path.Combine(tempSource, "test.txt"), "hello world");

        DummyIntermediateProducerNode.LastCreatedWorkspacePath = string.Empty;

        try
        {
            var loader = new PluginLoader();
            loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
            loader.RegisterNodeType<DummyIntermediateProducerNode>();

            var graph = new WorkflowGraph { Name = "Cleanup Success Graph" };
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "src",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = tempSource }
            });
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "producer",
                NodeTypeName = nameof(DummyIntermediateProducerNode),
                Parameters = new Dictionary<string, object?>()
            });
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "sink",
                NodeTypeName = "DestinationSinkNode",
                Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = tempDest }
            });

            graph.Edges.Add(new WorkflowEdge { SourceNodeId = "src", SourcePortName = "Out", TargetNodeId = "producer", TargetPortName = "In" });
            graph.Edges.Add(new WorkflowEdge { SourceNodeId = "producer", SourcePortName = "Out", TargetNodeId = "sink", TargetPortName = "In" });

            var executor = new WorkflowExecutor
            {
                AutoCleanIntermediateTempFiles = true,
                MaxDegreeOfParallelism = 1
            };

            // Act
            await executor.ExecuteAsync(graph, loader, CancellationToken.None);

            // Assert
            string capturedWorkspace = DummyIntermediateProducerNode.LastCreatedWorkspacePath;
            capturedWorkspace.Should().NotBeNullOrWhiteSpace();
            Directory.Exists(capturedWorkspace).Should().BeFalse("the scoped temporary workspace must be completely purged when AutoClean is true");
        }
        finally
        {
            try { if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true); } catch { }
            try { if (Directory.Exists(tempDest)) Directory.Delete(tempDest, true); } catch { }
            try { if (!string.IsNullOrWhiteSpace(DummyIntermediateProducerNode.LastCreatedWorkspacePath) && Directory.Exists(DummyIntermediateProducerNode.LastCreatedWorkspacePath)) Directory.Delete(DummyIntermediateProducerNode.LastCreatedWorkspacePath, true); } catch { }
        }
    }

    [Fact]
    public async Task WorkflowExecutor_WithAutoClean_ShouldPurgeTemporaryWorkspaceEvenOnFailure()
    {
        // Arrange
        string tempSource = Path.Combine(Path.GetTempPath(), "FF_CleanupFail_Source_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempSource);
        File.WriteAllText(Path.Combine(tempSource, "fail_test.txt"), "hello failure");

        DummyIntermediateProducerNode.LastCreatedWorkspacePath = string.Empty;

        try
        {
            var loader = new PluginLoader();
            loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
            loader.RegisterNodeType<DummyIntermediateProducerNode>();
            loader.RegisterNodeType<DummyFailingNode>();

            var graph = new WorkflowGraph { Name = "Cleanup Failure Graph" };
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "src",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = tempSource }
            });
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "producer",
                NodeTypeName = nameof(DummyIntermediateProducerNode),
                Parameters = new Dictionary<string, object?>()
            });
            graph.Nodes.Add(new WorkflowNode
            {
                Id = "fail",
                NodeTypeName = nameof(DummyFailingNode),
                Parameters = new Dictionary<string, object?>()
            });

            graph.Edges.Add(new WorkflowEdge { SourceNodeId = "src", SourcePortName = "Out", TargetNodeId = "producer", TargetPortName = "In" });
            graph.Edges.Add(new WorkflowEdge { SourceNodeId = "producer", SourcePortName = "Out", TargetNodeId = "fail", TargetPortName = "In" });

            var executor = new WorkflowExecutor
            {
                AutoCleanIntermediateTempFiles = true,
                MaxDegreeOfParallelism = 1
            };

            // Act
            var act = async () => await executor.ExecuteAsync(graph, loader, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            string capturedWorkspace = DummyIntermediateProducerNode.LastCreatedWorkspacePath;
            if (!string.IsNullOrWhiteSpace(capturedWorkspace))
            {
                Directory.Exists(capturedWorkspace).Should().BeFalse("the scoped temporary workspace must be cleaned up in finally block even when a node throws");
            }
        }
        finally
        {
            try { if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true); } catch { }
            try { if (!string.IsNullOrWhiteSpace(DummyIntermediateProducerNode.LastCreatedWorkspacePath) && Directory.Exists(DummyIntermediateProducerNode.LastCreatedWorkspacePath)) Directory.Delete(DummyIntermediateProducerNode.LastCreatedWorkspacePath, true); } catch { }
        }
    }
}
