using System;
using System.Linq;
using System.Threading;
using FileFlow.App.Services;
using Windows.Storage.Pickers;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// Diálogos de archivo del host Uno con el MISMO contrato síncrono que el escritorio
/// (<see cref="IFileDialogService"/>), para que <see cref="NodeParameterViewModel.BrowsePath"/> y el
/// «Probar» del inspector funcionen sin tocar el núcleo.
///
/// <para><b>Las dos restricciones de plataforma medidas</b>: (1) los pickers de WinUI exigen el hilo de
/// UI (WinRT lanza «Access is denied» desde otro), y (2) el contrato del núcleo es síncrono — un
/// <c>await</c> no cabe en la interfaz. La cura es la misma que el host de escritorio ya aplica con
/// Avalonia: bloquear con <c>.Wait()</c> SIN interbloqueo porque el hilo que espera NO es el que
/// despacha (el await se cuelga del contexto del picker); de todos modos, si el llamador ya está en el
/// hilo de UI, el bloqueo daría interbloqueo — por eso el guard: si ya estamos en UI, se aborta con
/// null (y queda declarado, no fingido). El parámetro de filtro del contrato se mapea al único patrón
/// que los pickers de WinRT aceptan por su extensión.</para>
/// </summary>
public sealed class UnoFileDialogService : IFileDialogService
{
    public string? ShowOpenFileDialog(string title, string filter, string defaultExt = "")
    {
        return RunOnUi(() =>
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List
            };
            ApplyFilter(picker, filter, defaultExt);
            return picker.PickSingleFileAsync().AsTask().GetAwaiter().GetResult()?.Path;
        });
    }

    public string? ShowSaveFileDialog(string title, string filter, string defaultExt = "", string defaultFileName = "")
    {
        return RunOnUi(() =>
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = string.IsNullOrWhiteSpace(defaultFileName) ? "flujo" : defaultFileName
            };
            ApplyFilter(picker, filter, defaultExt);
            return picker.PickSaveFileAsync().AsTask().GetAwaiter().GetResult()?.Path;
        });
    }

    public string? ShowFolderBrowserDialog(string title)
    {
        return RunOnUi(() =>
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add("*");
            return picker.PickSingleFolderAsync().AsTask().GetAwaiter().GetResult()?.Path;
        });
    }

    /// <summary>El picker exige hilo de UI; el bloqueo exige NO estar ya en él. Ambas a la vez o null.</summary>
    private static string? RunOnUi(Func<string?> pick)
    {
        var window = App.MainWindow;
        if (window is null)
        {
            return null;
        }

        var dispatcherQueue = window.DispatcherQueue;
        if (dispatcherQueue.HasThreadAccess)
        {
            // Llamada síncrona desde el hilo de UI: bloquear aquí interbloquearía. Sin forma de
            // entregar un resultado síncrono, el servicio devuelve null y queda declarado.
            return null;
        }

        string? result = null;
        var completed = new ManualResetEventSlim(false);
        if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    result = pick();
                }
                finally
                {
                    completed.Set();
                }
            }))
        {
            return null;
        }

        completed.Wait();
        return result;
    }

    /// <summary>El patrón «Todos los archivos (*.*)|*.*» del contrato al FileTypeFilter de WinRT.</summary>
    private static void ApplyFilter(FileOpenPicker picker, string filter, string defaultExt)
    {
        picker.FileTypeFilter.Clear();
        var exts = filter
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(pattern => pattern.TrimStart('*').StartsWith('.'))?
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => e.StartsWith('*') && e.IndexOf('.') > 0 && !e.EndsWith(".*", StringComparison.Ordinal))
            .Select(e => e.TrimStart('*'))
            .ToList();

        if (exts is { Count: > 0 })
        {
            foreach (var ext in exts)
            {
                picker.FileTypeFilter.Add(ext);
            }
        }
        else
        {
            picker.FileTypeFilter.Add("*");
        }
    }

    private static void ApplyFilter(FileSavePicker picker, string filter, string defaultExt)
    {
        var ext = defaultExt?.Trim();
        if (string.IsNullOrEmpty(ext))
        {
            ext = filter
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(pattern => pattern.TrimStart('*').StartsWith('.'))?
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .FirstOrDefault(e => e.StartsWith('*') && e.IndexOf('.') > 0 && !e.EndsWith(".*", StringComparison.Ordinal))
                ?.TrimStart('*');
        }

        picker.FileTypeChoices.Add("Archivo", [string.IsNullOrEmpty(ext) ? ".bin" : (ext.StartsWith('.') ? ext : "." + ext)]);
    }
}
