using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("DestinationSinkNode_Name", "Files", "DestinationSinkNode_Desc", PipelineRole.Sink,
    "destino", "guardar", "mover", "escribir", "consolidar", "salida", "output", "sink", "destination")]
public sealed class DestinationSinkNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("DestinationSinkNode_Name", "Destination Sink");
    public string Category => "Files";
    public string Description => LocalizationManager.Instance.GetString("DestinationSinkNode_Desc", "Writes or moves final processed file to projected target path.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Done", typeof(FileItemContext), PortDirection.Output, "Done"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DestinationRoot"] = @"{RelativeDir}\Output",
        ["ConflictStrategy"] = "RenameIncremental"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("DestinationRoot", ParameterEditorType.FolderPath, DefaultValue: @"{RelativeDir}\Output", DisplayOrder: 1),
        new("ConflictStrategy", ParameterEditorType.Dropdown, DefaultValue: "RenameIncremental", DisplayOrder: 2, Options: ["RenameIncremental", "Overwrite", "Skip", "ThrowError"])
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        string destPattern = Parameters.TryGetValue("DestinationRoot", out var dirVal) ? ParameterHelper.GetString(dirVal, @"{RelativeDir}\Output") : @"{RelativeDir}\Output";
        string destRoot = ParameterHelper.ResolveOutputPath(destPattern, item);
        string strategy = Parameters.TryGetValue("ConflictStrategy", out var sVal) ? ParameterHelper.GetString(sVal, "RenameIncremental") : "RenameIncremental";

        StorageCollisionStrategy collisionStrategy = strategy.ToUpperInvariant() switch
        {
            "SKIP" => StorageCollisionStrategy.Skip,
            "THROWERROR" or "FAIL" => StorageCollisionStrategy.ThrowError,
            "OVERWRITE" => StorageCollisionStrategy.Overwrite,
            _ => StorageCollisionStrategy.RenameIncremental
        };

        var storage = context.GetStorage();
        string sourcePath = item.GetExistingPhysicalPath();
        if (string.IsNullOrWhiteSpace(sourcePath)) sourcePath = item.CurrentPath;

        bool hasFile = await storage.FileExistsAsync(sourcePath, cancellationToken).ConfigureAwait(false)
            || await storage.DirectoryExistsAsync(sourcePath, cancellationToken).ConfigureAwait(false);

        bool hasVirtualContent = item.Metadata.TryGetValue("VirtualContent", out var vc) && vc != null;
        if (!hasVirtualContent && item.Metadata.TryGetValue("ReportContent", out var rc) && rc != null)
        {
            vc = rc;
            hasVirtualContent = true;
        }

        if (!hasFile && !hasVirtualContent && !item.IsVirtual && !context.IsVirtualFileSystemEnabled)
        {
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Sink_NoFileFound", "[Destination Sink] Input file not found: '{0}'", item.CurrentPath), LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            string fileName = Path.GetFileName(item.CurrentPath);
            string targetPath = Path.Combine(destRoot, fileName);

            StorageOperationResult result;
            if (hasVirtualContent && !hasFile)
            {
                if (vc is byte[] bytes)
                {
                    result = await storage.WriteAllBytesAsync(targetPath, bytes, collisionStrategy, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result = await storage.WriteAllTextAsync(targetPath, vc?.ToString() ?? string.Empty, collisionStrategy, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                result = await storage.CopyAsync(sourcePath, targetPath, collisionStrategy, cancellationToken).ConfigureAwait(false);
            }

            sw.Stop();

            if (result.WasSkipped)
            {
                context.Log(LocalizationManager.Instance.GetFormattedString("Log_Sink_SkipCollision", "[Destination Sink] Skipped due to existing collision (Strategy: Skip): '{0}'", targetPath), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds);
                item.AddLog($"DestinationSinkNode skipped due to conflict: {targetPath}");
                await context.EmitAsync("Done", item);
                return;
            }

            if (!result.IsSuccess)
            {
                throw new IOException(result.ErrorMessage ?? $"Failed to save file to '{targetPath}'.");
            }

            if (result.WasCollision)
            {
                context.Log(LocalizationManager.Instance.GetFormattedString("Log_Sink_IncrementalRename", "[Destination Sink] Incremental rename to avoid collision: '{0}'", Path.GetFileName(result.FinalPath)), LogLevel.Debug, item);
            }

            // Enlazar entrada de origen y destino si VFS está activo
            if (context.VirtualFileSystem != null)
            {
                var entry = new FileFlow.Sdk.VirtualFileSystem.VirtualFileEntry(
                    VirtualPath: result.FinalPath,
                    OriginalPath: item.OriginalPath,
                    FileName: Path.GetFileName(result.FinalPath),
                    Extension: Path.GetExtension(result.FinalPath),
                    DirectoryPath: destRoot,
                    FileSizeBytes: item.FileSizeBytes > 0 ? item.FileSizeBytes : result.BytesProcessed,
                    OperationType: result.WasCollision ? FileFlow.Sdk.VirtualFileSystem.VirtualOperationType.ConflictRenamed : FileFlow.Sdk.VirtualFileSystem.VirtualOperationType.Saved,
                    Role: FileFlow.Sdk.VirtualFileSystem.VirtualFileRole.Destination,
                    RelatedSourcePath: item.OriginalPath,
                    SourceNodeName: Name,
                    SourceNodeId: Id,
                    Metadata: new Dictionary<string, object?>(item.Metadata, StringComparer.OrdinalIgnoreCase),
                    ExecutionLog: [$"Saved to destination folder: {result.FinalPath}"],
                    TimestampUtc: DateTime.UtcNow
                );
                context.VirtualFileSystem.AddOrUpdateFile(entry);

                if (!string.IsNullOrWhiteSpace(item.OriginalPath))
                {
                    var sourceEntry = context.VirtualFileSystem.GetFile(item.OriginalPath);
                    if (sourceEntry != null && string.IsNullOrEmpty(sourceEntry.DestinationPath))
                    {
                        context.VirtualFileSystem.AddOrUpdateFile(sourceEntry with
                        {
                            DestinationPath = result.FinalPath
                        });
                    }
                }
            }

            item.PhysicalPath = result.FinalPath;
            item.CurrentPath = result.FinalPath;

            string normalDetailsJson = $"{{\"destinationRoot\": \"{destRoot.Replace("\\", "\\\\")}\", \"targetPath\": \"{result.FinalPath.Replace("\\", "\\\\")}\", \"strategy\": \"{strategy}\", \"isDryRun\": {context.IsDryRun.ToString().ToLowerInvariant()}, \"sizeBytes\": {item.FileSizeBytes}}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Sink_SavedSuccess", "[Destination Sink] Successfully saved to '{0}' (Strategy: {1}, DryRun={2})", result.FinalPath, strategy, context.IsDryRun), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: normalDetailsJson);

            item.AddLog($"DestinationSinkNode output saved to {result.FinalPath}");
            await context.EmitAsync("Done", item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errDetails = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"destinationRoot\": \"{destRoot.Replace("\\", "\\\\")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Sink_SaveError", "[Destination Sink] Error saving file: {0}", ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errDetails);
            item.AddLog($"DestinationSinkNode failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }
}
