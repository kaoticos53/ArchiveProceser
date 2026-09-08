namespace FileFlow.Sdk.Services;

/// <summary>
/// Contrato canónico para transcodificación y conversión de flujos multimedia.
/// </summary>
public interface IMediaTranscoderService
{
    /// <summary>
    /// Indica si el motor de transcodificación (ej: FFmpeg) está disponible en el entorno.
    /// </summary>
    bool IsAvailable();

    /// <summary>
    /// Indica de forma asíncrona si el motor de transcodificación (ej: FFmpeg) está disponible en el entorno.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(IsAvailable());

    /// <summary>
    /// Ejecuta la transcodificación de un archivo audiovisual al formato y ruta de destino indicados.
    /// </summary>
    Task<bool> TranscodeAsync(
        string inputPath,
        string outputPath,
        string arguments,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
