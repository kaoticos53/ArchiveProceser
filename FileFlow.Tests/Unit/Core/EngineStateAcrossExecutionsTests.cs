using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Core;

/// <summary>
/// <b>Lo que una ejecución deja detrás: el mismo motor, ejecutado dos veces.</b>
///
/// <para>Un <see cref="WorkflowExecutor"/> se reutiliza: la interfaz lo mantiene vivo y el botón Ejecutar lo
/// vuelve a lanzar sin construir nada nuevo. Por eso todo lo que el motor recuerda de la ejecución anterior
/// —modos, contadores, listas, el estado de pausa— tiene que decidirse <b>otra vez</b> al empezar la siguiente.
/// Es la lección que dejó el hito 200: <b>el estado que sobrevive a su ejecución miente</b>, y se destapa
/// ejecutando <b>dos veces con el mismo objeto</b>, no con uno nuevo.</para>
///
/// <para>Cada caso de aquí afirma una mitad distinta del reset: el modo virtual no se hereda, una ejecución que
/// se quedó pausada no pausa la siguiente, las acciones planificadas y el diario son de la ejecución que los
/// produjo, y los contadores de aristas empiezan en cero.</para>
/// </summary>
public class EngineStateAcrossExecutionsTests
{
    private static PluginLoader CreateLoader()
    {
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
        return loader;
    }

    private static WorkflowGraph RealCopyGraph(string sourceDir, string destinationDir) => new()
    {
        Name = "estado-entre-ejecuciones-" + Guid.NewGuid().ToString("N"),
        Nodes =
        {
            new WorkflowNode
            {
                Id = "origen",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = sourceDir }
            },
            new WorkflowNode
            {
                Id = "destino",
                NodeTypeName = "DestinationSinkNode",
                Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = destinationDir }
            }
        },
        Edges =
        {
            new WorkflowEdge { SourceNodeId = "origen", SourcePortName = "Out", TargetNodeId = "destino", TargetPortName = "In" }
        }
    };

    [Fact]
    public async Task ASyntheticRun_ShouldNotLeaveTheNextOneInVirtualMode()
    {
        // El modo virtual lo activa sola la ejecución cuando el grafo trae un origen sintético. Activado para una
        // ejecución, lo estaba para todas: la siguiente escribía en el almacén virtual y el disco quedaba vacío.
        string sourceDir = TestDirectory("real-src");
        string destinationDir = TestDirectory("real-dst");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "real.txt"), "contenido real");

        var loader = CreateLoader();
        var executor = new WorkflowExecutor();

        var syntheticGraph = new WorkflowGraph
        {
            Name = "sintetico-" + Guid.NewGuid().ToString("N"),
            Nodes =
            {
                new WorkflowNode
                {
                    Id = "sintetico",
                    NodeTypeName = "SyntheticDataSourceNode",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["Category"] = "Personalizada",
                        ["EmissionMode"] = "Virtual",
                        ["MaxItems"] = 2,
                        ["EmissionDelayMs"] = 0,
                        ["EmitDirectories"] = "False",
                        ["CustomItems"] = ""
                    }
                }
            }
        };

        await executor.ExecuteAsync(syntheticGraph, loader, CancellationToken.None);
        executor.IsVirtualFileSystemEnabled.Should().BeTrue("la ejecución sintética lo activa sola, y es su razón de ser");

        // Segunda ejecución: archivos de verdad, que tienen que acabar en el disco.
        await executor.ExecuteAsync(RealCopyGraph(sourceDir, destinationDir), loader, CancellationToken.None);

        executor.IsVirtualFileSystemEnabled.Should().BeFalse(
            "el modo virtual era de la ejecución anterior: la nueva trae archivos reales y no puede heredarlo");
        Directory.EnumerateFiles(destinationDir).Select(Path.GetFileName).Should().Contain("real.txt",
            "un flujo con archivos reales tiene que escribirlos en el disco, no en el almacén de la ejecución anterior");
    }

    [Fact]
    public async Task ARunThatEndedWhilePaused_ShouldNotLeaveTheNextOneWaiting()
    {
        string sourceDir = TestDirectory("pause-src");
        string destinationDir = TestDirectory("pause-dst");
        for (int i = 0; i < 20; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDir, $"item_{i:D2}.txt"), "x");
        }

        var loader = CreateLoader();
        var executor = new WorkflowExecutor();

        using var cancellation = new CancellationTokenSource();
        Task firstRun = executor.ExecuteAsync(RealCopyGraph(sourceDir, destinationDir), loader, cancellation.Token);
        await Task.Delay(120);
        executor.Pause();
        cancellation.Cancel();
        try
        {
            await firstRun;
        }
        catch (Exception)
        {
            // La ejecución se detuvo a mitad: cómo lo cuente no es lo que se juzga aquí.
        }

        string secondDestination = TestDirectory("pause-dst-2");
        Task secondRun = executor.ExecuteAsync(RealCopyGraph(sourceDir, secondDestination), loader, CancellationToken.None);

        Task winner = await Task.WhenAny(secondRun, Task.Delay(TimeSpan.FromSeconds(10)));
        if (winner != secondRun)
        {
            // Nadie la ha pausado: antes de juzgar se la desbloquea, para no dejar una tarea colgada en el proceso.
            executor.Resume();
            await Task.WhenAny(secondRun, Task.Delay(TimeSpan.FromSeconds(5)));
        }

        winner.Should().BeSameAs(secondRun,
            "una ejecución nueva arranca sola: el estado de pausa era de la ejecución anterior (que terminó pausada)");
        Directory.EnumerateFiles(secondDestination).Should().NotBeEmpty();
    }

    [Fact]
    public async Task PlannedActions_ShouldNotAccumulateFromPreviousDryRuns()
    {
        string sourceDir = TestDirectory("dry-src");
        string destinationDir = TestDirectory("dry-dst");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "a-mover.txt"), "x");

        var loader = CreateLoader();
        var executor = new WorkflowExecutor { IsDryRun = true, EnableCheckpointing = false };

        var firstGraph = RelocatorGraph(sourceDir, destinationDir, "copy");
        await executor.ExecuteAsync(firstGraph, loader, CancellationToken.None);
        int firstRunActions = executor.PlannedActions.Count;
        firstRunActions.Should().BeGreaterThan(0, "la simulación tiene que planificar el movimiento");

        var secondGraph = RelocatorGraph(sourceDir, destinationDir, "copy");
        await executor.ExecuteAsync(secondGraph, loader, CancellationToken.None);

        executor.PlannedActions.Should().HaveCount(firstRunActions,
            "la segunda simulación tiene que empezar su propio plan: las acciones de la anterior ya no son un plan de nada");
    }

    [Fact]
    public async Task Journal_ShouldOnlyContainTheOperationsOfTheCurrentRun()
    {
        // La interfaz ofrece «revertir la última ejecución» leyendo este diario: si acumula, un solo clic
        // deshace también lo que hizo la anterior.
        string firstSource = TestDirectory("journal-src-1");
        string secondSource = TestDirectory("journal-src-2");
        string destinationDir = TestDirectory("journal-dst");
        await File.WriteAllTextAsync(Path.Combine(firstSource, "primera.txt"), "x");
        await File.WriteAllTextAsync(Path.Combine(secondSource, "segunda.txt"), "x");

        var loader = CreateLoader();
        var executor = new WorkflowExecutor { EnableCheckpointing = false };

        await executor.ExecuteAsync(RelocatorGraph(firstSource, destinationDir, "move"), loader, CancellationToken.None);
        int firstRunEntries = executor.JournalService.Entries.Count;
        firstRunEntries.Should().BeGreaterThan(0, "el movimiento tiene que quedar en el diario para poder deshacerse");

        await executor.ExecuteAsync(RelocatorGraph(secondSource, destinationDir, "move"), loader, CancellationToken.None);

        executor.JournalService.Entries.Should().HaveCount(firstRunEntries,
            "el diario es de la ejecución que lo escribió: revertir la última no puede deshacer la anterior");
    }

    [Fact]
    public async Task EdgeItemCounts_ShouldStartFromZeroInEveryRun()
    {
        string sourceDir = TestDirectory("edge-src");
        string destinationDir = TestDirectory("edge-dst");
        for (int i = 0; i < 3; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDir, $"item_{i}.txt"), "x");
        }

        var loader = CreateLoader();
        var executor = new WorkflowExecutor { EnableCheckpointing = false };

        int highestSeen = 0;
        executor.EdgeItemDispatched += (_, _, count) => highestSeen = Math.Max(highestSeen, count);

        await executor.ExecuteAsync(RealCopyGraph(sourceDir, destinationDir), loader, CancellationToken.None);
        highestSeen.Should().Be(3, "los tres archivos cruzan la arista una vez cada uno");

        highestSeen = 0;
        await executor.ExecuteAsync(RealCopyGraph(sourceDir, destinationDir), loader, CancellationToken.None);

        highestSeen.Should().Be(3,
            "la segunda ejecución vuelve a contar desde cero: el contador es de la ejecución, no del motor");
    }

    private static WorkflowGraph RelocatorGraph(string sourceDir, string destinationDir, string operation) => new()
    {
        Name = "relocator-" + Guid.NewGuid().ToString("N"),
        Nodes =
        {
            new WorkflowNode
            {
                Id = "origen",
                NodeTypeName = "FolderSourceNode",
                Parameters = new Dictionary<string, object?> { ["SourcePath"] = sourceDir }
            },
            new WorkflowNode
            {
                Id = "mover",
                NodeTypeName = "FileRelocatorNode",
                Parameters = new Dictionary<string, object?>
                {
                    ["Operation"] = operation,
                    ["DestinationDirectory"] = destinationDir,
                    ["CreateDirectories"] = "True",
                    ["VerifyIntegrity"] = "False",
                    ["CleanupSource"] = "False"
                }
            }
        },
        Edges =
        {
            new WorkflowEdge { SourceNodeId = "origen", SourcePortName = "Out", TargetNodeId = "mover", TargetPortName = "In" }
        }
    };

    private static string TestDirectory(string label)
    {
        string path = Path.Combine(Path.GetTempPath(), $"FF_State_{label}_{Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(path);
        return path;
    }
}
