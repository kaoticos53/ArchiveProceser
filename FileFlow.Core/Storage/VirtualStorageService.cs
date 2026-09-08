using FileFlow.Sdk.VirtualFileSystem;

namespace FileFlow.Core.Storage;

/// <summary>
/// Adaptador en FileFlow.Core que hereda de <see cref="FileFlow.Sdk.Storage.VirtualStorageService"/>.
/// Mantiene compatibilidad hacia atrás con referencias existentes dentro del motor de orquestación.
/// </summary>
public class VirtualStorageService : FileFlow.Sdk.Storage.VirtualStorageService
{
    public VirtualStorageService(IVirtualFileSystemStore vfs) : base(vfs)
    {
    }
}
