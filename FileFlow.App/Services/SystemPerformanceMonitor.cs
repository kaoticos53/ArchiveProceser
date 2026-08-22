using System.Diagnostics;
using System.Windows.Threading;

namespace FileFlow.App.Services;

public class PerformanceMetrics
{
    public long WorkingSetBytes { get; set; }
    public double CpuPercentage { get; set; }

    public string RamFormatted
    {
        get
        {
            double mb = WorkingSetBytes / (1024.0 * 1024.0);
            return mb >= 1024 ? $"{mb / 1024.0:F2} GB" : $"{mb:F1} MB";
        }
    }

    public string CpuFormatted => $"{CpuPercentage:F0}%";
}

public class SystemPerformanceMonitor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Process _currentProcess;
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;
    private bool _disposed;

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

    private void OnTimerTick(object? sender, EventArgs e)
    {
        try
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

            var metrics = new PerformanceMetrics
            {
                WorkingSetBytes = _currentProcess.WorkingSet64,
                CpuPercentage = cpuPercent
            };

            PerformanceUpdated?.Invoke(metrics);
        }
        catch
        {
            // Ignore transient performance counter exceptions
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer.Stop();
            _currentProcess.Dispose();
            _disposed = true;
        }
    }
}
