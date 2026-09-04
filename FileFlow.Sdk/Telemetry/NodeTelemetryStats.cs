namespace FileFlow.Sdk.Telemetry;

/// <summary>
/// Nivel de calor visual de latencia para el mapa de calor de cuellos de botella (Bottleneck Heatmap).
/// </summary>
public enum LatencyHeatLevel
{
    None,
    Low,     // Verde (< 50 ms)
    Medium,  // Ámbar (50 ms - 500 ms)
    High     // Rojo neón / Cuello de botella (> 500 ms o nodo más pesado)
}

/// <summary>
/// Instantánea de métricas de telemetría y rendimiento granular para un nodo del flujo DAG.
/// </summary>
public readonly record struct NodeTelemetryStats(
    string NodeId,
    long ProcessedCount,
    double TotalTimeMs,
    double AverageTimeMs,
    double RelativeBottleneckRatio,
    bool IsBottleneck,
    LatencyHeatLevel HeatLevel,
    double RollingAvgDurationMs = 0.0,
    long RollingAvgAllocatedBytes = 0,
    long PeakAllocatedBytes = 0,
    double AvgCpuPercentage = 0.0,
    bool IsGpuAccelerated = false,
    IReadOnlyList<NodeExecutionSample>? RecentSamples = null
)
{
    public static NodeTelemetryStats Empty(string nodeId) => new(
        NodeId: nodeId,
        ProcessedCount: 0,
        TotalTimeMs: 0.0,
        AverageTimeMs: 0.0,
        RelativeBottleneckRatio: 0.0,
        IsBottleneck: false,
        HeatLevel: LatencyHeatLevel.None,
        RollingAvgDurationMs: 0.0,
        RollingAvgAllocatedBytes: 0,
        PeakAllocatedBytes: 0,
        AvgCpuPercentage: 0.0,
        IsGpuAccelerated: false,
        RecentSamples: Array.Empty<NodeExecutionSample>()
    );
}
