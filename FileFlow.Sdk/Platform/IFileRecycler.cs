namespace FileFlow.Sdk.Platform;

/// <summary>
/// Contrato para el reciclaje y envío seguro de archivos a la papelera del sistema operativo.
/// </summary>
public interface IFileRecycler
{
    /// <summary>
    /// Envía un archivo o directorio a la papelera de reciclaje del sistema operativo.
    /// </summary>
    bool Recycle(string path);
}
