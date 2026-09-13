using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileFlow.Sdk;

namespace FileFlow.App.Views;

public partial class AboutDialogWindow : Window
{
    private const string GitHubRepositoryUrl = "https://github.com/kaoticos53/ArchiveProceser";

    public AboutDialogWindow()
    {
        InitializeComponent();
        var txtVersion = this.FindControl<TextBlock>("TxtVersion");
        if (txtVersion != null)
        {
            txtVersion.Text = $"v{AppVersionInfo.DisplayVersion}";
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GitHubLink_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubRepositoryUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignored
        }
    }
}
