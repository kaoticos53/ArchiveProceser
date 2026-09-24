using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Plugin.Hashing;
using FileFlow.Plugin.Logic;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

/// <summary>
/// <b>La barrera de sincronización hace lo que su nombre dice: bifurca, espera a las ramas que le dijeron y
/// libera el ítem una sola vez —o no lo libera.</b>
///
/// <para>El nodo existía, el editor lo dibujaba y el catálogo lo anunciaba, pero <b>ningún flujo que lo usara
/// llegaba a ejecutarse</b>: la vuelta de las ramas a <c>Branch1_Done</c>/<c>Branch2_Done</c> es, para el orden
/// topológico, un ciclo —el nodo está aguas arriba de lo que le entra— y el motor rechazaba el grafo entero.
/// Los ejemplos 22 y 30 lo destaparon (hito 204) y los puertos de vuelta se marcaron como
/// <see cref="NodePort.IsFeedbackSignal"/>. Aquí se comprueba lo que la barrera <i>hace</i> cuando por fin corre,
/// sobre un grafo pequeño y sin depender de la red.</para>
///
/// <para>Las dos mitades importan: con las dos ramas de vuelta el ítem sale <b>una vez</b> por cada entrada
/// —liberarlo dos veces duplicaría el archivo, y ninguna prueba lo miraba—, y con una rama que no vuelve el
/// ítem <b>no sale</b>, que es el precio real de la barrera y el motivo por el que un flujo con una rama rota
/// pierde archivos en silencio.</para>
/// </summary>
public class ForkJoinBarrierNodeTests
{
    private const string BarrierId = "barrier";

    [Fact]
    public async Task TheBarrier_ShouldReleaseTheItemOnceEveryBranchReports()
    {
        string source = NewDirectory();
        string destination = NewDirectory();

        try
        {
            SeedFiles(source, count: 3);

            var graph = new WorkflowGraph
            {
                Name = "fork-join-complete",
                Nodes =
                {
                    new WorkflowNode
                    {
                        Id = "source",
                        NodeTypeName = typeof(FolderSourceNode).FullName!,
                        Parameters = new Dictionary<string, object?> { ["SourcePath"] = source }
                    },
                    new WorkflowNode
                    {
                        Id = BarrierId,
                        NodeTypeName = typeof(ForkJoinBarrierNode).FullName!,
                        Parameters = new Dictionary<string, object?> { ["RequiredBranchesCount"] = 2 }
                    },
                    new WorkflowNode { Id = "branch-hash", NodeTypeName = typeof(HashCalculatorNode).FullName! },
                    new WorkflowNode { Id = "branch-log", NodeTypeName = typeof(LogOutputNode).FullName! },
                    new WorkflowNode
                    {
                        Id = "sink",
                        NodeTypeName = typeof(DestinationSinkNode).FullName!,
                        Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = destination }
                    }
                },
                Edges =
                {
                    new WorkflowEdge { SourceNodeId = "source", SourcePortName = "Out", TargetNodeId = BarrierId, TargetPortName = "In" },
                    new WorkflowEdge { SourceNodeId = BarrierId, SourcePortName = "Fork1", TargetNodeId = "branch-hash", TargetPortName = "In" },
                    new WorkflowEdge { SourceNodeId = BarrierId, SourcePortName = "Fork2", TargetNodeId = "branch-log", TargetPortName = "In" },
                    new WorkflowEdge { SourceNodeId = "branch-hash", SourcePortName = "Out", TargetNodeId = BarrierId, TargetPortName = "Branch1_Done" },
                    new WorkflowEdge { SourceNodeId = "branch-log", SourcePortName = "Out", TargetNodeId = BarrierId, TargetPortName = "Branch2_Done" },
                    new WorkflowEdge { SourceNodeId = BarrierId, SourcePortName = "AllCompleted", TargetNodeId = "sink", TargetPortName = "In" }
                }
            };

            int allCompleted = await RunAsync(graph, destination);

            allCompleted.Should().Be(3,
                "la barrera libera cada ítem exactamente una vez, cuando las dos ramas han avisado por Branch1_Done y Branch2_Done");
            Files(destination).Should().HaveCount(3,
                "lo que la barrera libera es el ítem que bifurcó, y el sumidero lo entrega una vez por entrada");
        }
        finally
        {
            Clean(source, destination);
        }
    }

    [Fact]
    public async Task TheBarrier_ShouldHoldTheItemWhenOneOfItsBranchesNeverReports()
    {
        string source = NewDirectory();
        string destination = NewDirectory();

        try
        {
            SeedFiles(source, count: 3);

            // La rama del registro se deja al aire: su salida no vuelve a Branch2_Done.
            var graph = new WorkflowGraph
            {
                Name = "fork-join-half-wired",
                Nodes =
                {
                    new WorkflowNode
                    {
                        Id = "source",
                        NodeTypeName = typeof(FolderSourceNode).FullName!,
                        Parameters = new Dictionary<string, object?> { ["SourcePath"] = source }
                    },
                    new WorkflowNode
                    {
                        Id = BarrierId,
                        NodeTypeName = typeof(ForkJoinBarrierNode).FullName!,
                        Parameters = new Dictionary<string, object?> { ["RequiredBranchesCount"] = 2 }
                    },
                    new WorkflowNode { Id = "branch-hash", NodeTypeName = typeof(HashCalculatorNode).FullName! },
                    new WorkflowNode { Id = "branch-log", NodeTypeName = typeof(LogOutputNode).FullName! },
                    new WorkflowNode
                    {
                        Id = "sink",
                        NodeTypeName = typeof(DestinationSinkNode).FullName!,
                        Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = destination }
                    }
                },
                Edges =
                {
                    new WorkflowEdge { SourceNodeId = "source", SourcePortName = "Out", TargetNodeId = BarrierId, TargetPortName = "In" },
                    new WorkflowEdge { SourceNodeId = BarrierId, SourcePortName = "Fork1", TargetNodeId = "branch-hash", TargetPortName = "In" },
                    new WorkflowEdge { SourceNodeId = BarrierId, SourcePortName = "Fork2", TargetNodeId = "branch-log", TargetPortName = "In" },
                    new WorkflowEdge { SourceNodeId = "branch-hash", SourcePortName = "Out", TargetNodeId = BarrierId, TargetPortName = "Branch1_Done" },
                    new WorkflowEdge { SourceNodeId = BarrierId, SourcePortName = "AllCompleted", TargetNodeId = "sink", TargetPortName = "In" }
                }
            };

            int allCompleted = await RunAsync(graph, destination);

            allCompleted.Should().Be(0,
                "el nodo espera las dos ramas que se le pidieron —'RequiredBranchesCount' es 2— y una sola no cierra la barrera");
            Files(destination).Should().BeEmpty(
                "la consecuencia de una rama que no vuelve es que el archivo no llega al destino: ni error ni aviso, el ítem se queda dentro");
        }
        finally
        {
            Clean(source, destination);
        }
    }

    /// <summary>
    /// Corre el grafo y devuelve cuántas veces la barrera emitió por <c>AllCompleted</c>. Contar emisiones y no
    /// archivos entregados es lo que distingue «liberó una vez por ítem» de «liberó dos veces el mismo ítem y el
    /// sumidero sobrescribió»: en el disco las dos cosas se ven igual.
    /// </summary>
    private static async Task<int> RunAsync(WorkflowGraph graph, string destination)
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(HashCalculatorNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(ForkJoinBarrierNode).Assembly);

        var executor = new WorkflowExecutor
        {
            EnableCheckpointing = false,
            GlobalOutputDir = destination,
            TemporaryDirectory = Path.Combine(destination, "tmp")
        };

        int allCompleted = 0;
        executor.EdgeItemDispatched += (nodeId, port, _) =>
        {
            if (nodeId == BarrierId && port == "AllCompleted")
            {
                Interlocked.Increment(ref allCompleted);
            }
        };

        await executor.ExecuteAsync(graph, loader, CancellationToken.None);

        return allCompleted;
    }

    private static void SeedFiles(string source, int count)
    {
        for (int i = 0; i < count; i++)
        {
            File.WriteAllText(Path.Combine(source, $"item_{i}.txt"), $"contenido {i}");
        }
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "FF_Barrier_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static List<string> Files(string directory) =>
        [.. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}tmp{Path.DirectorySeparatorChar}"))];

    private static void Clean(params string[] directories)
    {
        foreach (string directory in directories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
