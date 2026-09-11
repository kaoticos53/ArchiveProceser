using System.IO;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FileFlow.Plugin.Archives.Services;

/// <summary>
/// Motor de extracción administrado en C# basado en SharpCompress con resiliencia por entrada
/// y modo continuo para archivos sólidos y estándares.
/// </summary>
public static class SharpCompressResilientExtractor
{
    public static async Task<ArchiveExtractionResult> ExtractAsync(
        string archivePath,
        string targetDir,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        string fullTargetDir = Path.GetFullPath(targetDir);
        string fullTargetDirWithSep = Path.TrimEndingDirectorySeparator(fullTargetDir) + Path.DirectorySeparatorChar;

        if (!Directory.Exists(fullTargetDir))
        {
            Directory.CreateDirectory(fullTargetDir);
        }

        var extractedFiles = new List<string>();
        var warnings = new List<string>();
        int totalEntries = 0;

        var readerOptions = new ReaderOptions
        {
            Password = password,
            LeaveStreamOpen = false
        };

        // Extracción resiliente con ArchiveFactory y aislamiento try/catch por entrada
        try
        {
            using var archive = ArchiveFactory.OpenArchive(new FileInfo(archivePath), readerOptions);

            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            totalEntries = entries.Count;

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string entryKey = entry.Key ?? string.Empty;
                if (string.IsNullOrWhiteSpace(entryKey))
                {
                    entryKey = $"entry_{extractedFiles.Count + 1}.dat";
                }

                string destinationPath = Path.GetFullPath(Path.Combine(fullTargetDir, entryKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));

                // Anti Zip-Slip
                if (!destinationPath.StartsWith(fullTargetDirWithSep, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(destinationPath, fullTargetDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new System.Security.SecurityException($"Zip Slip detectado! La entrada '{entryKey}' apunta fuera de '{fullTargetDir}'");
                }

                string? parentDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                try
                {
                    await using var entryStream = entry.OpenEntryStream();
                    await using var outStream = new FileStream(
                        destinationPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 64 * 1024,
                        useAsync: true);

                    await entryStream.CopyToAsync(outStream, cancellationToken);

                    if (File.Exists(destinationPath) && !extractedFiles.Contains(destinationPath, StringComparer.OrdinalIgnoreCase))
                    {
                        extractedFiles.Add(destinationPath);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Si falla el OpenEntryStream directo, intentamos WriteToDirectory como fallback
                    try
                    {
                        entry.WriteToDirectory(fullTargetDir, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });

                        if (File.Exists(destinationPath) && !extractedFiles.Contains(destinationPath, StringComparer.OrdinalIgnoreCase))
                        {
                            extractedFiles.Add(destinationPath);
                        }
                    }
                    catch (Exception exWrite) when (exWrite is not OperationCanceledException)
                    {
                        warnings.Add($"Fallo al extraer entrada '{entryKey}': {ex.Message} (WriteToDirectory: {exWrite.Message})");
                    }
                }
            }

            return new ArchiveExtractionResult(
                Success: extractedFiles.Count > 0 || totalEntries == 0,
                ArchivePath: archivePath,
                DestinationDirectory: fullTargetDir,
                ExtractedFiles: extractedFiles,
                TotalEntriesCount: totalEntries,
                EngineUsed: "SharpCompress_Managed",
                ValidPasswordUsed: password,
                ErrorMessage: extractedFiles.Count == 0 && totalEntries > 0 ? "No se pudo extraer ninguna entrada mediante SharpCompress." : null,
                Warnings: warnings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ArchiveExtractionResult(
                Success: false,
                ArchivePath: archivePath,
                DestinationDirectory: fullTargetDir,
                ExtractedFiles: extractedFiles,
                TotalEntriesCount: totalEntries,
                EngineUsed: "SharpCompress",
                ValidPasswordUsed: null,
                ErrorMessage: ex.Message,
                Warnings: warnings);
        }
    }
}
