using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("SafeRecycleDeleteNode_Name", "Files", "SafeRecycleDeleteNode_Desc", PipelineRole.Sink,
    "papelera", "borrar", "eliminar", "recycle", "delete", "trash", "recyclebin")]
public class SafeRecycleDeleteNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SafeRecycleDeleteNode_Name", "Safe Recycle Delete");
    public string Category => "Files";
    public string Description => LocalizationManager.Instance.GetString("SafeRecycleDeleteNode_Desc", "Sends files or folders to Windows Recycle Bin using native Shell API, ensuring they are recoverable and supporting rollback.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Deleted", typeof(FileItemContext), PortDirection.Output, "Deleted"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DeleteOriginalPath"] = false,
        ["ConfirmRecycle"] = true
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        bool deleteOriginal = Parameters.TryGetValue("DeleteOriginalPath", out var dVal) && ParameterHelper.GetBoolean(dVal, false);
        string targetPath = deleteOriginal ? item.OriginalPath : item.CurrentPath;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var storage = context.GetStorage();

        bool exists = await storage.FileExistsAsync(targetPath, cancellationToken).ConfigureAwait(false)
            || await storage.DirectoryExistsAsync(targetPath, cancellationToken).ConfigureAwait(false);

        if (!exists && !context.IsDryRun)
        {
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_SafeRecycle_NotFound", "[Safe Recycle] File or folder not found: '{0}'", targetPath), LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        if (context.IsDryRun)
        {
            context.RegisterPlannedAction(new PlannedAction(
                Guid.NewGuid(),
                Id,
                Name,
                PlannedOperationType.Recycle,
                targetPath,
                null,
                "Send to Windows Recycle Bin",
                item.FileSizeBytes
            ));
            item.AddLog($"[DryRun] Planned Safe Recycle Delete: {targetPath}");
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_SafeRecycle_DryRun", "[Safe Recycle] [DryRun] Planned sending to Recycle Bin: '{0}'", targetPath), LogLevel.Information, item);
            await context.EmitAsync("Deleted", item);
            return;
        }

        try
        {
            var result = await storage.DeleteAsync(targetPath, permanent: false, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (result.IsSuccess)
            {
                if (!context.IsDryRun)
                {
                    context.RecordJournalEntry(new JournalEntry(
                        Guid.NewGuid(),
                        Id,
                        JournalOperationType.DeletedToRecycleBin,
                        targetPath,
                        null,
                        Notes: "Sent to Recycle Bin via Storage Service"
                    ));
                }

                string detailsJson = $"{{\"targetPath\": \"{targetPath.Replace("\\", "\\\\")}\", \"fileSizeBytes\": {item.FileSizeBytes}, \"deleteOriginal\": {deleteOriginal.ToString().ToLowerInvariant()}}}";
                context.Log(LocalizationManager.Instance.GetFormattedString("Log_SafeRecycle_Success", "[Safe Recycle] Item successfully sent to Recycle Bin: '{0}'", Path.GetFileName(targetPath)), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

                item.AddLog($"Sent to Recycle Bin: {targetPath}");
                await context.EmitAsync("Deleted", item);
            }
            else
            {
                throw new IOException(result.ErrorMessage ?? $"Failed to recycle '{targetPath}'.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"targetPath\": \"{targetPath.Replace("\\", "\\\\")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_SafeRecycle_Error", "[Safe Recycle] Error sending to Recycle Bin: {0}", ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"Recycle failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }
}
