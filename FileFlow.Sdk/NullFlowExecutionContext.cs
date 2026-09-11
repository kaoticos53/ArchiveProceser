using System.Threading.Tasks;

namespace FileFlow.Sdk;

/// <summary>
/// Implementación nula de IFlowExecutionContext para ejecuciones aisladas, pruebas o vistas previas.
/// </summary>
public sealed class NullFlowExecutionContext : IFlowExecutionContext
{
    public static readonly NullFlowExecutionContext Instance = new();

    public bool IsDryRun => false;

    public Task EmitAsync(string outputPortName, FileItemContext item) => Task.CompletedTask;

    public void ReportProgress(double percentage, string statusMessage) { }

    public void Log(string message, LogLevel level) { }

    public void RegisterPlannedAction(PlannedAction action) { }

    public void RecordJournalEntry(JournalEntry entry) { }
}
