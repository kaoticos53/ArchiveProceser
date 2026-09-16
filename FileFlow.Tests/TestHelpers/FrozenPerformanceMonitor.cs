using System;
using FileFlow.App.Services;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Monitor de rendimiento que no muestrea nada.
///
/// El monitor real arranca un <c>DispatcherTimer</c> de un segundo que publica la RAM, la CPU y la GPU del
/// proceso. En una captura eso es ruido puro: los valores cambiarían entre dos ejecuciones del mismo test y
/// la línea base se volvería inestable. Con este doble la barra de estado muestra cifras fijas, y el test que
/// quiera ejercitar la ruta de actualización puede publicar métricas a mano.
/// </summary>
public sealed class FrozenPerformanceMonitor : ISystemPerformanceMonitor
{
    public event Action<PerformanceMetrics>? PerformanceUpdated;

    public bool IsDisposed { get; private set; }

    /// <summary>Publica métricas de forma explícita (sólo para el test que quiera simular una actualización).</summary>
    public void Publish(PerformanceMetrics metrics) => PerformanceUpdated?.Invoke(metrics);

    public void Dispose() => IsDisposed = true;
}
