using System.Threading.Channels;
using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

/// <summary>
/// Contrato de puerto para el servicio de supervisión de carpetas en tiempo real (Watch Folder).
/// </summary>
public interface IFolderWatcherService : IDisposable
{
    /// <summary>
    /// Indica si el servicio se encuentra actualmente supervisando rutas en disco.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Canal de lectura de elementos descubiertos y estabilizados listos para su procesamiento.
    /// </summary>
    ChannelReader<FileItemContext> ItemReader { get; }

    /// <summary>
    /// Evento emitido cuando un archivo ha sido descubierto y verificado tras el periodo de debounce.
    /// </summary>
    event Action<FileItemContext>? ItemDiscovered;

    /// <summary>
    /// Inicia la supervisión de un directorio específico.
    /// </summary>
    void Start(string folderPath, string filter = "*.*", bool includeSubdirectories = true, int debounceMs = 1000);

    /// <summary>
    /// Inicia la supervisión de múltiples directorios simultáneamente.
    /// </summary>
    void Start(IEnumerable<string> folderPaths, string filter = "*.*", bool includeSubdirectories = true, int debounceMs = 1000);

    /// <summary>
    /// Detiene la supervisión de todos los directorios activos y cancela las tareas de fondo.
    /// </summary>
    void Stop();
}
