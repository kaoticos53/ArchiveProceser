using System.Collections.Concurrent;
using System.Diagnostics;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.Core.Engine;

/// <summary>
/// Acumulador atómico de métricas de telemetría y cálculo de snapshots de rendimiento en tiempo real para el motor DAG.
/// Incluye tracking granular por nodo para el cálculo del mapa de calor de latencia y detección de cuellos de botella.
/// </summary>
public sealed class WorkflowTelemetryTracker
{
    private readonly Stopwatch _stopwatch = new();
    private long _processedItemsCount;
    private long _totalItemsCount;
    private long _expectedTotalItems;
    private long _sourceItemsEmitted;
    private long _completedFilesCount;
    private long _processedBytesCount;
    private string _lastCustomStatusMessage = string.Empty;

    private readonly ConcurrentDictionary<string, (long count, double totalMs)> _nodeStats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _nodeStatsLock = new();

    public void Reset()
    {
        Interlocked.Exchange(ref _processedItemsCount, 0);
        Interlocked.Exchange(ref _totalItemsCount, 0);
        Interlocked.Exchange(ref _expectedTotalItems, 0);
        Interlocked.Exchange(ref _sourceItemsEmitted, 0);
        Interlocked.Exchange(ref _completedFilesCount, 0);
        Interlocked.Exchange(ref _processedBytesCount, 0);
        Volatile.Write(ref _lastCustomStatusMessage, string.Empty);

        lock (_nodeStatsLock)
        {
            _nodeStats.Clear();
        }

        _stopwatch.Restart();
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public void SetTotalExpectedItems(long totalExpectedItems)
    {
        Interlocked.Exchange(ref _expectedTotalItems, totalExpectedItems);
    }

    public void SetCustomStatusMessage(string message)
    {
        Volatile.Write(ref _lastCustomStatusMessage, message);
    }

    public void IncrementSourceItemsEmitted()
    {
        Interlocked.Increment(ref _sourceItemsEmitted);
    }

    public long IncrementCompletedFiles()
    {
        return Interlocked.Increment(ref _completedFilesCount);
    }

    public void IncrementProcessedItems()
    {
        Interlocked.Increment(ref _processedItemsCount);
    }

    public void AddTotalItems(int count)
    {
        Interlocked.Add(ref _totalItemsCount, count);
    }

    public void AddProcessedBytes(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _processedBytesCount, bytes);
        }
    }

    public void RecordNodeExecution(string nodeId, double durationMs)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;

        _nodeStats.AddOrUpdate(
            nodeId,
            (1, Math.Max(0.0, durationMs)),
            (_, existing) => (existing.count + 1, existing.totalMs + Math.Max(0.0, durationMs)));
    }

    public long ExpectedTotalItems => Volatile.Read(ref _expectedTotalItems);
    public long CompletedFilesCount => Volatile.Read(ref _completedFilesCount);
    public long SourceItemsEmitted => Volatile.Read(ref _sourceItemsEmitted);
    public long ProcessedItemsCount => Volatile.Read(ref _processedItemsCount);

    public IReadOnlyDictionary<string, NodeTelemetryStats> GetNodeStats()
    {
        var rawStats = _nodeStats.ToArray();
        if (rawStats.Length == 0)
        {
            return new Dictionary<string, NodeTelemetryStats>();
        }

        double grandTotalMs = rawStats.Sum(s => s.Value.totalMs);
        string? bottleneckNodeId = null;
        double maxNodeTimeMs = 0.0;

        foreach (var (nodeId, (count, totalMs)) in rawStats)
        {
            if (totalMs > maxNodeTimeMs)
            {
                maxNodeTimeMs = totalMs;
                bottleneckNodeId = nodeId;
            }
        }

        var result = new Dictionary<string, NodeTelemetryStats>(StringComparer.OrdinalIgnoreCase);

        foreach (var (nodeId, (count, totalMs)) in rawStats)
        {
            double avgMs = count > 0 ? totalMs / count : 0.0;
            double ratio = grandTotalMs > 0.0 ? totalMs / grandTotalMs : 0.0;
            bool isBottleneck = rawStats.Length > 1 && nodeId.Equals(bottleneckNodeId, StringComparison.OrdinalIgnoreCase) && ratio >= 0.35 && totalMs >= 50.0;

            LatencyHeatLevel heatLevel;
            if (isBottleneck)
            {
                heatLevel = LatencyHeatLevel.High;
            }
            else if (avgMs < 50.0)
            {
                heatLevel = LatencyHeatLevel.Low;
            }
            else if (avgMs < 500.0)
            {
                heatLevel = LatencyHeatLevel.Medium;
            }
            else
            {
                heatLevel = LatencyHeatLevel.High;
            }

            result[nodeId] = new NodeTelemetryStats(
                NodeId: nodeId,
                ProcessedCount: count,
                TotalTimeMs: totalMs,
                AverageTimeMs: avgMs,
                RelativeBottleneckRatio: ratio,
                IsBottleneck: isBottleneck,
                HeatLevel: heatLevel
            );
        }

        return result;
    }

    public TelemetrySnapshot GetSnapshot(bool isRunning)
    {
        long doneElements = Volatile.Read(ref _completedFilesCount);
        long emittedElements = Volatile.Read(ref _sourceItemsEmitted);
        long expectedElements = Volatile.Read(ref _expectedTotalItems);
        long processedOps = Volatile.Read(ref _processedItemsCount);
        long totalOps = Volatile.Read(ref _totalItemsCount);

        long effectiveTotal = Math.Max(expectedElements, Math.Max(doneElements, emittedElements));
        long effectiveProcessed = doneElements > 0 ? doneElements : emittedElements;

        if (effectiveTotal == 0)
        {
            effectiveTotal = totalOps;
            effectiveProcessed = processedOps;
        }

        long bytes = Volatile.Read(ref _processedBytesCount);
        TimeSpan elapsed = _stopwatch.Elapsed;
        double elapsedSec = elapsed.TotalSeconds;

        double itemsPerSec = elapsedSec > 0.05 ? effectiveProcessed / elapsedSec : 0.0;
        double mbPerSec = elapsedSec > 0.05 ? (bytes / (1024.0 * 1024.0)) / elapsedSec : 0.0;

        double pct = 0.0;
        if (effectiveTotal > 0)
        {
            pct = (double)effectiveProcessed / effectiveTotal * 100.0;
            if (isRunning && pct >= 100.0)
            {
                pct = 99.0;
            }
            else if (pct > 100.0)
            {
                pct = 100.0;
            }
        }

        string status;
        if (!isRunning && effectiveProcessed > 0)
        {
            status = $"🟢 Completado: {effectiveProcessed:N0}/{effectiveProcessed:N0} elementos (100%)";
        }
        else if (effectiveTotal > 0)
        {
            status = $"⚡ Procesando: {effectiveProcessed:N0}/{effectiveTotal:N0} elementos ({pct:F0}%) • {itemsPerSec:F0} ops/s";
        }
        else if (effectiveProcessed > 0)
        {
            status = $"⚡ Procesando: {effectiveProcessed:N0} elementos • {itemsPerSec:F0} ops/s";
        }
        else
        {
            string customStatus = Volatile.Read(ref _lastCustomStatusMessage);
            status = !string.IsNullOrWhiteSpace(customStatus) ? customStatus : "Ejecutando...";
        }

        return new TelemetrySnapshot(
            ProcessedItems: effectiveProcessed,
            TotalItems: effectiveTotal,
            ProcessedBytes: bytes,
            ItemsPerSecond: itemsPerSec,
            MegabytesPerSecond: mbPerSec,
            Percentage: pct,
            Elapsed: elapsed,
            StatusMessage: status
        );
    }
}
