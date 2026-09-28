using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Plugin.Integrations.UI.ViewModels;
using FileFlow.Plugin.Integrations.UI.Views;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Catálogo de ventanas del host Avalonia. Mapea cada <see cref="DialogKeys"/> a la ventana concreta
/// y sabe construir su contenido a partir del <c>payload</c> que el ViewModel envía. Es el único sitio
/// del host que nombra tipos Window; los ViewModels piden por clave.
/// </summary>
public sealed class AvaloniaWindowService : IWindowService
{
    private Window? Owner => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public object? MainWindowOwner => Owner;

    public async Task<DialogResultPayload?> ShowDialogAsync(string dialogKey, object? payload = null)
    {
        var owner = Owner;
        Window dialog = dialogKey switch
        {
            DialogKeys.UpdateDialog => new Views.Components.UpdateDialogWindow(RequirePayload<UpdateDialogViewModel>(dialogKey, payload)),
            DialogKeys.WorkflowSettings => CreateWorkflowSettings(dialogKey, payload),
            DialogKeys.TextEditor => new Views.Components.TextEditorDialogWindow(RequirePayload<NodeParameterViewModel>(dialogKey, payload)),
            DialogKeys.VariablePicker => CreateVariablePicker(dialogKey, payload),
            DialogKeys.AiModelUrlsConfig => new Views.Components.AiModelUrlsConfigDialog(payload as string ?? string.Empty),
            // El GESTOR DE PRESETS (hito 263): el nodo de transcodificación declara la superficie al SDK y esta
            // es la ventana que la sirve —la del propio plugin, que es quien tiene el toolkit— sobre el MISMO
            // view model portable que declara el nodo. Antes la montaba el nodo dentro de su acción
            // personalizada; ahora la sirve el catálogo de ventanas del host, así que los dos hosts sirven la
            // misma superficie por el mismo contrato y sólo cambia la vista.
            DialogKeys.MediaPresetManager => new MediaPresetManagerWindow(
                RequirePayload<MediaPresetManagerViewModel>(dialogKey, payload)),
            _ => throw new ArgumentOutOfRangeException(nameof(dialogKey), dialogKey, "El host Avalonia no conoce este diálogo."),
        };

        bool confirmed = dialogKey switch
        {
            DialogKeys.VariablePicker => await ShowBoolResultAsync(dialog, owner),
            DialogKeys.TextEditor => await ShowBoolResultAsync(dialog, owner),
            _ => await ShowDefaultResultAsync(dialog, owner),
        };

        string? value = dialog switch
        {
            Views.Components.VariablePickerWindow picker => picker.SelectedToken,
            Views.Components.TextEditorDialogWindow editor => editor.ResultText,
            _ => null,
        };

        return new DialogResultPayload { Confirmed = confirmed, Value = value };
    }

    public void ShowWindow(string dialogKey, object? payload = null)
    {
        Window window = dialogKey switch
        {
            DialogKeys.About => new Views.AboutDialogWindow(),
            DialogKeys.VirtualFileSystemExplorer => new Views.Components.VirtualFileSystemExplorerWindow(
                RequirePayload<VirtualFileSystemStore>(dialogKey, payload)),
            DialogKeys.WorkflowMetricsDashboard => new Views.Components.WorkflowMetricsDashboardWindow(
                RequirePayload<WorkflowMetricsDashboardViewModel>(dialogKey, payload)),
            DialogKeys.ThemeCustomizer => new Views.Components.ThemeCustomizerWindow
            {
                DataContext = RequirePayload<ThemeCustomizerViewModel>(dialogKey, payload),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(dialogKey), dialogKey, "El host Avalonia no conoce esta ventana."),
        };

        window.Show();
    }

    private static Window CreateWorkflowSettings(string key, object? payload)
    {
        if (payload is string globalOutputDir)
        {
            return new Views.Components.WorkflowSettingsWindow(globalOutputDir);
        }
        var win = new Views.Components.WorkflowSettingsWindow();
        return win;
    }

    private static Window CreateVariablePicker(string key, object? payload)
    {
        if (payload is VariablePickerRequest request)
        {
            return new Views.Components.VariablePickerWindow(request.Groups, request.TargetNode, request.PreviewContext, request.Localization);
        }
        return new Views.Components.VariablePickerWindow();
    }

    private static T RequirePayload<T>(string key, object? payload) where T : class
        => payload as T ?? throw new InvalidOperationException(
            $"El diálogo '{key}' espera un payload de tipo {typeof(T).Name} y llegó {(payload?.GetType().Name ?? "null")}.");

    private static async Task<bool> ShowBoolResultAsync(Window dialog, Window? owner)
    {
        if (owner is null) return false;
        return await dialog.ShowDialog<bool>(owner);
    }

    private static async Task<bool> ShowDefaultResultAsync(Window dialog, Window? owner)
    {
        if (owner is null) return false;
        await dialog.ShowDialog(owner);
        return true;
    }
}
