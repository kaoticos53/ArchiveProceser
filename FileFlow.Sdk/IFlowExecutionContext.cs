namespace FileFlow.Sdk;

public interface IFlowExecutionContext
{
    bool IsDryRun { get; }
    string TemporaryDirectory => FileFlow.Sdk.Storage.AppPaths.DefaultTempDirectory;
    VirtualFileSystem.IVirtualFileSystemStore? VirtualFileSystem => null;
    bool IsVirtualFileSystemEnabled => VirtualFileSystem != null;
    Task EmitAsync(string outputPortName, FileItemContext item);
    void ReportProgress(double percentage, string statusMessage);
    void SetTotalExpectedItems(long totalExpectedItems) { }
    void Log(string message, LogLevel level);
    void Log(string message, LogLevel level, string? filePath, double durationMs = 0.0) => Log(message, level);
    void Log(string message, LogLevel level, FileItemContext? item, double durationMs = 0.0, string? detailsJson = null) => Log(message, level, item?.CurrentPath, durationMs);
    void Log(string message, LogLevel level, string? filePath, double durationMs, string? detailsJson, string? itemId = null) => Log(message, level, filePath, durationMs);
    FileFlow.Sdk.Storage.IStorageService Storage => VirtualFileSystem != null
        ? new FileFlow.Sdk.Storage.VirtualStorageService(VirtualFileSystem)
        : FileFlow.Sdk.Storage.NullStorageService.Instance;
    FileFlow.Sdk.Platform.IOsPlatformService Platform => FileFlow.Sdk.Platform.NullOsPlatformService.Instance;
    FileFlow.Sdk.Services.IExternalToolsService Tools => FileFlow.Sdk.Services.NullExternalToolsService.Instance;
    void RegisterPlannedAction(PlannedAction action);
    void RecordJournalEntry(JournalEntry entry);
}

