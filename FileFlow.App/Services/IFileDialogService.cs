namespace FileFlow.App.Services;

/// <summary>
/// Contrato para el servicio de diálogos de archivos de sistema operativo.
/// </summary>
public interface IFileDialogService
{
    string? ShowOpenFileDialog(string title, string filter, string defaultExt = "");
    string? ShowSaveFileDialog(string title, string filter, string defaultExt = "", string defaultFileName = "");
    string? ShowFolderBrowserDialog(string title);
}
