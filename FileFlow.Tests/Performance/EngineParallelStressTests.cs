using System.Diagnostics;
using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Sdk;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace FileFlow.Tests.Performance;

/// <summary>
/// Estrés de paralelismo concurrente del <see cref="WorkflowExecutor"/> REAL.
///
/// <para>La versión anterior de esta prueba construía el executor, no lo ejecutaba nunca y medía un
/// <c>Task.WhenAll</c> sobre lambdas locales — el umbral de 20 s no comparaba nada del motor. Ahora se
/// ejecuta un DAG real (origen de carpeta → sumidero de destino) sobre ficheros en disco, que es el
/// camino que ejercita el despacho concurrente de verdad: canales, telemetría y grado de paralelismo.</para>
///
/// <para>Contrato doble: <b>corrección</b> (todas las fichas del origen llegan al destino — determinista)
/// y <b>regresión</b> (el tiempo de ejecución no se desploma respecto a lo que esta máquina acaba de
/// demostrar, patrón calibrado de <see cref="CalibratedBenchmark"/>; el factor se eleva porque el I/O de
/// disco real tiene más varianza estructural que el cómputo puro).</para>
/// </summary>
public class EngineParallelStressTests
{
    private readonly ITestOutputHelper _output;

    public EngineParallelStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// OBJETO: despacho concurrente masivo del motor DAG real.
    /// QUÉ:    100 ficheros reales atraviesan el pipeline (descubrimiento → escritura en destino) con el
    ///         grado de paralelismo de la máquina, sin pérdidas y sin desplome de throughput.
    /// CÓMO:   ejecuta <c>WorkflowExecutor.ExecuteAsync</c> sobre un grafo real en carpetas temporales,
    ///         verifica el recuento exacto en destino y mide el tiempo con el patrón calibrado.
    /// </summary>
    [Fact]
    public async Task WorkflowExecutor_RealPipeline_ShouldDeliverAllItems_WithoutThroughputRegression()
    {
        const int fileQuantity = 100;

        string tempSource = Path.Combine(Path.GetTempPath(), "FF_StressSrc_" + Guid.NewGuid().ToString("N"));
        string tempDest = Path.Combine(Path.GetTempPath(), "FF_StressDst_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempSource);
        Directory.CreateDirectory(tempDest);

        try
        {
            for (int i = 0; i < fileQuantity; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(tempSource, $"file_{i:D4}.dat"), $"contenido de estrés {i}");
            }

            var loader = new PluginLoader();
            loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);

            var executor = new WorkflowExecutor
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };
            executor.LogEmitted += (_, _) => { };

            WorkflowGraph BuildGraph() => new()
            {
                Name = "stress-parallel",
                Nodes =
                {
                    new WorkflowNode
                    {
                        Id = "source",
                        NodeTypeName = "FolderSourceNode",
                        Parameters = new Dictionary<string, object?> { ["SourcePath"] = tempSource }
                    },
                    new WorkflowNode
                    {
                        Id = "sink",
                        NodeTypeName = "DestinationSinkNode",
                        Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = tempDest }
                    }
                },
                Edges =
                {
                    new WorkflowEdge
                    {
                        SourceNodeId = "source",
                        SourcePortName = "Out",
                        TargetNodeId = "sink",
                        TargetPortName = "In"
                    }
                }
            };

            // Corrección primero (determinista): una ejecución completa entrega exactamente las 100 fichas.
            await executor.ExecuteAsync(BuildGraph(), loader, CancellationToken.None);
            Directory.EnumerateFiles(tempDest).Count().Should().Be(fileQuantity,
                "el motor no puede perder fichas al despachar en paralelo");

            // Regresión después (calibrada): la medición limpia el destino entre pasadas para que cada una
            // mida el mismo trabajo; el drenaje queda excluido del tiempo medido.
            CalibratedBenchmark.MeasureAndAssert(
                _output,
                "Motor DAG real (origen → sumidero, 100 ficheros, paralelo)",
                unitsPerMeasurement: fileQuantity,
                unitName: "items",
                timeLimitFactor: 80.0, // I/O de disco real: más varianza estructural que el cómputo puro
                work: () => executor.ExecuteAsync(BuildGraph(), loader, CancellationToken.None)
                            .GetAwaiter().GetResult(),
                betweenMeasurements: () =>
                {
                    foreach (var f in Directory.EnumerateFiles(tempDest))
                    {
                        File.Delete(f);
                    }
                });
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
            if (Directory.Exists(tempDest)) Directory.Delete(tempDest, true);
        }
    }
}
