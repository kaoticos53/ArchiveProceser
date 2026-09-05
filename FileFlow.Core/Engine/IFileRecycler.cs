namespace FileFlow.Core.Engine;

/// <summary>
/// Contrato de puerto para operaciones de reciclaje y eliminación segura en el sistema operativo.
/// </summary>
public interface IFileRecycler
{
    /// <summary>
    /// Envía un archivo o directorio a la Papelera de reciclaje del sistema operativo sin confirmación interactiva.
    /// </summary>
    /// <param name="path">Ruta absoluta del archivo o directorio.</param>
    /// <returns><c>true</c> si la operación tuvo éxito; en caso contrario, <c>false</c>.</returns>
    bool Recycle(string path);
}
