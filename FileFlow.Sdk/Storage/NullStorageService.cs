using System.IO;

namespace FileFlow.Sdk.Storage;

/// <summary>
/// Implementación mínima y fallback seguro para <see cref="IStorageService"/>.
/// Realiza operaciones directas en el sistema de archivos local para entornos de prueba o ejecución sin DI.
/// </summary>
public class NullStorageService : IStorageService
{
    private static readonly Lazy<NullStorageService> _instance = new(() => new NullStorageService());
    public static NullStorageService Instance => _instance.Value;

    public ValueTask<bool> FileExistsAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult(!string.IsNullOrWhiteSpace(path) && File.Exists(path));

    public ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult(!string.IsNullOrWhiteSpace(path) && Directory.Exists(path));

    public ValueTask CreateDirectoryAsync(string path, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return ValueTask.CompletedTask;
    }

    private const int BufferSize = 131072; // 128 KB

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
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
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

        string finalTarget = await ResolveCollisionAsync(targetPath, collisionStrategy, ct).ConfigureAwait(false);
        if (collisionStrategy == StorageCollisionStrategy.Skip && File.Exists(targetPath))
        {
            return StorageOperationResult.Skipped(sourcePath, targetPath);
        }

        string? dir = Path.GetDirectoryName(finalTarget);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.Copy(sourcePath, finalTarget, overwrite: true);
        long length = new FileInfo(finalTarget).Length;
        return StorageOperationResult.Success(sourcePath, finalTarget, length, wasCollision: !string.Equals(finalTarget, targetPath, StringComparison.OrdinalIgnoreCase));
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

        string finalTarget = await ResolveCollisionAsync(targetPath, collisionStrategy, ct).ConfigureAwait(false);
        if (collisionStrategy == StorageCollisionStrategy.Skip && File.Exists(targetPath))
        {
            return StorageOperationResult.Skipped(sourcePath, targetPath);
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
        long length = new FileInfo(finalTarget).Length;
        return StorageOperationResult.Success(sourcePath, finalTarget, length, wasCollision: !string.Equals(finalTarget, targetPath, StringComparison.OrdinalIgnoreCase));
    }

    public ValueTask<StorageOperationResult> DeleteAsync(string path, bool permanent = false, CancellationToken ct = default)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return ValueTask.FromResult(StorageOperationResult.Success(path, path));
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }

        return ValueTask.FromResult(StorageOperationResult.Success(path, path));
    }

    public ValueTask<string> ResolveCollisionAsync(string targetPath, StorageCollisionStrategy strategy, CancellationToken ct = default)
    {
        if (!File.Exists(targetPath) || strategy == StorageCollisionStrategy.Overwrite || strategy == StorageCollisionStrategy.Skip)
        {
            return ValueTask.FromResult(targetPath);
        }

        if (strategy == StorageCollisionStrategy.ThrowError)
        {
            throw new IOException($"Destination file already exists: '{targetPath}'.");
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
        string finalTarget = await ResolveCollisionAsync(path, collisionStrategy, ct).ConfigureAwait(false);
        if (collisionStrategy == StorageCollisionStrategy.Skip && File.Exists(path))
        {
            return StorageOperationResult.Skipped(path, path);
        }

        string? dir = Path.GetDirectoryName(finalTarget);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(finalTarget, bytes, ct).ConfigureAwait(false);
        return StorageOperationResult.Success(path, finalTarget, bytes.Length, wasCollision: !string.Equals(finalTarget, path, StringComparison.OrdinalIgnoreCase));
    }

    public async ValueTask<StorageOperationResult> WriteAllTextAsync(
        string path,
        string text,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default)
    {
        string finalTarget = await ResolveCollisionAsync(path, collisionStrategy, ct).ConfigureAwait(false);
        if (collisionStrategy == StorageCollisionStrategy.Skip && File.Exists(path))
        {
            return StorageOperationResult.Skipped(path, path);
        }

        string? dir = Path.GetDirectoryName(finalTarget);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(finalTarget, text, ct).ConfigureAwait(false);
        return StorageOperationResult.Success(path, finalTarget, System.Text.Encoding.UTF8.GetByteCount(text), wasCollision: !string.Equals(finalTarget, path, StringComparison.OrdinalIgnoreCase));
    }

    public ValueTask<long> GetFileSizeAsync(string path, CancellationToken ct = default)
    {
        if (File.Exists(path))
        {
            return ValueTask.FromResult(new FileInfo(path).Length);
        }
        return ValueTask.FromResult(0L);
    }
}
