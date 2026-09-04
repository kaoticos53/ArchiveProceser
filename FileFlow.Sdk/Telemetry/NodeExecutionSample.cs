namespace FileFlow.Sdk.Telemetry;

/// <summary>
/// Muestra individual e inmutable de ejecución de un nodo con métricas de tiempo, memoria y aceleración.
/// </summary>
public readonly record struct NodeExecutionSample(
    double DurationMs,
    long AllocatedBytes,
    double CpuPercentage,
    bool GpuAccelerated,
    DateTime Timestamp
)
{
    public static NodeExecutionSample Create(double durationMs, long allocatedBytes, double cpuPercentage = 0, bool gpuAccelerated = false) =>
        new(durationMs, Math.Max(0, allocatedBytes), Math.Clamp(cpuPercentage, 0, 100), gpuAccelerated, DateTime.UtcNow);
}
