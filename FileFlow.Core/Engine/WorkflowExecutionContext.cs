using FileFlow.Core.Telemetry;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.Core.Engine;

/// <summary>
/// Contexto de ejecución que enlaza un nodo en ejecución con el motor orquestador y la telemetría.
/// </summary>
public class WorkflowExecutionContext : IFlowExecutionContext
{
    private readonly string _sourceNodeId;
    private readonly WorkflowExecutor _executor;
    private readonly CancellationToken _cancellationToken;
    public FileItemContext? CurrentItem { get; set; }
    internal bool HasEmittedAnyDownstream { get; private set; }

    public WorkflowExecutionContext(
        string sourceNodeId,
        WorkflowExecutor executor,
        CancellationToken cancellationToken,
        FileItemContext? currentItem = null)
    {
        _sourceNodeId = sourceNodeId;
        _executor = executor;
        _cancellationToken = cancellationToken;
        CurrentItem = currentItem;
    }

    public bool IsDryRun => _executor.IsDryRun;

    public async Task EmitAsync(string outputPortName, FileItemContext item)
    {
        HasEmittedAnyDownstream = true;
        await _executor.DispatchEmitAsync(_sourceNodeId, outputPortName, item, _cancellationToken).ConfigureAwait(false);
    }

    public void ReportProgress(double percentage, string statusMessage)
    {
        _executor.SetCustomStatusMessage(statusMessage);
        _executor.NotifyNodeProgress(_sourceNodeId, percentage, statusMessage);
        _executor.NotifyProgress(percentage, statusMessage);
    }

    public void SetTotalExpectedItems(long totalExpectedItems)
    {
        _executor.SetTotalExpectedItems(totalExpectedItems);
    }

    public void Log(string message, LogLevel level)
    {
        if (_executor.IsLoggingDisabledForNode(_sourceNodeId)) return;
        Log(message, level, CurrentItem, 0.0, null);
    }

    public void Log(string message, LogLevel level, string? filePath, double durationMs = 0.0)
    {
        if (_executor.IsLoggingDisabledForNode(_sourceNodeId)) return;
        string? effectivePath = !string.IsNullOrWhiteSpace(filePath) ? filePath : CurrentItem?.CurrentPath;
        string? effectiveFileName = !string.IsNullOrWhiteSpace(filePath) ? null : CurrentItem?.FileName;
        string? itemId = CurrentItem?.IdString;
        long fileSize = CurrentItem?.FileSizeBytes ?? 0;
        _executor.NotifyLog(_sourceNodeId, message, level, effectivePath, fileSize, durationMs, detailsJson: null, itemId: itemId, fileName: effectiveFileName);
    }

    public void Log(string message, LogLevel level, FileItemContext? item, double durationMs = 0.0, string? detailsJson = null)
    {
        if (_executor.IsLoggingDisabledForNode(_sourceNodeId)) return;
        var effectiveItem = item ?? CurrentItem;
        string? path = effectiveItem?.CurrentPath;
        string? fileName = effectiveItem?.FileName;
        string? itemId = effectiveItem?.IdString;
        long fileSize = effectiveItem?.FileSizeBytes ?? 0;
        _executor.NotifyLog(_sourceNodeId, message, level, path, fileSize, durationMs, detailsJson, itemId, fileName);
    }

    public void Log(string message, LogLevel level, string? filePath, double durationMs, string? detailsJson, string? itemId = null)
    {
        if (_executor.IsLoggingDisabledForNode(_sourceNodeId)) return;
        string? effectivePath = !string.IsNullOrWhiteSpace(filePath) ? filePath : CurrentItem?.CurrentPath;
        string? effectiveFileName = !string.IsNullOrWhiteSpace(filePath) ? null : CurrentItem?.FileName;
        string? effectiveItemId = !string.IsNullOrWhiteSpace(itemId) ? itemId : CurrentItem?.IdString;
        long fileSize = CurrentItem?.FileSizeBytes ?? 0;
        _executor.NotifyLog(_sourceNodeId, message, level, effectivePath, fileSize, durationMs, detailsJson, effectiveItemId, effectiveFileName);
    }

    public void RegisterPlannedAction(PlannedAction action)
    {
        _executor.RegisterPlannedAction(action);
    }

    public void RecordJournalEntry(JournalEntry entry)
    {
        _executor.JournalService.Record(entry);
    }
}
