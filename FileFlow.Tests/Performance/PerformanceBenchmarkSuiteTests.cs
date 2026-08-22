using System.Diagnostics;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FileFlow.Sdk.TemplateEngine;
using FluentAssertions;
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
}
