using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("DestinationSinkNode_Name", "Files", "DestinationSinkNode_Desc", PipelineRole.Sink,
    "destino", "guardar", "mover", "escribir", "consolidar", "salida", "output", "sink", "destination")]
public class DestinationSinkNode : IFlowNode
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

        string sourcePath = item.GetExistingPhysicalPath();
        bool hasPhysicalFile = !string.IsNullOrWhiteSpace(sourcePath) && (File.Exists(sourcePath) || Directory.Exists(sourcePath));
        bool hasVirtualContent = item.Metadata.TryGetValue("VirtualContent", out var vc) && vc != null;
        if (!hasVirtualContent && item.Metadata.TryGetValue("ReportContent", out var rc) && rc != null)
        {
            vc = rc;
            hasVirtualContent = true;
        }

        bool isDryRun = context.IsDryRun;

        if (!hasPhysicalFile && !hasVirtualContent)
        {
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Sink_NoFileFound", "[Destination Sink] Input file not found: '{0}'", item.CurrentPath), LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            if (!Directory.Exists(destRoot) && !isDryRun)
            {
                Directory.CreateDirectory(destRoot);
            }

            string fileName = Path.GetFileName(item.CurrentPath);
            string targetPath = Path.Combine(destRoot, fileName);

            if (File.Exists(targetPath))
            {
                switch (strategy.ToUpperInvariant())
                {
                    case "SKIP":
                        context.Log(LocalizationManager.Instance.GetFormattedString("Log_Sink_SkipCollision", "[Destination Sink] Skipped due to existing collision (Strategy: Skip): '{0}'", targetPath), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds);
                        item.AddLog($"DestinationSinkNode skipped due to conflict: {targetPath}");
                        await context.EmitAsync("Done", item);
                        return;

                    case "RENAMEINCREMENTAL":
                        string originalTarget = targetPath;
                        targetPath = GetIncrementalFileName(destRoot, fileName);
                        context.Log(LocalizationManager.Instance.GetFormattedString("Log_Sink_IncrementalRename", "[Destination Sink] Incremental rename to avoid collision: '{0}'", Path.GetFileName(targetPath)), LogLevel.Debug, item);
                        break;

                    case "OVERWRITE":
                    default:
                        context.Log(LocalizationManager.Instance.GetFormattedString("Log_Sink_OverwriteExisting", "[Destination Sink] Overwriting existing target file: '{0}'", targetPath), LogLevel.Debug, item);
                        break;
                }
            }

            if (!isDryRun)
            {
                if (hasPhysicalFile)
                {
                    const long asyncCopyThreshold = 256 * 1024; // 256 KB
                    var fileInfo = new FileInfo(sourcePath);
                    if (fileInfo.Exists && fileInfo.Length > asyncCopyThreshold)
                    {
                        var readOptions = new FileStreamOptions
                        {
                            Mode = FileMode.Open,
                            Access = FileAccess.Read,
                            Share = FileShare.Read,
                            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                            BufferSize = 131072
                        };
                        var writeOptions = new FileStreamOptions
                        {
                            Mode = FileMode.Create,
                            Access = FileAccess.Write,
                            Share = FileShare.None,
                            Options = FileOptions.Asynchronous,
                            BufferSize = 131072
                        };
                        await using var sourceStream = new FileStream(sourcePath, readOptions);
                        await using var destStream = new FileStream(targetPath, writeOptions);
                        await sourceStream.CopyToAsync(destStream, 131072, cancellationToken);
                    }
                    else
                    {
                        File.Copy(sourcePath, targetPath, overwrite: true);
                    }
                }
                else if (hasVirtualContent)
                {
                    if (vc is byte[] bytes)
                    {
                        await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken);
                    }
                    else
                    {
                        await File.WriteAllTextAsync(targetPath, vc?.ToString() ?? string.Empty, cancellationToken);
                    }
                }

                item.PhysicalPath = targetPath;
                item.CurrentPath = targetPath;
            }

            sw.Stop();
            string detailsJson = $"{{\"destinationRoot\": \"{destRoot.Replace("\\", "\\\\")}\", \"targetPath\": \"{targetPath.Replace("\\", "\\\\")}\", \"strategy\": \"{strategy}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}, \"sizeBytes\": {item.FileSizeBytes}}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Sink_SavedSuccess", "[Destination Sink] Successfully saved to '{0}' (Strategy: {1}, DryRun={2})", targetPath, strategy, isDryRun), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            item.AddLog($"DestinationSinkNode output saved to {targetPath}");
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

    private static string GetIncrementalFileName(string folder, string fileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        int counter = 1;
        string targetPath;

        do
        {
            targetPath = Path.Combine(folder, $"{nameWithoutExt}_{counter}{ext}");
            counter++;
        } while (File.Exists(targetPath));

        return targetPath;
    }
}
