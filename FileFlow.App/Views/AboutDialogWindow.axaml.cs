using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FileFlow.App.Views;

public partial class AboutDialogWindow : Window
{
    public AboutDialogWindow()
    {
        InitializeComponent();
        
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
        {
            var txt = this.FindControl<TextBlock>("TxtVersion");
            if (txt != null)
            {
                txt.Text = $"v{version.Major}.{version.Minor}.{version.Build} (net9.0 - Avalonia 12)";
            }
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
