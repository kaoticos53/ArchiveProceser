using System.IO;
using System.IO.Compression;

namespace FileFlow.Plugin.Archives.Services;

/// <summary>
/// Motor de descompresión nativo de .NET 9 para archivos ZIP, CBZ, EPUB y JAR.
/// Proporciona alta velocidad, cero dependencias nativas externas y protección estricta contra Zip Slip.
/// </summary>
public static class DotNetZipArchiveExtractor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".cbz", ".epub", ".jar", ".nupkg", ".apk"
    };

    public static bool CanHandle(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return false;
        }

        string ext = Path.GetExtension(archivePath);
        if (SupportedExtensions.Contains(ext))
        {
            return true;
        }

        // Validación por firma mágica ZIP (PK\x03\x04 o PK\x05\x06 o PK\x07\x08)
        try
        {
            using var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < 4) return false;
            byte[] header = new byte[4];
            int read = fs.Read(header, 0, 4);
            return read == 4 && header[0] == 0x50 && header[1] == 0x4B &&
                   (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07);
        }
        catch
        {
            return false;
        }
    }

    public static async Task<ArchiveExtractionResult> ExtractAsync(
        string archivePath,
        string targetDir,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        // .NET 9 ZipArchive no soporta contraseñas nativamente de forma directa en ZipFile sin librerías externas.
        // Si se requiere contraseña, delegamos al siguiente motor.
        if (!string.IsNullOrEmpty(password))
        {
            return new ArchiveExtractionResult(
                Success: false,
                ArchivePath: archivePath,
                DestinationDirectory: targetDir,
                ExtractedFiles: [],
                TotalEntriesCount: 0,
                EngineUsed: "DotNetZip",
                ValidPasswordUsed: null,
                ErrorMessage: "DotNetZipArchiveExtractor does not support encrypted/password-protected ZIPs.",
                Warnings: []);
        }

        string fullTargetDir = Path.GetFullPath(targetDir);
        string fullTargetDirWithSep = Path.TrimEndingDirectorySeparator(fullTargetDir) + Path.DirectorySeparatorChar;

        if (!Directory.Exists(fullTargetDir))
        {
            Directory.CreateDirectory(fullTargetDir);
        }

        var extractedFiles = new List<string>();
        var warnings = new List<string>();
        int totalEntries = 0;

        try
        {
            await using var fileStream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 64 * 1024,
                useAsync: true);

            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);

            totalEntries = archive.Entries.Count;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string entryPath = entry.FullName.Replace('\\', '/');

                // Si es un directorio vacío dentro del zip
                if (entryPath.EndsWith('/') || string.IsNullOrEmpty(entry.Name))
                {
                    string dirDest = Path.GetFullPath(Path.Combine(fullTargetDir, entryPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (dirDest.StartsWith(fullTargetDirWithSep, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(dirDest, fullTargetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.CreateDirectory(dirDest);
                    }
                    continue;
                }

                string destinationPath = Path.GetFullPath(Path.Combine(fullTargetDir, entryPath.Replace('/', Path.DirectorySeparatorChar)));

                // Mitigación estricta de Zip Slip
                if (!destinationPath.StartsWith(fullTargetDirWithSep, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(destinationPath, fullTargetDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new System.Security.SecurityException($"Zip Slip detectado! La entrada '{entryPath}' apunta fuera del directorio destino: '{destinationPath}'");
                }

                string? parentDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                try
                {
                    await using var entryStream = entry.Open();
                    await using var outStream = new FileStream(
                        destinationPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 64 * 1024,
                        useAsync: true);

                    await entryStream.CopyToAsync(outStream, cancellationToken);
                    extractedFiles.Add(destinationPath);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    warnings.Add($"Error al extraer entrada '{entryPath}': {ex.Message}");
                }
            }

            return new ArchiveExtractionResult(
                Success: extractedFiles.Count > 0 || totalEntries == 0,
                ArchivePath: archivePath,
                DestinationDirectory: targetDir,
                ExtractedFiles: extractedFiles,
                TotalEntriesCount: totalEntries,
                EngineUsed: "DotNetZip",
                ValidPasswordUsed: null,
                ErrorMessage: extractedFiles.Count == 0 && totalEntries > 0 ? "No se pudo extraer ninguna entrada del archivo." : null,
                Warnings: warnings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ArchiveExtractionResult(
                Success: false,
                ArchivePath: archivePath,
                DestinationDirectory: targetDir,
                ExtractedFiles: extractedFiles,
                TotalEntriesCount: totalEntries,
                EngineUsed: "DotNetZip",
                ValidPasswordUsed: null,
                ErrorMessage: ex.Message,
                Warnings: warnings);
        }
    }
}
