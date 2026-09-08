using System.Collections.Generic;

namespace FileFlow.Sdk.VirtualFileSystem;

/// <summary>
/// Contrato para el almacén del sistema de archivos virtual (VFS) en memoria.
/// </summary>
public interface IVirtualFileSystemStore
{
    /// <summary>
    /// Total de archivos registrados activamente en el VFS.
    /// </summary>
    int TotalFiles { get; }

    /// <summary>
    /// Total de bytes acumulados por los archivos registrados en el VFS.
    /// </summary>
    long TotalBytes { get; }

    /// <summary>
    /// Registra o actualiza una entrada de archivo en el VFS.
    /// </summary>
    void AddOrUpdateFile(VirtualFileEntry file);

    /// <summary>
    /// Registra explícitamente un directorio virtual en el VFS.
    /// </summary>
    void AddOrUpdateDirectory(string directoryPath);

    /// <summary>
    /// Comprueba si existe un archivo virtual en la ruta especificada.
    /// </summary>
    bool FileExists(string virtualPath);

    /// <summary>
    /// Comprueba si existe un directorio virtual en la ruta especificada.
    /// </summary>
    bool DirectoryExists(string virtualPath);

    /// <summary>
    /// Obtiene la entrada de archivo para la ruta virtual dada, o null si no existe.
    /// </summary>
    VirtualFileEntry? GetFile(string virtualPath);

    /// <summary>
    /// Obtiene todos los archivos virtuales registrados en el VFS.
    /// </summary>
    IReadOnlyList<VirtualFileEntry> GetAllFiles();

    /// <summary>
    /// Obtiene los archivos virtuales que pertenecen a carpetas de origen.
    /// </summary>
    IReadOnlyList<VirtualFileEntry> GetSourceFiles();

    /// <summary>
    /// Obtiene los archivos virtuales que pertenecen a carpetas de destino (salida/organización).
    /// </summary>
    IReadOnlyList<VirtualFileEntry> GetDestinationFiles();

    /// <summary>
    /// Obtiene todas las rutas de directorios virtuales conocidos.
    /// </summary>
    IReadOnlyList<string> GetAllDirectories();

    /// <summary>
    /// Renombra una entrada de archivo virtual en su misma ubicación o actualiza su ruta virtual.
    /// </summary>
    bool RenameFile(string sourceVirtualPath, string targetVirtualPath, string sourceNodeName, string sourceNodeId);

    /// <summary>
    /// Mueve una entrada de archivo virtual de una ruta a otra preservando la trazabilidad del origen.
    /// </summary>
    bool MoveFile(string sourceVirtualPath, string targetVirtualPath, string sourceNodeName, string sourceNodeId);

    /// <summary>
    /// Copia una entrada de archivo virtual a una nueva ruta de destino.
    /// </summary>
    bool CopyFile(string sourceVirtualPath, string targetVirtualPath, string sourceNodeName, string sourceNodeId);

    /// <summary>
    /// Elimina o marca como reciclada una entrada de archivo virtual.
    /// </summary>
    bool DeleteFile(string virtualPath, string sourceNodeName, string sourceNodeId, bool isRecycled = false);

    /// <summary>
    /// Genera una representación en árbol de texto ASCII indentado (estilo tree).
    /// </summary>
    string GenerateAsciiTree(string? rootDirectory = null);

    /// <summary>
    /// Limpia todas las entradas del VFS.
    /// </summary>
    void Clear();
}
