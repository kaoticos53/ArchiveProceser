using System.Collections.Generic;
using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Logic;
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
        _pluginLoader.RegisterNodeTypesFromAssembly(typeof(ForkJoinBarrierNode).Assembly);
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

    /// <summary>
    /// <b>La vuelta de una rama a la barrera no es un ciclo.</b> Es el grafo de un fork/join —los ejemplos 22 y 30
    /// del catálogo— y el motor lo rechazaba entero («Graph contains a cycle») porque contaba la arista de vuelta
    /// como precedencia: el nodo barrera quedaba inutilizable y los ejemplos no llegaban a ejecutarse (hito 204).
    /// </summary>
    [Fact]
    public void Validate_ShouldAcceptABranchReturningToABarrierNode()
    {
        var graph = new WorkflowGraph
        {
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode { Id = "source", NodeTypeName = typeof(FolderSourceNode).FullName! },
                new WorkflowNode { Id = "barrier", NodeTypeName = typeof(ForkJoinBarrierNode).FullName! },
                new WorkflowNode { Id = "branch1", NodeTypeName = typeof(LogOutputNode).FullName! },
                new WorkflowNode { Id = "branch2", NodeTypeName = typeof(LogOutputNode).FullName! },
                new WorkflowNode { Id = "sink", NodeTypeName = typeof(LogOutputNode).FullName! }
            },
            Edges = new List<WorkflowEdge>
            {
                new WorkflowEdge { Id = "e1", SourceNodeId = "source", SourcePortName = "Out", TargetNodeId = "barrier", TargetPortName = "In" },
                new WorkflowEdge { Id = "e2", SourceNodeId = "barrier", SourcePortName = "Fork1", TargetNodeId = "branch1", TargetPortName = "In" },
                new WorkflowEdge { Id = "e3", SourceNodeId = "barrier", SourcePortName = "Fork2", TargetNodeId = "branch2", TargetPortName = "In" },
                new WorkflowEdge { Id = "e4", SourceNodeId = "branch1", SourcePortName = "Out", TargetNodeId = "barrier", TargetPortName = "Branch1_Done" },
                new WorkflowEdge { Id = "e5", SourceNodeId = "branch2", SourcePortName = "Out", TargetNodeId = "barrier", TargetPortName = "Branch2_Done" },
                new WorkflowEdge { Id = "e6", SourceNodeId = "barrier", SourcePortName = "AllCompleted", TargetNodeId = "sink", TargetPortName = "In" }
            }
        };

        var result = _validator.Validate(graph, _pluginLoader);

        result.IsValid.Should().BeTrue(string.Join(" | ", result.Errors));
        result.Errors.Should().BeEmpty();
        result.TopologicalOrder.Should().HaveCount(5, "ningún nodo se pierde al excluir la vuelta del orden");
        result.TopologicalOrder.Select(n => n.Id).Should().ContainInOrder("source", "barrier");
    }

    /// <summary>
    /// La exención es <b>por puerto</b>, no «los grafos con barrera pueden tener ciclos». Un ciclo que entra por
    /// una entrada normal se rechaza igual aunque haya una barrera en el grafo.
    /// </summary>
    [Fact]
    public void Validate_ShouldStillRejectACycleThatDoesNotEnterThroughAFeedbackPort()
    {
        var graph = new WorkflowGraph
        {
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode { Id = "barrier", NodeTypeName = typeof(ForkJoinBarrierNode).FullName! },
                new WorkflowNode { Id = "node1", NodeTypeName = typeof(LogOutputNode).FullName! },
                new WorkflowNode { Id = "node2", NodeTypeName = typeof(LogOutputNode).FullName! }
            },
            Edges = new List<WorkflowEdge>
            {
                new WorkflowEdge { Id = "e1", SourceNodeId = "node1", SourcePortName = "Out", TargetNodeId = "node2", TargetPortName = "In" },
                new WorkflowEdge { Id = "e2", SourceNodeId = "node2", SourcePortName = "Out", TargetNodeId = "node1", TargetPortName = "In" },
                new WorkflowEdge { Id = "e3", SourceNodeId = "node1", SourcePortName = "Out", TargetNodeId = "barrier", TargetPortName = "In" }
            }
        };

        var result = _validator.Validate(graph, _pluginLoader);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("cycle"));
    }

    /// <summary>
    /// Una arista que entra por <c>Branch1_Done</c> desde un nodo que la barrera no alimenta no es la vuelta de
    /// ninguna rama: la barrera esperará para siempre un aviso que le llega de un sitio que no bifurcó. No es un
    /// error —el grafo corre— sino el defecto silencioso que hay que decir en voz alta.
    /// </summary>
    [Fact]
    public void Validate_ShouldWarnWhenAFeedbackEdgeDoesNotComeFromABranchOfThatNode()
    {
        var graph = new WorkflowGraph
        {
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode { Id = "source", NodeTypeName = typeof(FolderSourceNode).FullName! },
                new WorkflowNode { Id = "barrier", NodeTypeName = typeof(ForkJoinBarrierNode).FullName! },
                new WorkflowNode { Id = "stranger", NodeTypeName = typeof(LogOutputNode).FullName! }
            },
            Edges = new List<WorkflowEdge>
            {
                new WorkflowEdge { Id = "e1", SourceNodeId = "source", SourcePortName = "Out", TargetNodeId = "barrier", TargetPortName = "In" },
                new WorkflowEdge { Id = "e2", SourceNodeId = "source", SourcePortName = "Out", TargetNodeId = "stranger", TargetPortName = "In" },
                new WorkflowEdge { Id = "e3", SourceNodeId = "stranger", SourcePortName = "Out", TargetNodeId = "barrier", TargetPortName = "Branch1_Done" }
            }
        };

        var result = _validator.Validate(graph, _pluginLoader);

        result.IsValid.Should().BeTrue("no es un ciclo que impida ejecutar, es una rama que nunca llegará");
        result.Warnings.Should().ContainSingle(w => w.Contains("Branch1_Done") && w.Contains("not downstream"));
    }

    /// <summary>
    /// Un nodo al que <b>sólo</b> le entran avisos de ramas no lo arranca nadie: no recibe el ítem, no bifurca, y
    /// los avisos que espera no existen. Es el mismo silencio que un puerto mal escrito, dicho por adelantado.
    /// </summary>
    [Fact]
    public void Validate_ShouldWarnWhenANodeIsOnlyFedByFeedbackPorts()
    {
        var graph = new WorkflowGraph
        {
            Nodes = new List<WorkflowNode>
            {
                new WorkflowNode { Id = "barrier", NodeTypeName = typeof(ForkJoinBarrierNode).FullName! },
                new WorkflowNode { Id = "branch", NodeTypeName = typeof(LogOutputNode).FullName! }
            },
            Edges = new List<WorkflowEdge>
            {
                new WorkflowEdge { Id = "e1", SourceNodeId = "barrier", SourcePortName = "Fork1", TargetNodeId = "branch", TargetPortName = "In" },
                new WorkflowEdge { Id = "e2", SourceNodeId = "branch", SourcePortName = "Out", TargetNodeId = "barrier", TargetPortName = "Branch2_Done" }
            }
        };

        var result = _validator.Validate(graph, _pluginLoader);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Contains("only fed by feedback ports"));
        result.Warnings.Should().NotContain(w => w.Contains("not downstream"),
            "el aviso es que nadie lo arranca, no que la rama venga de otro sitio: son dos defectos distintos");
    }
}
