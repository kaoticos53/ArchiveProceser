namespace FileFlow.Sdk.Common;

/// <summary>
/// Claves estándar de metadatos compartidas por el pipeline DAG y los nodos de FileFlow Studio.
/// </summary>
public static class WellKnownMetadataKeys
{
    public const string WorkflowExecutionId = "WorkflowExecutionId";
    public const string OriginalPath = "OriginalPath";
    public const string FileSizeBytes = "FileSizeBytes";
    public const string Hash = "Hash";
    public const string HashSha256 = "Hash:SHA256";
    public const string DuplicateOf = "DuplicateOf";
    public const string TranscodedFrom = "TranscodedFrom";
    public const string TranscodePreset = "TranscodePreset";
}
