using System.Diagnostics;
using System.IO;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace FileFlow.Tests.Performance;

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
        // Arrange
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

        // GC Baseline Metrics
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        int initialGen0 = GC.CollectionCount(0);
        int initialGen1 = GC.CollectionCount(1);
        int initialGen2 = GC.CollectionCount(2);
        long initialMemory = GC.GetTotalMemory(forceFullCollection: false);

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < itemQuantity; i++)
        {
            _ = VariableTemplateResolver.Resolve(template, items[i]);
        }
        sw.Stop();

        long finalMemory = GC.GetTotalMemory(forceFullCollection: false);
        int gen0Collections = GC.CollectionCount(0) - initialGen0;
        int gen1Collections = GC.CollectionCount(1) - initialGen1;
        int gen2Collections = GC.CollectionCount(2) - initialGen2;

        double opsPerSecond = (itemQuantity / (double)sw.ElapsedMilliseconds) * 1000.0;

        _output.WriteLine($"=== TEMPLATE ENGINE BENCHMARK ===");
        _output.WriteLine($"Items Processed: {itemQuantity:N0}");
        _output.WriteLine($"Total Time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Throughput: {opsPerSecond:N0} ops/sec");
        _output.WriteLine($"GC Collections -> Gen0: {gen0Collections}, Gen1: {gen1Collections}, Gen2: {gen2Collections}");
        _output.WriteLine($"Memory Delta: {(finalMemory - initialMemory) / 1024.0 / 1024.0:F2} MB");

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(2500, "50,000 template interpolations should process in under 2.5 seconds");
        opsPerSecond.Should().BeGreaterThan(15000, "Throughput should exceed 15,000 ops/sec");
    }

    [Fact]
    public void Benchmark_FileItemContext_DeepCloneExactCapacityPerformance()
    {
        // Arrange
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

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < itemQuantity; i++)
        {
            _ = sourceItem.DeepClone();
        }
        sw.Stop();

        double opsPerSecond = (itemQuantity / (double)sw.ElapsedMilliseconds) * 1000.0;

        _output.WriteLine($"=== DEEP CLONE FAN-OUT BENCHMARK ===");
        _output.WriteLine($"Clones Created: {itemQuantity:N0}");
        _output.WriteLine($"Total Time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Throughput: {opsPerSecond:N0} clones/sec");

        // Assert
        sw.ElapsedMilliseconds.Should().BeLessThan(1000, "20,000 deep clones with heavy metadata should take under 1 second");
    }

    [Fact]
    public async Task Benchmark_Telemetry_HighThroughput_ParallelIngestion()
    {
        // Arrange
        var store = new FileFlow.Core.Telemetry.SqliteLogStore($"BenchDb_{Guid.NewGuid():N}");
        const int totalRecords = 50_000;
        int workerCount = Environment.ProcessorCount;
        int recordsPerWorker = totalRecords / workerCount;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        int initialGen0 = GC.CollectionCount(0);
        long initialMemory = GC.GetTotalMemory(forceFullCollection: false);

        // Act
        var sw = Stopwatch.StartNew();

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
        sw.Stop();

        long finalMemory = GC.GetTotalMemory(forceFullCollection: false);
        int gen0Collections = GC.CollectionCount(0) - initialGen0;
        double opsPerSecond = (totalRecords / (double)sw.ElapsedMilliseconds) * 1000.0;

        _output.WriteLine($"=== TELEMETRY HIGH-THROUGHPUT BENCHMARK ===");
        _output.WriteLine($"Records Processed: {totalRecords:N0} across {workerCount} CPU cores");
        _output.WriteLine($"Total Time (Enqueue + In-Memory SQLite Flush): {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Throughput: {opsPerSecond:N0} logs/sec");
        _output.WriteLine($"Gen0 Collections: {gen0Collections}");
        _output.WriteLine($"Memory Delta: {(finalMemory - initialMemory) / 1024.0 / 1024.0:F2} MB");

        await store.DisposeAsync();
    }

    [Fact]
    public void Benchmark_TensorPreprocessors_SpanSimdVectorizationPerformance()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(1280, 720);
        const int iterations = 50;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var (tensor, info) = FileFlow.Plugin.AI.Inference.TensorPreprocessors.CreateLetterboxTensor(image, 640, 640, 114);
        }
        sw.Stop();

        double msPerImage = sw.ElapsedMilliseconds / (double)iterations;
        _output.WriteLine($"=== TENSOR PREPROCESSOR SIMD BENCHMARK ===");
        _output.WriteLine($"720p Images Letterboxed to 640x640: {iterations}");
        _output.WriteLine($"Total Time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average Time Per Image: {msPerImage:F2} ms");

        msPerImage.Should().BeLessThan(100.0, "Vectorized letterboxing on 720p should process fast in under 100 ms per image in test suite runner");
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
            const int iterations = 100; // 100 MB total

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                await node.ExecuteAsync("In", item, mockContext, CancellationToken.None);
            }
            sw.Stop();

            double totalMb = iterations * 1.0;
            double mbPerSec = (totalMb / sw.ElapsedMilliseconds) * 1000.0;

            _output.WriteLine($"=== HASH STREAMING I/O BENCHMARK ===");
            _output.WriteLine($"Total Data Hashed: {totalMb:N0} MB across {iterations} iterations");
            _output.WriteLine($"Total Time: {sw.ElapsedMilliseconds} ms");
            _output.WriteLine($"Throughput: {mbPerSec:F2} MB/sec");

            mbPerSec.Should().BeGreaterThan(50.0, "SHA256 streaming with SequentialScan should exceed 50 MB/sec in local benchmarks");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
