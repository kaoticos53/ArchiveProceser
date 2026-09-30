namespace FileFlow.App.Services;

/// <summary>
/// Doble de prueba para <see cref="IFileDialogService"/> en el suite unitario.
/// Permite configurar los resultados de diálogo y evitar llamadas interactivas a la UI del sistema operativo.
/// </summary>
public class FileDialogService : IFileDialogService
{
    public string? OpenResult { get; set; }
    public string? SaveResult { get; set; }
    public string? FolderResult { get; set; }

    public string? ShowOpenFileDialog(string title, string filter, string defaultExt = "") => OpenResult;
    public string? ShowSaveFileDialog(string title, string filter, string defaultExt = "", string defaultFileName = "") => SaveResult;
    public string? ShowFolderBrowserDialog(string title) => FolderResult;
}
