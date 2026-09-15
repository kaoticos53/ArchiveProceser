using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.Plugin.FileSystem.UI.ViewModels;

namespace FileFlow.Plugin.FileSystem.UI.Views;

public partial class SyntheticDataSetDesignerWindow : Window
{
    public SyntheticDataSetDesignerWindow()
    {
        InitializeComponent();
    }

    public SyntheticDataSetDesignerWindow(SyntheticDataSetDesignerViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
