using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using FileFlow.Sdk.Services;
using FluentAvalonia.UI.Controls;

namespace FileFlow.App.Services;

/// <summary>
/// Adaptador de infraestructura multiplataforma para <see cref="IDialogService"/> utilizando diálogos FluentAvalonia.
/// </summary>
public class AvaloniaDialogService : IDialogService
{
    private static readonly Lazy<AvaloniaDialogService> _instance = new(() => new AvaloniaDialogService());
    public static AvaloniaDialogService Instance => _instance.Value;

    public void ShowInformation(string message, string title = "FileFlow Studio")
    {
        ShowModalDialog(title, message, ContentDialogButton.Primary, "Aceptar");
    }

    public void ShowWarning(string message, string title = "FileFlow Studio")
    {
        ShowModalDialog(title, message, ContentDialogButton.Primary, "Aceptar");
    }

    public void ShowError(string message, string title = "Error")
    {
        ShowModalDialog(title, message, ContentDialogButton.Primary, "Aceptar");
    }

    public bool ShowConfirmation(string message, string title = "FileFlow Studio")
    {
        var res = ShowModalDialog(title, message, ContentDialogButton.Primary, "Sí", "No");
        return res == ContentDialogResult.Primary;
    }

    public DialogResult ShowYesNoCancel(string message, string title = "FileFlow Studio")
    {
        var res = ShowModalDialog(title, message, ContentDialogButton.Primary, "Sí", "No", "Cancelar");
        return res switch
        {
            ContentDialogResult.Primary => DialogResult.Yes,
            ContentDialogResult.Secondary => DialogResult.No,
            _ => DialogResult.Cancel
        };
    }

    private static ContentDialogResult ShowModalDialog(
        string title, 
        string message, 
        ContentDialogButton defaultBtn,
        string primaryText, 
        string? secondaryText = null, 
        string? closeText = null)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
        {
            return ContentDialogResult.None;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return ShowContentDialogCore(title, message, defaultBtn, primaryText, secondaryText, closeText).GetAwaiter().GetResult();
        }

        return Dispatcher.UIThread.InvokeAsync(async () =>
        {
            return await ShowContentDialogCore(title, message, defaultBtn, primaryText, secondaryText, closeText);
        }).GetAwaiter().GetResult();
    }

    private static async Task<ContentDialogResult> ShowContentDialogCore(
        string title, 
        string message, 
        ContentDialogButton defaultBtn,
        string primaryText, 
        string? secondaryText, 
        string? closeText)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = primaryText,
                SecondaryButtonText = secondaryText,
                CloseButtonText = closeText,
                DefaultButton = defaultBtn
            };

            return await dialog.ShowAsync();
        }
        catch
        {
            return ContentDialogResult.None;
        }
    }
}
