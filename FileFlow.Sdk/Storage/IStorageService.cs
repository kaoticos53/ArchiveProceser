using System.IO;

namespace FileFlow.Sdk.Storage;

/// <summary>
/// Servicio universal y desacoplado para operaciones sobre el sistema de archivos (físico o virtual).
/// Permite que los nodos de procesamiento ejecuten I/O sin acoplarse al sistema operativo ni a rutas físicas.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Determina si un archivo existe en la ruta indicada.
    /// </summary>
    ValueTask<bool> FileExistsAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Determina si un directorio existe en la ruta indicada.
    /// </summary>
    ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Crea el directorio en la ruta especificada si aún no existe.
    /// </summary>
    ValueTask CreateDirectoryAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Abre un flujo de solo lectura para el archivo especificado.
    /// </summary>
    ValueTask<Stream> OpenReadAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Abre o crea un flujo de escritura para el archivo especificado.
    /// </summary>
    ValueTask<Stream> OpenWriteAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Copia un archivo desde la ruta de origen a la de destino aplicando la estrategia de colisión especificada.
    /// </summary>
    ValueTask<StorageOperationResult> CopyAsync(
        string sourcePath,
        string targetPath,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default);

    /// <summary>
    /// Mueve un archivo desde la ruta de origen a la de destino aplicando la estrategia de colisión especificada.
    /// </summary>
    ValueTask<StorageOperationResult> MoveAsync(
        string sourcePath,
        string targetPath,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina un archivo o directorio. Si <paramref name="permanent"/> es falso, se envía a la papelera del SO o reciclaje virtual.
    /// </summary>
    ValueTask<StorageOperationResult> DeleteAsync(
        string path,
        bool permanent = false,
        CancellationToken ct = default);

    /// <summary>
    /// Resuelve de forma anticipada la ruta resultante ante colisiones según la estrategia indicada.
    /// </summary>
    ValueTask<string> ResolveCollisionAsync(
        string targetPath,
        StorageCollisionStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Lee la totalidad de los bytes del archivo en la ruta indicada.
    /// </summary>
    ValueTask<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Lee la totalidad del contenido textual del archivo en la ruta indicada.
    /// </summary>
    ValueTask<string> ReadAllTextAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Escribe los bytes proporcionados en la ruta indicada con resolución de colisiones.
    /// </summary>
    ValueTask<StorageOperationResult> WriteAllBytesAsync(
        string path,
        byte[] bytes,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default);

    /// <summary>
    /// Escribe el texto proporcionado en la ruta indicada con resolución de colisiones.
    /// </summary>
    ValueTask<StorageOperationResult> WriteAllTextAsync(
        string path,
        string text,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene el tamaño en bytes del archivo.
    /// </summary>
    ValueTask<long> GetFileSizeAsync(string path, CancellationToken ct = default);
}
