namespace FileFlow.Sdk.Services;

/// <summary>
/// Contrato abstracto para el acceso al portapapeles del sistema operativo de forma agnóstica de plataforma.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Establece el texto en el portapapeles del sistema de forma asíncrona.
    /// </summary>
    Task SetTextAsync(string text);

    /// <summary>
    /// Obtiene el texto actual almacenado en el portapapeles del sistema.
    /// </summary>
    Task<string?> GetTextAsync();

    /// <summary>
    /// Comprueba si el portapapeles contiene texto.
    /// </summary>
    Task<bool> ContainsTextAsync();
}
