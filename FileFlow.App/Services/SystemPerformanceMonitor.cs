using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

public class PerformanceMetrics
{
    public long WorkingSetBytes { get; set; }
    public double CpuPercentage { get; set; }
    public double GpuPercentage { get; set; }

    public string RamFormatted
    {
        get
        {
            double mb = WorkingSetBytes / (1024.0 * 1024.0);
            return mb >= 1024 ? $"{mb / 1024.0:F2} GB" : $"{mb:F1} MB";
        }
    }

    public string CpuFormatted => $"{CpuPercentage:F0}%";
    public string GpuFormatted => $"{GpuPercentage:F0}%";
}

public class SystemPerformanceMonitor : ISystemPerformanceMonitor
{
    /// <summary>El periodo del latido. Público para que la prueba de cadencia mida <i>este</i> número.</summary>
    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    /// <summary>Nombre del latido en el registro: con él se busca, se mide su cadencia y se sabe cuál falló.</summary>
    public const string SampleBeat = "performance-sample";

    private readonly IHeartbeat _sampleBeat;
    private readonly Process _currentProcess;
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;
    private bool _disposed;
    private bool _isSampling;

    public event Action<PerformanceMetrics>? PerformanceUpdated;

    /// <summary>
    /// El latido se <b>declara</b> en el registro, que pone la fontanería: reloj inyectado (no un
    /// <c>DispatcherTimer</c>, cuya cadencia no se puede medir sin esperarla), despacho al hilo de la interfaz y
    /// entrega protegida. Su vencimiento entra así en el inventario de trabajo aplazado por el único sitio del
    /// producto que programa latidos (ver <c>DeferredWorkInventoryGuardTests</c>).
    /// </summary>
    public SystemPerformanceMonitor(TimeProvider? timeProvider = null, IUiDispatcher? uiDispatcher = null,
        IHeartbeatService? heartbeats = null)
    {
        _currentProcess = Process.GetCurrentProcess();
        _lastCpuTime = _currentProcess.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;

        TimeProvider clock = timeProvider ?? TimeProvider.System;
        IUiDispatcher ui = uiDispatcher ?? AvaloniaUiDispatcher.Instance;

        _sampleBeat = (heartbeats ?? new HeartbeatService(clock, ui))
            .Declare(SampleBeat, SampleInterval, () => _ = SampleNowAsync())
            .Start();
    }

    /// <summary>
    /// El <b>latido</b> del muestreo: toma una muestra del proceso y publica las métricas si hay alguien
    /// escuchando.
    ///
    /// <para><b>Público y con <see cref="Task"/> para poder ejercitarlo desde las pruebas</b>, como el paso del
    /// barrido de la splash (hito 169): el temporizador sólo corre en la aplicación, así que sin una entrada
    /// alcanzable el camino del tick —con su guarda de reentrada, su captura de excepciones transitorias y su
    /// comprobación de desecho— no se ejecutaba nunca en el suite. <see cref="OnTimerTick"/> sólo lo reenvía.</para>
    ///
    /// <para>La guarda se levanta <b>antes</b> del primer <c>await</c> a propósito: dos ticks solapados no
    /// pueden producir dos muestras (la segunda entra cuando la primera aún no ha publicado), y una excepción
    /// transitoria del proceso se registra y se traga en lugar de tumbar la aplicación —es un latido, no una
    /// tarea de la que dependa nada—.</para>
    /// </summary>
    public async Task SampleNowAsync()
    {
        if (_isSampling || _disposed) return;
        _isSampling = true;

        try
        {
            var metrics = await Task.Run(() => SampleMetrics()).ConfigureAwait(true);
            if (!_disposed)
            {
                PerformanceUpdated?.Invoke(metrics);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemPerformanceMonitor] Transient sampling exception: {ex.Message}");
        }
        finally
        {
            _isSampling = false;
        }
    }

    private PerformanceMetrics SampleMetrics()
    {
        _currentProcess.Refresh();
        var now = DateTime.UtcNow;
        var cpuTime = _currentProcess.TotalProcessorTime;

        var timeDelta = (now - _lastSampleTime).TotalMilliseconds;
        var cpuDelta = (cpuTime - _lastCpuTime).TotalMilliseconds;

        _lastSampleTime = now;
        _lastCpuTime = cpuTime;

        double cpuPercent = 0;
        if (timeDelta > 0)
        {
            cpuPercent = (cpuDelta / (timeDelta * Environment.ProcessorCount)) * 100.0;
            cpuPercent = Math.Clamp(cpuPercent, 0, 100);
        }

        return new PerformanceMetrics
        {
            WorkingSetBytes = _currentProcess.WorkingSet64,
            CpuPercentage = cpuPercent,
            GpuPercentage = 0
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _sampleBeat.Dispose();
            _currentProcess.Dispose();
            _disposed = true;
        }
    }
}
