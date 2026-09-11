using System.IO;
using FileFlow.Sdk;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FileFlow.Plugin.Archives.Services;

/// <summary>
/// Motor de descompresión configurable.
/// </summary>
public enum ArchiveExtractionEngine
{
    Auto,
    SevenZip,
    DotNetZip,
    SharpCompress
}

/// <summary>
/// Resultado detallado de la operación de extracción de un archivo comprimido.
/// </summary>
public sealed record ArchiveExtractionResult(
    bool Success,
    string ArchivePath,
    string DestinationDirectory,
    List<string> ExtractedFiles,
    int TotalEntriesCount,
    string EngineUsed,
    string? ValidPasswordUsed,
    string? ErrorMessage,
    List<string> Warnings);

/// <summary>
/// Motor desacoplado para la apertura segura y extracción recursiva de archivos comprimidos con mitigación de Zip Slip,
/// soporte multi-estrategia con fallback transparente (.NET 9 Zip, 7-Zip CLI, SharpCompress Resiliente) y contraseñas.
/// </summary>
public static class SafeArchiveExtractor
{
    public static async Task<List<string?>> GetPasswordCandidatesAsync(
        string passwordListParam,
        string passwordFileParam,
        FileItemContext item,
        FileFlow.Sdk.Storage.IStorageService? storage = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<string?> { null, string.Empty };

        if (!string.IsNullOrWhiteSpace(passwordFileParam))
        {
            string resolvedFile = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(passwordFileParam, item);
            if (storage != null && await storage.FileExistsAsync(resolvedFile, cancellationToken))
            {
                await using var stream = await storage.OpenReadAsync(resolvedFile, cancellationToken);
                using var reader = new StreamReader(stream);
                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    string p = line.Trim();
                    if (!string.IsNullOrEmpty(p) && !candidates.Contains(p))
                    {
                        candidates.Add(p);
                    }
                }
            }
            else if (File.Exists(resolvedFile))
            {
                var lines = await File.ReadAllLinesAsync(resolvedFile, cancellationToken);
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

    /// <summary>
    /// Ejecuta la extracción universal de un archivo comprimido utilizando la estrategia óptima o la seleccionada,
    /// con degradación progresiva automática ante cualquier fallo.
    /// </summary>
    public static async Task<ArchiveExtractionResult> UniversalExtractAsync(
        string archivePath,
        string targetDir,
        List<string?> passwordCandidates,
        ArchiveExtractionEngine engine = ArchiveExtractionEngine.Auto,
        string? customSevenZipPath = null,
        IFlowExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var allWarnings = new List<string>();

        // Si se fuerza un motor específico:
        if (engine == ArchiveExtractionEngine.DotNetZip)
        {
            foreach (var pwd in passwordCandidates)
            {
                var res = await DotNetZipArchiveExtractor.ExtractAsync(archivePath, targetDir, pwd, cancellationToken);
                if (res.Success) return res;
            }
            return new ArchiveExtractionResult(
                Success: false,
                ArchivePath: archivePath,
                DestinationDirectory: targetDir,
                ExtractedFiles: [],
                TotalEntriesCount: 0,
                EngineUsed: "DotNetZip",
                ValidPasswordUsed: null,
                ErrorMessage: "Fallo en extracción con motor .NET Zip especificado.",
                Warnings: allWarnings);
        }

        if (engine == ArchiveExtractionEngine.SevenZip)
        {
            foreach (var pwd in passwordCandidates)
            {
                var res = await SevenZipCliRunner.ExtractAsync(archivePath, targetDir, pwd, customSevenZipPath, cancellationToken);
                if (res.Success) return res;
                if (!string.IsNullOrEmpty(res.ErrorMessage)) allWarnings.Add(res.ErrorMessage);
            }
            return new ArchiveExtractionResult(
                Success: false,
                ArchivePath: archivePath,
                DestinationDirectory: targetDir,
                ExtractedFiles: [],
                TotalEntriesCount: 0,
                EngineUsed: "SevenZipCLI",
                ValidPasswordUsed: null,
                ErrorMessage: "Fallo en extracción con motor 7-Zip CLI especificado.",
                Warnings: allWarnings);
        }

        if (engine == ArchiveExtractionEngine.SharpCompress)
        {
            foreach (var pwd in passwordCandidates)
            {
                var res = await SharpCompressResilientExtractor.ExtractAsync(archivePath, targetDir, pwd, cancellationToken);
                if (res.Success) return res;
                if (!string.IsNullOrEmpty(res.ErrorMessage)) allWarnings.Add(res.ErrorMessage);
            }
            return new ArchiveExtractionResult(
                Success: false,
                ArchivePath: archivePath,
                DestinationDirectory: targetDir,
                ExtractedFiles: [],
                TotalEntriesCount: 0,
                EngineUsed: "SharpCompress",
                ValidPasswordUsed: null,
                ErrorMessage: "Fallo en extracción con motor SharpCompress especificado.",
                Warnings: allWarnings);
        }

        // --- MODO AUTO: ESTRATEGIA INTELIGENTE CON FALLBACK MULTI-NIVEL ---

        // Nivel 1: Si es ZIP/CBZ sin contraseña, probar primero con .NET 9 ZipArchive (ultrarrápido)
        if (DotNetZipArchiveExtractor.CanHandle(archivePath))
        {
            var zipResult = await DotNetZipArchiveExtractor.ExtractAsync(archivePath, targetDir, password: null, cancellationToken);
            if (zipResult.Success && zipResult.ExtractedFiles.Count > 0)
            {
                context?.Log($"SafeArchiveExtractor: Extraído exitosamente mediante motor nativo .NET 9 Zip ('{Path.GetFileName(archivePath)}').", LogLevel.Debug);
                return zipResult;
            }
            if (zipResult.Warnings.Count > 0)
            {
                allWarnings.AddRange(zipResult.Warnings);
            }
        }

        // Nivel 2: Si 7-Zip CLI está disponible en el sistema (100% de soporte de códecs/formatos)
        if (SevenZipCliRunner.IsAvailable(customSevenZipPath))
        {
            foreach (var pwd in passwordCandidates)
            {
                var sevenZipRes = await SevenZipCliRunner.ExtractAsync(archivePath, targetDir, pwd, customSevenZipPath, cancellationToken);
                if (sevenZipRes.Success && sevenZipRes.ExtractedFiles.Count > 0)
                {
                    context?.Log($"SafeArchiveExtractor: Extraído exitosamente mediante 7-Zip CLI ('{Path.GetFileName(archivePath)}').", LogLevel.Debug);
                    return sevenZipRes;
                }
                if (!string.IsNullOrEmpty(sevenZipRes.ErrorMessage))
                {
                    allWarnings.Add($"7-Zip intento (pwd: {pwd ?? "sin clave"}): {sevenZipRes.ErrorMessage}");
                }
            }
        }

        // Nivel 3: Fallback a SharpCompress Resiliente (Secuencial + ArchiveFactory)
        foreach (var pwd in passwordCandidates)
        {
            var sharpRes = await SharpCompressResilientExtractor.ExtractAsync(archivePath, targetDir, pwd, cancellationToken);
            if (sharpRes.Success && (sharpRes.ExtractedFiles.Count > 0 || sharpRes.TotalEntriesCount == 0))
            {
                context?.Log($"SafeArchiveExtractor: Extraído exitosamente mediante SharpCompress Resiliente ('{Path.GetFileName(archivePath)}').", LogLevel.Debug);
                return sharpRes;
            }
            if (!string.IsNullOrEmpty(sharpRes.ErrorMessage))
            {
                allWarnings.Add($"SharpCompress intento: {sharpRes.ErrorMessage}");
            }
            allWarnings.AddRange(sharpRes.Warnings);
        }

        return new ArchiveExtractionResult(
            Success: false,
            ArchivePath: archivePath,
            DestinationDirectory: targetDir,
            ExtractedFiles: [],
            TotalEntriesCount: 0,
            EngineUsed: "Auto (All engines failed)",
            ValidPasswordUsed: null,
            ErrorMessage: $"No se pudo extraer el archivo '{Path.GetFileName(archivePath)}' con ninguno de los motores disponibles. Detalles: {string.Join(" | ", allWarnings.Take(3))}",
            Warnings: allWarnings);
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

    public static async Task ExtractNestedArchivesAsync(
        string targetDir,
        List<string?> candidates,
        IFlowExecutionContext context,
        FileFlow.Sdk.Storage.IStorageService? storage,
        ArchiveExtractionEngine engine = ArchiveExtractionEngine.Auto,
        string? customSevenZipPath = null,
        CancellationToken cancellationToken = default)
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
                    string nestedExtractDir = Path.GetDirectoryName(nestedArchive) ?? targetDir;
                    var result = await UniversalExtractAsync(nestedArchive, nestedExtractDir, candidates, engine, customSevenZipPath, context, cancellationToken);

                    if (result.Success)
                    {
                        if (storage != null)
                        {
                            await storage.DeleteAsync(nestedArchive, permanent: true, ct: cancellationToken);
                        }
                        else
                        {
                            File.Delete(nestedArchive);
                        }
                        context.Log($"SmartUnpackNode: Archivo anidado intermedio descomprimido y eliminado '{Path.GetFileName(nestedArchive)}'.", LogLevel.Information);
                    }
                    else
                    {
                        context.Log($"SmartUnpackNode: Advertencia al descomprimir anidado '{nestedArchive}': {result.ErrorMessage}", LogLevel.Warning);
                    }
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
                    if (storage != null)
                    {
                        await storage.DeleteAsync(secVol, permanent: true, ct: cancellationToken);
                    }
                    else
                    {
                        File.Delete(secVol);
                    }
                    context.Log($"SmartUnpackNode: Volumen secundario intermedio eliminado '{Path.GetFileName(secVol)}'.", LogLevel.Information);
                }
                catch { }
            }
        }
    }
}
