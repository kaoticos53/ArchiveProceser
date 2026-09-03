using System.IO;
using System.Windows;
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
    public static async Task<string?> ExportLogsWithDialogAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Archivos de Log (*.log;*.txt)|*.log;*.txt|Todos los archivos (*.*)|*.*",
            DefaultExt = ".log",
            FileName = $"fileflow_execution_{DateTime.Now:yyyyMMdd_HHmmss}.log"
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        string targetPath = dialog.FileName;
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
            MessageBox.Show($"Error al exportar el log: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }
}
