using System;
using FileFlow.Sdk.VirtualFileSystem;

namespace FileFlow.Sdk.Storage;

/// <summary>
/// Extensiones y utilidades para obtener instancias seguras de <see cref="IStorageService"/> a partir de un contexto de ejecución.
/// </summary>
public static class FlowExecutionContextStorageExtensions
{
    /// <summary>
    /// Obtiene el servicio de almacenamiento configurado para el contexto de ejecución.
    /// Si el contexto no implementa <see cref="IStorageService"/> o devuelve null (por ejemplo en tests con mocks sueltos),
    /// resuelve automáticamente hacia <see cref="VirtualStorageService"/> si existe un VFS activo, o hacia <see cref="NullStorageService"/> si es físico.
    /// </summary>
    public static IStorageService GetStorage(this IFlowExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var storage = context.Storage;
            if (storage != null && storage is not NullStorageService)
            {
                return storage;
            }

            if (context.VirtualFileSystem != null)
            {
                return new VirtualStorageService(context.VirtualFileSystem);
            }

            return storage ?? NullStorageService.Instance;
        }
        catch
        {
            if (context.VirtualFileSystem != null)
            {
                return new VirtualStorageService(context.VirtualFileSystem);
            }
            return NullStorageService.Instance;
        }
    }
}
