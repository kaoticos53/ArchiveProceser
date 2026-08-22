using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("LogOutputNode_Name", "Integrations", "LogOutputNode_Desc")]
public class LogOutputNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("LogOutputNode_Name", "Log Inspector");
    public string Category => "Integrations";
    public string Description => LocalizationManager.Instance.GetString("LogOutputNode_Desc", "Logs detailed context, metadata, tags, and history of incoming items to console.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LogMetadata"] = true,
        ["LogExecutionHistory"] = true,
        ["CompactFormat"] = false,
        ["LogLevel"] = "Information"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        bool logMetadata = Parameters.TryGetValue("LogMetadata", out var mVal) && ParameterHelper.GetBoolean(mVal, true);
        bool logHistory = Parameters.TryGetValue("LogExecutionHistory", out var hVal) && ParameterHelper.GetBoolean(hVal, true);
        bool compactFormat = Parameters.TryGetValue("CompactFormat", out var cVal) && ParameterHelper.GetBoolean(cVal, false);
        string levelStr = Parameters.TryGetValue("LogLevel", out var lVal) ? ParameterHelper.GetString(lVal, "Information") : "Information";

        if (!Enum.TryParse<LogLevel>(levelStr, true, out var parsedLevel))
        {
            context.Log($"LogOutputNode: Invalid log level '{levelStr}', defaulting to Information.", LogLevel.Warning);
            parsedLevel = LogLevel.Information;
        }
        LogLevel level = parsedLevel;

        if (compactFormat)
        {
            double mb = item.FileSizeBytes / (1024.0 * 1024.0);
            string metaSummary = item.Metadata.Count > 0 ? string.Join(", ", item.Metadata.Select(kv => $"{kv.Key}={kv.Value}")) : "none";
            context.Log($"[Log Inspector] {item.CurrentPath} ({mb:F2} MB) | Meta: [{metaSummary}]", level);
            item.AddLog($"LogInspectorNode logged compact item state ({item.CurrentPath})");
            await context.EmitAsync("Out", item);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"=== [Log Inspector Output] ===");
        sb.AppendLine($"• ID: {item.Id}");
        sb.AppendLine($"• Current Path: {item.CurrentPath}");
        sb.AppendLine($"• Original Path: {item.OriginalPath}");
        sb.AppendLine($"• Is Directory: {item.IsDirectory}");
        sb.AppendLine($"• File Size: {item.FileSizeBytes:N0} bytes");

        if (item.Tags.Count > 0)
        {
            sb.AppendLine($"• Tags: [{string.Join(", ", item.Tags)}]");
        }

        if (logMetadata && item.Metadata.Count > 0)
        {
            sb.AppendLine("• Metadata:");
            foreach (var (k, v) in item.Metadata)
            {
                sb.AppendLine($"   - {k}: {v}");
            }
        }

        if (logHistory && item.ExecutionLog.Count > 0)
        {
            sb.AppendLine("• Node History Log:");
            foreach (string entry in item.ExecutionLog)
            {
                sb.AppendLine($"   - {entry}");
            }
        }

        sb.AppendLine("==============================");

        string logMessage = sb.ToString();
        context.Log(logMessage, level);
        item.AddLog($"LogInspectorNode logged item state ({item.CurrentPath})");

        await context.EmitAsync("Out", item);
    }
}
