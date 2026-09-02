using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class WorkflowCheckpointTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly string _tempFilesDir;

    public WorkflowCheckpointTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "FileFlow_CP_Mgr_" + Guid.NewGuid().ToString("N"));
        _tempFilesDir = Path.Combine(Path.GetTempPath(), "FileFlow_CP_Files_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testBaseDir);
        Directory.CreateDirectory(_tempFilesDir);
    }

    [Fact]
    public void CheckpointManager_SaveRetrieveAndClear_OperatesCorrectly()
    {
        // Arrange
        var manager = new WorkflowCheckpointManager(_testBaseDir);
        string wfName = "Image Batch Pipeline";

        var cp = new WorkflowCheckpointData
        {
            WorkflowName = wfName,
            ProcessedItemsCount = 10,
            CompletedFileKeys = ["C:\\Data\\file1.png", "C:\\Data\\file2.png"]
        };

        // Act & Assert 1: Guardar y verificar existencia
        manager.SaveCheckpoint(cp);
        bool hasPending = manager.HasPendingCheckpoint(wfName, out var retrieved);

        hasPending.Should().BeTrue();
        retrieved.Should().NotBeNull();
        retrieved!.CompletedFileKeys.Should().HaveCount(2);
        retrieved.CompletedFileKeys.Should().Contain("C:\\Data\\file1.png");

        // Act & Assert 2: Limpiar checkpoint
        manager.ClearCheckpoint(wfName);
        manager.HasPendingCheckpoint(wfName, out var emptyCheck).Should().BeFalse();
    }

    [Fact]
    public async Task WorkflowExecutor_WithExistingCheckpoint_SkipsCompletedItems()
    {
        // Arrange
        string file1 = Path.Combine(_tempFilesDir, "done1.txt");
        string file2 = Path.Combine(_tempFilesDir, "new2.txt");
        await File.WriteAllTextAsync(file1, "Old Completed Content");
        await File.WriteAllTextAsync(file2, "New Pending Content");

        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(ThrottleDelayNode).Assembly);

        var graph = new WorkflowGraph { Name = "Resumption Test Workflow" };
        var src = new WorkflowNode
        {
            Id = "src-1",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            Parameters = new Dictionary<string, object?> { ["SourcePath"] = _tempFilesDir }
        };
        var throttle = new WorkflowNode
        {
            Id = "th-1",
            NodeTypeName = typeof(ThrottleDelayNode).FullName!,
            Parameters = new Dictionary<string, object?> { ["DelayMilliseconds"] = 1 }
        };
        graph.Nodes.Add(src);
        graph.Nodes.Add(throttle);
        graph.Edges.Add(new WorkflowEdge
        {
            SourceNodeId = "src-1",
            SourcePortName = "Out",
            TargetNodeId = "th-1",
            TargetPortName = "In"
        });

        // Simular un checkpoint existente donde file1 ya fue completado
        var existingCp = new WorkflowCheckpointData
        {
            WorkflowName = graph.Name,
            CompletedFileKeys = [file1],
            ProcessedItemsCount = 1
        };

        var executor = new WorkflowExecutor
        {
            IsDryRun = false,
            EnableCheckpointing = true,
            Checkpoint = existingCp
        };

        // Act
        await executor.ExecuteAsync(graph, loader, CancellationToken.None);

        // Assert: El checkpoint final debe contener ambos archivos completados
        executor.Checkpoint.Should().NotBeNull();
        executor.Checkpoint!.CompletedFileKeys.Should().Contain(file1);
        executor.Checkpoint.CompletedFileKeys.Should().Contain(file2);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir)) Directory.Delete(_testBaseDir, true);
            if (Directory.Exists(_tempFilesDir)) Directory.Delete(_tempFilesDir, true);
        }
        catch { }
    }
}
