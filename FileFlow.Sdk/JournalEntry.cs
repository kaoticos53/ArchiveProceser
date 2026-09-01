namespace FileFlow.Sdk;

public enum JournalOperationType
{
    Moved,
    Copied,
    Renamed,
    DeletedToRecycleBin,
    DeletedPermanently,
    CreatedDirectory,
    ModifiedFile
}

public sealed record JournalEntry(
    Guid Id,
    string NodeId,
    JournalOperationType OperationType,
    string SourcePath,
    string? DestinationPath,
    Func<CancellationToken, Task<bool>>? UndoAction = null,
    string? Notes = null
)
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
