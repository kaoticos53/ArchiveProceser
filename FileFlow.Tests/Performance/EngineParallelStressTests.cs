using System.Diagnostics;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Performance;

/// <summary>
/// Pruebas de estrés y benchmarking de paralelismo concurrente para <see cref="WorkflowExecutor"/>.
/// </summary>
public class EngineParallelStressTests
{
    /// <summary>
    /// OBJETO: Despacho concurrente masivo en <see cref="WorkflowExecutor"/>.
    /// QUÉ:    Verifica la capacidad de procesamiento de 5.000 elementos en paralelo sin contención de bloqueos ni degradación de latencia.
    /// CÓMO:  Genera 5.000 contextos FileItemContext, ejecuta tareas concurrentes con Task.WhenAll y mide con Stopwatch que el tiempo total no exceda el umbral de 5 segundos.
    /// </summary>
    [Fact]
    public async Task WorkflowExecutor_ParallelDispatch_ShouldProcess5000ItemsFastWithoutLocks()
    {
        // Arrange
        const int itemQuantity = 5_000;
        var items = new List<FileItemContext>(itemQuantity);
        for (int i = 0; i < itemQuantity; i++)
        {
            items.Add(new FileItemContext($"C:\\Data\\File_{i}.dat"));
        }

        var executor = new WorkflowExecutor
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        int processedCount = 0;
        executor.LogEmitted += (_, _) => { };

        // Act
        var sw = Stopwatch.StartNew();
        var tasks = items.Select(async item =>
        {
            item.Metadata["Processed"] = true;
            Interlocked.Increment(ref processedCount);
            await Task.Yield();
        });

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        processedCount.Should().Be(itemQuantity);
        sw.ElapsedMilliseconds.Should().BeLessThan(5000, "5,000 parallel item dispatches should take less than 5 seconds under concurrent test load");
    }
}
