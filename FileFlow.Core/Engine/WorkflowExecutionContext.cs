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
    public string TemporaryDirectory => !string.IsNullOrWhiteSpace(_executor.TemporaryDirectory) ? _executor.TemporaryDirectory : FileFlow.Sdk.Storage.AppPaths.DefaultTempDirectory;
    public FileFlow.Sdk.VirtualFileSystem.IVirtualFileSystemStore? VirtualFileSystem => _executor.VirtualFileSystem;
    public bool IsVirtualFileSystemEnabled => _executor.IsVirtualFileSystemEnabled;

    private FileFlow.Sdk.Storage.IStorageService? _storageService;
    public FileFlow.Sdk.Storage.IStorageService Storage
    {
        get
        {
            if (_storageService != null) return _storageService;
            if ((IsVirtualFileSystemEnabled || (CurrentItem != null && CurrentItem.IsVirtual)) && VirtualFileSystem != null)
            {
                _storageService = new FileFlow.Core.Storage.VirtualStorageService(VirtualFileSystem);
            }
            else
            {
                _storageService = new FileFlow.Core.Storage.PhysicalStorageService(Platform, IsDryRun, RegisterPlannedAction);
            }
            return _storageService;
        }
    }

    public FileFlow.Sdk.Platform.IOsPlatformService Platform => FileFlow.Core.Platform.OsPlatformServiceFactory.Instance;
    public FileFlow.Sdk.Services.IExternalToolsService Tools => FileFlow.Core.Services.ExternalToolsService.Instance;

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
        string? detailsJson = null;
        if (CurrentItem?.Metadata != null && CurrentItem.Metadata.Count > 0)
        {
            try { detailsJson = System.Text.Json.JsonSerializer.Serialize(CurrentItem.Metadata); } catch { }
        }
        _executor.NotifyLog(_sourceNodeId, message, level, effectivePath, fileSize, durationMs, detailsJson: detailsJson, itemId: itemId, fileName: effectiveFileName);
    }

    public void Log(string message, LogLevel level, FileItemContext? item, double durationMs = 0.0, string? detailsJson = null)
    {
        if (_executor.IsLoggingDisabledForNode(_sourceNodeId)) return;
        var effectiveItem = item ?? CurrentItem;
        string? path = effectiveItem?.CurrentPath;
        string? fileName = effectiveItem?.FileName;
        string? itemId = effectiveItem?.IdString;
        long fileSize = effectiveItem?.FileSizeBytes ?? 0;
        if (detailsJson == null && effectiveItem?.Metadata != null && effectiveItem.Metadata.Count > 0)
        {
            try { detailsJson = System.Text.Json.JsonSerializer.Serialize(effectiveItem.Metadata); } catch { }
        }
        _executor.NotifyLog(_sourceNodeId, message, level, path, fileSize, durationMs, detailsJson, itemId, fileName);
    }

    public void Log(string message, LogLevel level, string? filePath, double durationMs, string? detailsJson, string? itemId = null)
    {
        if (_executor.IsLoggingDisabledForNode(_sourceNodeId)) return;
        string? effectivePath = !string.IsNullOrWhiteSpace(filePath) ? filePath : CurrentItem?.CurrentPath;
        string? effectiveFileName = !string.IsNullOrWhiteSpace(filePath) ? null : CurrentItem?.FileName;
        string? effectiveItemId = !string.IsNullOrWhiteSpace(itemId) ? itemId : CurrentItem?.IdString;
        long fileSize = CurrentItem?.FileSizeBytes ?? 0;
        if (detailsJson == null && CurrentItem?.Metadata != null && CurrentItem.Metadata.Count > 0)
        {
            try { detailsJson = System.Text.Json.JsonSerializer.Serialize(CurrentItem.Metadata); } catch { }
        }
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
