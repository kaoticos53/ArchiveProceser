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
    string? ItemId,
    string? FilePath,
    string? FileName,
    long FileSizeBytes,
    double DurationMs,
    string Message,
    string? DetailsJson = null
)
{
    public string FormattedTimestamp => $"[{Timestamp:HH:mm:ss.fff}]";

    public bool HasDetails => !string.IsNullOrWhiteSpace(DetailsJson);

    public string ShortItemId => !string.IsNullOrWhiteSpace(ItemId)
        ? (ItemId.Length > 8 ? ItemId[..8] : ItemId)
        : string.Empty;

    public string FormattedFileSize
    {
        get
        {
            if (FileSizeBytes <= 0) return string.Empty;
            if (FileSizeBytes >= 1024 * 1024 * 1024)
                return FormattableString.Invariant($"{FileSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB");
            if (FileSizeBytes >= 1024 * 1024)
                return FormattableString.Invariant($"{FileSizeBytes / (1024.0 * 1024.0):F2} MB");
            if (FileSizeBytes >= 1024)
                return FormattableString.Invariant($"{FileSizeBytes / 1024.0:F1} KB");
            return $"{FileSizeBytes} B";
        }
    }

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
            var item = !string.IsNullOrWhiteSpace(ShortItemId) ? $"[#{ShortItemId}] " : "";
            var file = !string.IsNullOrWhiteSpace(FileName) ? $"📄 {FileName} " : "";
            return $"{FormattedTimestamp} {BadgeText} {node}{item}{file}{Message}";
        }
    }

    public static StructuredLogRecord Create(
        string executionId,
        LogLevel level,
        string message,
        string? nodeId = null,
        string? nodeName = null,
        string? filePath = null,
        double durationMs = 0.0,
        string? itemId = null,
        long fileSizeBytes = 0,
        string? detailsJson = null)
    {
        string? fileName = !string.IsNullOrWhiteSpace(filePath) ? System.IO.Path.GetFileName(filePath) : null;
        return new StructuredLogRecord(
            Id: 0,
            ExecutionId: executionId ?? string.Empty,
            Timestamp: DateTime.Now,
            Level: level,
            NodeId: nodeId,
            NodeName: nodeName,
            ItemId: itemId,
            FilePath: filePath,
            FileName: fileName,
            FileSizeBytes: fileSizeBytes,
            DurationMs: durationMs,
            Message: message,
            DetailsJson: detailsJson
        );
    }
}
