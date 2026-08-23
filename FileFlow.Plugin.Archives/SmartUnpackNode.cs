using FileFlow.Plugin.Archives.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FileFlow.Plugin.Archives;

[NodeDefinition("SmartUnpackNode_Name", "Archives", "SmartUnpackNode_Desc")]
public class SmartUnpackNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SmartUnpackNode_Name", "Smart Unpack");
    public string Category => "Archives";
    public string Description => LocalizationManager.Instance.GetString("SmartUnpackNode_Desc", "Inspects archive structure and extracts intelligently, supporting password lists and multipart archives.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DestinationFolder"] = @"{RelativeDir}\Unpacked",
        ["CleanWrapper"] = true,
        ["AutoDeleteAfterExtraction"] = false,
        ["RecursiveUnpack"] = true,
        ["PasswordList"] = "",
        ["PasswordFile"] = ""
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string archivePath = item.CurrentPath;
        string destPattern = Parameters.TryGetValue("DestinationFolder", out var val) ? ParameterHelper.GetString(val, @"{RelativeDir}\Unpacked") : @"{RelativeDir}\Unpacked";
        string destFolder = ParameterHelper.ResolveOutputPath(destPattern, item);
        bool cleanWrapper = Parameters.TryGetValue("CleanWrapper", out var cwVal) ? ParameterHelper.GetBoolean(cwVal, true) : true;
        bool autoDelete = Parameters.TryGetValue("AutoDeleteAfterExtraction", out var adVal) && ParameterHelper.GetBoolean(adVal, false);
        bool recursiveUnpack = !Parameters.TryGetValue("RecursiveUnpack", out var ruVal) || ParameterHelper.GetBoolean(ruVal, true);
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);

        string pwdListParam = Parameters.TryGetValue("PasswordList", out var plVal) ? ParameterHelper.GetString(plVal, "") : "";
        string pwdFileParam = Parameters.TryGetValue("PasswordFile", out var pfVal) ? ParameterHelper.GetString(pfVal, "") : "";

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            context.Log($"[Descompresor] Archivo comprimido no encontrado: '{archivePath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            var passwordCandidates = GetPasswordCandidates(pwdListParam, pwdFileParam, item);
            var (archive, validPassword) = OpenArchiveWithPassword(archivePath, passwordCandidates, context);

            using (archive)
            {
                var entryKeys = archive.Entries
                    .Where(e => !e.IsDirectory)
                    .Select(e => e.Key?.Replace('\\', '/') ?? string.Empty)
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .ToList();

                string? commonRoot = ArchiveVolumeResolver.GetCommonRootFolder(entryKeys);
                bool hasSingleWrapper = !string.IsNullOrEmpty(commonRoot);

                string archiveNameNoExt = Path.GetFileNameWithoutExtension(archivePath);
                string finalExtractDir;

                if (hasSingleWrapper && cleanWrapper)
                {
                    finalExtractDir = destFolder;
                    context.Log($"[Descompresor] Carpeta envoltorio única detectada ('{commonRoot}'). Limpiando nivel redundante y extrayendo en: {finalExtractDir}", LogLevel.Debug, item);
                }
                else
                {
                    finalExtractDir = Path.Combine(destFolder, archiveNameNoExt);
                    context.Log($"[Descompresor] Múltiples entradas raíz. Extrayendo en subcarpeta: {finalExtractDir}", LogLevel.Debug, item);
                }

                if (!isDryRun)
                {
                    if (!Directory.Exists(finalExtractDir))
                    {
                        Directory.CreateDirectory(finalExtractDir);
                    }

                    string fullTargetDir = Path.GetFullPath(finalExtractDir);
                    string fullTargetDirWithSep = Path.TrimEndingDirectorySeparator(fullTargetDir) + Path.DirectorySeparatorChar;

                    foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string entryPath = entry.Key ?? string.Empty;
                        string destinationPath = Path.GetFullPath(Path.Combine(finalExtractDir, entryPath));

                        if (!destinationPath.StartsWith(fullTargetDirWithSep, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(destinationPath, fullTargetDir, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new System.Security.SecurityException($"Zip Slip attempt detected! Entry '{entryPath}' targets outside of extraction directory.");
                        }

                        entry.WriteToDirectory(finalExtractDir, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }

                    if (recursiveUnpack)
                    {
                        ExtractNestedArchives(finalExtractDir, passwordCandidates, context, cancellationToken);
                    }

                    if (autoDelete)
                    {
                        File.Delete(archivePath);
                        context.Log($"[Descompresor] Archivo comprimido original eliminado tras extracción: '{archivePath}'", LogLevel.Debug, item);
                    }
                }

                sw.Stop();
                var outputItem = new FileItemContext(finalExtractDir, isDirectory: true);
                outputItem.Metadata["UnpackedFrom"] = archivePath;
                outputItem.Metadata["HasSingleWrapper"] = hasSingleWrapper;
                outputItem.Metadata["ArchiveFormat"] = Path.GetExtension(archivePath).TrimStart('.').ToUpperInvariant();
                outputItem.Metadata["UnpackedFileCount"] = entryKeys.Count;
                if (!string.IsNullOrEmpty(validPassword))
                {
                    outputItem.Metadata["UsedPassword"] = validPassword;
                }
                outputItem.AddLog($"SmartUnpackNode extracted to {finalExtractDir}");

                string detailsJson = $"{{\"archive\": \"{archivePath.Replace("\\", "\\\\")}\", \"extractDir\": \"{finalExtractDir.Replace("\\", "\\\\")}\", \"entriesCount\": {entryKeys.Count}, \"hasSingleWrapper\": {hasSingleWrapper.ToString().ToLowerInvariant()}, \"passwordProtected\": {!string.IsNullOrEmpty(validPassword)}}}";
                context.Log($"[Descompresor] Descompresión completada: {entryKeys.Count} ficheros extraídos en '{finalExtractDir}'", LogLevel.Information, outputItem, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

                await context.EmitAsync("Out", outputItem);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"archive\": \"{archivePath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Descompresor] Error en descompresión: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"SmartUnpackNode error: {ex.Message}");

            var relatedVolumes = ArchiveVolumeResolver.FindRelatedVolumeFiles(archivePath);
            item.Metadata["RelatedVolumeFiles"] = string.Join(";", relatedVolumes);
            item.Metadata["IsMultipartArchive"] = relatedVolumes.Count > 1;

            await context.EmitAsync("Error", item);
        }
    }

    private static List<string?> GetPasswordCandidates(string passwordListParam, string passwordFileParam, FileItemContext item)
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

    private static (IArchive archive, string? validPassword) OpenArchiveWithPassword(string archivePath, List<string?> candidates, IFlowExecutionContext context)
    {
        foreach (var pwd in candidates)
        {
            try
            {
                var readerOpts = new ReaderOptions { Password = pwd };
                var archive = ArchiveFactory.OpenArchive(new FileInfo(archivePath), readerOpts);

                var firstEntry = archive.Entries.FirstOrDefault(e => !e.IsDirectory);
                if (firstEntry != null)
                {
                    using var stream = firstEntry.OpenEntryStream();
                    byte[] buffer = new byte[64];
                    int read = stream.Read(buffer, 0, buffer.Length);
                }

                if (!string.IsNullOrEmpty(pwd))
                {
                    context.Log($"SmartUnpackNode: Unlocked password protected archive '{Path.GetFileName(archivePath)}' using valid password.", LogLevel.Information);
                }

                return (archive, pwd);
            }
            catch (Exception ex) when (ex is CryptographicException ||
                                      ex is InvalidFormatException ||
                                      ex is ArchiveException ||
                                      ex is System.IO.InvalidDataException)
            {
                continue;
            }
        }

        throw new CryptographicException($"Archive '{Path.GetFileName(archivePath)}' is password protected or corrupted, and no valid password was found in the provided password list/file.");
    }

    private static void ExtractNestedArchives(string targetDir, List<string?> candidates, IFlowExecutionContext context, CancellationToken cancellationToken)
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
                context.Log($"SmartUnpackNode: Found nested archive '{Path.GetFileName(nestedArchive)}'. Extracting recursively...", LogLevel.Information);

                try
                {
                    var (archive, validPassword) = OpenArchiveWithPassword(nestedArchive, candidates, context);
                    using (archive)
                    {
                        string nestedExtractDir = Path.GetDirectoryName(nestedArchive) ?? targetDir;

                        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            string entryPath = entry.Key ?? string.Empty;
                            string destPath = Path.GetFullPath(Path.Combine(nestedExtractDir, entryPath));

                            if (!destPath.StartsWith(Path.GetFullPath(nestedExtractDir), StringComparison.OrdinalIgnoreCase))
                            {
                                throw new System.Security.SecurityException($"Zip Slip attempt detected in nested archive '{entryPath}'");
                            }

                            entry.WriteToDirectory(nestedExtractDir, new ExtractionOptions
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        }
                    }

                    File.Delete(nestedArchive);
                    context.Log($"SmartUnpackNode: Deleted intermediate nested archive '{Path.GetFileName(nestedArchive)}'.", LogLevel.Information);
                }
                catch (Exception ex)
                {
                    context.Log($"SmartUnpackNode: Failed to unpack nested archive '{nestedArchive}': {ex.Message}", LogLevel.Warning);
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
                    context.Log($"SmartUnpackNode: Deleted intermediate secondary volume '{Path.GetFileName(secVol)}'.", LogLevel.Information);
                }
                catch { }
            }
        }
    }
}
