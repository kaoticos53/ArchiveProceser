using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace FileFlow.App.Services;

/// <summary>
/// Implementación multiplataforma de Avalonia para el servicio de diálogos de archivos y carpetas.
/// </summary>
public class FileDialogService : IFileDialogService
{
    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            return topLevel?.StorageProvider;
        }
        return null;
    }

    public string? ShowOpenFileDialog(string title, string filter, string defaultExt = "")
    {
        var sp = GetStorageProvider();
        if (sp == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        var task = sp.OpenFilePickerAsync(options);
        var result = task.GetAwaiter().GetResult();
        return result?.FirstOrDefault()?.TryGetLocalPath();
    }

    public string? ShowSaveFileDialog(string title, string filter, string defaultExt = "", string defaultFileName = "")
    {
        var sp = GetStorageProvider();
        if (sp == null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = defaultExt.TrimStart('.'),
            SuggestedFileName = defaultFileName
        };

        var task = sp.SaveFilePickerAsync(options);
        var result = task.GetAwaiter().GetResult();
        return result?.TryGetLocalPath();
    }

    public string? ShowFolderBrowserDialog(string title)
    {
        var sp = GetStorageProvider();
        if (sp == null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        var task = sp.OpenFolderPickerAsync(options);
        var result = task.GetAwaiter().GetResult();
        return result?.FirstOrDefault()?.TryGetLocalPath();
    }
}
