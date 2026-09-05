using System.Diagnostics;
using System.Windows.Threading;

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

    private List<PerformanceCounter>? _gpuCounters;
    private DateTime _lastGpuScan = DateTime.MinValue;
    private bool _gpuCategoryAvailable = true;

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
        catch
        {
            // Ignore transient performance counter exceptions
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

        double gpuPercent = SampleGpuUsage();

        return new PerformanceMetrics
        {
            WorkingSetBytes = _currentProcess.WorkingSet64,
            CpuPercentage = cpuPercent,
            GpuPercentage = gpuPercent
        };
    }

    private double SampleGpuUsage()
    {
        if (!_gpuCategoryAvailable) return 0;

        try
        {
            var now = DateTime.UtcNow;
            if (_gpuCounters == null || (now - _lastGpuScan).TotalSeconds > 8)
            {
                _lastGpuScan = now;
                RefreshGpuCounters();
            }

            if (_gpuCounters == null || _gpuCounters.Count == 0)
                return 0;

            float totalGpu = 0;
            foreach (var counter in _gpuCounters)
            {
                try
                {
                    totalGpu += counter.NextValue();
                }
                catch
                {
                    // Instance may have closed
                }
            }

            return Math.Clamp((double)totalGpu, 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private void RefreshGpuCounters()
    {
        if (_gpuCounters != null)
        {
            foreach (var c in _gpuCounters)
            {
                try { c.Dispose(); } catch { }
            }
            _gpuCounters.Clear();
        }
        else
        {
            _gpuCounters = new List<PerformanceCounter>();
        }

        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
            {
                _gpuCategoryAvailable = false;
                return;
            }

            var category = new PerformanceCounterCategory("GPU Engine");
            string pidPrefix = $"pid_{_currentProcess.Id}_";
            var instanceNames = category.GetInstanceNames();

            foreach (var name in instanceNames)
            {
                if (name.StartsWith(pidPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, readOnly: true);
                        counter.NextValue(); // Initial sample
                        _gpuCounters.Add(counter);
                    }
                    catch { }
                }
            }
        }
        catch
        {
            _gpuCategoryAvailable = false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer.Stop();
            _currentProcess.Dispose();

            if (_gpuCounters != null)
            {
                foreach (var c in _gpuCounters)
                {
                    try { c.Dispose(); } catch { }
                }
                _gpuCounters.Clear();
                _gpuCounters = null;
            }

            _disposed = true;
        }
    }
}
