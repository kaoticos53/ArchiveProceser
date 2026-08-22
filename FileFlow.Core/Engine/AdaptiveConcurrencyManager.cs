using System.Collections.Concurrent;

namespace FileFlow.Core.Engine;

/// <summary>
/// Gestiona la concurrencia adaptativa particionando semáforos por disco/volumen físico (I/O-bound) y por procesadores lógicos (CPU-bound).
/// </summary>
public sealed class AdaptiveConcurrencyManager : IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _driveSemaphores = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _cpuSemaphore;
    private readonly int _maxConcurrentPerDrive;
    private bool _disposed;

    public AdaptiveConcurrencyManager(int? maxCpuThreads = null, int maxConcurrentPerDrive = 4)
    {
        int cpuThreads = maxCpuThreads ?? Math.Max(1, Environment.ProcessorCount);
        _cpuSemaphore = new SemaphoreSlim(cpuThreads, cpuThreads);
        _maxConcurrentPerDrive = Math.Max(1, maxConcurrentPerDrive);
    }

    /// <summary>
    /// Adquiere un slot de concurrencia para una operación I/O ligada a una ruta de disco específica.
    /// </summary>
    public async ValueTask<IDisposable> AcquireIoLockAsync(string? targetPath, CancellationToken ct = default)
    {
        string driveKey = GetDriveKey(targetPath);
        var sem = _driveSemaphores.GetOrAdd(driveKey, _ => new SemaphoreSlim(_maxConcurrentPerDrive, _maxConcurrentPerDrive));

        await sem.WaitAsync(ct).ConfigureAwait(false);
        return new Releaser(sem);
    }

    /// <summary>
    /// Adquiere un slot de concurrencia para una tarea intensiva en CPU (hashing, compresión, imágenes).
    /// </summary>
    public async ValueTask<IDisposable> AcquireCpuLockAsync(CancellationToken ct = default)
    {
        await _cpuSemaphore.WaitAsync(ct).ConfigureAwait(false);
        return new Releaser(_cpuSemaphore);
    }

    private static string GetDriveKey(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "DEFAULT";
        try
        {
            string root = Path.GetPathRoot(path) ?? string.Empty;
            return string.IsNullOrEmpty(root) ? "DEFAULT" : root.ToUpperInvariant();
        }
        catch
        {
            return "DEFAULT";
        }
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _sem = semaphore;

        public void Dispose()
        {
            var sem = Interlocked.Exchange(ref _sem, null);
            sem?.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cpuSemaphore.Dispose();
        foreach (var sem in _driveSemaphores.Values)
        {
            sem.Dispose();
        }
        _driveSemaphores.Clear();
    }
}
