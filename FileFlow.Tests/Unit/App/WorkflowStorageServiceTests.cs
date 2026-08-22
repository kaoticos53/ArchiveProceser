using System.IO;
using FileFlow.App.Services;
using FileFlow.Core.Engine;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

public class WorkflowStorageServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly WorkflowStorageService _storageService;

    public WorkflowStorageServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FileFlow_StorageTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
        _storageService = new WorkflowStorageService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task SaveAndLoadWorkflowAsync_ShouldPersistGraphAccurately()
    {
        // Arrange
        var graph = new WorkflowGraph { Name = "Test Pipeline" };
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "node-1",
            NodeTypeName = "SourceScanNode",
            X = 100,
            Y = 150,
            HasBreakpoint = true,
            Parameters = new Dictionary<string, object?> { ["DirectoryPath"] = "C:\\Input" }
        });
        graph.Nodes.Add(new WorkflowNode
        {
            Id = "node-2",
            NodeTypeName = "DestinationSinkNode",
            X = 400,
            Y = 150,
            HasBreakpoint = false,
            Parameters = new Dictionary<string, object?> { ["OutputPath"] = "C:\\Output" }
        });
        graph.Edges.Add(new WorkflowEdge
        {
            SourceNodeId = "node-1",
            SourcePortName = "Out",
            TargetNodeId = "node-2",
            TargetPortName = "In"
        });
        graph.BreakpointNodeIds.Add("node-1");

        string filePath = Path.Combine(_tempDirectory, "test_flow.json");

        // Act
        await _storageService.SaveWorkflowAsync(filePath, graph);
        var loadedGraph = await _storageService.LoadWorkflowAsync(filePath);

        // Assert
        loadedGraph.Should().NotBeNull();
        loadedGraph.Name.Should().Be("Test Pipeline");
        loadedGraph.Nodes.Should().HaveCount(2);
        loadedGraph.Edges.Should().HaveCount(1);
        loadedGraph.BreakpointNodeIds.Should().Contain("node-1");
        loadedGraph.Nodes[0].Parameters["DirectoryPath"]?.ToString().Should().Be("C:\\Input");
    }

    [Fact]
    public void SerializeAndDeserialize_ShouldMatchGraphContent()
    {
        // Arrange
        var graph = new WorkflowGraph { Name = "InMemory Pipeline" };
        graph.Nodes.Add(new WorkflowNode { Id = "n1", NodeTypeName = "TestNode", X = 50, Y = 50 });

        // Act
        string json = _storageService.SerializeGraph(graph);
        var deserialized = _storageService.DeserializeGraph(json);

        // Assert
        json.Should().Contain("InMemory Pipeline");
        deserialized.Name.Should().Be("InMemory Pipeline");
        deserialized.Nodes.Should().HaveCount(1);
    }

    [Fact]
    public void AllFortyGeneratedWorkflowExamples_ShouldDeserializeSuccessfully()
    {
        // Arrange
        string examplesDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "docs", "examples"));
        if (!Directory.Exists(examplesDir))
        {
            return;
        }

        string[] jsonFiles = Directory.GetFiles(examplesDir, "*.json", SearchOption.AllDirectories);

        // Assert
        jsonFiles.Length.Should().Be(40);

        foreach (var file in jsonFiles)
        {
            string json = File.ReadAllText(file);
            var graph = _storageService.DeserializeGraph(json);

            graph.Should().NotBeNull();
            graph.Nodes.Should().NotBeNull();
            graph.Edges.Should().NotBeNull();
            graph.Nodes.Count.Should().BeGreaterThan(0);
        }
    }
}
