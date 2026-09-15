using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class AiModelUrlsConfigDialog : Window
{
    private readonly AiModelUrlsConfigViewModel _viewModel;

    public AiModelUrlsConfigViewModel ViewModel => _viewModel;

    public AiModelUrlsConfigDialog() : this(string.Empty)
    {
    }

    public AiModelUrlsConfigDialog(string modelId) : this(new AiModelUrlsConfigViewModel(modelId))
    {
    }

    public AiModelUrlsConfigDialog(AiModelUrlsConfigViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
