using System.IO;
using FileFlow.Core.Platform;
using FileFlow.Sdk;
using FileFlow.Sdk.Platform;
using FileFlow.Sdk.Storage;

namespace FileFlow.Core.Storage;

/// <summary>
/// Implementación de <see cref="IStorageService"/> sobre el sistema de archivos físico.
/// Integra buffers optimizados para .NET 9, resolución concurrente de colisiones y reciclaje nativo por SO.
/// </summary>
public class PhysicalStorageService : IStorageService
{
    private readonly IOsPlatformService _platform;
    private readonly bool _isDryRun;
    private readonly Action<PlannedAction>? _onRegisterPlannedAction;
    private const int BufferSize = 131072; // 128 KB
    private const long AsyncThreshold = 256 * 1024; // 256 KB

    public PhysicalStorageService(
        IOsPlatformService? platform = null,
        bool isDryRun = false,
        Action<PlannedAction>? onRegisterPlannedAction = null)
    {
        _platform = platform ?? OsPlatformServiceFactory.Instance;
        _isDryRun = isDryRun;
        _onRegisterPlannedAction = onRegisterPlannedAction;
    }

    public ValueTask<bool> FileExistsAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult(!string.IsNullOrWhiteSpace(path) && File.Exists(path));

    public ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult(!string.IsNullOrWhiteSpace(path) && Directory.Exists(path));

    public ValueTask CreateDirectoryAsync(string path, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path) && !_isDryRun)
        {
            Directory.CreateDirectory(path);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = BufferSize,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        };
        Stream stream = new FileStream(path, options);
        return ValueTask.FromResult(stream);
    }

    public ValueTask<Stream> OpenWriteAsync(string path, CancellationToken ct = default)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir) && !_isDryRun)
        {
            Directory.CreateDirectory(dir);
        }

        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = BufferSize,
            Options = FileOptions.Asynchronous
        };
        Stream stream = new FileStream(path, options);
        return ValueTask.FromResult(stream);
    }

    public ValueTask<Stream> OpenAppendAsync(string path, CancellationToken ct = default)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir) && !_isDryRun)
        {
            Directory.CreateDirectory(dir);
        }

        var options = new FileStreamOptions
        {
            Mode = FileMode.Append,
            Access = FileAccess.Write,
            Share = FileShare.ReadWrite,
            BufferSize = BufferSize,
            Options = FileOptions.Asynchronous
        };
        Stream stream = new FileStream(path, options);
        return ValueTask.FromResult(stream);
    }

    public async ValueTask<StorageOperationResult> CopyAsync(
        string sourcePath,
        string targetPath,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
        {
            return StorageOperationResult.Failure(sourcePath, targetPath, $"Source file not found: {sourcePath}");
        }

        if (collisionStrategy == StorageCollisionStrategy.Skip && File.Exists(targetPath))
        {
            return StorageOperationResult.Skipped(sourcePath, targetPath);
        }

        string finalTarget = await ResolveCollisionAsync(targetPath, collisionStrategy, ct).ConfigureAwait(false);
        bool wasCollision = !string.Equals(finalTarget, targetPath, StringComparison.OrdinalIgnoreCase);

        var sourceInfo = new FileInfo(sourcePath);
        long fileSize = sourceInfo.Exists ? sourceInfo.Length : 0;

        if (_isDryRun)
        {
            _onRegisterPlannedAction?.Invoke(new PlannedAction(
                Guid.NewGuid(),
                "PhysicalStorageService",
                "StorageService",
                PlannedOperationType.Copy,
                sourcePath,
                finalTarget,
                $"Copy to {finalTarget}",
                fileSize
            ));
            return StorageOperationResult.Success(sourcePath, finalTarget, fileSize, wasCollision);
        }

        string? dir = Path.GetDirectoryName(finalTarget);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (fileSize > AsyncThreshold)
        {
            await using var sourceStream = await OpenReadAsync(sourcePath, ct).ConfigureAwait(false);
            await using var destStream = await OpenWriteAsync(finalTarget, ct).ConfigureAwait(false);
            await sourceStream.CopyToAsync(destStream, BufferSize, ct).ConfigureAwait(false);
        }
        else
        {
            File.Copy(sourcePath, finalTarget, overwrite: true);
        }

        return StorageOperationResult.Success(sourcePath, finalTarget, fileSize, wasCollision);
    }

    public async ValueTask<StorageOperationResult> MoveAsync(
        string sourcePath,
        string targetPath,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
        {
            return StorageOperationResult.Failure(sourcePath, targetPath, $"Source file not found: {sourcePath}");
        }

        if (collisionStrategy == StorageCollisionStrategy.Skip && File.Exists(targetPath))
        {
            return StorageOperationResult.Skipped(sourcePath, targetPath);
        }

        string finalTarget = await ResolveCollisionAsync(targetPath, collisionStrategy, ct).ConfigureAwait(false);
        bool wasCollision = !string.Equals(finalTarget, targetPath, StringComparison.OrdinalIgnoreCase);

        var sourceInfo = new FileInfo(sourcePath);
        long fileSize = sourceInfo.Exists ? sourceInfo.Length : 0;

        if (_isDryRun)
        {
            _onRegisterPlannedAction?.Invoke(new PlannedAction(
                Guid.NewGuid(),
                "PhysicalStorageService",
                "StorageService",
                PlannedOperationType.Move,
                sourcePath,
                finalTarget,
                $"Move to {finalTarget}",
                fileSize
            ));
            return StorageOperationResult.Success(sourcePath, finalTarget, fileSize, wasCollision);
        }

        string? dir = Path.GetDirectoryName(finalTarget);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(finalTarget) && !string.Equals(sourcePath, finalTarget, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(finalTarget);
        }

        File.Move(sourcePath, finalTarget);
        return StorageOperationResult.Success(sourcePath, finalTarget, fileSize, wasCollision);
    }

    public ValueTask<StorageOperationResult> DeleteAsync(
        string path,
        bool permanent = false,
        CancellationToken ct = default)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return ValueTask.FromResult(StorageOperationResult.Success(path, path));
        }

        if (_isDryRun)
        {
            _onRegisterPlannedAction?.Invoke(new PlannedAction(
                Guid.NewGuid(),
                "PhysicalStorageService",
                "StorageService",
                permanent ? PlannedOperationType.Delete : PlannedOperationType.Recycle,
                path,
                null,
                permanent ? $"Delete {path}" : $"Recycle {path}"
            ));
            return ValueTask.FromResult(StorageOperationResult.Success(path, path));
        }

        bool success;
        if (permanent)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, true);
                success = true;
            }
            catch (Exception ex)
            {
                return ValueTask.FromResult(StorageOperationResult.Failure(path, path, ex.Message));
            }
        }
        else
        {
            success = _platform.MoveToTrash(path);
            if (!success)
            {
                // Fallback a borrado normal
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                    else if (Directory.Exists(path)) Directory.Delete(path, true);
                    success = true;
                }
                catch (Exception ex)
                {
                    return ValueTask.FromResult(StorageOperationResult.Failure(path, path, ex.Message));
                }
            }
        }

        return ValueTask.FromResult(success
            ? StorageOperationResult.Success(path, path)
            : StorageOperationResult.Failure(path, path, "Failed to delete or recycle file"));
    }

    public ValueTask<string> ResolveCollisionAsync(
        string targetPath,
        StorageCollisionStrategy strategy,
        CancellationToken ct = default)
    {
        if (!File.Exists(targetPath) || strategy == StorageCollisionStrategy.Overwrite || strategy == StorageCollisionStrategy.Skip)
        {
            return ValueTask.FromResult(targetPath);
        }

        if (strategy == StorageCollisionStrategy.ThrowError)
        {
            throw new IOException($"Target file already exists: '{targetPath}'.");
        }

        string dir = Path.GetDirectoryName(targetPath) ?? string.Empty;
        string nameWithoutExt = Path.GetFileNameWithoutExtension(targetPath);
        string ext = Path.GetExtension(targetPath);
        int counter = 1;
        string candidate;

        do
        {
            candidate = Path.Combine(dir, $"{nameWithoutExt}_{counter}{ext}");
            counter++;
        } while (File.Exists(candidate));

        return ValueTask.FromResult(candidate);
    }

    public async ValueTask<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default) =>
        await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);

    public async ValueTask<string> ReadAllTextAsync(string path, CancellationToken ct = default) =>
        await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);

    public async ValueTask<StorageOperationResult> WriteAllBytesAsync(
        string path,
        byte[] bytes,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default)
    {
        if (collisionStrategy == StorageCollisionStrategy.Skip && File.Exists(path))
        {
            return StorageOperationResult.Skipped(path, path);
        }

        string finalTarget = await ResolveCollisionAsync(path, collisionStrategy, ct).ConfigureAwait(false);
        bool wasCollision = !string.Equals(finalTarget, path, StringComparison.OrdinalIgnoreCase);

        if (_isDryRun)
        {
            return StorageOperationResult.Success(path, finalTarget, bytes.Length, wasCollision);
        }

        string? dir = Path.GetDirectoryName(finalTarget);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(finalTarget, bytes, ct).ConfigureAwait(false);
        return StorageOperationResult.Success(path, finalTarget, bytes.Length, wasCollision);
    }

    public async ValueTask<StorageOperationResult> WriteAllTextAsync(
        string path,
        string text,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default)
    {
        if (collisionStrategy == StorageCollisionStrategy.Skip && File.Exists(path))
        {
            return StorageOperationResult.Skipped(path, path);
        }

        string finalTarget = await ResolveCollisionAsync(path, collisionStrategy, ct).ConfigureAwait(false);
        bool wasCollision = !string.Equals(finalTarget, path, StringComparison.OrdinalIgnoreCase);

        if (_isDryRun)
        {
            return StorageOperationResult.Success(path, finalTarget, System.Text.Encoding.UTF8.GetByteCount(text), wasCollision);
        }

        string? dir = Path.GetDirectoryName(finalTarget);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(finalTarget, text, ct).ConfigureAwait(false);
        return StorageOperationResult.Success(path, finalTarget, System.Text.Encoding.UTF8.GetByteCount(text), wasCollision);
    }

    public ValueTask<long> GetFileSizeAsync(string path, CancellationToken ct = default)
    {
        if (File.Exists(path))
        {
            return ValueTask.FromResult(new FileInfo(path).Length);
        }
        return ValueTask.FromResult(0L);
    }

    public ValueTask<DateTimeOffset> GetCreationTimeAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult<DateTimeOffset>(File.Exists(path) ? new DateTimeOffset(File.GetCreationTimeUtc(path), TimeSpan.Zero) : DateTimeOffset.UtcNow);

    public ValueTask<DateTimeOffset> GetLastWriteTimeAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult<DateTimeOffset>(File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : DateTimeOffset.UtcNow);
}
