using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;

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
    private readonly DispatcherTimer _timer;
    private readonly Process _currentProcess;
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;
    private bool _disposed;
    private bool _isSampling;

    public event Action<PerformanceMetrics>? PerformanceUpdated;

    public SystemPerformanceMonitor()
    {
        _currentProcess = Process.GetCurrentProcess();
        _lastCpuTime = _currentProcess.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
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
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _currentProcess.Dispose();
            _disposed = true;
        }
    }
}
