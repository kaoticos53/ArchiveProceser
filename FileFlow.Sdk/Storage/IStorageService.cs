using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    /// Rutas de las <b>subcarpetas inmediatas</b> de una carpeta, sin recursión, en orden determinista. Una
    /// carpeta que no existe devuelve una lista vacía (no lanza).
    ///
    /// <para><b>Por qué está en el contrato</b>: sin enumeración, un nodo que recorre un árbol —el limpiador de
    /// carpetas vacías es el caso— sólo puede hacerlo mirando el disco, y entonces una ejecución virtual no
    /// funciona de verdad: el borrado pasa por el almacenamiento (así que se ejecuta el del sistema virtual) y el
    /// recorrido mira el sistema de archivos del anfitrión, que en una ejecución virtual no tiene esas carpetas.
    /// El resultado es un nodo que dice «no hay nada que limpiar» sobre un árbol que existe en su propio almacén.
    /// Con esta pregunta en el contrato, el recorrido y el borrado miran el mismo sitio.</para>
    ///
    /// <para>Tiene implementación por defecto sobre el sistema de archivos para que añadirla no rompa a ninguna
    /// implementación existente: una implementación que no la sobrescriba se comporta como el disco.</para>
    /// </summary>
    ValueTask<IReadOnlyList<string>> EnumerateDirectoriesAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult<IReadOnlyList<string>>(
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
                ? [.. Directory.EnumerateDirectories(path).Order(StringComparer.OrdinalIgnoreCase)]
                : []);

    /// <summary>
    /// Rutas de <b>todo el contenido inmediato</b> de una carpeta —archivos y subcarpetas—, sin recursión, en
    /// orden determinista. Una carpeta que no existe devuelve una lista vacía (no lanza).
    ///
    /// <para>Es la mitad que contesta «¿está vacía esta carpeta?» sin fiarse de la del nodo: quien decide qué es
    /// contenido es el almacén. Ver <see cref="EnumerateDirectoriesAsync"/> para por qué está en el contrato.</para>
    /// </summary>
    ValueTask<IReadOnlyList<string>> EnumerateFileSystemEntriesAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult<IReadOnlyList<string>>(
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
                ? [.. Directory.EnumerateFileSystemEntries(path).Order(StringComparer.OrdinalIgnoreCase)]
                : []);

    /// <summary>
    /// Abre un flujo de solo lectura para el archivo especificado.
    /// </summary>
    ValueTask<Stream> OpenReadAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Abre o crea un flujo de escritura para el archivo especificado.
    /// </summary>
    ValueTask<Stream> OpenWriteAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Abre o crea un flujo de adición (append) para el archivo especificado posicionando el cursor al final.
    /// </summary>
    ValueTask<Stream> OpenAppendAsync(string path, CancellationToken ct = default)
    {
        var streamTask = OpenWriteAsync(path, ct);
        if (streamTask.IsCompletedSuccessfully)
        {
            var stream = streamTask.Result;
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.End);
            }
            return ValueTask.FromResult(stream);
        }
        return AwaitAppendStreamAsync(streamTask);

        static async ValueTask<Stream> AwaitAppendStreamAsync(ValueTask<Stream> task)
        {
            var stream = await task.ConfigureAwait(false);
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.End);
            }
            return stream;
        }
    }

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

    /// <summary>
    /// Obtiene la fecha y hora de creación del archivo (UTC).
    /// </summary>
    ValueTask<DateTimeOffset> GetCreationTimeAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult(DateTimeOffset.UtcNow);

    /// <summary>
    /// Obtiene la fecha y hora de última modificación del archivo (UTC).
    /// </summary>
    ValueTask<DateTimeOffset> GetLastWriteTimeAsync(string path, CancellationToken ct = default) =>
        ValueTask.FromResult(DateTimeOffset.UtcNow);
}
