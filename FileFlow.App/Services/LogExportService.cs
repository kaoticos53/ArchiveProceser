using System.IO;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FileFlow.Core.Telemetry;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio responsable de la exportación a disco de registros de telemetría y logs de ejecución.
/// </summary>
public static class LogExportService
{
    /// <summary>
    /// Abre un diálogo modal para guardar los logs de SqliteLogStore en un archivo de texto o log.
    /// </summary>
    public static async Task<string?> ExportLogsWithDialogAsync(IDialogService? dialogService = null)
    {
        var topLevel = App.MainWindow != null ? TopLevel.GetTopLevel(App.MainWindow) : null;
        if (topLevel == null)
        {
            return null;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar Registros de Logs",
            SuggestedFileName = $"fileflow_execution_{DateTime.Now:yyyyMMdd_HHmmss}.log",
            DefaultExtension = "log",
            FileTypeChoices =
            [
                new FilePickerFileType("Archivos de Log (*.log;*.txt)") { Patterns = ["*.log", "*.txt"] },
                new FilePickerFileType("Todos los archivos (*.*)") { Patterns = ["*.*"] }
            ]
        });

        if (file == null)
        {
            return null;
        }

        string targetPath = file.Path.LocalPath;
        try
        {
            await Task.Run(async () =>
            {
                await SqliteLogStore.Instance.FlushPendingLogsAsync().ConfigureAwait(false);
                await using var writer = new StreamWriter(targetPath);
                await SqliteLogStore.Instance.ExportLogsAsync(writer).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return targetPath;
        }
        catch (Exception ex)
        {
            var ds = dialogService ?? (App.Services?.GetService(typeof(IDialogService)) as IDialogService) ?? NullDialogService.Instance;
            ds.ShowError($"Error al exportar el log: {ex.Message}", "Error");
            return null;
        }
    }
}
