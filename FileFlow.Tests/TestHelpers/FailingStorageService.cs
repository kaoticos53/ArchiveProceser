using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk.Storage;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// <b>Un almacenamiento que falla</b>: el mismo contrato que el almacenamiento real, con el borrado averiado.
///
/// <para><b>Por qué existe</b>: hay ramas de nodo que solo se recorren cuando el entorno falla —el limpiador de
/// carpetas vacías no tiene ninguna entrada de la configuración que le haga fallar, porque lo único que puede
/// fallar es el borrado—. Sin una avería inyectable, esas ramas quedan declaradas «no forzables» y nadie las
/// ejecuta nunca: es justo donde el hito 188 encontró tres nodos que cortaban el flujo en silencio. Averiar el
/// almacenamiento en la prueba es el camino <b>portable</b>: los permisos del sistema de archivos que hacen
/// fallar un borrado en Linux no lo hacen fallar en Windows, y al revés.</para>
///
/// <para>Solo <see cref="DeleteAsync"/> falla: las demás operaciones se delegan al almacenamiento físico de
/// siempre. Averiar todo a la vez probaría menos —no se sabría qué operación es la que el nodo no supo
/// tolerar— y esta prueba quiere saber exactamente que el nodo pidió el borrado.</para>
/// </summary>
public sealed class FailingStorageService : IStorageService
{
    /// <summary>Cómo se avería el borrado.</summary>
    public enum Fault
    {
        /// <summary>
        /// El almacenamiento <b>responde que no pudo</b>, como el almacenamiento físico real ante un error de E/S
        /// (<c>PhysicalStorageService</c> captura la excepción y devuelve un resultado fallido).
        /// </summary>
        ReportsFailure,

        /// <summary>
        /// El almacenamiento <b>revienta</b>: la avería no está prevista en el contrato y el nodo tiene que
        /// tolerarla igual, por la misma rama.
        /// </summary>
        Throws
    }

    private readonly Fault _fault;
    private readonly string? _onlyForPath;

    private FailingStorageService(Fault fault, string? onlyForPath)
    {
        _fault = fault;
        _onlyForPath = onlyForPath;
    }

    /// <summary>El borrado responde con un fallo (el caso que el nodo tiene que leer del resultado).</summary>
    /// <param name="onlyForPath">
    /// Ruta a la que se limita la avería. <c>null</c> avería cualquier borrado.
    /// </param>
    public static FailingStorageService DeleteReportsFailure(string? onlyForPath = null) =>
        new(Fault.ReportsFailure, onlyForPath);

    /// <summary>El borrado lanza una excepción (la avería que el contrato no cubre).</summary>
    /// <param name="onlyForPath">
    /// Ruta a la que se limita la avería. <c>null</c> avería cualquier borrado.
    /// </param>
    public static FailingStorageService DeleteThrows(string? onlyForPath = null) =>
        new(Fault.Throws, onlyForPath);

    /// <summary>
    /// Las operaciones que el nodo pidió, en orden (<c>DeleteAsync:&lt;ruta&gt;</c>). Es lo que permite afirmar
    /// que el nodo pidió el borrado al almacenamiento y no lo hizo por su cuenta contra el disco.
    /// </summary>
    public List<string> Operations { get; } = [];

    /// <summary>Rutas cuyo borrado se pidió.</summary>
    public List<string> DeletedPaths { get; } = [];

    private static IStorageService Physical => NullStorageService.Instance;

    public ValueTask<StorageOperationResult> DeleteAsync(string path, bool permanent = false, CancellationToken ct = default)
    {
        Operations.Add($"DeleteAsync:{path}");
        DeletedPaths.Add(path);

        if (_onlyForPath is not null && !string.Equals(path, _onlyForPath, StringComparison.Ordinal))
        {
            return Physical.DeleteAsync(path, permanent, ct);
        }

        return _fault switch
        {
            Fault.Throws => throw new IOException($"Almacenamiento averiado (inyectado): no se pudo borrar '{path}'."),
            _ => ValueTask.FromResult(StorageOperationResult.Failure(
                path, path, $"Almacenamiento averiado (inyectado): no se pudo borrar '{path}'."))
        };
    }

    public ValueTask<bool> FileExistsAsync(string path, CancellationToken ct = default) =>
        Physical.FileExistsAsync(path, ct);

    public ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken ct = default) =>
        Physical.DirectoryExistsAsync(path, ct);

    public ValueTask<IReadOnlyList<string>> EnumerateDirectoriesAsync(string path, CancellationToken ct = default) =>
        Physical.EnumerateDirectoriesAsync(path, ct);

    public ValueTask<IReadOnlyList<string>> EnumerateFileSystemEntriesAsync(string path, CancellationToken ct = default) =>
        Physical.EnumerateFileSystemEntriesAsync(path, ct);

    public ValueTask CreateDirectoryAsync(string path, CancellationToken ct = default) =>
        Physical.CreateDirectoryAsync(path, ct);

    public ValueTask<Stream> OpenReadAsync(string path, CancellationToken ct = default) =>
        Physical.OpenReadAsync(path, ct);

    public ValueTask<Stream> OpenWriteAsync(string path, CancellationToken ct = default) =>
        Physical.OpenWriteAsync(path, ct);

    public ValueTask<StorageOperationResult> CopyAsync(
        string sourcePath,
        string targetPath,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default) =>
        Physical.CopyAsync(sourcePath, targetPath, collisionStrategy, ct);

    public ValueTask<StorageOperationResult> MoveAsync(
        string sourcePath,
        string targetPath,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default) =>
        Physical.MoveAsync(sourcePath, targetPath, collisionStrategy, ct);

    public ValueTask<string> ResolveCollisionAsync(string targetPath, StorageCollisionStrategy strategy, CancellationToken ct = default) =>
        Physical.ResolveCollisionAsync(targetPath, strategy, ct);

    public ValueTask<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default) =>
        Physical.ReadAllBytesAsync(path, ct);

    public ValueTask<string> ReadAllTextAsync(string path, CancellationToken ct = default) =>
        Physical.ReadAllTextAsync(path, ct);

    public ValueTask<StorageOperationResult> WriteAllBytesAsync(
        string path,
        byte[] bytes,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default) =>
        Physical.WriteAllBytesAsync(path, bytes, collisionStrategy, ct);

    public ValueTask<StorageOperationResult> WriteAllTextAsync(
        string path,
        string text,
        StorageCollisionStrategy collisionStrategy = StorageCollisionStrategy.Overwrite,
        CancellationToken ct = default) =>
        Physical.WriteAllTextAsync(path, text, collisionStrategy, ct);

    public ValueTask<long> GetFileSizeAsync(string path, CancellationToken ct = default) =>
        Physical.GetFileSizeAsync(path, ct);

    public ValueTask<DateTimeOffset> GetCreationTimeAsync(string path, CancellationToken ct = default) =>
        Physical.GetCreationTimeAsync(path, ct);

    public ValueTask<DateTimeOffset> GetLastWriteTimeAsync(string path, CancellationToken ct = default) =>
        Physical.GetLastWriteTimeAsync(path, ct);
}
