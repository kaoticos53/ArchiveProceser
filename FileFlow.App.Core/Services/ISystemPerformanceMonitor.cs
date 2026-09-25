namespace FileFlow.App.Services;

/// <summary>
/// Contrato para la supervisión y muestreo reactivo de telemetría de rendimiento del sistema (CPU, RAM, GPU).
/// </summary>
public interface ISystemPerformanceMonitor : IDisposable
{
    /// <summary>
    /// Evento emitido periódicamente con las métricas de rendimiento actualizadas del proceso.
    /// </summary>
    event Action<PerformanceMetrics>? PerformanceUpdated;
}
