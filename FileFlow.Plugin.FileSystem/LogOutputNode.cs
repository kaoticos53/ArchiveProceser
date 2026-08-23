using System.Text.Json;
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

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
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

        string fileName = !string.IsNullOrWhiteSpace(item.CurrentPath)
            ? System.IO.Path.GetFileName(item.CurrentPath)
            : (!string.IsNullOrWhiteSpace(item.OriginalPath) ? System.IO.Path.GetFileName(item.OriginalPath) : "Elemento");

        double mb = item.FileSizeBytes / (1024.0 * 1024.0);
        string sizeText = item.FileSizeBytes > 0 ? (mb >= 1.0 ? $"{mb:F2} MB" : $"{item.FileSizeBytes / 1024.0:F1} KB") : (item.IsDirectory ? "Carpeta" : "0 B");

        var payload = new Dictionary<string, object?>
        {
            ["itemId"] = item.Id.ToString(),
            ["currentPath"] = item.CurrentPath,
            ["originalPath"] = item.OriginalPath,
            ["isDirectory"] = item.IsDirectory,
            ["fileSizeBytes"] = item.FileSizeBytes,
            ["tags"] = item.Tags.ToList()
        };

        if (logMetadata && item.Metadata.Count > 0)
        {
            payload["metadata"] = item.Metadata;
        }

        if (logHistory && item.ExecutionLog.Count > 0)
        {
            payload["executionLog"] = item.ExecutionLog;
        }

        string detailsJson = JsonSerializer.Serialize(payload, _jsonOptions);

        string summaryMessage;
        if (compactFormat)
        {
            summaryMessage = $"🔍 {fileName} ({sizeText}) | {item.Metadata.Count} meta";
        }
        else
        {
            var parts = new List<string> { $"🔍 Inspección: {fileName} ({sizeText})" };
            if (item.Tags.Count > 0) parts.Add($"{item.Tags.Count} tags");
            if (item.Metadata.Count > 0) parts.Add($"{item.Metadata.Count} metadatos");
            if (item.ExecutionLog.Count > 0) parts.Add($"{item.ExecutionLog.Count} nodos previos");
            summaryMessage = string.Join(" • ", parts);
        }

        context.Log(summaryMessage, level, item, durationMs: 0.0, detailsJson: detailsJson);
        item.AddLog($"LogOutputNode inspeccionó estado ({fileName})");

        await context.EmitAsync("Out", item);
    }
}
