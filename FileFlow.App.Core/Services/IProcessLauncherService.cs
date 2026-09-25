namespace FileFlow.App.Services;

/// <summary>
/// Contrato de puerto para el lanzamiento desacoplado de procesos externos, URLs y visualización en el explorador del sistema operativo.
/// </summary>
public interface IProcessLauncherService
{
    /// <summary>
    /// Abre una dirección URL en el navegador web predeterminado del sistema.
    /// </summary>
    bool OpenUrl(string url);

    /// <summary>
    /// Abre un directorio en el explorador de archivos del sistema operativo.
    /// </summary>
    bool OpenFolder(string folderPath);

    /// <summary>
    /// Selecciona y resalta un archivo específico en el explorador de archivos.
    /// </summary>
    bool OpenFileInExplorer(string filePath);

    /// <summary>
    /// Inicia un proceso externo del sistema operativo.
    /// </summary>
    bool StartProcess(string fileName, string? arguments = null);
}
