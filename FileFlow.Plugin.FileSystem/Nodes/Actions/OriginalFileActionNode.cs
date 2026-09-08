using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("OriginalFileActionNode_Name", "Files", "OriginalFileActionNode_Desc", PipelineRole.Sink,
    "original", "cuarentena", "papelera", "borrar", "eliminar", "quarantine", "recycle", "lifecycle", "cleanup")]
public class OriginalFileActionNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("OriginalFileActionNode_Name", "Original File Action");
    public string Category => "Files";
    public string Description => LocalizationManager.Instance.GetString("OriginalFileActionNode_Desc", "Centralized policy execution on original source files (Keep, Move to Recycle Bin, Move to Quarantine, Permanent Delete).");

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
        ["ActionType"] = "Keep",
        ["QuarantinePath"] = @"{RelativeDir}\Quarantine"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("ActionType", ParameterEditorType.Dropdown, DefaultValue: "Keep", DisplayOrder: 1, Options: ["Keep", "MoveToRecycleBin", "MoveToQuarantine", "PermanentDelete"]),
        new("QuarantinePath", ParameterEditorType.FolderPath, DefaultValue: @"{RelativeDir}\Quarantine", DisplayOrder: 2)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        string actionType = Parameters.TryGetValue("ActionType", out var aVal) ? ParameterHelper.GetString(aVal, "Keep") : "Keep";
        string quarantinePattern = Parameters.TryGetValue("QuarantinePath", out var qVal) ? ParameterHelper.GetString(qVal, @"{RelativeDir}\Quarantine") : @"{RelativeDir}\Quarantine";
        string quarantinePath = ParameterHelper.ResolveOutputPath(quarantinePattern, item);
        string targetFilePath = item.OriginalPath;
        bool isDryRun = context.IsDryRun || (item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false));
        var storage = context.GetStorage();

        bool exists = await storage.FileExistsAsync(targetFilePath, cancellationToken).ConfigureAwait(false)
            || await storage.DirectoryExistsAsync(targetFilePath, cancellationToken).ConfigureAwait(false);

        if (!exists && !isDryRun)
        {
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_SourceNotFound", "[Original File Action] Original file not found: '{0}'", targetFilePath), LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            switch (actionType.ToUpperInvariant())
            {
                case "KEEP":
                    context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_Keep", "[Original File Action] Keeping original file intact: '{0}'", targetFilePath), LogLevel.Information, item);
                    break;

                case "MOVETORECYCLEBIN":
                    string detailsRecycle = $"{{\"action\": \"MoveToRecycleBin\", \"targetPath\": \"{targetFilePath.Replace("\\", "\\\\")}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}}}";
                    context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_Recycle", "[Original File Action] Sending original to Recycle Bin: '{0}' (DryRun={1})", targetFilePath, isDryRun), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsRecycle);

                    if (!isDryRun)
                    {
                        var recycleResult = await storage.DeleteAsync(targetFilePath, permanent: false, cancellationToken).ConfigureAwait(false);
                        if (!recycleResult.IsSuccess)
                        {
                            throw new IOException(recycleResult.ErrorMessage ?? $"Failed to send original file '{targetFilePath}' to Recycle Bin.");
                        }
                    }
                    break;

                case "MOVETOQUARANTINE":
                    string destPath = Path.Combine(quarantinePath, Path.GetFileName(targetFilePath));
                    string detailsMove = $"{{\"action\": \"MoveToQuarantine\", \"quarantinePath\": \"{destPath.Replace("\\", "\\\\")}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}}}";
                    context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_Quarantine", "[Original File Action] Moving original to quarantine: '{0}' (DryRun={1})", destPath, isDryRun), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsMove);

                    if (!isDryRun)
                    {
                        await storage.CreateDirectoryAsync(quarantinePath, cancellationToken).ConfigureAwait(false);
                        var moveResult = await storage.MoveAsync(targetFilePath, destPath, StorageCollisionStrategy.Overwrite, cancellationToken).ConfigureAwait(false);
                        if (!moveResult.IsSuccess)
                        {
                            throw new IOException(moveResult.ErrorMessage ?? $"Failed to move original file to quarantine '{destPath}'.");
                        }
                    }
                    break;

                case "PERMANENTDELETE":
                    string detailsDelete = $"{{\"action\": \"PermanentDelete\", \"targetPath\": \"{targetFilePath.Replace("\\", "\\\\")}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}}}";
                    context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_PermanentDelete", "[Original File Action] Permanently deleting original: '{0}' (DryRun={1})", targetFilePath, isDryRun), LogLevel.Warning, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsDelete);

                    if (!isDryRun)
                    {
                        var deleteResult = await storage.DeleteAsync(targetFilePath, permanent: true, cancellationToken).ConfigureAwait(false);
                        if (!deleteResult.IsSuccess)
                        {
                            throw new IOException(deleteResult.ErrorMessage ?? $"Failed to permanently delete original file '{targetFilePath}'.");
                        }
                    }
                    break;

                default:
                    context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_UnknownPolicy", "[Original File Action] Unknown action policy: '{0}', retaining file.", actionType), LogLevel.Warning, item);
                    break;
            }

            sw.Stop();
            item.AddLog($"OriginalFileActionNode applied policy '{actionType}'");
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"targetPath\": \"{targetFilePath.Replace("\\", "\\\\")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_OriginalAction_Error", "[Original File Action] Error applying policy: {0}", ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"OriginalFileActionNode failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }
}
