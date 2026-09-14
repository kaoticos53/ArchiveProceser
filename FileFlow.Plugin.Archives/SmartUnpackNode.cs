using System.IO;
using System.Text.Json;
using System.Windows;
using FileFlow.Plugin.Archives.Services;
using FileFlow.Plugin.Archives.UI.Views;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.SyntheticData;
using FileFlow.Sdk.VirtualFileSystem;

namespace FileFlow.Plugin.Archives;

[NodeDefinition("SmartUnpackNode_Name", "Archives", "SmartUnpackNode_Desc", PipelineRole.Source,
    "descomprimir", "extraer", "zip", "rar", "7z", "tar", "cbz", "cbr", "cb7", "unpack", "extract", "comprimido")]
public sealed class SmartUnpackNode : IFlowNode, INodeCustomActionProvider
{
    private readonly Lock _lock = new();
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SmartUnpackNode_Name", "Smart Unpack");
    public string Category => "Archives";
    public string Description => LocalizationManager.Instance.GetString("SmartUnpackNode_Desc", "Inspects archive structure and extracts intelligently, supporting password lists, multi-engine extraction (.NET 9, 7-Zip, SharpCompress) and multipart archives.");

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
        ["ExtractionEngine"] = "Auto",
        ["CustomSevenZipPath"] = "",
        ["PasswordList"] = "",
        ["PasswordFile"] = ""
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("DestinationFolder", ParameterEditorType.FolderPath, DefaultValue: @"{RelativeDir}\Unpacked", DisplayOrder: 1),
        new("ExtractionEngine", ParameterEditorType.Dropdown, DefaultValue: "Auto", Options: ["Auto", "SevenZip", "DotNetZip", "SharpCompress"], DisplayOrder: 2),
        new("CleanWrapper", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3),
        new("AutoDeleteAfterExtraction", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 4),
        new("RecursiveUnpack", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 5),
        new("CustomSevenZipPath", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 6),
        new("PasswordList", ParameterEditorType.PasswordList, DefaultValue: "", DisplayOrder: 7),
        new("PasswordFile", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 8)
    ];

    public IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("ManagePasswords", "🔑 Claves...", "🔑", "Gestionar lista de contraseñas para descompresión de archivos cifrados")
    ];

    public void ExecuteCustomAction(string actionId, object? context = null)
    {
        if (actionId.Equals("ManagePasswords", StringComparison.OrdinalIgnoreCase) ||
            actionId.Equals("OpenPasswordManager", StringComparison.OrdinalIgnoreCase))
        {
            string currentPasswords = Parameters.TryGetValue("PasswordList", out var pVal) ? pVal?.ToString() ?? string.Empty : string.Empty;
            var window = new PasswordManagerWindow(currentPasswords);
            if (context is Window ownerWindow)
            {
                window.Owner = ownerWindow;
            }
            else if (Application.Current?.MainWindow != null)
            {
                window.Owner = Application.Current.MainWindow;
            }

            if (window.ShowDialog() == true)
            {
                lock (_lock)
                {
                    Parameters["PasswordList"] = window.PasswordsText;
                }
            }
        }
    }

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

        string engineStr = Parameters.TryGetValue("ExtractionEngine", out var eeVal) ? ParameterHelper.GetString(eeVal, "Auto") : "Auto";
        var engine = Enum.TryParse<ArchiveExtractionEngine>(engineStr, true, out var parsedEngine) ? parsedEngine : ArchiveExtractionEngine.Auto;
        string customSevenZipPath = Parameters.TryGetValue("CustomSevenZipPath", out var szVal) ? ParameterHelper.GetString(szVal, "") : "";

        string pwdListParam = Parameters.TryGetValue("PasswordList", out var plVal) ? ParameterHelper.GetString(plVal, "") : "";
        string pwdFileParam = Parameters.TryGetValue("PasswordFile", out var pfVal) ? ParameterHelper.GetString(pfVal, "") : "";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var storage = context.GetStorage();

        bool isVirtualOrSimulated = item.IsVirtual || context.IsVirtualFileSystemEnabled || item.Metadata.ContainsKey("Archive:Entries");

        if (isVirtualOrSimulated && (item.IsVirtual || !await storage.FileExistsAsync(archivePath, cancellationToken) || item.Metadata.ContainsKey("Archive:Entries")))
        {
            string archiveNameNoExt = Path.GetFileNameWithoutExtension(archivePath);
            string finalExtractDir = Path.Combine(destFolder, archiveNameNoExt);

            List<SyntheticArchiveEntryDefinition> simulatedEntries = [];
            if (item.Metadata.TryGetValue("Archive:Entries", out var entriesObj) && entriesObj != null)
            {
                if (entriesObj is string entriesJson && !string.IsNullOrWhiteSpace(entriesJson))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<SyntheticArchiveEntryDefinition>>(entriesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (parsed != null) simulatedEntries = parsed;
                    }
                    catch { }
                }
                else if (entriesObj is List<SyntheticArchiveEntryDefinition> directList)
                {
                    simulatedEntries = directList;
                }
            }

            if (simulatedEntries.Count == 0)
            {
                simulatedEntries.Add(new SyntheticArchiveEntryDefinition(
                    $"{archiveNameNoExt}_content.dat",
                    item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024 * 1024));
            }

            if (context.VirtualFileSystem != null)
            {
                foreach (var entry in simulatedEntries)
                {
                    string entryRel = entry.InnerPath.Replace('\\', '/').TrimStart('/');
                    string targetVirtualPath = Path.Combine(finalExtractDir, entryRel.Replace('/', Path.DirectorySeparatorChar));
                    string targetDir = Path.GetDirectoryName(targetVirtualPath) ?? finalExtractDir;

                    var meta = new Dictionary<string, object?>(entry.Metadata, StringComparer.OrdinalIgnoreCase)
                    {
                        ["UnpackedFrom"] = archivePath,
                        ["VirtualSample"] = true,
                        ["IsVirtual"] = true
                    };

                    var vfe = new VirtualFileEntry(
                        VirtualPath: targetVirtualPath,
                        OriginalPath: targetVirtualPath,
                        FileName: Path.GetFileName(targetVirtualPath),
                        Extension: entry.IsDirectory ? string.Empty : Path.GetExtension(targetVirtualPath),
                        DirectoryPath: targetDir,
                        FileSizeBytes: entry.FileSizeBytes,
                        OperationType: VirtualOperationType.Saved,
                        SourceNodeName: Name,
                        SourceNodeId: Id,
                        Metadata: meta,
                        ExecutionLog: [$"Extracted virtually from {archivePath}"],
                        TimestampUtc: DateTime.UtcNow
                    );
                    context.VirtualFileSystem.AddOrUpdateFile(vfe);
                }
            }

            sw.Stop();
            var outputItem = new FileItemContext(finalExtractDir, isDirectory: true);
            outputItem.Metadata["VirtualSample"] = true;
            outputItem.Metadata["IsVirtual"] = true;
            outputItem.Metadata["UnpackedFrom"] = archivePath;
            outputItem.Metadata["ArchiveFormat"] = Path.GetExtension(archivePath).TrimStart('.').ToUpperInvariant();
            outputItem.Metadata["UnpackedFileCount"] = simulatedEntries.Count;
            outputItem.AddLog($"SmartUnpackNode virtual extraction to {finalExtractDir}");

            context.Log($"[Descompresor] Extracción virtual completada: {simulatedEntries.Count} ficheros simulados en '{finalExtractDir}'", LogLevel.Information, outputItem, durationMs: sw.Elapsed.TotalMilliseconds);

            await context.EmitAsync("Out", outputItem);
            return;
        }

        if (string.IsNullOrWhiteSpace(archivePath) || !await storage.FileExistsAsync(archivePath, cancellationToken))
        {
            context.Log($"[Descompresor] Archivo comprimido no encontrado: '{archivePath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            var passwordCandidates = await SafeArchiveExtractor.GetPasswordCandidatesAsync(pwdListParam, pwdFileParam, item, storage, cancellationToken);
            string archiveNameNoExt = Path.GetFileNameWithoutExtension(archivePath);
            string finalExtractDir = Path.Combine(destFolder, archiveNameNoExt);

            if (!isDryRun)
            {
                if (!await storage.DirectoryExistsAsync(finalExtractDir, cancellationToken))
                {
                    await storage.CreateDirectoryAsync(finalExtractDir, cancellationToken);
                }

                var extractionResult = await SafeArchiveExtractor.UniversalExtractAsync(
                    archivePath,
                    finalExtractDir,
                    passwordCandidates,
                    engine,
                    customSevenZipPath,
                    context,
                    cancellationToken);

                if (!extractionResult.Success)
                {
                    throw new InvalidOperationException(extractionResult.ErrorMessage ?? "Error desconocido en descompresión.");
                }

                // Identificar si existe una única carpeta envoltorio
                var extractedFiles = Directory.Exists(finalExtractDir)
                    ? Directory.GetFiles(finalExtractDir, "*.*", SearchOption.AllDirectories)
                    : [];

                var entryRelKeys = extractedFiles
                    .Select(f => Path.GetRelativePath(finalExtractDir, f).Replace('\\', '/'))
                    .ToList();

                string? commonRoot = ArchiveVolumeResolver.GetCommonRootFolder(entryRelKeys);
                bool hasSingleWrapper = !string.IsNullOrEmpty(commonRoot);

                if (hasSingleWrapper && cleanWrapper && !string.IsNullOrEmpty(commonRoot))
                {
                    string wrapperPath = Path.Combine(finalExtractDir, commonRoot.Trim('/', '\\'));
                    if (Directory.Exists(wrapperPath))
                    {
                        foreach (var subDir in Directory.GetDirectories(wrapperPath))
                        {
                            string destSubDir = Path.Combine(finalExtractDir, Path.GetFileName(subDir));
                            if (!Directory.Exists(destSubDir))
                            {
                                Directory.Move(subDir, destSubDir);
                            }
                        }
                        foreach (var subFile in Directory.GetFiles(wrapperPath))
                        {
                            string destSubFile = Path.Combine(finalExtractDir, Path.GetFileName(subFile));
                            if (!File.Exists(destSubFile))
                            {
                                File.Move(subFile, destSubFile);
                            }
                        }
                        try { Directory.Delete(wrapperPath, true); } catch { }
                    }
                }

                if (recursiveUnpack)
                {
                    await SafeArchiveExtractor.ExtractNestedArchivesAsync(finalExtractDir, passwordCandidates, context, storage, engine, customSevenZipPath, cancellationToken);
                }

                if (autoDelete)
                {
                    await storage.DeleteAsync(archivePath, permanent: true, ct: cancellationToken);
                    context.Log($"[Descompresor] Archivo comprimido original eliminado tras extracción: '{archivePath}'", LogLevel.Debug, item);
                }

                // Recuento final de ficheros extraídos
                var finalFiles = Directory.Exists(finalExtractDir)
                    ? Directory.GetFiles(finalExtractDir, "*.*", SearchOption.AllDirectories)
                    : [];

                sw.Stop();
                var outputItem = new FileItemContext(finalExtractDir, isDirectory: true);
                outputItem.Metadata["UnpackedFrom"] = archivePath;
                outputItem.Metadata["HasSingleWrapper"] = hasSingleWrapper;
                outputItem.Metadata["ArchiveFormat"] = Path.GetExtension(archivePath).TrimStart('.').ToUpperInvariant();
                outputItem.Metadata["UnpackedFileCount"] = finalFiles.Length;
                outputItem.Metadata["ExtractionEngineUsed"] = extractionResult.EngineUsed;
                if (!string.IsNullOrEmpty(extractionResult.ValidPasswordUsed))
                {
                    outputItem.Metadata["UsedPassword"] = extractionResult.ValidPasswordUsed;
                }
                outputItem.AddLog($"SmartUnpackNode extracted to {finalExtractDir} using {extractionResult.EngineUsed}");

                string detailsJson = $"{{\"archive\": \"{archivePath.Replace("\\", "\\\\")}\", \"extractDir\": \"{finalExtractDir.Replace("\\", "\\\\")}\", \"entriesCount\": {finalFiles.Length}, \"engine\": \"{extractionResult.EngineUsed}\", \"hasSingleWrapper\": {hasSingleWrapper.ToString().ToLowerInvariant()}, \"passwordProtected\": {!string.IsNullOrEmpty(extractionResult.ValidPasswordUsed)}}}";
                context.Log($"[Descompresor] Descompresión completada: {finalFiles.Length} ficheros extraídos en '{finalExtractDir}' mediante {extractionResult.EngineUsed}", LogLevel.Information, outputItem, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

                await context.EmitAsync("Out", outputItem);
            }
            else
            {
                sw.Stop();
                var outputItem = new FileItemContext(finalExtractDir, isDirectory: true);
                outputItem.Metadata["UnpackedFrom"] = archivePath;
                outputItem.Metadata["DryRun"] = true;
                await context.EmitAsync("Out", outputItem);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
}
