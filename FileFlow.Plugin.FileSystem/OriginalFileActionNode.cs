using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("OriginalFileActionNode_Name", "FileSystem", "OriginalFileActionNode_Desc")]
public class OriginalFileActionNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("OriginalFileActionNode_Name", "Original File Action");
    public string Category => "FileSystem";
    public string Description => LocalizationManager.Instance.GetString("OriginalFileActionNode_Desc", "Applies lifecycle policy to the original file (keep, quarantine, or delete).");

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
        ["QuarantinePath"] = @"C:\Quarantine"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string actionType = Parameters.TryGetValue("ActionType", out var val) ? val?.ToString() ?? "Keep" : "Keep";
        string quarantinePath = Parameters.TryGetValue("QuarantinePath", out var qVal) ? qVal?.ToString() ?? @"C:\Quarantine" : @"C:\Quarantine";
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && Convert.ToBoolean(dryVal);

        string targetFilePath = item.OriginalPath;
        if (string.IsNullOrWhiteSpace(targetFilePath) || (!File.Exists(targetFilePath) && !Directory.Exists(targetFilePath)))
        {
            context.Log($"OriginalFileActionNode: Target file '{targetFilePath}' does not exist.", LogLevel.Warning);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            switch (actionType.ToUpperInvariant())
            {
                case "KEEP":
                    context.Log($"[OriginalFileActionNode] Keeping original file: {targetFilePath}", LogLevel.Information);
                    break;

                case "MOVETOQUARANTINE":
                    if (!Directory.Exists(quarantinePath))
                    {
                        Directory.CreateDirectory(quarantinePath);
                    }
                    string destPath = Path.Combine(quarantinePath, Path.GetFileName(targetFilePath));
                    context.Log($"[OriginalFileActionNode] Moving original to quarantine: {destPath} (DryRun={isDryRun})", LogLevel.Information);
                    if (!isDryRun)
                    {
                        if (item.IsDirectory)
                        {
                            Directory.Move(targetFilePath, destPath);
                        }
                        else
                        {
                            File.Move(targetFilePath, destPath, overwrite: true);
                        }
                    }
                    break;

                case "PERMANENTDELETE":
                    context.Log($"[OriginalFileActionNode] Permanently deleting original: {targetFilePath} (DryRun={isDryRun})", LogLevel.Warning);
                    if (!isDryRun)
                    {
                        if (item.IsDirectory)
                        {
                            Directory.Delete(targetFilePath, recursive: true);
                        }
                        else
                        {
                            File.Delete(targetFilePath);
                        }
                    }
                    break;

                default:
                    context.Log($"[OriginalFileActionNode] Unknown action policy: '{actionType}', retaining file.", LogLevel.Warning);
                    break;
            }

            item.AddLog($"OriginalFileActionNode applied policy '{actionType}'");
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            context.Log($"OriginalFileActionNode Error: {ex.Message}", LogLevel.Error);
            item.AddLog($"OriginalFileActionNode failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }
}
