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

[NodeDefinition("ArchiveFanOutNode_Name", "Archives", "ArchiveFanOutNode_Desc", PipelineRole.Source,
    "descomprimir", "fanout", "stream", "extraer", "zip", "rar", "7z", "cbz", "cbr", "cb7", "unpack", "split", "lote")]
public sealed class ArchiveFanOutNode : IFlowNode, INodeCustomActionProvider
{
    private readonly Lock _lock = new();

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ArchiveFanOutNode_Name", "Desempaquetador de Archivo (Fan-Out)");
    public string Category => "Archives";
    public string Description => LocalizationManager.Instance.GetString("ArchiveFanOutNode_Desc", "Descomprime un archivo en una sesión de trabajo temporal y emite cada elemento interno individualmente con metadatos de correlación de sesión.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("ItemOut", typeof(FileItemContext), PortDirection.Output, "ItemOut"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WorkingFolder"] = @"{TempDir}\FileFlow_Sessions",
        ["CleanWrapper"] = false,
        ["DeleteOriginalArchive"] = false,
        ["ExtractionEngine"] = "Auto",
        ["CustomSevenZipPath"] = "",
        ["PasswordList"] = "",
        ["PasswordFile"] = ""
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("WorkingFolder", ParameterEditorType.FolderPath, DefaultValue: @"{TempDir}\FileFlow_Sessions", DisplayOrder: 1, HelpText: "Carpeta temporal base para las sesiones de descompresión."),
        new("ExtractionEngine", ParameterEditorType.Dropdown, DefaultValue: "Auto", Options: ["Auto", "SevenZip", "DotNetZip", "SharpCompress"], DisplayOrder: 2, HelpText: "Motor de descompresión utilizado (Auto selecciona la mejor opción disponible)."),
        new("CleanWrapper", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 3, HelpText: "Elimina la carpeta envoltorio redundante si el archivo contiene una única raíz coincidente con el nombre."),
        new("DeleteOriginalArchive", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 4, HelpText: "Elimina el archivo comprimido original tras la extracción exitosa."),
        new("CustomSevenZipPath", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 5, HelpText: "Ruta opcional al ejecutable 7z.exe."),
        new("PasswordList", ParameterEditorType.PasswordList, DefaultValue: "", DisplayOrder: 6),
        new("PasswordFile", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 7)
    ];

    public IReadOnlyList<NodeActionDescriptor> CustomActions =>
    [
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
        string origArchivePath = !string.IsNullOrWhiteSpace(item.OriginalPath) ? item.OriginalPath : archivePath;
        string? sourceRoot = item.Metadata.TryGetValue("SourceRootPath", out var srp) && srp != null ? srp.ToString() : null;
        string archiveRelDir = FileFlow.Sdk.TemplateEngine.Resolvers.PathRelativeCalculator.CalculateRelativeDirectory(origArchivePath, sourceRoot);
        string archiveRelFile = FileFlow.Sdk.TemplateEngine.Resolvers.PathRelativeCalculator.CalculateRelativeFilePath(origArchivePath, sourceRoot);

        string workingPattern = Parameters.TryGetValue("WorkingFolder", out var wfVal) ? ParameterHelper.GetString(wfVal, @"{TempDir}\FileFlow_Sessions") : @"{TempDir}\FileFlow_Sessions";
        string baseWorkingDir = ParameterHelper.ResolveOutputPath(workingPattern, item);
        bool cleanWrapper = Parameters.TryGetValue("CleanWrapper", out var cwVal) && ParameterHelper.GetBoolean(cwVal, false);
        bool deleteOriginal = Parameters.TryGetValue("DeleteOriginalArchive", out var doVal) && ParameterHelper.GetBoolean(doVal, false);
        bool isDryRun = context.IsDryRun || (item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false));

        string engineStr = Parameters.TryGetValue("ExtractionEngine", out var eeVal) ? ParameterHelper.GetString(eeVal, "Auto") : "Auto";
        var engine = Enum.TryParse<ArchiveExtractionEngine>(engineStr, true, out var parsedEngine) ? parsedEngine : ArchiveExtractionEngine.Auto;
        string customSevenZipPath = Parameters.TryGetValue("CustomSevenZipPath", out var szVal) ? ParameterHelper.GetString(szVal, "") : "";

        string pwdListParam = Parameters.TryGetValue("PasswordList", out var plVal) ? ParameterHelper.GetString(plVal, "") : "";
        string pwdFileParam = Parameters.TryGetValue("PasswordFile", out var pfVal) ? ParameterHelper.GetString(pfVal, "") : "";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var storage = context.GetStorage();

        string sessionId = Guid.NewGuid().ToString("N");
        string sessionWorkingDir = Path.Combine(baseWorkingDir, sessionId);

        bool isVirtualOrSimulated = item.IsVirtual || context.IsVirtualFileSystemEnabled || item.Metadata.ContainsKey("Archive:Entries");

        if (isVirtualOrSimulated && (item.IsVirtual || !await storage.FileExistsAsync(archivePath, cancellationToken) || item.Metadata.ContainsKey("Archive:Entries")))
        {
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
                string archiveNameNoExt = Path.GetFileNameWithoutExtension(archivePath);
                simulatedEntries.Add(new SyntheticArchiveEntryDefinition(
                    $"{archiveNameNoExt}_content.dat",
                    item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024 * 1024));
            }

            sw.Stop();
            int simIndex = 1;
            int totalSim = simulatedEntries.Count;

            context.Log($"[Fan-Out Archivo] Simulación: emitiendo {totalSim} elementos virtuales para sesión {sessionId}", LogLevel.Information, item);

            foreach (var entry in simulatedEntries)
            {
                string entryRel = entry.InnerPath.Replace('\\', '/').TrimStart('/');
                string targetVirtualPath = Path.Combine(sessionWorkingDir, entryRel.Replace('/', Path.DirectorySeparatorChar));

                var childItem = new FileItemContext(targetVirtualPath, isDirectory: entry.IsDirectory)
                {
                    OriginalPath = targetVirtualPath,
                    FileSizeBytes = entry.FileSizeBytes
                };

                childItem.Metadata["IsVirtual"] = true;

                foreach (var kvp in item.Metadata)
                {
                    childItem.Metadata[kvp.Key] = kvp.Value;
                }
                foreach (var kvp in entry.Metadata)
                {
                    childItem.Metadata[kvp.Key] = kvp.Value;
                }

                childItem.Metadata["Archive:SessionId"] = sessionId;
                childItem.Metadata["Archive:OriginalArchivePath"] = origArchivePath;
                childItem.Metadata["Archive:OriginalArchiveFileName"] = Path.GetFileName(origArchivePath);
                childItem.Metadata["Archive:OriginalArchiveFormat"] = Path.GetExtension(origArchivePath).TrimStart('.').ToUpperInvariant();
                childItem.Metadata["Archive:OriginalArchiveRelativeDir"] = archiveRelDir;
                childItem.Metadata["Archive:OriginalArchiveRelativePath"] = archiveRelFile;
                childItem.Metadata["Archive:RelativeDir"] = archiveRelDir;
                childItem.Metadata["Archive:RelativePath"] = entryRel;
                childItem.Metadata["Archive:EntryIndex"] = simIndex++;
                childItem.Metadata["Archive:TotalEntries"] = totalSim;
                childItem.Metadata["Archive:WorkingFolder"] = sessionWorkingDir;
                childItem.Metadata["Archive:CleanWorkingFolder"] = true;

                await context.EmitAsync("ItemOut", childItem);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(archivePath) || !await storage.FileExistsAsync(archivePath, cancellationToken))
        {
            context.Log($"[Fan-Out Archivo] Archivo comprimido no encontrado: '{archivePath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            var passwordCandidates = await SafeArchiveExtractor.GetPasswordCandidatesAsync(pwdListParam, pwdFileParam, item, storage, cancellationToken);

            if (!isDryRun)
            {
                if (!await storage.DirectoryExistsAsync(sessionWorkingDir, cancellationToken))
                {
                    await storage.CreateDirectoryAsync(sessionWorkingDir, cancellationToken);
                }

                var extractionResult = await SafeArchiveExtractor.UniversalExtractAsync(
                    archivePath,
                    sessionWorkingDir,
                    passwordCandidates,
                    engine,
                    customSevenZipPath,
                    context,
                    cancellationToken);

                context.RegisterTemporaryDirectory(sessionWorkingDir);

                if (!extractionResult.Success)
                {
                    throw new InvalidOperationException(extractionResult.ErrorMessage ?? "Error desconocido en descompresión para Fan-Out.");
                }

                // Enumerar todos los archivos reales extraídos en la sesión
                var extractedFiles = Directory.Exists(sessionWorkingDir)
                    ? Directory.GetFiles(sessionWorkingDir, "*.*", SearchOption.AllDirectories)
                    : [];

                if (extractedFiles.Length == 0)
                {
                    context.Log($"[Fan-Out Archivo] El archivo comprimido '{Path.GetFileName(archivePath)}' no contiene ficheros.", LogLevel.Warning, item);
                    await context.EmitAsync("Error", item);
                    return;
                }

                var entryRelKeys = extractedFiles
                    .Select(f => Path.GetRelativePath(sessionWorkingDir, f).Replace('\\', '/'))
                    .ToList();

                string? commonRoot = ArchiveVolumeResolver.GetCommonRootFolder(entryRelKeys);
                bool hasSingleWrapper = !string.IsNullOrEmpty(commonRoot);

                // Si tenía single wrapper y cleanWrapper es true, movemos los contenidos al nivel raíz de la sesión
                if (hasSingleWrapper && cleanWrapper && !string.IsNullOrEmpty(commonRoot))
                {
                    string wrapperPath = Path.Combine(sessionWorkingDir, commonRoot.Trim('/', '\\'));
                    if (Directory.Exists(wrapperPath))
                    {
                        foreach (var subDir in Directory.GetDirectories(wrapperPath))
                        {
                            string destSubDir = Path.Combine(sessionWorkingDir, Path.GetFileName(subDir));
                            if (!Directory.Exists(destSubDir))
                            {
                                Directory.Move(subDir, destSubDir);
                            }
                        }
                        foreach (var subFile in Directory.GetFiles(wrapperPath))
                        {
                            string destSubFile = Path.Combine(sessionWorkingDir, Path.GetFileName(subFile));
                            if (!File.Exists(destSubFile))
                            {
                                File.Move(subFile, destSubFile);
                            }
                        }
                        try { Directory.Delete(wrapperPath, true); } catch { }
                    }

                    // Re-enumerar tras mover
                    extractedFiles = Directory.GetFiles(sessionWorkingDir, "*.*", SearchOption.AllDirectories);
                }

                if (deleteOriginal)
                {
                    await storage.DeleteAsync(archivePath, permanent: true, ct: cancellationToken);
                    context.Log($"[Fan-Out Archivo] Archivo comprimido original eliminado tras extracción: '{archivePath}'", LogLevel.Debug, item);
                }

                int totalCount = extractedFiles.Length;
                sw.Stop();

                context.Log($"[Fan-Out Archivo] Desempaquetados {totalCount} elementos mediante {extractionResult.EngineUsed} en '{sessionWorkingDir}' (Sesión {sessionId}). Emitiendo a downstream...", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds);

                int index = 1;
                foreach (var extractedFile in extractedFiles)
                {
                    string relPath = Path.GetRelativePath(sessionWorkingDir, extractedFile).Replace('\\', '/');
                    long fileSize = new FileInfo(extractedFile).Length;

                    var childItem = new FileItemContext(extractedFile, isDirectory: false)
                    {
                        OriginalPath = extractedFile,
                        FileSizeBytes = fileSize
                    };

                    // Heredar metadatos del padre
                    foreach (var kvp in item.Metadata)
                    {
                        childItem.Metadata[kvp.Key] = kvp.Value;
                    }

                    childItem.Metadata["Archive:SessionId"] = sessionId;
                    childItem.Metadata["Archive:OriginalArchivePath"] = origArchivePath;
                    childItem.Metadata["Archive:OriginalArchiveFileName"] = Path.GetFileName(origArchivePath);
                    childItem.Metadata["Archive:OriginalArchiveFormat"] = Path.GetExtension(origArchivePath).TrimStart('.').ToUpperInvariant();
                    childItem.Metadata["Archive:OriginalArchiveRelativeDir"] = archiveRelDir;
                    childItem.Metadata["Archive:OriginalArchiveRelativePath"] = archiveRelFile;
                    childItem.Metadata["Archive:RelativeDir"] = archiveRelDir;
                    childItem.Metadata["Archive:RelativePath"] = relPath;
                    childItem.Metadata["Archive:EntryIndex"] = index++;
                    childItem.Metadata["Archive:TotalEntries"] = totalCount;
                    childItem.Metadata["Archive:WorkingFolder"] = sessionWorkingDir;
                    childItem.Metadata["Archive:CleanWorkingFolder"] = true;
                    childItem.Metadata["Archive:ExtractionEngineUsed"] = extractionResult.EngineUsed;
                    if (!string.IsNullOrEmpty(extractionResult.ValidPasswordUsed))
                    {
                        childItem.Metadata["Archive:UsedPassword"] = extractionResult.ValidPasswordUsed;
                    }

                    childItem.AddLog($"ArchiveFanOutNode extracted from {archivePath} (Session {sessionId}) using {extractionResult.EngineUsed}");

                    await context.EmitAsync("ItemOut", childItem);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"archive\": \"{archivePath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Fan-Out Archivo] Error al desempaquetar archivo: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"ArchiveFanOutNode error: {ex.Message}");

            var relatedVolumes = ArchiveVolumeResolver.FindRelatedVolumeFiles(archivePath);
            item.Metadata["RelatedVolumeFiles"] = string.Join(";", relatedVolumes);
            item.Metadata["IsMultipartArchive"] = relatedVolumes.Count > 1;

            await context.EmitAsync("Error", item);
        }
    }
}
