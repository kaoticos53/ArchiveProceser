using System.Collections.Generic;
using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// Pruebas unitarias para el validador estático y de ordenación topológica de grafos DAG <see cref="GraphValidator"/>.
/// </summary>
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

    /// <summary>
    /// OBJETO: Validación y ordenación topológica de un grafo acíclico válido.
    /// QUÉ:    Verifica que un grafo conexo sin ciclos sea declarado válido y su orden topológico respete la causalidad (nodo productor antes de nodo consumidor).
    /// CÓMO:  Construye un grafo de dos nodos (FolderSourceNode -> LogOutputNode), ejecuta Validate y comprueba que TopologicalOrder contenga node1 seguido de node2.
    /// </summary>
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

    /// <summary>
    /// OBJETO: Detección de ciclos y bucles infinitos en el grafo.
    /// QUÉ:    Garantiza la invariante de Grafo Dirigido Acíclico (DAG) rechazando grafos que contengan bucles circulares de retroalimentación.
    /// CÓMO:  Crea un grafo con dos aristas circulares (node1 -> node2 y node2 -> node1), ejecuta Validate y comprueba que IsValid sea false y el error mencione 'cycle'.
    /// </summary>
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

    /// <summary>
    /// OBJETO: Validación de existencia de tipos de nodo en el catálogo de plugins.
    /// QUÉ:    Comprueba que el validador reporte un error si el grafo contiene un nodo con un nombre de tipo no registrado o desconocido.
    /// CÓMO:  Crea un nodo con NodeTypeName sintético inexistente, ejecuta Validate y valida el mensaje de error correspondiente.
    /// </summary>
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

    /// <summary>
    /// OBJETO: Validación de integridad de puertos en las conexiones.
    /// QUÉ:    Asegura que si una arista intenta vincularse a un puerto de salida inexistente, la validación falle con un error explícito.
    /// CÓMO:  Define una arista con SourcePortName = 'InvalidPortName', ejecuta Validate y comprueba que se señale la ausencia del puerto.
    /// </summary>
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

    /// <summary>
    /// OBJETO: Soporte de grafos disconexos y nodos aislados.
    /// QUÉ:    Verifica que la presencia de múltiples componentes disconexos o nodos aislados sin aristas no impida una validación y ordenación topológica válida.
    /// CÓMO:  Crea dos nodos FolderSourceNode sin aristas intermedias, valida el grafo y comprueba que IsValid sea true y el TopologicalOrder incluya ambos nodos.
    /// </summary>
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
