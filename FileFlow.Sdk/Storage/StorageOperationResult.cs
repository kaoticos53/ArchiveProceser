namespace FileFlow.Sdk.Storage;

/// <summary>
/// Resultado inmutable de una operación sobre el sistema de almacenamiento (físico o virtual).
/// </summary>
public record StorageOperationResult(
    bool IsSuccess,
    string SourcePath,
    string FinalPath,
    bool WasSkipped = false,
    bool WasCollision = false,
    long BytesProcessed = 0,
    string? ErrorMessage = null
)
{
    public static StorageOperationResult Success(string sourcePath, string finalPath, long bytesProcessed = 0, bool wasCollision = false) =>
        new(true, sourcePath, finalPath, WasSkipped: false, WasCollision: wasCollision, BytesProcessed: bytesProcessed);

    public static StorageOperationResult Skipped(string sourcePath, string finalPath) =>
        new(true, sourcePath, finalPath, WasSkipped: true, WasCollision: true);

    public static StorageOperationResult Failure(string sourcePath, string finalPath, string errorMessage) =>
        new(false, sourcePath, finalPath, ErrorMessage: errorMessage);
}
