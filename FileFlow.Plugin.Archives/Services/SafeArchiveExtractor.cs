using FileFlow.Sdk;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FileFlow.Plugin.Archives.Services;

/// <summary>
/// Motor desacoplado para la apertura segura y extracción recursiva de archivos comprimidos con mitigación de Zip Slip y soporte de contraseñas.
/// </summary>
public static class SafeArchiveExtractor
{
    public static List<string?> GetPasswordCandidates(string passwordListParam, string passwordFileParam, FileItemContext item)
    {
        var candidates = new List<string?> { null, string.Empty };

        if (!string.IsNullOrWhiteSpace(passwordFileParam))
        {
            string resolvedFile = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(passwordFileParam, item);
            if (File.Exists(resolvedFile))
            {
                var lines = File.ReadAllLines(resolvedFile);
                foreach (var line in lines)
                {
                    string p = line.Trim();
                    if (!string.IsNullOrEmpty(p) && !candidates.Contains(p))
                    {
                        candidates.Add(p);
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(passwordListParam))
        {
            string resolvedList = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(passwordListParam, item);
            var parts = resolvedList.Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                if (!string.IsNullOrEmpty(p) && !candidates.Contains(p))
                {
                    candidates.Add(p);
                }
            }
        }

        return candidates;
    }

    public static (IArchive archive, string? validPassword) OpenArchiveWithPassword(string archivePath, List<string?> candidates, IFlowExecutionContext context)
    {
        foreach (var pwd in candidates)
        {
            IArchive? archive = null;
            try
            {
                var readerOpts = new ReaderOptions { Password = pwd };
                archive = ArchiveFactory.OpenArchive(new FileInfo(archivePath), readerOpts);

                var firstEntry = archive.Entries.FirstOrDefault(e => !e.IsDirectory);
                if (firstEntry != null)
                {
                    using var stream = firstEntry.OpenEntryStream();
                    byte[] buffer = new byte[64];
                    int read = stream.Read(buffer, 0, buffer.Length);
                }

                if (!string.IsNullOrEmpty(pwd))
                {
                    context.Log($"SmartUnpackNode: Desbloqueado archivo protegido '{Path.GetFileName(archivePath)}' mediante contraseña válida.", LogLevel.Information);
                }

                return (archive, pwd);
            }
            catch (Exception ex) when (ex is CryptographicException ||
                                      ex is InvalidFormatException ||
                                      ex is ArchiveException ||
                                      ex is InvalidDataException)
            {
                archive?.Dispose();
                continue;
            }
        }

        throw new CryptographicException($"El archivo '{Path.GetFileName(archivePath)}' está protegido por contraseña o dañado, y no se encontró una contraseña válida.");
    }

    public static void ExtractEntriesSafely(IArchive archive, string targetDir, CancellationToken cancellationToken)
    {
        string fullTargetDir = Path.GetFullPath(targetDir);
        string fullTargetDirWithSep = Path.TrimEndingDirectorySeparator(fullTargetDir) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string entryPath = entry.Key ?? string.Empty;
            string destinationPath = Path.GetFullPath(Path.Combine(targetDir, entryPath));

            if (!destinationPath.StartsWith(fullTargetDirWithSep, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(destinationPath, fullTargetDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new System.Security.SecurityException($"Zip Slip detectado! La entrada '{entryPath}' apunta fuera del directorio de extracción.");
            }

            entry.WriteToDirectory(targetDir, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
    }

    public static void ExtractNestedArchives(string targetDir, List<string?> candidates, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        const int maxDepth = 5;

        for (int depth = 0; depth < maxDepth; depth++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var allFiles = Directory.EnumerateFiles(targetDir, "*.*", SearchOption.AllDirectories).ToList();

            var nestedPrimaryArchives = allFiles
                .Where(f => ArchiveVolumeResolver.IsPrimaryArchiveFile(f))
                .ToList();

            if (nestedPrimaryArchives.Count == 0)
            {
                break;
            }

            foreach (var nestedArchive in nestedPrimaryArchives)
            {
                cancellationToken.ThrowIfCancellationRequested();
                context.Log($"SmartUnpackNode: Archivo anidado detectado '{Path.GetFileName(nestedArchive)}'. Descomprimiendo recursivamente...", LogLevel.Information);

                try
                {
                    var (archive, validPassword) = OpenArchiveWithPassword(nestedArchive, candidates, context);
                    using (archive)
                    {
                        string nestedExtractDir = Path.GetDirectoryName(nestedArchive) ?? targetDir;
                        ExtractEntriesSafely(archive, nestedExtractDir, cancellationToken);
                    }

                    File.Delete(nestedArchive);
                    context.Log($"SmartUnpackNode: Archivo anidado intermedio eliminado '{Path.GetFileName(nestedArchive)}'.", LogLevel.Information);
                }
                catch (Exception ex)
                {
                    context.Log($"SmartUnpackNode: Error al descomprimir archivo anidado '{nestedArchive}': {ex.Message}", LogLevel.Warning);
                }
            }

            var secondaryVolumes = Directory.EnumerateFiles(targetDir, "*.*", SearchOption.AllDirectories)
                .Where(f => ArchiveVolumeResolver.IsSecondaryVolumeFile(f))
                .ToList();

            foreach (var secVol in secondaryVolumes)
            {
                try
                {
                    File.Delete(secVol);
                    context.Log($"SmartUnpackNode: Volumen secundario intermedio eliminado '{Path.GetFileName(secVol)}'.", LogLevel.Information);
                }
                catch { }
            }
        }
    }
}
