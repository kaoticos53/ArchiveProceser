using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.Plugin.AI.ViewModels;

namespace FileFlow.Plugin.AI.UI;

public partial class MultimodalVlmConfigWindow : Window
{
    private readonly MultimodalVlmConfigViewModel _viewModel;

    public MultimodalVlmConfigViewModel ViewModel => _viewModel;

    public MultimodalVlmConfigWindow() : this(new MultimodalVlmConfigViewModel())
    {
    }

    public MultimodalVlmConfigWindow(MultimodalVlmConfigViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _viewModel.RequestClose = () =>
        {
            Close(true);
        };
        DataContext = _viewModel;
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
