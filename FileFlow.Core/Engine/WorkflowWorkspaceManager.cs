using System.Collections.Concurrent;
using System.IO;
using FileFlow.Sdk.Storage;

namespace FileFlow.Core.Engine;

/// <summary>
/// Gestor seguro y concurrente del espacio de trabajo temporal para una ejecución de flujo activa.
/// Garantiza el aislamiento por sesión y la liberación determinista de espacio en disco.
/// </summary>
public sealed class WorkflowWorkspaceManager : ITempWorkspaceManager, IDisposable, IAsyncDisposable
{
    private readonly string _executionTempDirectory;
    private readonly ConcurrentBag<string> _registeredFiles = [];
    private readonly ConcurrentBag<string> _registeredDirectories = [];
    private readonly Lock _lock = new();
    private bool _isCleanedUp;

    public WorkflowWorkspaceManager(string executionId, string? baseTempDirectory = null)
    {
        string baseDir = !string.IsNullOrWhiteSpace(baseTempDirectory)
            ? baseTempDirectory
            : AppPaths.RunsDirectory;

        _executionTempDirectory = Path.Combine(baseDir, executionId);
    }

    public string ExecutionTempDirectory
    {
        get
        {
            if (!Directory.Exists(_executionTempDirectory))
            {
                try
                {
                    Directory.CreateDirectory(_executionTempDirectory);
                }
                catch { }
            }
            return _executionTempDirectory;
        }
    }

    public string CreateSubdirectory(string purpose)
    {
        string cleanPurpose = string.IsNullOrWhiteSpace(purpose) ? "misc" : purpose.Trim();
        string subDir = Path.Combine(ExecutionTempDirectory, cleanPurpose);
        try
        {
            if (!Directory.Exists(subDir))
            {
                Directory.CreateDirectory(subDir);
            }
        }
        catch { }

        RegisterTemporaryDirectory(subDir);
        return subDir;
    }

    public void RegisterTemporaryFile(string filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            _registeredFiles.Add(filePath);
        }
    }

    public void RegisterTemporaryDirectory(string directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            _registeredDirectories.Add(directoryPath);
        }
    }

    public Task<long> CleanupExecutionWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isCleanedUp) return Task.FromResult(0L);
            _isCleanedUp = true;
        }

        long totalBytesFreed = 0;

        // 1. Eliminar archivos temporales sueltos registrados
        foreach (var file in _registeredFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (File.Exists(file))
                {
                    long size = new FileInfo(file).Length;
                    File.Delete(file);
                    totalBytesFreed += size;
                }
            }
            catch { }
        }

        // 2. Eliminar directorios temporales registrados (ej. sesiones de FanOut)
        foreach (var dir in _registeredDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    long dirSize = GetDirectorySize(dir);
                    Directory.Delete(dir, recursive: true);
                    totalBytesFreed += dirSize;
                }
            }
            catch { }
        }

        // 3. Eliminar el directorio raíz de la ejecución (Runs/{ExecutionId}/)
        try
        {
            if (Directory.Exists(_executionTempDirectory))
            {
                long remainingSize = GetDirectorySize(_executionTempDirectory);
                Directory.Delete(_executionTempDirectory, recursive: true);
                totalBytesFreed += remainingSize;
            }
        }
        catch { }

        return Task.FromResult(totalBytesFreed);
    }

    private static long GetDirectorySize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;
        try
        {
            return Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; } catch { return 0L; }
                });
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        CleanupExecutionWorkspaceAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupExecutionWorkspaceAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
