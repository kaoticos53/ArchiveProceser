using System.IO;
using System.Text.Json;
using FileFlow.Plugin.Archives.Services;

using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.SyntheticData;
using FileFlow.Sdk.VirtualFileSystem;

namespace FileFlow.Plugin.Archives;

[NodeDefinition("SmartUnpackNode_Name", "Archives", "SmartUnpackNode_Desc", PipelineRole.Source,
    "descomprimir", "extraer", "zip", "rar", "7z", "tar", "cbz", "cbr", "cb7", "unpack", "extract", "comprimido")]
public sealed class SmartUnpackNode : FlowNodeBase, INodeCustomActionProvider, INodeDialogSurfaceProvider
{
    private readonly Lock _lock = new();

    public override string Name => LocalizationManager.Instance.GetString("SmartUnpackNode_Name", "Smart Unpack");
    public override string Category => "Archives";
    public override string Description => LocalizationManager.Instance.GetString("SmartUnpackNode_Desc", "Extrae archivos comprimidos eliminando carpetas redundantes y resolviendo contraseñas automáticamente.");

    public SmartUnpackNode()
    {
        Inputs =
        [
            new("In", typeof(FileItemContext), PortDirection.Input, "In", "Flujo de archivos comprimidos de entrada")
        ];

        Outputs =
        [
            new("Out", typeof(FileItemContext), PortDirection.Output, "Out", "Flujo de archivos extraídos"),
            new("Error", typeof(FileItemContext), PortDirection.Output, "Error", "Archivos con error de descompresión")
        ];

        Parameters["OutputDirectory"] = "";
        Parameters["ArchiveFormat"] = "Auto";
        Parameters["PreserveDirectoryStructure"] = true;
        Parameters["FilterPattern"] = "*.*";
        Parameters["PasswordList"] = "";
        Parameters["CleanRedundantFolder"] = true;
        Parameters["DeleteArchiveAfterExtraction"] = false;
        Parameters["PasswordFile"] = "";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "", DisplayOrder: 1),
        new("ArchiveFormat", ParameterEditorType.Dropdown, DefaultValue: "Auto", DisplayOrder: 2, Options: ["Auto", "Zip", "Rar", "7Zip", "Tar", "GZip"]),
        new("PreserveDirectoryStructure", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3),
        new("FilterPattern", ParameterEditorType.Text, DefaultValue: "*.*", DisplayOrder: 4),
        new("PasswordList", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 5),
        new("CleanRedundantFolder", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 6),
        new("DeleteArchiveAfterExtraction", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 7),
        new("PasswordFile", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 8)
    ];

    public override IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("ManagePasswords", "🔑 Claves...", "🔑", "Gestionar lista de contraseñas para descompresión de archivos cifrados")
    ];

    /// <summary>
    /// El GESTOR DE CONTRASEÑAS que este nodo declara al SDK: es la puerta que CUALQUIER host puede cumplir —la
    /// clave del catálogo (<see cref="DialogKeys.PasswordManager"/>) y el view model portable que la contiene—.
    ///
    /// <para><b>Por qué existe.</b> La ventana del gestor es una ventana de Avalonia y sólo la puede montar un
    /// host con el toolkit del escritorio: sin esta declaración, la acción de la tarjeta y el botón de la fila
    /// no tenían más salida en el host Uno que DECLARAR la frontera —el usuario leía «se abre en el host de
    /// escritorio» en vez de gestionar sus claves—. Con ella, el host que no puede montar la ventana sirve la
    /// MISMA superficie con su propia vista sobre este view model, que es quien escribe la lista en el parámetro
    /// del nodo.</para>
    /// </summary>
    public string DialogKey => DialogKeys.PasswordManager;

    /// <summary>La acción personalizada que esta superficie sustituye: el «🔑 Claves...» de la tarjeta del nodo.</summary>
    public string? ReplacesCustomActionId => "ManagePasswords";

    /// <inheritdoc />
    public object? CreateDialogPayload(object? context = null) =>
        new UI.ViewModels.PasswordManagerViewModel(
            GetParameter("PasswordList", string.Empty),
            SavePasswordList,
            (context as NodeCustomActionContext)?.Dialogs);

    /// <summary>
    /// La vuelta del gestor: la lista que el usuario confirmó se escribe en el parámetro del nodo, que es de
    /// quien lo posee. La usan los dos caminos —la superficie declarada y la ventana del toolkit—, así que la
    /// regla de dónde se guarda vive en un solo sitio.
    /// </summary>
    private void SavePasswordList(string passwords)
    {
        lock (_lock)
        {
            Parameters["PasswordList"] = passwords;
        }
    }

    public async void ExecuteCustomAction(string actionId, object? context = null)
    {
        try
        {
            if (actionId.Equals("ManagePasswords", StringComparison.OrdinalIgnoreCase) ||
                actionId.Equals("OpenPasswordManager", StringComparison.OrdinalIgnoreCase))
            {
                DesktopOnlySurface.Declare(
                    (context as NodeCustomActionContext)?.Dialogs,
                    LocalizationManager.Instance.GetString("PasswordManager_WindowTitle", "Gestor de Claves y Contraseñas"),
                    LocalizationManager.Instance.GetString("Plugin_DesktopOnly_Title", "Ventana del host de escritorio"),
                    LocalizationManager.Instance.GetFormattedString(
                        "Plugin_DesktopOnly_Message",
                        "«{0}» se abre en el host de escritorio: este host no tiene el toolkit que la monta. Ábrela desde la aplicación de escritorio.",
                        LocalizationManager.Instance.GetString("PasswordManager_WindowTitle", "Gestor de Claves y Contraseñas")));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SmartUnpackNode] Error executing custom action '{actionId}': {ex}");
        }
    }

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string archivePath = item.CurrentPath;
        string destPattern = GetParameter("DestinationFolder", @"{RelativeDir}\Unpacked");
        string destFolder = ParameterHelper.ResolveOutputPath(destPattern, item);
        bool cleanWrapper = GetParameter("CleanWrapper", true);
        bool autoDelete = GetParameter("AutoDeleteAfterExtraction", false);
        bool recursiveUnpack = GetParameter("RecursiveUnpack", true);
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);

        string engineStr = GetParameter("ExtractionEngine", "Auto");
        var engine = Enum.TryParse<ArchiveExtractionEngine>(engineStr, true, out var parsedEngine) ? parsedEngine : ArchiveExtractionEngine.Auto;
        string customSevenZipPath = GetParameter("CustomSevenZipPath", "");

        string pwdListParam = GetParameter("PasswordList", "");
        string pwdFileParam = GetParameter("PasswordFile", "");

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
