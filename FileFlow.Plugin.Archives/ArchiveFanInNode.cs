using System.Collections.Concurrent;
using System.IO;
using FileFlow.Plugin.Archives.Services;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.TemplateEngine;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace FileFlow.Plugin.Archives;

[NodeDefinition("ArchiveFanInNode_Name", "Archives", "ArchiveFanInNode_Desc", PipelineRole.Sink,
    "comprimir", "fanin", "aggregate", "empaquetar", "zip", "7z", "cbz", "targz", "join", "reducir", "lote")]
public sealed class ArchiveFanInNode : IFlowNode
{
    private sealed class ArchiveSessionState
    {
        public string SessionId { get; init; } = string.Empty;
        public string OriginalArchivePath { get; init; } = string.Empty;
        public string OriginalArchiveFileName { get; init; } = string.Empty;
        public string OriginalArchiveFormat { get; init; } = "ZIP";
        public string WorkingFolder { get; init; } = string.Empty;
        public int TotalEntries { get; init; }
        public bool CleanWorkingFolder { get; init; } = true;
        public FileItemContext TemplateItem { get; set; } = null!;
        public List<FileItemContext> ReceivedItems { get; } = [];
        public DateTime CreatedUtc { get; } = DateTime.UtcNow;
    }

    private readonly ConcurrentDictionary<string, ArchiveSessionState> _activeSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ArchiveFanInNode_Name", "Agregador y Empaquetador (Fan-In)");
    public string Category => "Archives";
    public string Description => LocalizationManager.Instance.GetString("ArchiveFanInNode_Desc", "Recolecta todos los archivos procesados de una sesión de descompresión y los re-empaqueta en el archivo final en el destino.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DestinationFolder"] = @"{RelativeDir}\Processed",
        ["ArchiveName"] = "{Archive:OriginalArchiveFileName}",
        ["ArchiveFormat"] = "Auto",
        ["CompressionType"] = "Deflate",
        ["CleanWorkingFolder"] = true,
        ["TimeoutSeconds"] = 120
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("DestinationFolder", ParameterEditorType.FolderPath, DefaultValue: @"{RelativeDir}\Processed", DisplayOrder: 1, HelpText: "Carpeta de destino para el archivo comprimido final."),
        new("ArchiveName", ParameterEditorType.Text, DefaultValue: "{Archive:OriginalArchiveFileName}", DisplayOrder: 2, HelpText: "Nombre del archivo final. Admite tokens como {Archive:OriginalArchiveFileName} o {FileNameWithoutExtension}.cbz."),
        new("ArchiveFormat", ParameterEditorType.Dropdown, DefaultValue: "Auto", DisplayOrder: 3, Options: ["Auto", "CBZ", "ZIP", "7Z", "TAR", "GZ"], HelpText: "Formato de compresión. Auto preserva el formato original."),
        new("CompressionType", ParameterEditorType.Dropdown, DefaultValue: "Deflate", DisplayOrder: 4, Options: ["Deflate", "Store", "LZMA", "BZip2"]),
        new("CleanWorkingFolder", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 5, HelpText: "Elimina los archivos temporales de trabajo tras empaquetar con éxito."),
        new("TimeoutSeconds", ParameterEditorType.Number, DefaultValue: 120, DisplayOrder: 6, Min: 5, Max: 3600, Step: 5)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Verificar si pertenece a una sesión Fan-Out
        if (!item.Metadata.TryGetValue("Archive:SessionId", out var sIdObj) || sIdObj?.ToString() is not { } sessionId || string.IsNullOrWhiteSpace(sessionId))
        {
            context.Log($"[Fan-In Archivo] El elemento '{item.FileName}' no contiene metadato 'Archive:SessionId'. Emitiendo a Out sin agregar.", LogLevel.Warning, item);
            await context.EmitAsync("Out", item);
            return;
        }

        ArchiveSessionState? sessionToComplete = null;

        lock (_lock)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                string origArchivePath = item.Metadata.TryGetValue("Archive:OriginalArchivePath", out var oap) ? oap?.ToString() ?? string.Empty : string.Empty;
                string origArchiveName = item.Metadata.TryGetValue("Archive:OriginalArchiveFileName", out var oan) ? oan?.ToString() ?? item.FileName : item.FileName;
                string origFormat = item.Metadata.TryGetValue("Archive:OriginalArchiveFormat", out var ofm) ? ofm?.ToString() ?? "ZIP" : "ZIP";
                string workingDir = item.Metadata.TryGetValue("Archive:WorkingFolder", out var wf) ? wf?.ToString() ?? string.Empty : string.Empty;
                int totalEntries = item.Metadata.TryGetValue("Archive:TotalEntries", out var te) ? ParameterHelper.GetInt32(te, 1) : 1;
                bool cleanWf = !item.Metadata.TryGetValue("Archive:CleanWorkingFolder", out var cwf) || ParameterHelper.GetBoolean(cwf, true);

                session = new ArchiveSessionState
                {
                    SessionId = sessionId,
                    OriginalArchivePath = origArchivePath,
                    OriginalArchiveFileName = origArchiveName,
                    OriginalArchiveFormat = origFormat,
                    WorkingFolder = workingDir,
                    TotalEntries = totalEntries,
                    CleanWorkingFolder = cleanWf,
                    TemplateItem = item.DeepClone()
                };

                _activeSessions[sessionId] = session;
            }

            session.ReceivedItems.Add(item);
            context.Log($"[Fan-In Archivo] Elemento {session.ReceivedItems.Count}/{session.TotalEntries} recibido para sesión '{session.OriginalArchiveFileName}' ({sessionId})", LogLevel.Debug, item);

            if (session.ReceivedItems.Count >= session.TotalEntries)
            {
                _activeSessions.TryRemove(sessionId, out _);
                sessionToComplete = session;
            }
        }

        if (sessionToComplete != null)
        {
            await CompleteArchiveSessionAsync(sessionToComplete, context, cancellationToken);
        }
    }

    private async Task CompleteArchiveSessionAsync(
        ArchiveSessionState session,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var storage = context.GetStorage();

        string destPattern = Parameters.TryGetValue("DestinationFolder", out var dfVal) && !string.IsNullOrWhiteSpace(dfVal?.ToString())
            ? ParameterHelper.GetString(dfVal, @"{RelativeDir}\Processed")
            : @"{RelativeDir}\Processed";

        string destDir = ParameterHelper.ResolveOutputPath(destPattern, session.TemplateItem);

        string namePattern = Parameters.TryGetValue("ArchiveName", out var aVal) && !string.IsNullOrWhiteSpace(aVal?.ToString())
            ? ParameterHelper.GetString(aVal, "{Archive:OriginalArchiveFileName}")
            : "{Archive:OriginalArchiveFileName}";

        string archiveName = VariableTemplateResolver.Resolve(namePattern, session.TemplateItem);
        if (string.IsNullOrWhiteSpace(archiveName))
        {
            archiveName = !string.IsNullOrWhiteSpace(session.OriginalArchiveFileName)
                ? session.OriginalArchiveFileName
                : "Archive.zip";
        }

        string formatParam = Parameters.TryGetValue("ArchiveFormat", out var fVal)
            ? ParameterHelper.GetString(fVal, "Auto").ToUpperInvariant()
            : "AUTO";

        string targetFormat = formatParam switch
        {
            "AUTO" => session.OriginalArchiveFormat.ToUpperInvariant(),
            _ => formatParam
        };

        // Si el formato es CBZ o el nombre termina en .cbz, aseguramos extensión correcta
        if (targetFormat.Equals("CBZ", StringComparison.OrdinalIgnoreCase))
        {
            if (!archiveName.EndsWith(".cbz", StringComparison.OrdinalIgnoreCase))
            {
                archiveName = Path.ChangeExtension(archiveName, ".cbz");
            }
        }

        string compTypeStr = Parameters.TryGetValue("CompressionType", out var cVal)
            ? ParameterHelper.GetString(cVal, "Deflate").ToUpperInvariant()
            : "DEFLATE";

        try
        {
            if (!await storage.DirectoryExistsAsync(destDir, cancellationToken))
            {
                await storage.CreateDirectoryAsync(destDir, cancellationToken);
            }

            string targetArchivePath = Path.Combine(destDir, archiveName);

            // Sincronizar archivos externos a la carpeta de trabajo de la sesión si algún nodo los guardó fuera
            if (!string.IsNullOrWhiteSpace(session.WorkingFolder) && Directory.Exists(session.WorkingFolder))
            {
                foreach (var recItem in session.ReceivedItems)
                {
                    string curPath = recItem.CurrentPath;
                    if (!string.IsNullOrWhiteSpace(curPath) && File.Exists(curPath))
                    {
                        string fullCur = Path.GetFullPath(curPath);
                        string fullWork = Path.GetFullPath(session.WorkingFolder);

                        if (!fullCur.StartsWith(fullWork, StringComparison.OrdinalIgnoreCase))
                        {
                            // El archivo fue generado en otra carpeta (ej. _optimized.webp en OutputDirectory externo)
                            string relPath = recItem.Metadata.TryGetValue("Archive:RelativePath", out var rpVal) && rpVal != null
                                ? rpVal.ToString()!
                                : Path.GetFileName(curPath);

                            // Cambiar la extensión en el destino si el archivo cambió de formato
                            string targetInnerFile = Path.Combine(session.WorkingFolder, relPath);
                            string? targetExt = Path.GetExtension(curPath);
                            if (!string.IsNullOrEmpty(targetExt) && !string.Equals(Path.GetExtension(targetInnerFile), targetExt, StringComparison.OrdinalIgnoreCase))
                            {
                                targetInnerFile = Path.ChangeExtension(targetInnerFile, targetExt);
                            }

                            string? targetInnerDir = Path.GetDirectoryName(targetInnerFile);
                            if (!string.IsNullOrEmpty(targetInnerDir) && !Directory.Exists(targetInnerDir))
                            {
                                Directory.CreateDirectory(targetInnerDir);
                            }

                            // Si había un original anterior en workingFolder con distinta extensión (ej. .jpg), eliminarlo
                            string oldCandidate = Path.Combine(session.WorkingFolder, relPath);
                            if (File.Exists(oldCandidate) && !string.Equals(oldCandidate, targetInnerFile, StringComparison.OrdinalIgnoreCase))
                            {
                                try { File.Delete(oldCandidate); } catch { }
                            }

                            File.Copy(curPath, targetInnerFile, overwrite: true);

                            // Si el archivo origen era un temporal intermedio, eliminarlo para no acumular basura
                            if (curPath.Contains(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) ||
                                (recItem.Metadata.TryGetValue("IsTemporary", out var isTempObj) && isTempObj is true))
                            {
                                try { File.Delete(curPath); } catch { }
                            }
                        }
                    }
                }
            }

            ArchiveType archiveType = targetFormat switch
            {
                "TAR" => ArchiveType.Tar,
                "GZ" => ArchiveType.GZip,
                "7Z" => ArchiveType.SevenZip,
                "CBZ" => ArchiveType.Zip,
                _ => ArchiveType.Zip
            };

            CompressionType compType = compTypeStr switch
            {
                "STORE" or "NONE" => CompressionType.None,
                "LZMA" => CompressionType.LZMA,
                "BZIP2" => CompressionType.BZip2,
                "PPMD" => CompressionType.PPMd,
                _ => CompressionType.Deflate
            };

            // Empaquetar
            await using (var stream = await storage.OpenWriteAsync(targetArchivePath, cancellationToken))
            using (var writer = WriterFactory.OpenWriter(stream, archiveType, new WriterOptions(compType)))
            {
                if (!string.IsNullOrWhiteSpace(session.WorkingFolder) && Directory.Exists(session.WorkingFolder))
                {
                    var filesToPack = Directory.GetFiles(session.WorkingFolder, "*.*", SearchOption.AllDirectories);
                    foreach (var file in filesToPack)
                    {
                        string relativeEntryName = Path.GetRelativePath(session.WorkingFolder, file).Replace('\\', '/');
                        await using var inStream = await storage.OpenReadAsync(file, cancellationToken);
                        writer.Write(relativeEntryName, inStream);
                    }
                }
            }

            sw.Stop();
            long outSize = await storage.FileExistsAsync(targetArchivePath, cancellationToken)
                ? await storage.GetFileSizeAsync(targetArchivePath, cancellationToken)
                : 0;

            long origSize = !string.IsNullOrWhiteSpace(session.OriginalArchivePath) && File.Exists(session.OriginalArchivePath)
                ? new FileInfo(session.OriginalArchivePath).Length
                : session.ReceivedItems.Sum(i => i.FileSizeBytes);

            double compressionRatio = origSize > 0 ? (double)outSize / origSize * 100.0 : 100.0;
            double savedPercent = origSize > 0 ? (1.0 - ((double)outSize / origSize)) * 100.0 : 0.0;

            // Limpiar carpeta de trabajo temporal si se requiere
            if (session.CleanWorkingFolder && !string.IsNullOrWhiteSpace(session.WorkingFolder) && Directory.Exists(session.WorkingFolder))
            {
                try
                {
                    Directory.Delete(session.WorkingFolder, recursive: true);
                }
                catch (Exception ex)
                {
                    context.Log($"[Fan-In Archivo] No se pudo limpiar carpeta temporal '{session.WorkingFolder}': {ex.Message}", LogLevel.Debug, session.TemplateItem);
                }
            }

            var finalItem = new FileItemContext(targetArchivePath, isDirectory: false)
            {
                OriginalPath = !string.IsNullOrWhiteSpace(session.OriginalArchivePath) ? session.OriginalArchivePath : targetArchivePath,
                FileSizeBytes = outSize
            };

            foreach (var kvp in session.TemplateItem.Metadata)
            {
                finalItem.Metadata[kvp.Key] = kvp.Value;
            }

            finalItem.Metadata["Archive:OriginalArchivePath"] = session.OriginalArchivePath;
            finalItem.Metadata["Archive:OriginalArchiveFileName"] = session.OriginalArchiveFileName;
            finalItem.Metadata["Archive:OriginalSize"] = origSize;
            finalItem.Metadata["Archive:CompressedSize"] = outSize;
            finalItem.Metadata["Archive:SavedBytes"] = origSize - outSize;
            finalItem.Metadata["Archive:SavedPercent"] = Math.Round(savedPercent, 2);
            finalItem.Metadata["Archive:CompressionRatio"] = Math.Round(compressionRatio, 2);
            finalItem.Metadata["Archive:EntriesCount"] = session.ReceivedItems.Count;
            finalItem.Metadata["Archive:TargetFormat"] = targetFormat;
            finalItem.AddLog($"ArchiveFanInNode packed {session.ReceivedItems.Count} entries into {targetArchivePath}");

            string detailsJson = $"{{\"archive\": \"{targetArchivePath.Replace("\\", "\\\\")}\", \"entries\": {session.ReceivedItems.Count}, \"originalBytes\": {origSize}, \"compressedBytes\": {outSize}, \"savingsPct\": {savedPercent.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}}}";
            context.Log($"[Fan-In Archivo] Empaquetado completado: '{Path.GetFileName(targetArchivePath)}' ({session.ReceivedItems.Count} entradas, Ahorro: {savedPercent:F1}%)", LogLevel.Information, finalItem, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            await context.EmitAsync("Out", finalItem);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"session\": \"{session.SessionId}\"}}";
            context.Log($"[Fan-In Archivo] Error al empaquetar sesión: {ex.Message}", LogLevel.Error, session.TemplateItem, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            session.TemplateItem.AddLog($"ArchiveFanInNode error: {ex.Message}");
            await context.EmitAsync("Error", session.TemplateItem);
        }
    }
}
