using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Animation;
using FileFlow.Sdk;

namespace FileFlow.App.Views;

public partial class AboutDialogWindow : Window
{
    private const string GitHubRepositoryUrl = "https://github.com/kaoticos53/ArchiveProceser";

    public AboutDialogWindow()
    {
        InitializeComponent();
        TxtVersion.Text = $"v{AppVersionInfo.DisplayVersion}";

        Loaded += (s, e) =>
        {
            if (Resources["FadeInStoryboard"] is Storyboard fadeIn)
            {
                fadeIn.Begin(this);
            }
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GitHubLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubRepositoryUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo abrir el navegador: {ex.Message}", "FileFlow Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
