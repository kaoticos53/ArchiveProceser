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
        ["QuarantinePath"] = @"{RelativeDir}\Quarantine"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string actionType = Parameters.TryGetValue("ActionType", out var val) ? ParameterHelper.GetString(val, "Keep") : "Keep";
        string quarantinePattern = Parameters.TryGetValue("QuarantinePath", out var qVal) ? ParameterHelper.GetString(qVal, @"{RelativeDir}\Quarantine") : @"{RelativeDir}\Quarantine";
        string quarantinePath = ParameterHelper.ResolveOutputPath(quarantinePattern, item);
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        string targetFilePath = item.OriginalPath;
        if (string.IsNullOrWhiteSpace(targetFilePath) || (!File.Exists(targetFilePath) && !Directory.Exists(targetFilePath)))
        {
            context.Log($"[Acción Archivo Origen] Archivo original no encontrado: '{targetFilePath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            switch (actionType.ToUpperInvariant())
            {
                case "KEEP":
                    context.Log($"[Acción Archivo Origen] Conservando archivo original intacto: '{targetFilePath}'", LogLevel.Information, item);
                    break;

                case "MOVETOQUARANTINE":
                    if (!Directory.Exists(quarantinePath) && !isDryRun)
                    {
                        Directory.CreateDirectory(quarantinePath);
                    }
                    string destPath = Path.Combine(quarantinePath, Path.GetFileName(targetFilePath));
                    string detailsMove = $"{{\"action\": \"MoveToQuarantine\", \"quarantinePath\": \"{destPath.Replace("\\", "\\\\")}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}}}";
                    context.Log($"[Acción Archivo Origen] Moviendo original a cuarentena: '{destPath}' (DryRun={isDryRun})", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsMove);
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
                    string detailsDelete = $"{{\"action\": \"PermanentDelete\", \"targetPath\": \"{targetFilePath.Replace("\\", "\\\\")}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}}}";
                    context.Log($"[Acción Archivo Origen] Eliminando permanentemente original: '{targetFilePath}' (DryRun={isDryRun})", LogLevel.Warning, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsDelete);
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
                    context.Log($"[Acción Archivo Origen] Política de acción desconocida: '{actionType}', reteniendo archivo.", LogLevel.Warning, item);
                    break;
            }

            sw.Stop();
            item.AddLog($"OriginalFileActionNode applied policy '{actionType}'");
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"targetPath\": \"{targetFilePath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Acción Archivo Origen] Error al aplicar política: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"OriginalFileActionNode failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }
}
