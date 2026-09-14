using Microsoft.Win32;

namespace FileFlow.App.Services;

/// <summary>
/// Implementación nativa de WPF para el servicio de diálogos de archivos.
/// </summary>
public class FileDialogService : IFileDialogService
{
    public string? ShowOpenFileDialog(string title, string filter, string defaultExt = "")
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExt
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string title, string filter, string defaultExt = "", string defaultFileName = "")
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExt,
            FileName = defaultFileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowFolderBrowserDialog(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
