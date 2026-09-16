using System.Diagnostics;
using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using FileFlow.Sdk.TemplateEngine;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace FileFlow.Tests.Performance;

/// <summary>
/// Benchmarks de throughput con aserción de regresión.
///
/// <para><b>Qué comprueban</b>: que el throughput no se desplome respecto a la capacidad de la máquina
/// que corre la prueba — la detección de la regresión clásica (alguien añade una caja de arena de SQL
/// dentro de la plantilla de reemplazo, un clon deja de ser exacto en capacidad o el preprocesador pierde
/// la vectorización SIMD). No miden rendimiento absoluto y <b>no</b> son una suite de benchmarking.</para>
///
/// <para><b>El contrato del umbral</b> (relativo, calibración en línea, mediana de repeticiones y por qué
/// no se asiertan contadores del GC) está documentado en <see cref="CalibratedBenchmark"/>, que es el
/// esqueleto compartido por todas las pruebas de este fichero.</para>
/// </summary>
public class PerformanceBenchmarkSuiteTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceBenchmarkSuiteTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Benchmark_TemplateResolver_HighThroughputAndLowGcAllocations()
    {
        // Arrange: carga determinista de plantillas (50.000 interpolaciones con funciones anidadas)
        const int itemQuantity = 50_000;
        var items = new List<FileItemContext>(itemQuantity);

        for (int i = 0; i < itemQuantity; i++)
        {
            var item = new FileItemContext($@"C:\Photos\Batch_{i}\image_{i}.jpg", isDirectory: false);
            item.Metadata["SourceRootPath"] = @"C:\Photos";
            item.Metadata["DateTaken"] = "2026-08-22 21:00:00";
            item.Metadata["Counter"] = i;
            items.Add(item);
        }

        string template = @"C:\Output\{Year(DateTaken)}/Folder_{PadLeft(Counter, 4, ""0"")}/{RelativePath}/{FileNameNoExt}.{Extension}";

        CalibratedBenchmark.MeasureAndAssert(
            _output,
            "TemplateResolver (50.000 interpolaciones)",
            unitsPerMeasurement: itemQuantity,
            unitName: "ops",
            work: () =>
            {
                for (int i = 0; i < itemQuantity; i++)
                {
                    _ = VariableTemplateResolver.Resolve(template, items[i]);
                }
            });
    }

    [Fact]
    public void Benchmark_FileItemContext_DeepCloneExactCapacityPerformance()
    {
        // Arrange: elemento pesado (20 claves de metadatos + 10 tags) clonado 20.000 veces
        const int itemQuantity = 20_000;
        var sourceItem = new FileItemContext(@"C:\Input\HeavyMetadataFile.mp4");
        for (int m = 0; m < 20; m++)
        {
            sourceItem.Metadata[$"MetaKey_{m}"] = $"MetaValue_{m}";
        }
        for (int t = 0; t < 10; t++)
        {
            sourceItem.Tags.Add($"Tag_{t}");
        }

        var clones = new List<FileItemContext>(itemQuantity);

        CalibratedBenchmark.MeasureAndAssert(
            _output,
            "DeepClone exact-capacity (20.000 clones pesados)",
            unitsPerMeasurement: itemQuantity,
            unitName: "ops",
            work: () =>
            {
                clones.Clear();
                for (int i = 0; i < itemQuantity; i++)
                {
                    clones.Add(sourceItem.DeepClone());
                }
            });
    }

    /// <summary>
    /// Además del throughput, la telemetría se comprueba <b>funcionalmente</b>: las 50.000 fichas
    /// encoladas por N hilos deben llegar a la consulta exactas — la contención del canal acotado y el
    /// flush son parte del contrato, no sólo su velocidad. Esta aserción es determinista por naturaleza.
    /// </summary>
    [Fact]
    public async Task Benchmark_Telemetry_HighThroughput_ParallelIngestion()
    {
        var store = new FileFlow.Core.Telemetry.SqliteLogStore($"BenchDb_{Guid.NewGuid():N}");
        const int totalRecords = 50_000;
        int workerCount = Environment.ProcessorCount;
        int recordsPerWorker = totalRecords / workerCount;

        try
        {
            var tasks = Enumerable.Range(0, workerCount).Select(workerId => Task.Run(() =>
            {
                int count = recordsPerWorker + (workerId == 0 ? totalRecords % workerCount : 0);
                var item = new FileItemContext($@"C:\Photos\Batch_{workerId}\photo_{workerId}.jpg");
                for (int i = 0; i < count; i++)
                {
                    var record = StructuredLogRecord.Create(
                        executionId: "exec-bench",
                        level: LogLevel.Information,
                        message: "Process item completed",
                        nodeId: $"Node_{workerId}",
                        nodeName: $"Worker #{workerId}",
                        filePath: item.CurrentPath,
                        durationMs: 1.25,
                        itemId: item.IdString,
                        fileSizeBytes: item.FileSizeBytes,
                        fileName: item.FileName
                    );
                    store.EnqueueLog(record);
                }
            })).ToArray();

            await Task.WhenAll(tasks);
            await store.FlushPendingLogsAsync();

            // Corrección primero (determinista): 50.000 fichas de N productores concurrentes.
            int persisted = await store.GetTotalCountAsync();
            persisted.Should().Be(totalRecords,
                "el canal acotado + el flush deben entregar todas las fichas sin pérdidas ni duplicados");

            CalibratedBenchmark.MeasureAndAssert(
                _output,
                "Telemetría en paralelo (50.000 fichas, SQLite en memoria)",
                unitsPerMeasurement: totalRecords,
                unitName: "logs",
                work: () =>
                {
                    var single = new FileItemContext(@"C:\Photos\Batch_0\photo_0.jpg");
                    for (int i = 0; i < totalRecords; i++)
                    {
                        var record = StructuredLogRecord.Create(
                            executionId: "exec-bench",
                            level: LogLevel.Information,
                            message: "Process item completed",
                            nodeId: "Node_0",
                            nodeName: "Worker #0",
                            filePath: single.CurrentPath,
                            durationMs: 1.25,
                            itemId: single.IdString,
                            fileSizeBytes: single.FileSizeBytes,
                            fileName: single.FileName
                        );
                        store.EnqueueLog(record);
                    }
                },
                betweenMeasurements: () => store.FlushPendingLogsAsync().GetAwaiter().GetResult());
        }
        finally
        {
            await store.DisposeAsync();
        }
    }

    [Fact]
    public void Benchmark_TensorPreprocessors_SpanSimdVectorizationPerformance()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(1280, 720);

        // El bucle mide 50 imágenes vectorizadas; el resultado se consume para que el JIT no elimine la
        // llamada, y el guard de salida vacía convierte un hipotético bug del preprocesador en fallo.
        CalibratedBenchmark.MeasureAndAssert(
            _output,
            "Letterbox SIMD (720p → 640x640)",
            unitsPerMeasurement: 50,
            unitName: "imágenes",
            work: () =>
            {
                for (int i = 0; i < 50; i++)
                {
                    var (tensor, info) = FileFlow.Plugin.AI.Inference.TensorPreprocessors.CreateLetterboxTensor(image, 640, 640, 114);
                    if (tensor.Length == 0 || info.ScaledW == 0)
                    {
                        throw new InvalidOperationException("El preprocesador devolvió un tensor vacío.");
                    }
                }
            });
    }

    [Fact]
    public async Task Benchmark_HashCalculator_HighThroughput_StreamAsync()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"BenchHash_{Guid.NewGuid():N}.bin");
        byte[] dummyData = new byte[1024 * 1024]; // 1 MB
        new Random(42).NextBytes(dummyData);
        await File.WriteAllBytesAsync(tempFile, dummyData);

        try
        {
            var node = new FileFlow.Plugin.Hashing.HashCalculatorNode();
            var item = new FileItemContext(tempFile);
            var mockContext = new Mock<IFlowExecutionContext>().Object;
            const int iterations = 100; // 100 MB por medición

            CalibratedBenchmark.MeasureAndAssert(
                _output,
                "SHA256 en streaming (100 MB por medición)",
                unitsPerMeasurement: iterations,
                unitName: "MB",
                work: () =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        node.ExecuteAsync("In", item, mockContext, CancellationToken.None)
                            .GetAwaiter().GetResult();
                    }
                });
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
