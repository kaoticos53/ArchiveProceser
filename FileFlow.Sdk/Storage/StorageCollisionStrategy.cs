namespace FileFlow.Sdk.Storage;

/// <summary>
/// Estrategia de resolución cuando existe un conflicto de nombre al guardar, copiar o mover un archivo.
/// </summary>
public enum StorageCollisionStrategy
{
    /// <summary>
    /// Genera automáticamente un sufijo numérico incremental (ej: nombre_1.ext, nombre_2.ext).
    /// </summary>
    RenameIncremental,

    /// <summary>
    /// Sobrescribe el archivo de destino existente.
    /// </summary>
    Overwrite,

    /// <summary>
    /// Omite la operación de almacenamiento sin generar error.
    /// </summary>
    Skip,

    /// <summary>
    /// Lanza una excepción de I/O si el archivo destino ya existe.
    /// </summary>
    ThrowError
}
