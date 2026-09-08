namespace FileFlow.Sdk.Services;

/// <summary>
/// Configuración de rutas a ejecutables y herramientas externas del sistema.
/// </summary>
public class ExternalToolsConfig
{
    public string FfmpegPath { get; set; } = string.Empty;
    public string FfprobePath { get; set; } = string.Empty;
    public string SevenZipPath { get; set; } = string.Empty;
    public string PythonPath { get; set; } = string.Empty;
}
