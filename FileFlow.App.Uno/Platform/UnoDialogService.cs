using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FileFlow.Sdk.Localization;
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
        => ShowOrAsk(title, message);

    public void ShowWarning(string message, string title = "FileFlow Studio")
        => ShowOrAsk(title, message);

    public void ShowError(string message, string title = "Error")
        => ShowOrAsk(title, message);

    public bool ShowConfirmation(string message, string title = "FileFlow Studio")
        => ShowWithResult(title, message, withCancel: false) == ContentDialogResult.Primary;

    /// <summary>
    /// La CONFIRMACIÓN ASÍNCRONA (hito 263): la respuesta REAL del usuario, sin bloquear el hilo de UI.
    ///
    /// <para>Es el camino que un modal del host —el gestor de presets, por ejemplo— tiene que usar para
    /// preguntar antes de destruir: la variante síncrona de este host devuelve «no» desde el hilo de UI (no
    /// puede bloquearse) y la de un servicio sin diálogos devuelve «sí» sin preguntar, así que la MISMA orden
    /// se comportaba distinto según quién la pidiera. Con la respuesta real en la mano, se borra si el usuario
    /// dijo que sí y no se borra en ningún otro caso.</para>
    ///
    /// <para>Si hay un modal abierto la pregunta se monta DENTRO de él (WinUI sólo admite un
    /// <see cref="ContentDialog"/> por raíz); si no, en el diálogo modal normal del host.</para>
    /// </summary>
    public Task<bool> ConfirmAsync(string message, string title = "FileFlow Studio")
    {
        if (UnoWindowService.ActiveDialog is not null)
        {
            return UnoWindowService.AskInsideActiveDialogAsync(
                message, title, LocalizationManager.Instance.GetString("Common_Cancel", "Cancelar"));
        }

        return ShowConfirmationDialogAsync(message, title);
    }

    /// <summary>
    /// Un aviso del producto. Con un modal abierto no cabe un segundo <c>ContentDialog</c>, así que el aviso se
    /// enseña DENTRO del modal (antes se perdía en una excepción no observada: el usuario no veía nada y nadie
    /// se enteraba).
    /// </summary>
    private static void ShowOrAsk(string title, string message)
    {
        if (UnoWindowService.ActiveDialog is not null)
        {
            _ = UnoWindowService.AskInsideActiveDialogAsync(message, title);
            return;
        }

        ShowFireAndForget(title, message, null);
    }

    /// <summary>
    /// El diálogo de confirmación abierto AHORA (lo lee la sonda: es la prueba de que la orden lo abrió).
    /// </summary>
    internal static ContentDialog? ActiveConfirmation { get; private set; }

    /// <summary>
    /// La confirmación ASÍNCRONA (hito 258): la espera un comando del host sin bloquear el hilo de UI.
    ///
    /// <para>Es la vía que el contrato SÍNCRONO de <see cref="IDialogService"/> no puede ofrecer —sus
    /// métodos bloquean o devuelven «cancelado» cuando el llamador es el hilo de UI— y por la que las
    /// órdenes de flujo del menú (Nuevo Flujo) no podían cumplirse en este host. No sustituye a la
    /// síncrona: la complementa para quien puede esperar.</para>
    ///
    /// <para>WinUI sólo admite un <see cref="ContentDialog"/> a la vez: si ya hay uno abierto (el editor de
    /// un parámetro, por ejemplo), la confirmación no se muestra y se devuelve «no confirmado» dejándolo
    /// escrito, en vez de caer con una excepción.</para>
    /// </summary>
    public static async Task<bool> ShowConfirmationAsync(string message, string title = "FileFlow Studio")
    {
        if (UnoWindowService.ActiveDialog is not null)
        {
            // Ya hay un modal abierto: la pregunta se hace DENTRO de él, que es lo único que WinUI admite y lo
            // único que el usuario ve (declinarla dejaba la orden sin efecto y sin aviso).
            return await UnoWindowService.AskInsideActiveDialogAsync(
                message, title, LocalizationManager.Instance.GetString("Common_Cancel", "Cancelar"));
        }

        return await ShowConfirmationDialogAsync(message, title);
    }

    /// <summary>
    /// La confirmación en su propio modal (sin ningún otro abierto). Los botones son los del diccionario del
    /// host —el mismo «Aceptar»/«Cancelar» que usan las demás superficies— y el modal lleva su ancla de
    /// automatización, que es como el canal externo lo distingue de cualquier otro diálogo.
    /// </summary>
    private static async Task<bool> ShowConfirmationDialogAsync(string message, string title)
    {
        if (App.MainWindow?.Content is not FrameworkElement root)
        {
            return false;
        }

        var loc = LocalizationManager.Instance;
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = loc.GetString("Common_Accept", "Aceptar"),
            CloseButtonText = loc.GetString("Common_Cancel", "Cancelar"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root.XamlRoot,
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(dialog, "HostConfirmationDialog");

        ActiveConfirmation = dialog;
        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        finally
        {
            ActiveConfirmation = null;
        }
    }

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

        if (UnoWindowService.ActiveDialog is not null)
        {
            // Declarado, no fingido: un segundo ContentDialog no cabe y esta llamada no es la que pregunta.
            Console.Error.WriteLine("[UnoDialogService] aviso no mostrado: ya hay un diálogo abierto en este host");
            return ContentDialogResult.None;
        }

        var loc = LocalizationManager.Instance;
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = loc.GetString("Common_Accept", "Aceptar"),
            CloseButtonText = secondary ?? loc.GetString("Common_Close", "Cerrar"),
            XamlRoot = root.XamlRoot,
        };

        // El aviso se muestra PUBLICÁNDOSE como el modal abierto (hito 270): el estado lo consulta el propio
        // host antes de abrir otro ContentDialog —WinUI sólo admite uno— y lo lee la sonda, que sin él no puede
        // distinguir un aviso mostrado de un botón que no hizo nada. Antes se mostraba por fuera de ese estado:
        // el aviso se veía, pero el host no sabía que estaba ahí.
        return await UnoWindowService.RunOwnedAsync(dialog);
    }
}
