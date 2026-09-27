using System;
using System.Linq;
using System.Threading;
using FileFlow.App.Services;
using Windows.Storage.Pickers;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// Diálogos de archivo del host Uno: el contrato síncrono del núcleo (para <see
/// cref="NodeParameterViewModel.BrowsePath"/> y los VMs que bloquean fuera de UI) y, desde el hito
/// 240, la variante asíncrona REAL — pickers de WinRT encolados al DispatcherQueue sin bloquear el
/// hilo llamador, la vía del «Probar» del inspector desde un click de UI.
///
/// <para><b>Las restricciones de plataforma medidas</b>: los pickers de WinUI exigen el hilo de UI
/// (WinRT lanza «Access is denied» desde otro). El síncrono bloquea con un Wait SIN interbloqueo
/// porque el hilo que espera NO es el que despacha — y si el llamador ya está en UI, se aborta con
/// null (declarado, no fingido). El asíncrono no bloquea nunca: encola y espera por el await. El
/// parámetro de filtro del contrato se mapea al único patrón que los pickers de WinRT aceptan por
/// su extensión.</para>
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

    // ── La variante asíncrona (hito 240): pickers WinRT encolados al DispatcherQueue con
    // TaskCompletionSource — nunca bloquea el hilo llamador (a diferencia del síncrono, funciona
    // TAMBIÉN desde el hilo de UI: es la vía del «Probar» del inspector). La espera corre en hilo
    // de fondo (ConfigureAwait(false)): la continuación no depende del contexto de UI. ──

    public Task<string?> ShowOpenFileDialogAsync(string title, string filter, string defaultExt = "")
    {
        return EnqueueOnUiAsync(() =>
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

    public Task<string?> ShowSaveFileDialogAsync(string title, string filter, string defaultExt = "", string defaultFileName = "")
    {
        return EnqueueOnUiAsync(() =>
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

    public Task<string?> ShowFolderBrowserDialogAsync(string title)
    {
        return EnqueueOnUiAsync(() =>
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add("*");
            return picker.PickSingleFolderAsync().AsTask().GetAwaiter().GetResult()?.Path;
        });
    }

    /// <summary>Encola el picker en UI y devuelve la Task del resultado: sin Wait ni interbloqueo.</summary>
    private static Task<string?> EnqueueOnUiAsync(Func<string?> pick)
    {
        var window = App.MainWindow;
        if (window is null)
        {
            return Task.FromResult<string?>(null);
        }

        var dispatcherQueue = window.DispatcherQueue;
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                completion.SetResult(pick());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }))
        {
            completion.SetResult(null);
        }

        return completion.Task;
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
