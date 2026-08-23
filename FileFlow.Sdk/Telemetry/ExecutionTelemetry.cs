namespace FileFlow.Sdk.Telemetry;

/// <summary>
/// Instantánea inmutable O(1) de métricas de telemetría de ejecución.
/// Permite desacoplar el motor de ejecución en segundo plano de la frecuencia de renderizado de la UI.
/// </summary>
public readonly record struct TelemetrySnapshot(
    long ProcessedItems,
    long TotalItems,
    long ProcessedBytes,
    double ItemsPerSecond,
    double MegabytesPerSecond,
    double Percentage,
    TimeSpan Elapsed,
    string StatusMessage
)
{
    public static TelemetrySnapshot Empty => new(
        ProcessedItems: 0,
        TotalItems: 0,
        ProcessedBytes: 0,
        ItemsPerSecond: 0,
        MegabytesPerSecond: 0,
        Percentage: 0,
        Elapsed: TimeSpan.Zero,
        StatusMessage: string.Empty
    );
}
