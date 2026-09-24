using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace FileFlow.Tests.Performance;

/// <summary>
/// <b>El punto de control no puede costar más que el trabajo: miles de archivos, medidos antes y después.</b>
///
/// <para>El defecto era que <b>cada archivo completado</b> persistía el conjunto entero de claves —serializado
/// con sangría y escrito a disco de una vez— bajo el candado del punto de control: con N archivos, N escrituras
/// de un conjunto que crece hasta N claves, y un punto de serialización por ítem justo en el camino que reparte
/// el trabajo entre hilos.</para>
///
/// <para>La prueba corre <b>el mismo flujo real dos veces</b> sobre los mismos archivos, en la misma máquina y
/// en el mismo proceso, cambiando sólo <see cref="WorkflowExecutor.CheckpointFilesPerWrite"/>:
/// <c>1</c> es el comportamiento anterior —una escritura por archivo, conservado para poder medirlo— y el valor
/// por omisión es el arreglo. El cronómetro es la diferencia; las cuentas de escritura son el hecho
/// (<see cref="WorkflowCheckpointManager.SaveCount"/>, que lleva el propio gestor).</para>
///
/// <para>Corre en la colección exclusiva <see cref="EngineFirstRunCollection"/> porque <b>mide</b> tiempo de
/// pared y de disco: una colección vecina ejecutando su propio flujo ensucia la resta.</para>
/// </summary>
[Collection(EngineFirstRunCollection.Name)]
public class CheckpointWriteCostTests
{
    private const int FileCount = 2_000;

    private readonly ITestOutputHelper _output;

    public CheckpointWriteCostTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task CheckpointCost_PerFileAgainstBatched_WithThousandsOfFiles()
    {
        string source = Path.Combine(Path.GetTempPath(), "FF_CpCost_Src_" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(Path.GetTempPath(), "FF_CpCost_Dst_" + Guid.NewGuid().ToString("N"));
        string checkpoints = Path.Combine(Path.GetTempPath(), "FF_CpCost_Cp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(checkpoints);

        try
        {
            for (int i = 0; i < FileCount; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(source, $"item_{i:D5}.txt"), "x");
            }

            _output.WriteLine($"archivos: {FileCount} | CPUs lógicos: {Environment.ProcessorCount} | punto de control en directorio temporal");

            // El flujo es el mínimo que hace el trabajo real y pasa por el punto de control: origen de carpeta y
            // sumidero (nodo terminal, que es quien registra el archivo completado).
            var before = await MeasureAsync(source, destination, checkpoints, filesPerWrite: 1, label: "antes (una escritura por archivo)");
            var after = await MeasureAsync(source, destination, checkpoints, filesPerWrite: WorkflowCheckpointHandler.DefaultFilesPerCheckpointWrite, label: "después (por lotes)");

            _output.WriteLine(
                $"escrituras del punto de control: antes {before.SaveCount} · después {after.SaveCount} " +
                $"(x{(double)before.SaveCount / Math.Max(1, after.SaveCount):F0} menos) | " +
                $"tiempo: antes {before.ElapsedMilliseconds} ms · después {after.ElapsedMilliseconds} ms " +
                $"(x{(double)before.ElapsedMilliseconds / Math.Max(1, after.ElapsedMilliseconds):F2} menos)");

            before.SaveCount.Should().BeGreaterThanOrEqualTo(FileCount,
                "una escritura por archivo es el defecto que esta prueba mide: tiene que verse en la cuenta");

            after.SaveCount.Should().BeLessThanOrEqualTo((FileCount / WorkflowCheckpointHandler.DefaultFilesPerCheckpointWrite) + 1,
                "por lotes, 2.000 archivos son unos pocos volcados del conjunto, no uno por archivo");

            after.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(before.ElapsedMilliseconds,
                "agrupar las escrituras no puede salir más caro que escribirlas todas: el trabajo del flujo es el mismo");

            // El trabajo no cambia: los dos tienen que entregar todos los archivos.
            before.Delivered.Should().Be(FileCount);
            after.Delivered.Should().Be(FileCount);
        }
        finally
        {
            if (Directory.Exists(source)) Directory.Delete(source, true);
            if (Directory.Exists(destination)) Directory.Delete(destination, true);
            if (Directory.Exists(checkpoints)) Directory.Delete(checkpoints, true);
        }
    }

    private async Task<(long ElapsedMilliseconds, long SaveCount, int Delivered)> MeasureAsync(
        string source,
        string destination,
        string checkpoints,
        int filesPerWrite,
        string label)
    {
        foreach (string stale in Directory.EnumerateFiles(destination))
        {
            File.Delete(stale);
        }

        // Un gestor y un ejecutor por medición: en memoria y en disco, nada de la corrida anterior.
        var manager = new WorkflowCheckpointManager(checkpoints);
        var loader = new PluginLoader();
        loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);

        var executor = new WorkflowExecutor
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            EnableCheckpointing = true,
            CheckpointManager = manager,
            CheckpointFilesPerWrite = filesPerWrite
        };

        var graph = new WorkflowGraph
        {
            Name = "checkpoint-cost-" + Guid.NewGuid().ToString("N"),
            Nodes =
            {
                new WorkflowNode
                {
                    Id = "source",
                    NodeTypeName = "FolderSourceNode",
                    Parameters = new Dictionary<string, object?> { ["SourcePath"] = source }
                },
                new WorkflowNode
                {
                    Id = "sink",
                    NodeTypeName = "DestinationSinkNode",
                    Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = destination }
                }
            },
            Edges =
            {
                new WorkflowEdge { SourceNodeId = "source", SourcePortName = "Out", TargetNodeId = "sink", TargetPortName = "In" }
            }
        };

        var clock = Stopwatch.StartNew();
        await executor.ExecuteAsync(graph, loader, CancellationToken.None);
        clock.Stop();

        int delivered = Directory.EnumerateFiles(destination).Count();
        _output.WriteLine($"{label}: pared {clock.ElapsedMilliseconds} ms, volcados {manager.SaveCount}, entregados {delivered}");

        return (clock.ElapsedMilliseconds, manager.SaveCount, delivered);
    }
}
