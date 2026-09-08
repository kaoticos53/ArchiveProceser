namespace FileFlow.Sdk.Services;

/// <summary>
/// Contrato para la resolución y verificación de herramientas y ejecutables externos de procesamiento.
/// Desacopla la lógica de búsqueda de binarios en el disco o configuración de la interfaz de usuario.
/// </summary>
public interface IExternalToolsService
{
    /// <summary>
    /// Resuelve la ruta ejecutable para una herramienta por nombre (ej: "ffmpeg", "ffprobe", "7z").
    /// </summary>
    string ResolveToolPath(string toolName);

    /// <summary>
    /// Determina si la herramienta indicada está instalada y ejecutable en el sistema.
    /// </summary>
    bool IsToolAvailable(string toolName);

    /// <summary>
    /// Determina de forma asíncrona si la herramienta indicada está instalada y ejecutable en el sistema.
    /// </summary>
    Task<bool> IsToolAvailableAsync(string toolName, CancellationToken cancellationToken = default) =>
        Task.FromResult(IsToolAvailable(toolName));

    /// <summary>
    /// Configuración actual de ejecutables externos.
    /// </summary>
    ExternalToolsConfig Config => new();

    /// <summary>
    /// Guarda la configuración de herramientas externas.
    /// </summary>
    void SaveConfig(ExternalToolsConfig config) { }

    /// <summary>
    /// Escanea el sistema para autodetectar la presencia de herramientas externas conocidas.
    /// </summary>
    Task<ExternalToolsConfig> AutoDetectToolsAsync() => Task.FromResult(new ExternalToolsConfig());

    /// <summary>
    /// Ruta o comando para el ejecutable de FFmpeg.
    /// </summary>
    string FfmpegExecutable => ResolveToolPath("ffmpeg");

    /// <summary>
    /// Ruta o comando para el ejecutable de FFprobe.
    /// </summary>
    string FfprobeExecutable => ResolveToolPath("ffprobe");

    /// <summary>
    /// Ruta o comando para el ejecutable de 7-Zip.
    /// </summary>
    string SevenZipExecutable => ResolveToolPath("7z");
}
