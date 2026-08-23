namespace FileFlow.Sdk;

public interface IFlowExecutionContext
{
    bool IsDryRun { get; }
    Task EmitAsync(string outputPortName, FileItemContext item);
    void ReportProgress(double percentage, string statusMessage);
    void SetTotalExpectedItems(long totalExpectedItems) { }
    void Log(string message, LogLevel level);
    void Log(string message, LogLevel level, string? filePath, double durationMs = 0.0) => Log(message, level);
    void RegisterPlannedAction(PlannedAction action);
    void RecordJournalEntry(JournalEntry entry);
}

