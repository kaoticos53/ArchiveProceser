using System.Collections.Concurrent;
using System.IO;
using System.Text;
using FileFlow.Sdk.VirtualFileSystem;

namespace FileFlow.Core.Engine;

/// <summary>
/// Implementación concurrente y thread-safe del almacén de sistema de archivos virtual (VFS) en memoria.
/// </summary>
public class VirtualFileSystemStore : IVirtualFileSystemStore
{
    private readonly System.Threading.Lock _lock = new();
    private readonly Dictionary<string, VirtualFileEntry> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public int TotalFiles
    {
        get
        {
            lock (_lock)
            {
                return _files.Count(f => IsActive(f.Value));
            }
        }
    }

    public long TotalBytes
    {
        get
        {
            lock (_lock)
            {
                return _files.Values
                    .Where(IsActive)
                    .Sum(f => f.FileSizeBytes);
            }
        }
    }

    public void AddOrUpdateFile(VirtualFileEntry file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string normalizedPath = NormalizePath(file.VirtualPath);

        lock (_lock)
        {
            _files[normalizedPath] = file with { VirtualPath = normalizedPath };
            RegisterDirectories(normalizedPath);
        }
    }

    public void AddOrUpdateDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) return;
        string normalized = NormalizePath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        lock (_lock)
        {
            _directories.Add(normalized);
        }
    }

    public bool FileExists(string virtualPath)
    {
        if (string.IsNullOrWhiteSpace(virtualPath)) return false;
        string normalized = NormalizePath(virtualPath);

        lock (_lock)
        {
            return _files.TryGetValue(normalized, out var entry) && IsActive(entry);
        }
    }

    public bool DirectoryExists(string virtualPath)
    {
        if (string.IsNullOrWhiteSpace(virtualPath)) return false;
        string normalized = NormalizePath(virtualPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        lock (_lock)
        {
            if (_directories.Contains(normalized)) return true;

            // Comprobar si algún archivo activo reside bajo este prefijo de directorio
            string prefix = normalized + Path.DirectorySeparatorChar;
            return _files.Values.Any(f => IsActive(f) && f.VirtualPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }

    public VirtualFileEntry? GetFile(string virtualPath)
    {
        if (string.IsNullOrWhiteSpace(virtualPath)) return null;
        string normalized = NormalizePath(virtualPath);

        lock (_lock)
        {
            return _files.TryGetValue(normalized, out var entry) ? entry : null;
        }
    }

    public IReadOnlyList<VirtualFileEntry> GetSourceFiles()
    {
        lock (_lock)
        {
            return _files.Values
                .Where(f => f.Role == VirtualFileRole.Source)
                .OrderBy(f => f.VirtualPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<VirtualFileEntry> GetDestinationFiles()
    {
        lock (_lock)
        {
            return _files.Values
                .Where(f => f.Role == VirtualFileRole.Destination && IsActive(f))
                .OrderBy(f => f.VirtualPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<VirtualFileEntry> GetAllFiles()
    {
        lock (_lock)
        {
            return _files.Values
                .OrderBy(f => f.VirtualPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<string> GetAllDirectories()
    {
        lock (_lock)
        {
            return _directories
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public bool RenameFile(string sourceVirtualPath, string targetVirtualPath, string sourceNodeName, string sourceNodeId)
    {
        if (string.IsNullOrWhiteSpace(sourceVirtualPath) || string.IsNullOrWhiteSpace(targetVirtualPath)) return false;
        string normSource = NormalizePath(sourceVirtualPath);
        string normTarget = NormalizePath(targetVirtualPath);

        lock (_lock)
        {
            if (!_files.TryGetValue(normSource, out var existing))
            {
                return false;
            }

            var logs = new List<string>(existing.ExecutionLog)
            {
                $"Renamed in VFS: '{normSource}' -> '{normTarget}' by {sourceNodeName}"
            };

            // Conservar el archivo de origen en el VFS indicando que fue renombrado y su nueva ruta
            var updatedSource = existing with
            {
                OperationType = VirtualOperationType.Renamed,
                DestinationPath = normTarget,
                SourceNodeName = sourceNodeName,
                SourceNodeId = sourceNodeId,
                ExecutionLog = logs,
                TimestampUtc = DateTime.UtcNow
            };
            _files[normSource] = updatedSource;

            // Si la ruta cambió, registrar la nueva entrada para que nodos subsecuentes la encuentren
            if (!string.Equals(normSource, normTarget, StringComparison.OrdinalIgnoreCase))
            {
                var renamedEntry = existing with
                {
                    VirtualPath = normTarget,
                    FileName = Path.GetFileName(normTarget),
                    Extension = Path.GetExtension(normTarget),
                    DirectoryPath = Path.GetDirectoryName(normTarget) ?? string.Empty,
                    OperationType = VirtualOperationType.Renamed,
                    Role = existing.Role == VirtualFileRole.Source ? VirtualFileRole.Intermediate : existing.Role,
                    DestinationPath = null,
                    RelatedSourcePath = existing.OriginalPath,
                    SourceNodeName = sourceNodeName,
                    SourceNodeId = sourceNodeId,
                    ExecutionLog = logs,
                    TimestampUtc = DateTime.UtcNow
                };

                _files[normTarget] = renamedEntry;
                RegisterDirectories(normTarget);
            }

            return true;
        }
    }

    public bool MoveFile(string sourceVirtualPath, string targetVirtualPath, string sourceNodeName, string sourceNodeId)
    {
        if (string.IsNullOrWhiteSpace(sourceVirtualPath) || string.IsNullOrWhiteSpace(targetVirtualPath)) return false;
        string normSource = NormalizePath(sourceVirtualPath);
        string normTarget = NormalizePath(targetVirtualPath);

        lock (_lock)
        {
            if (!_files.TryGetValue(normSource, out var existing) || !IsActive(existing))
            {
                return false;
            }

            var logs = new List<string>(existing.ExecutionLog)
            {
                $"Moved in VFS: '{normSource}' -> '{normTarget}' by {sourceNodeName}"
            };

            // Conservar el archivo de origen en VFS marcado como Moved y con DestinationPath
            var movedSource = existing with
            {
                OperationType = VirtualOperationType.Moved,
                DestinationPath = normTarget,
                SourceNodeName = sourceNodeName,
                SourceNodeId = sourceNodeId,
                ExecutionLog = logs,
                TimestampUtc = DateTime.UtcNow
            };
            _files[normSource] = movedSource;

            // Registrar el archivo destino con Role = Destination y enlace al origen
            var movedEntry = existing with
            {
                VirtualPath = normTarget,
                FileName = Path.GetFileName(normTarget),
                Extension = Path.GetExtension(normTarget),
                DirectoryPath = Path.GetDirectoryName(normTarget) ?? string.Empty,
                Role = VirtualFileRole.Destination,
                OperationType = VirtualOperationType.Moved,
                DestinationPath = null,
                RelatedSourcePath = existing.OriginalPath ?? normSource,
                SourceNodeName = sourceNodeName,
                SourceNodeId = sourceNodeId,
                ExecutionLog = logs,
                TimestampUtc = DateTime.UtcNow
            };

            _files[normTarget] = movedEntry;
            RegisterDirectories(normTarget);
            return true;
        }
    }

    public bool CopyFile(string sourceVirtualPath, string targetVirtualPath, string sourceNodeName, string sourceNodeId)
    {
        if (string.IsNullOrWhiteSpace(sourceVirtualPath) || string.IsNullOrWhiteSpace(targetVirtualPath)) return false;
        string normSource = NormalizePath(sourceVirtualPath);
        string normTarget = NormalizePath(targetVirtualPath);

        lock (_lock)
        {
            if (!_files.TryGetValue(normSource, out var existing) || !IsActive(existing))
            {
                return false;
            }

            var logs = new List<string>(existing.ExecutionLog)
            {
                $"Copied in VFS: '{normSource}' -> '{normTarget}' by {sourceNodeName}"
            };

            // Conservar el origen indicando su copia a destino
            _files[normSource] = existing with
            {
                DestinationPath = normTarget,
                ExecutionLog = logs
            };

            var copiedEntry = existing with
            {
                VirtualPath = normTarget,
                FileName = Path.GetFileName(normTarget),
                Extension = Path.GetExtension(normTarget),
                DirectoryPath = Path.GetDirectoryName(normTarget) ?? string.Empty,
                Role = VirtualFileRole.Destination,
                OperationType = VirtualOperationType.Copied,
                DestinationPath = null,
                RelatedSourcePath = existing.OriginalPath ?? normSource,
                SourceNodeName = sourceNodeName,
                SourceNodeId = sourceNodeId,
                ExecutionLog = logs,
                TimestampUtc = DateTime.UtcNow
            };

            _files[normTarget] = copiedEntry;
            RegisterDirectories(normTarget);
            return true;
        }
    }

    public bool DeleteFile(string virtualPath, string sourceNodeName, string sourceNodeId, bool isRecycled = false)
    {
        if (string.IsNullOrWhiteSpace(virtualPath)) return false;
        string norm = NormalizePath(virtualPath);

        lock (_lock)
        {
            if (!_files.TryGetValue(norm, out var existing))
            {
                return false;
            }

            var logs = new List<string>(existing.ExecutionLog)
            {
                $"{(isRecycled ? "Recycled" : "Deleted")} in VFS by {sourceNodeName}"
            };

            _files[norm] = existing with
            {
                OperationType = isRecycled ? VirtualOperationType.Recycled : VirtualOperationType.Deleted,
                SourceNodeName = sourceNodeName,
                SourceNodeId = sourceNodeId,
                ExecutionLog = logs,
                TimestampUtc = DateTime.UtcNow
            };
            return true;
        }
    }

    public string GenerateAsciiTree(string? rootDirectory = null)
    {
        lock (_lock)
        {
            var sourceFiles = _files.Values
                .Where(f => f.Role == VirtualFileRole.Source)
                .OrderBy(f => f.VirtualPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var destFiles = _files.Values
                .Where(f => f.Role == VirtualFileRole.Destination && IsActive(f))
                .OrderBy(f => f.VirtualPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sourceFiles.Count == 0 && destFiles.Count == 0)
            {
                return "(Sistema de archivos virtual vacío)";
            }

            var sb = new StringBuilder();
            sb.AppendLine("📁 [Raíz Virtual]");

            if (sourceFiles.Count > 0)
            {
                sb.AppendLine("├── 📥 [Carpetas de Origen]");
                AppendTreeSection(sb, sourceFiles, "│   ");
            }

            if (destFiles.Count > 0)
            {
                sb.AppendLine(sourceFiles.Count > 0 ? "└── 📤 [Carpetas de Destino]" : "├── 📤 [Carpetas de Destino]");
                AppendTreeSection(sb, destFiles, sourceFiles.Count > 0 ? "    " : "│   ");
            }

            return sb.ToString();
        }
    }

    private static void AppendTreeSection(StringBuilder sb, List<VirtualFileEntry> files, string indent)
    {
        var grouped = files.GroupBy(f => f.DirectoryPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < grouped.Count; i++)
        {
            var group = grouped[i];
            bool isLastDir = (i == grouped.Count - 1);
            string dirPrefix = isLastDir ? "└── " : "├── ";
            string childPrefix = isLastDir ? "    " : "│   ";

            sb.AppendLine($"{indent}{dirPrefix}📁 {group.Key}");

            var dirFiles = group.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList();
            for (int j = 0; j < dirFiles.Count; j++)
            {
                var file = dirFiles[j];
                bool isLastFile = (j == dirFiles.Count - 1);
                string filePrefix = isLastFile ? "└── " : "├── ";
                string sizeStr = FormatBytes(file.FileSizeBytes);
                string destInfo = !string.IsNullOrEmpty(file.DestinationPath) ? $" ➔ {Path.GetFileName(file.DestinationPath)}" : string.Empty;
                sb.AppendLine($"{indent}{childPrefix}{filePrefix}📄 {file.FileName} ({sizeStr}) [{file.OperationType}]{destInfo}");
            }
        }
    }

    public async Task ExportToPhysicalDirectoryAsync(string destinationRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        Directory.CreateDirectory(destinationRoot);

        List<VirtualFileEntry> filesToExport;
        lock (_lock)
        {
            filesToExport = _files.Values
                .Where(IsActive)
                .ToList();
        }

        foreach (var file in filesToExport)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Despojar la letra de unidad (ej. "C:\" -> "C_Drive") para crear una estructura segura dentro de destinationRoot
            string safeRelativePath = file.VirtualPath;
            if (Path.IsPathRooted(safeRelativePath))
            {
                string root = Path.GetPathRoot(safeRelativePath) ?? string.Empty;
                string cleanRoot = root.Replace(":", "_Drive").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string remainder = safeRelativePath[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                safeRelativePath = Path.Combine(cleanRoot, remainder);
            }

            string physicalDest = Path.Combine(destinationRoot, safeRelativePath);
            string? dir = Path.GetDirectoryName(physicalDest);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (file.BinaryContent != null && file.BinaryContent.Length > 0)
            {
                await File.WriteAllBytesAsync(physicalDest, file.BinaryContent, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(file.TextContent))
            {
                await File.WriteAllTextAsync(physicalDest, file.TextContent, cancellationToken);
            }
            else
            {
                // Generar contenido informativo con los metadatos reales del archivo virtual
                var sb = new StringBuilder();
                sb.AppendLine($"# FileFlow Studio - Archivo Virtual Exportado");
                sb.AppendLine($"Nombre: {file.FileName}");
                sb.AppendLine($"Ruta Virtual: {file.VirtualPath}");
                sb.AppendLine($"Ruta Original: {file.OriginalPath}");
                sb.AppendLine($"Tamaño Simulado: {file.FileSizeBytes} bytes ({FormatBytes(file.FileSizeBytes)})");
                sb.AppendLine($"Operación: {file.OperationType}");
                sb.AppendLine($"Nodo Responsable: {file.SourceNodeName}");
                sb.AppendLine($"Fecha UTC: {file.TimestampUtc:O}");
                sb.AppendLine();
                sb.AppendLine("## Metadatos:");
                foreach (var kvp in file.Metadata.OrderBy(m => m.Key))
                {
                    sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
                sb.AppendLine();
                sb.AppendLine("## Historial de Pasos (ExecutionLog):");
                foreach (var step in file.ExecutionLog)
                {
                    sb.AppendLine($"- {step}");
                }

                await File.WriteAllTextAsync(physicalDest, sb.ToString(), cancellationToken);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _files.Clear();
            _directories.Clear();
        }
    }

    private void RegisterDirectories(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrWhiteSpace(dir))
        {
            _directories.Add(dir);
            dir = Path.GetDirectoryName(dir);
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path.Replace('/', '\\').Trim();
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static bool IsActive(VirtualFileEntry entry) =>
        entry.OperationType != VirtualOperationType.Deleted &&
        entry.OperationType != VirtualOperationType.Recycled &&
        !(entry.OperationType == VirtualOperationType.Moved && !string.IsNullOrEmpty(entry.DestinationPath));
}
