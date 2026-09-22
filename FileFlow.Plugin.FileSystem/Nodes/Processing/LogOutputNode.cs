using System.Text.Json;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("LogOutputNode_Name", "Integrations", "LogOutputNode_Desc", PipelineRole.Control,
    "log", "consola", "mensaje", "registro", "diagnostico", "telemetria", "print")]
public sealed class LogOutputNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("LogOutputNode_Name", "Log Inspector");
    public override string Category => "Integrations";
    public override string Description => LocalizationManager.Instance.GetString("LogOutputNode_Desc", "Logs detailed context, metadata, tags, and history of incoming items to console.");

    public LogOutputNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
        ];

        Parameters["CustomMessage"] = string.Empty;
        Parameters["LogLevel"] = "Information";
        Parameters["LogMetadata"] = true;
        Parameters["LogExecutionHistory"] = true;
        Parameters["CompactFormat"] = false;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("CustomMessage", ParameterEditorType.MultiLineText, DefaultValue: string.Empty, DisplayOrder: 1,
            HelpText: "Mensaje personalizado a registrar en el log (admite variables de plantilla {FileName}, {Extension}, {FileSize}, {AI:VlmTags}, etc.). Si se deja vacío, registrará el resumen de inspección estándar."),
        new("LogLevel", ParameterEditorType.Dropdown, DefaultValue: "Information", DisplayOrder: 2,
            Options: ["Trace", "Debug", "Information", "Warning", "Error", "Critical"],
            HelpText: "Nivel de severidad para el registro en consola y telemetría."),
        new("LogMetadata", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3,
            HelpText: "Incluir diccionario completo de metadatos en la carga JSON de detalles del log."),
        new("LogExecutionHistory", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4,
            HelpText: "Incluir historial de nodos previos por los que ha transitado el elemento."),
        new("CompactFormat", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 5,
            HelpText: "Formato resumido en una sola línea compacta.")
    ];

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string customMsg = GetParameter("CustomMessage", string.Empty);
        bool logMetadata = GetParameter("LogMetadata", false);
        bool logHistory = GetParameter("LogExecutionHistory", false);
        bool compactFormat = GetParameter("CompactFormat", false);
        string levelStr = GetParameter("LogLevel", "Information");

        if (!Enum.TryParse<LogLevel>(levelStr, true, out var parsedLevel))
        {
            context.Log($"LogOutputNode: Invalid log level '{levelStr}', defaulting to Information.", LogLevel.Warning);
            parsedLevel = LogLevel.Information;
        }
        LogLevel level = parsedLevel;

        string fileName = !string.IsNullOrWhiteSpace(item.CurrentPath)
            ? CrossPlatformPath.GetFileName(item.CurrentPath)
            : (!string.IsNullOrWhiteSpace(item.OriginalPath) ? CrossPlatformPath.GetFileName(item.OriginalPath) : "Elemento");

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
        if (!string.IsNullOrWhiteSpace(customMsg))
        {
            summaryMessage = VariableTemplateResolver.Resolve(customMsg, item);
        }
        else if (compactFormat)
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

        if (!string.IsNullOrWhiteSpace(customMsg))
        {
            item.AddLog($"LogOutputNode: {summaryMessage}");
        }
        else
        {
            item.AddLog($"LogOutputNode inspeccionó estado ({fileName})");
        }

        await context.EmitAsync("Out", item);
    }
}
