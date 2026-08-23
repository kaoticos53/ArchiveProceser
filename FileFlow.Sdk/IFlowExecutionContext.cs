namespace FileFlow.Sdk;

public interface IFlowExecutionContext
{
    bool IsDryRun { get; }
    Task EmitAsync(string outputPortName, FileItemContext item);
    void ReportProgress(double percentage, string statusMessage);
    void SetTotalExpectedItems(long totalExpectedItems) { }
    void Log(string message, LogLevel level);
    void Log(string message, LogLevel level, string? filePath, double durationMs = 0.0) => Log(message, level);
    void Log(string message, LogLevel level, FileItemContext? item, double durationMs = 0.0, string? detailsJson = null) => Log(message, level, item?.CurrentPath, durationMs);
    void Log(string message, LogLevel level, string? filePath, double durationMs, string? detailsJson, string? itemId = null) => Log(message, level, filePath, durationMs);
    void RegisterPlannedAction(PlannedAction action);
    void RecordJournalEntry(JournalEntry entry);
}

