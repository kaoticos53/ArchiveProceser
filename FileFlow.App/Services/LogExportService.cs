using System;
using System.IO;
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
        var fileDialog = new FileDialogService();
        string? targetPath = fileDialog.ShowSaveFileDialog(
            "Exportar Logs",
            "Archivos de Log (*.log;*.txt)|*.log;*.txt|Todos los archivos (*.*)|*.*",
            ".log",
            $"fileflow_execution_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        if (string.IsNullOrEmpty(targetPath))
        {
            return null;
        }
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
