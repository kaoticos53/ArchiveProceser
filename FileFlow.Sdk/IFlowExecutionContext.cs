namespace FileFlow.Sdk;

public interface IFlowExecutionContext
{
    bool IsDryRun { get; }
    Task EmitAsync(string outputPortName, FileItemContext item);
    void ReportProgress(double percentage, string statusMessage);
    void Log(string message, LogLevel level);
    void RegisterPlannedAction(PlannedAction action);
    void RecordJournalEntry(JournalEntry entry);
}

