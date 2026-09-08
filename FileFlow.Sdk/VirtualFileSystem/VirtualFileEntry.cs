namespace FileFlow.Sdk.VirtualFileSystem;

/// <summary>
/// Rol semántico que desempeña una entrada dentro del sistema de archivos virtual.
/// </summary>
public enum VirtualFileRole
{
    /// <summary>
    /// Archivo que reside en la carpeta de origen (entrada).
    /// </summary>
    Source,

    /// <summary>
    /// Archivo resultante que reside en una carpeta de destino (salida/organización).
    /// </summary>
    Destination,

    /// <summary>
    /// Archivo temporal o intermedio en el flujo.
    /// </summary>
    Intermediate
}

/// <summary>
/// Tipo de operación simulada realizada sobre una entrada del sistema de archivos virtual.
/// </summary>
public enum VirtualOperationType
{
    Original,
    Saved,
    Copied,
    Moved,
    Renamed,
    ConflictRenamed,
    Deleted,
    Recycled
}

/// <summary>
/// Representa un archivo o directorio registrado dentro del sistema de archivos virtual en memoria.
/// </summary>
public sealed record VirtualFileEntry(
    string VirtualPath,
    string OriginalPath,
    string FileName,
    string Extension,
    string DirectoryPath,
    long FileSizeBytes,
    VirtualOperationType OperationType,
    string SourceNodeName,
    string SourceNodeId,
    IReadOnlyDictionary<string, object?>? Metadata = null,
    IReadOnlyList<string>? ExecutionLog = null,
    DateTime TimestampUtc = default,
    string? TextContent = null,
    byte[]? BinaryContent = null,
    VirtualFileRole Role = VirtualFileRole.Destination,
    string? DestinationPath = null,
    string? RelatedSourcePath = null
)
{
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = Metadata ?? new Dictionary<string, object?>();
    public IReadOnlyList<string> ExecutionLog { get; init; } = ExecutionLog ?? [];
    public DateTime TimestampUtc { get; init; } = TimestampUtc == default ? DateTime.UtcNow : TimestampUtc;
    public bool IsDirectory => string.IsNullOrEmpty(Extension) && (FileSizeBytes == 0);
}
