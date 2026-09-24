using System.IO;
using System.Text.Json;
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
public sealed class ArchiveFanOutNode : FlowNodeBase, INodeCustomActionProvider
{
    private readonly Lock _lock = new();

    public override string Name => LocalizationManager.Instance.GetString("ArchiveFanOutNode_Name", "Archive Stream Unpack (Fan-Out)");
    public override string Category => "Archives";
    public override string Description => LocalizationManager.Instance.GetString("ArchiveFanOutNode_Desc", "Extrae el contenido de un archivo comprimido y emite CADA elemento extraído como un ítem de flujo individual (Fan-Out 1:N).");

    public ArchiveFanOutNode()
    {
        Inputs =
        [
            new("In", typeof(FileItemContext), PortDirection.Input, "In", "Flujo de archivos comprimidos")
        ];

        Outputs =
        [
            new("Out", typeof(FileItemContext), PortDirection.Output, "Out", "Flujo de archivos individuales extraídos (Fan-Out)"),
            new("Error", typeof(FileItemContext), PortDirection.Output, "Error", "Archivos comprimidos que no se pudieron extraer")
        ];

        Parameters["OutputDirectory"] = "";
        Parameters["ArchiveFormat"] = "Auto";
        Parameters["PreserveDirectoryStructure"] = true;
        Parameters["FilterPattern"] = "*.*";
        Parameters["PasswordList"] = "";
        Parameters["PasswordFile"] = "";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "", DisplayOrder: 1),
        new("ArchiveFormat", ParameterEditorType.Dropdown, DefaultValue: "Auto", DisplayOrder: 2, Options: ["Auto", "Zip", "Rar", "7Zip", "Tar", "GZip"]),
        new("PreserveDirectoryStructure", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3),
        new("FilterPattern", ParameterEditorType.Text, DefaultValue: "*.*", DisplayOrder: 4),
        new("PasswordList", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 5),
        new("PasswordFile", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 7)
    ];

    public override IReadOnlyList<NodeActionDescriptor> CustomActions =>
    [
        new("ManagePasswords", "🔑 Claves...", "🔑", "Gestionar lista de contraseñas para descompresión de archivos cifrados")
    ];

    public async void ExecuteCustomAction(string actionId, object? context = null)
    {
        try
        {
            if (actionId.Equals("ManagePasswords", StringComparison.OrdinalIgnoreCase) ||
                actionId.Equals("OpenPasswordManager", StringComparison.OrdinalIgnoreCase))
            {
                Action? onCompleted = null;
                object? parentWindow = context;

                if (context is NodeCustomActionContext customCtx)
                {
                    parentWindow = customCtx.ParentWindow;
                    onCompleted = customCtx.OnCompleted;
                }
                else if (context is Action callback)
                {
                    onCompleted = callback;
                }

                string currentPasswords = GetParameter("PasswordList", string.Empty);
                var window = new PasswordManagerWindow(currentPasswords);

                Avalonia.Controls.Window? owner = parentWindow as Avalonia.Controls.Window;
                if (owner == null && Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.MainWindow;
                }

                bool result = false;
                if (owner != null)
                {
                    result = await window.ShowDialog<bool>(owner);
                }
                else
                {
                    window.Show();
                }

                if (result)
                {
                    lock (_lock)
                    {
                        Parameters["PasswordList"] = window.PasswordsText;
                    }
                    onCompleted?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ArchiveFanOutNode] Error executing custom action '{actionId}': {ex}");
        }
    }

    public override async Task ExecuteAsync(
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

        string workingPattern = GetParameter("WorkingFolder", @"{TempDir}\FileFlow_Sessions");
        string baseWorkingDir = ParameterHelper.ResolveOutputPath(workingPattern, item);
        bool cleanWrapper = GetParameter("CleanWrapper", false);
        bool deleteOriginal = GetParameter("DeleteOriginalArchive", false);
        bool isDryRun = context.IsDryRun || (item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false));

        string engineStr = GetParameter("ExtractionEngine", "Auto");
        var engine = Enum.TryParse<ArchiveExtractionEngine>(engineStr, true, out var parsedEngine) ? parsedEngine : ArchiveExtractionEngine.Auto;
        string customSevenZipPath = GetParameter("CustomSevenZipPath", "");

        string pwdListParam = GetParameter("PasswordList", "");
        string pwdFileParam = GetParameter("PasswordFile", "");

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

                await context.EmitAsync("Out", childItem);
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

                    await context.EmitAsync("Out", childItem);
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
