using System.Collections.Generic;
using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

public class GraphValidatorTests
{
    private readonly PluginLoader _pluginLoader;
    private readonly GraphValidator _validator;

    public GraphValidatorTests()
    {
        _pluginLoader = new PluginLoader();
        _pluginLoader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        _validator = new GraphValidator();
    }

    [Fact]
    public void Validate_ShouldReturnSuccessAndTopologicalOrder_WhenGraphIsAcyclicAndValid()
    {
        // Arrange
        var graph = new WorkflowGraph
        {
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode { Id = "node1", NodeTypeName = typeof(FolderSourceNode).FullName! },
                new WorkflowNode { Id = "node2", NodeTypeName = typeof(LogOutputNode).FullName! }
            },
            Edges = new List<WorkflowEdge>
            {
                new WorkflowEdge
                {
                    Id = "edge1",
                    SourceNodeId = "node1",
                    SourcePortName = "Out",
                    TargetNodeId = "node2",
                    TargetPortName = "In"
                }
            }
        };

        // Act
        var result = _validator.Validate(graph, _pluginLoader);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.TopologicalOrder.Should().HaveCount(2);
        result.TopologicalOrder[0].Id.Should().Be("node1");
        result.TopologicalOrder[1].Id.Should().Be("node2");
    }

    [Fact]
    public void Validate_ShouldFail_WhenGraphContainsCycle()
    {
        // Arrange
        var graph = new WorkflowGraph
        {
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode { Id = "node1", NodeTypeName = typeof(LogOutputNode).FullName! },
                new WorkflowNode { Id = "node2", NodeTypeName = typeof(LogOutputNode).FullName! }
            },
            Edges = new List<WorkflowEdge>
            {
                new WorkflowEdge
                {
                    Id = "edge1",
                    SourceNodeId = "node1",
                    SourcePortName = "Out",
                    TargetNodeId = "node2",
                    TargetPortName = "In"
                },
                new WorkflowEdge
                {
                    Id = "edge2",
                    SourceNodeId = "node2",
                    SourcePortName = "Out",
                    TargetNodeId = "node1",
                    TargetPortName = "In"
                }
            }
        };

        // Act
        var result = _validator.Validate(graph, _pluginLoader);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("cycle"));
    }

    [Fact]
    public void Validate_ShouldFail_WhenUnknownNodeTypeGiven()
    {
        // Arrange
        var graph = new WorkflowGraph
        {
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode { Id = "node1", NodeTypeName = "NonExistentNamespace.NonExistentNode" }
            }
        };

        // Act
        var result = _validator.Validate(graph, _pluginLoader);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("unknown node type"));
    }

    [Fact]
    public void Validate_ShouldFail_WhenEdgeReferencesNonExistentPort()
    {
        // Arrange
        var graph = new WorkflowGraph
        {
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode { Id = "node1", NodeTypeName = typeof(FolderSourceNode).FullName! },
                new WorkflowNode { Id = "node2", NodeTypeName = typeof(LogOutputNode).FullName! }
            },
            Edges = new List<WorkflowEdge>
            {
                new WorkflowEdge
                {
                    Id = "edge1",
                    SourceNodeId = "node1",
                    SourcePortName = "InvalidPortName",
                    TargetNodeId = "node2",
                    TargetPortName = "In"
                }
            }
        };

        // Act
        var result = _validator.Validate(graph, _pluginLoader);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("does not have output port"));
    }

    [Fact]
    public void Validate_ShouldHandleDisconnectedIslandNodes_Successfully()
    {
        // Arrange
        var graph = new WorkflowGraph
        {
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode { Id = "island1", NodeTypeName = typeof(FolderSourceNode).FullName! },
                new WorkflowNode { Id = "island2", NodeTypeName = typeof(FolderSourceNode).FullName! }
            },
            Edges = new List<WorkflowEdge>()
        };

        // Act
        var result = _validator.Validate(graph, _pluginLoader);

        // Assert
        result.IsValid.Should().BeTrue();
        result.TopologicalOrder.Should().HaveCount(2);
    }
}
