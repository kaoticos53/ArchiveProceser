using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// Adaptador de diálogos sobre <see cref="ContentDialog"/> de WinUI, cumpliendo el contrato
/// <see cref="IDialogService"/> de FileFlow.Sdk (métodos síncronos).
///
/// Limitación honesta de la primera rebanada: WinUI sólo ofrece ShowAsync, sin API síncrona.
/// Los métodos informativos son fire-and-forget; los que devuelven resultado bloquean al llamador
/// sólo si viene de un hilo de fondo (se despachan al hilo de UI y esperan). Si el llamador ya está
/// en el hilo de UI, devuelven Cancel en lugar de bloquear el hilo y congelar la aplicación.
/// </summary>
public sealed class UnoDialogService : IDialogService
{
    public void ShowInformation(string message, string title = "FileFlow Studio")
        => ShowFireAndForget(title, message, null);

    public void ShowWarning(string message, string title = "FileFlow Studio")
        => ShowFireAndForget(title, message, null);

    public void ShowError(string message, string title = "Error")
        => ShowFireAndForget(title, message, null);

    public bool ShowConfirmation(string message, string title = "FileFlow Studio")
        => ShowWithResult(title, message, withCancel: false) == ContentDialogResult.Primary;

    public DialogResult ShowYesNoCancel(string message, string title = "FileFlow Studio")
        => ShowWithResult(title, message, withCancel: true) switch
        {
            ContentDialogResult.Primary => DialogResult.Yes,
            ContentDialogResult.Secondary => DialogResult.No,
            _ => DialogResult.Cancel,
        };

    private static void ShowFireAndForget(string title, string message, string? secondary)
    {
        _ = ShowCoreAsync(title, message, secondary);
    }

    private static ContentDialogResult ShowWithResult(string title, string message, bool withCancel)
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        if (queue.HasThreadAccess)
        {
            // El llamador está en el hilo de UI: bloquear aquí congelaría el render.
            // Se devuelve Cancel y se documenta; la rebanada de diálogos nativos lo afinará.
            return ContentDialogResult.None;
        }

        var tcs = new TaskCompletionSource<ContentDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.TryEnqueue(async () =>
        {
            try { tcs.SetResult(await ShowCoreAsync(title, message, withCancel ? "No" : null)); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task.GetAwaiter().GetResult();
    }

    private static async Task<ContentDialogResult> ShowCoreAsync(string title, string message, string? secondary)
    {
        if (App.MainWindow?.Content is not FrameworkElement root)
        {
            return ContentDialogResult.None;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "OK",
            CloseButtonText = secondary ?? "Cancel",
            XamlRoot = root.XamlRoot,
        };
        return await dialog.ShowAsync();
    }
}
