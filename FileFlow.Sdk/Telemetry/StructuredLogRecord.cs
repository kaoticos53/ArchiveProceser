namespace FileFlow.Sdk.Telemetry;

/// <summary>
/// Registro inmutable de log estructurado con metadatos de ejecución, archivo, nodo y métricas de tiempo.
/// </summary>
public record StructuredLogRecord(
    long Id,
    string ExecutionId,
    DateTime Timestamp,
    LogLevel Level,
    string? NodeId,
    string? NodeName,
    string? FilePath,
    string? FileName,
    double DurationMs,
    string Message
)
{
    public string FormattedTimestamp => $"[{Timestamp:HH:mm:ss.fff}]";

    public string BadgeText => Level switch
    {
        LogLevel.Critical => "[CRT]",
        LogLevel.Error => "[ERR]",
        LogLevel.Warning => "[WRN]",
        LogLevel.Information => "[INF]",
        LogLevel.Debug => "[DBG]",
        _ => "[TRC]"
    };

    public string FormattedLine
    {
        get
        {
            var node = !string.IsNullOrWhiteSpace(NodeName) ? $"[{NodeName}] " : (!string.IsNullOrWhiteSpace(NodeId) ? $"[{NodeId}] " : "");
            var file = !string.IsNullOrWhiteSpace(FileName) ? $"📄 {FileName} " : "";
            return $"{FormattedTimestamp} {BadgeText} {node}{file}{Message}";
        }
    }

    public static StructuredLogRecord Create(
        string executionId,
        LogLevel level,
        string message,
        string? nodeId = null,
        string? nodeName = null,
        string? filePath = null,
        double durationMs = 0.0)
    {
        string? fileName = !string.IsNullOrWhiteSpace(filePath) ? System.IO.Path.GetFileName(filePath) : null;
        return new StructuredLogRecord(
            Id: 0,
            ExecutionId: executionId ?? string.Empty,
            Timestamp: DateTime.Now,
            Level: level,
            NodeId: nodeId,
            NodeName: nodeName,
            FilePath: filePath,
            FileName: fileName,
            DurationMs: durationMs,
            Message: message
        );
    }
}
