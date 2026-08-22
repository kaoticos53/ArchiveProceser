namespace FileFlow.Sdk;

public enum PlannedOperationType
{
    Move,
    Copy,
    Rename,
    Delete,
    Recycle,
    Compress,
    Extract,
    TransformMedia,
    ExecuteCommand,
    Custom
}

public sealed record PlannedAction(
    Guid Id,
    string NodeId,
    string NodeName,
    PlannedOperationType OperationType,
    string SourcePath,
    string? DestinationPath,
    string Description,
    long EstimatedImpactBytes = 0,
    IReadOnlyDictionary<string, object?>? AdditionalData = null
)
{
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
