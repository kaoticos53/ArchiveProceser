using System.IO;
using System.Text;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.VirtualFileSystem;

namespace FileFlow.Sdk.Storage;

/// <summary>
/// Implementación de <see cref="IStorageService"/> sobre el Sistema de Archivos Virtual (<see cref="IVirtualFileSystemStore"/>).
/// Permite que cualquier nodo interactúe con datos en memoria exactamente igual que si estuvieran en disco.
/// </summary>
public class VirtualStorageService : IStorageService
{
    private readonly IVirtualFileSystemStore _vfs;

    public VirtualStorageService(IVirtualFileSystemStore vfs)
    {
        _vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
    }

    public IVirtualFileSystemStore Store => _vfs;

    public ValueTask<bool> FileExistsAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult(!string.IsNullOrWhiteSpace(path) && _vfs.FileExists(path));

    public ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult(!string.IsNullOrWhiteSpace(path) && _vfs.DirectoryExists(path));

    public ValueTask CreateDirectoryAsync(string path, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            _vfs.AddOrUpdateDirectory(path);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var entry = _vfs.GetFile(path);
        if (entry == null)
        {
            throw new FileNotFoundException($"Virtual file not found: '{path}'", path);
        }

        if (!string.IsNullOrEmpty(entry.TextContent))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(entry.TextContent);
            return ValueTask.FromResult<Stream>(new MemoryStream(bytes));
        }

        if (entry.Metadata.TryGetValue("VirtualContent", out var vc) && vc is byte[] rawBytes)
        {
            return ValueTask.FromResult<Stream>(new MemoryStream(rawBytes));
        }

        // Si es una muestra virtual con tamaño simulado
        long size = Math.Max(0, Math.Min(entry.FileSizeBytes, 64 * 1024)); // Máximo 64 KB de dummy bytes en memoria
        byte[] dummy = new byte[size];
        return ValueTask.FromResult<Stream>(new MemoryStream(dummy));
    }

    public ValueTask<Stream> OpenWriteAsync(string path, CancellationToken ct = default)
    {
        string dir = Path.GetDirectoryName(path) ?? string.Empty;
        if (!string.IsNullOrEmpty(dir))
        {
            _vfs.AddOrUpdateDirectory(dir);
        }

        var stream = new VirtualFileWriteStream(path, _vfs);
        return ValueTask.FromResult<Stream>(stream);
    }

    public async ValueTask<StorageOperationResult> CopyAsync(
        string sourcePath,
        string targetPath,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default)
    {
        if (collisionStrategy == StorageCollisionStrategy.Skip && _vfs.FileExists(targetPath))
        {
            return StorageOperationResult.Skipped(sourcePath, targetPath);
        }

        string finalTarget = await ResolveCollisionAsync(targetPath, collisionStrategy, ct).ConfigureAwait(false);
        bool wasCollision = !string.Equals(finalTarget, targetPath, StringComparison.OrdinalIgnoreCase);

        var sourceEntry = _vfs.GetFile(sourcePath);
        long fileSize = sourceEntry?.FileSizeBytes ?? 1024;

        if (sourceEntry != null)
        {
            _vfs.CopyFile(sourcePath, finalTarget, "StorageService", "VirtualStorage");
        }
        else
        {
            var newEntry = new VirtualFileEntry(
                VirtualPath: finalTarget,
                OriginalPath: sourcePath,
                FileName: Path.GetFileName(finalTarget),
                Extension: Path.GetExtension(finalTarget),
                DirectoryPath: Path.GetDirectoryName(finalTarget) ?? string.Empty,
                FileSizeBytes: fileSize,
                OperationType: wasCollision ? VirtualOperationType.ConflictRenamed : VirtualOperationType.Copied,
                Role: VirtualFileRole.Destination,
                RelatedSourcePath: sourcePath,
                SourceNodeName: "StorageService",
                SourceNodeId: "VirtualStorage",
                Metadata: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                ExecutionLog: [$"Copied to VFS: {finalTarget}"],
                TimestampUtc: DateTime.UtcNow
            );
            _vfs.AddOrUpdateFile(newEntry);
        }

        return StorageOperationResult.Success(sourcePath, finalTarget, fileSize, wasCollision);
    }

    public async ValueTask<StorageOperationResult> MoveAsync(
        string sourcePath,
        string targetPath,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default)
    {
        if (collisionStrategy == StorageCollisionStrategy.Skip && _vfs.FileExists(targetPath))
        {
            return StorageOperationResult.Skipped(sourcePath, targetPath);
        }

        string finalTarget = await ResolveCollisionAsync(targetPath, collisionStrategy, ct).ConfigureAwait(false);
        bool wasCollision = !string.Equals(finalTarget, targetPath, StringComparison.OrdinalIgnoreCase);

        var sourceEntry = _vfs.GetFile(sourcePath);
        long fileSize = sourceEntry?.FileSizeBytes ?? 1024;

        if (sourceEntry != null)
        {
            _vfs.MoveFile(sourcePath, finalTarget, "StorageService", "VirtualStorage");
        }
        else
        {
            var newEntry = new VirtualFileEntry(
                VirtualPath: finalTarget,
                OriginalPath: sourcePath,
                FileName: Path.GetFileName(finalTarget),
                Extension: Path.GetExtension(finalTarget),
                DirectoryPath: Path.GetDirectoryName(finalTarget) ?? string.Empty,
                FileSizeBytes: fileSize,
                OperationType: wasCollision ? VirtualOperationType.ConflictRenamed : VirtualOperationType.Moved,
                Role: VirtualFileRole.Destination,
                RelatedSourcePath: sourcePath,
                SourceNodeName: "StorageService",
                SourceNodeId: "VirtualStorage",
                Metadata: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                ExecutionLog: [$"Moved to VFS: {finalTarget}"],
                TimestampUtc: DateTime.UtcNow
            );
            _vfs.AddOrUpdateFile(newEntry);
        }

        return StorageOperationResult.Success(sourcePath, finalTarget, fileSize, wasCollision);
    }

    public ValueTask<StorageOperationResult> DeleteAsync(
        string path,
        bool permanent = false,
        CancellationToken ct = default)
    {
        if (!_vfs.FileExists(path))
        {
            return ValueTask.FromResult(StorageOperationResult.Failure(path, path, $"Virtual file not found: {path}"));
        }

        bool success = _vfs.DeleteFile(path, "StorageService", "VirtualStorage", isRecycled: !permanent);
        return ValueTask.FromResult(success
            ? StorageOperationResult.Success(path, path)
            : StorageOperationResult.Failure(path, path, "Failed to delete or recycle virtual file"));
    }

    public ValueTask<string> ResolveCollisionAsync(
        string targetPath,
        StorageCollisionStrategy strategy,
        CancellationToken ct = default)
    {
        if (!_vfs.FileExists(targetPath) || strategy == StorageCollisionStrategy.Overwrite || strategy == StorageCollisionStrategy.Skip)
        {
            return ValueTask.FromResult(targetPath);
        }

        if (strategy == StorageCollisionStrategy.ThrowError)
        {
            throw new IOException($"Target file already exists in VFS: '{targetPath}'.");
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
        } while (_vfs.FileExists(candidate));

        return ValueTask.FromResult(candidate);
    }

    public async ValueTask<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default)
    {
        await using var stream = await OpenReadAsync(path, ct).ConfigureAwait(false);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    public ValueTask<string> ReadAllTextAsync(string path, CancellationToken ct = default)
    {
        var entry = _vfs.GetFile(path);
        return ValueTask.FromResult(entry?.TextContent ?? string.Empty);
    }

    public async ValueTask<StorageOperationResult> WriteAllBytesAsync(
        string path,
        byte[] bytes,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default)
    {
        if (collisionStrategy == StorageCollisionStrategy.Skip && _vfs.FileExists(path))
        {
            return StorageOperationResult.Skipped(path, path);
        }

        string finalTarget = await ResolveCollisionAsync(path, collisionStrategy, ct).ConfigureAwait(false);
        bool wasCollision = !string.Equals(finalTarget, path, StringComparison.OrdinalIgnoreCase);

        var entry = new VirtualFileEntry(
            VirtualPath: finalTarget,
            OriginalPath: finalTarget,
            FileName: Path.GetFileName(finalTarget),
            Extension: Path.GetExtension(finalTarget),
            DirectoryPath: Path.GetDirectoryName(finalTarget) ?? string.Empty,
            FileSizeBytes: bytes.Length,
            OperationType: wasCollision ? VirtualOperationType.ConflictRenamed : VirtualOperationType.Saved,
            Role: VirtualFileRole.Destination,
            RelatedSourcePath: null,
            SourceNodeName: "StorageService",
            SourceNodeId: "VirtualStorage",
            Metadata: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["VirtualContent"] = bytes },
            ExecutionLog: [$"Written to VFS: {finalTarget}"],
            TimestampUtc: DateTime.UtcNow
        );

        _vfs.AddOrUpdateFile(entry);
        return StorageOperationResult.Success(path, finalTarget, bytes.Length, wasCollision);
    }

    public async ValueTask<StorageOperationResult> WriteAllTextAsync(
        string path,
        string text,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default)
    {
        if (collisionStrategy == StorageCollisionStrategy.Skip && _vfs.FileExists(path))
        {
            return StorageOperationResult.Skipped(path, path);
        }

        string finalTarget = await ResolveCollisionAsync(path, collisionStrategy, ct).ConfigureAwait(false);
        bool wasCollision = !string.Equals(finalTarget, path, StringComparison.OrdinalIgnoreCase);
        int bytesCount = Encoding.UTF8.GetByteCount(text);

        var entry = new VirtualFileEntry(
            VirtualPath: finalTarget,
            OriginalPath: finalTarget,
            FileName: Path.GetFileName(finalTarget),
            Extension: Path.GetExtension(finalTarget),
            DirectoryPath: Path.GetDirectoryName(finalTarget) ?? string.Empty,
            FileSizeBytes: bytesCount,
            OperationType: wasCollision ? VirtualOperationType.ConflictRenamed : VirtualOperationType.Saved,
            Role: VirtualFileRole.Destination,
            RelatedSourcePath: null,
            SourceNodeName: "StorageService",
            SourceNodeId: "VirtualStorage",
            Metadata: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            ExecutionLog: [$"Written to VFS: {finalTarget}"],
            TimestampUtc: DateTime.UtcNow,
            TextContent: text
        );

        _vfs.AddOrUpdateFile(entry);
        return StorageOperationResult.Success(path, finalTarget, bytesCount, wasCollision);
    }

    public ValueTask<long> GetFileSizeAsync(string path, CancellationToken ct = default)
    {
        var entry = _vfs.GetFile(path);
        return ValueTask.FromResult(entry?.FileSizeBytes ?? 0L);
    }

    private sealed class VirtualFileWriteStream : MemoryStream
    {
        private readonly string _targetPath;
        private readonly IVirtualFileSystemStore _vfs;
        private bool _isDisposed;

        public VirtualFileWriteStream(string targetPath, IVirtualFileSystemStore vfs)
        {
            _targetPath = targetPath;
            _vfs = vfs;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                _isDisposed = true;
                byte[] bytes = ToArray();
                var entry = new VirtualFileEntry(
                    VirtualPath: _targetPath,
                    OriginalPath: _targetPath,
                    FileName: Path.GetFileName(_targetPath),
                    Extension: Path.GetExtension(_targetPath),
                    DirectoryPath: Path.GetDirectoryName(_targetPath) ?? string.Empty,
                    FileSizeBytes: bytes.Length,
                    OperationType: VirtualOperationType.Saved,
                    Role: VirtualFileRole.Destination,
                    RelatedSourcePath: null,
                    SourceNodeName: "StorageService",
                    SourceNodeId: "VirtualWriteStream",
                    Metadata: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["VirtualContent"] = bytes },
                    ExecutionLog: [$"Written via stream: {_targetPath}"],
                    TimestampUtc: DateTime.UtcNow
                );
                _vfs.AddOrUpdateFile(entry);
            }
            base.Dispose(disposing);
        }
    }
}
