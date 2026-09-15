using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FileFlow.App.Views.Components;

public partial class AiModelDownloadDialog : Window
{
    public AiModelDownloadDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
