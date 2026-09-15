using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Adaptador de infraestructura multiplataforma moderno para <see cref="IDialogService"/> utilizando diálogos nativos Avalonia 12.
/// </summary>
public class AvaloniaDialogService : IDialogService
{
    private static readonly Lazy<AvaloniaDialogService> _instance = new(() => new AvaloniaDialogService());
    public static AvaloniaDialogService Instance => _instance.Value;

    public void ShowInformation(string message, string title = "FileFlow Studio")
    {
        ShowModalDialog(title, message, "Info", "Aceptar");
    }

    public void ShowWarning(string message, string title = "FileFlow Studio")
    {
        ShowModalDialog(title, message, "Warning", "Aceptar");
    }

    public void ShowError(string message, string title = "Error")
    {
        ShowModalDialog(title, message, "Error", "Aceptar");
    }

    public bool ShowConfirmation(string message, string title = "FileFlow Studio")
    {
        var res = ShowModalDialog(title, message, "Question", "Sí", "No");
        return res == DialogResult.Yes;
    }

    public DialogResult ShowYesNoCancel(string message, string title = "FileFlow Studio")
    {
        return ShowModalDialog(title, message, "Question", "Sí", "No", "Cancelar");
    }

    private static DialogResult ShowModalDialog(
        string title,
        string message,
        string iconType,
        string primaryText,
        string? secondaryText = null,
        string? cancelText = null)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
        {
            return DialogResult.None;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return ShowDialogWindow(desktop.MainWindow, title, message, iconType, primaryText, secondaryText, cancelText).GetAwaiter().GetResult();
        }

        return Dispatcher.UIThread.InvokeAsync(async () =>
        {
            return await ShowDialogWindow(desktop.MainWindow, title, message, iconType, primaryText, secondaryText, cancelText);
        }).GetAwaiter().GetResult();
    }

    private static async Task<DialogResult> ShowDialogWindow(
        Window owner,
        string title,
        string message,
        string iconType,
        string primaryText,
        string? secondaryText,
        string? cancelText)
    {
        var result = DialogResult.None;

        var window = new Window
        {
            Title = title,
            Width = 440,
            MinWidth = 360,
            MaxWidth = 550,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        // Header icon & text
        string iconGlyph = iconType switch
        {
            "Warning" => "⚠️",
            "Error" => "❌",
            "Question" => "❓",
            _ => "ℹ️"
        };

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(24)
        };

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var iconBlock = new TextBlock
        {
            Text = iconGlyph,
            FontSize = 20,
            VerticalAlignment = VerticalAlignment.Center
        };

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        headerPanel.Children.Add(iconBlock);
        headerPanel.Children.Add(titleBlock);
        Grid.SetRow(headerPanel, 0);
        rootGrid.Children.Add(headerPanel);

        var msgBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 24)
        };
        Grid.SetRow(msgBlock, 1);
        rootGrid.Children.Add(msgBlock);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        var primaryBtn = new Button
        {
            Content = primaryText,
            MinWidth = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        primaryBtn.Click += (_, _) =>
        {
            result = secondaryText != null ? DialogResult.Yes : DialogResult.Ok;
            window.Close();
        };
        buttonPanel.Children.Add(primaryBtn);

        if (!string.IsNullOrEmpty(secondaryText))
        {
            var secondaryBtn = new Button
            {
                Content = secondaryText,
                MinWidth = 80,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            secondaryBtn.Click += (_, _) =>
            {
                result = DialogResult.No;
                window.Close();
            };
            buttonPanel.Children.Add(secondaryBtn);
        }

        if (!string.IsNullOrEmpty(cancelText))
        {
            var cancelBtn = new Button
            {
                Content = cancelText,
                MinWidth = 80,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            cancelBtn.Click += (_, _) =>
            {
                result = DialogResult.Cancel;
                window.Close();
            };
            buttonPanel.Children.Add(cancelBtn);
        }

        Grid.SetRow(buttonPanel, 2);
        rootGrid.Children.Add(buttonPanel);

        window.Content = rootGrid;

        try
        {
            await window.ShowDialog(owner);
        }
        catch
        {
            // Resiliente en entornos headless o sin ventana
        }

        return result;
    }
}
