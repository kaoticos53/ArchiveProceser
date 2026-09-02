using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class WorkflowFolderWatcherTests : IDisposable
{
    private readonly string _testDir1;
    private readonly string _testDir2;

    public WorkflowFolderWatcherTests()
    {
        _testDir1 = Path.Combine(Path.GetTempPath(), "FileFlow_Watch_Test1_" + Guid.NewGuid().ToString("N"));
        _testDir2 = Path.Combine(Path.GetTempPath(), "FileFlow_Watch_Test2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir1);
        Directory.CreateDirectory(_testDir2);
    }

    [Fact]
    public async Task FolderWatcherService_MultiDirectory_ShouldDetectFilesAndEmitEvents()
    {
        // Arrange
        using var watcher = new FolderWatcherService();
        var discoveredItems = new List<FileItemContext>();
        watcher.ItemDiscovered += item => discoveredItems.Add(item);

        // Act
        watcher.Start([_testDir1, _testDir2], filter: "*.*", includeSubdirectories: true, debounceMs: 100);

        string file1 = Path.Combine(_testDir1, "incoming1.txt");
        string file2 = Path.Combine(_testDir2, "incoming2.pdf");

        await File.WriteAllTextAsync(file1, "Content 1");
        await File.WriteAllTextAsync(file2, "Content 2");

        // Wait for debounce and processing loop
        for (int i = 0; i < 30 && discoveredItems.Count < 2; i++)
        {
            await Task.Delay(100);
        }

        // Assert
        watcher.IsWatching.Should().BeTrue();
        discoveredItems.Should().NotBeEmpty();
        discoveredItems.Any(i => i.FileName == "incoming1.txt").Should().BeTrue();
        discoveredItems.Any(i => i.FileName == "incoming2.pdf").Should().BeTrue();

        watcher.Stop();
        watcher.IsWatching.Should().BeFalse();
    }

    [Fact]
    public async Task WorkflowExecutor_ExecuteWatchMode_ShouldOnlyProcessNewIncomingFile()
    {
        // Arrange
        string preExistingFile = Path.Combine(_testDir1, "old_file.txt");
        await File.WriteAllTextAsync(preExistingFile, "Old content");

        using var watcher = new FolderWatcherService();
        var executor = new WorkflowExecutor { IsDryRun = true };
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(ThrottleDelayNode).Assembly);

        var graph = new WorkflowGraph { Name = "Watch Mode Single Item Flow" };
        var sourceNode = new WorkflowNode
        {
            Id = "source-node-1",
            NodeTypeName = typeof(FolderSourceNode).FullName!,
            Parameters = new Dictionary<string, object?>
            {
                ["SourcePath"] = _testDir1
            }
        };
        var throttleNode = new WorkflowNode
        {
            Id = "throttle-node-1",
            NodeTypeName = typeof(ThrottleDelayNode).FullName!,
            Parameters = new Dictionary<string, object?>
            {
                ["DelayMilliseconds"] = 1
            }
        };
        graph.Nodes.Add(sourceNode);
        graph.Nodes.Add(throttleNode);
        graph.Edges.Add(new WorkflowEdge
        {
            SourceNodeId = "source-node-1",
            SourcePortName = "Out",
            TargetNodeId = "throttle-node-1",
            TargetPortName = "In"
        });

        using var cts = new CancellationTokenSource();

        // Iniciar vigilancia
        watcher.Start([_testDir1], filter: "*.*", debounceMs: 100);

        var watchTask = Task.Run(async () =>
        {
            await executor.ExecuteWatchModeAsync(graph, loader, watcher, cts.Token);
        });

        // Crear SOLO un archivo nuevo
        string newFile = Path.Combine(_testDir1, "brand_new_item.log");
        await File.WriteAllTextAsync(newFile, "Log payload");

        // Esperar hasta que el nodo downstream haya procesado el archivo
        for (int i = 0; i < 30; i++)
        {
            var currentStats = executor.GetNodeTelemetryStats();
            if (currentStats.TryGetValue("throttle-node-1", out var s) && s.ProcessedCount > 0)
            {
                break;
            }
            await Task.Delay(100);
        }

        cts.Cancel();
        try
        {
            await watchTask;
        }
        catch (OperationCanceledException) { }

        // Assert
        watcher.Stop();
        var nodeStats = executor.GetNodeTelemetryStats();
        nodeStats.Should().ContainKey("throttle-node-1");
        // El nodo downstream solo debe haber procesado 1 elemento (el nuevo), no 2 ni todos
        nodeStats["throttle-node-1"].ProcessedCount.Should().Be(1);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir1)) Directory.Delete(_testDir1, true);
            if (Directory.Exists(_testDir2)) Directory.Delete(_testDir2, true);
        }
        catch { }
    }
}
