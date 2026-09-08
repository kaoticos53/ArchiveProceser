using System.Windows;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class AiModelUrlsConfigDialog : Window
{
    private readonly AiModelUrlsConfigViewModel _viewModel;

    public AiModelUrlsConfigViewModel ViewModel => _viewModel;

    public AiModelUrlsConfigDialog(string modelId) : this(new AiModelUrlsConfigViewModel(modelId))
    {
    }

    public AiModelUrlsConfigDialog(AiModelUrlsConfigViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Save())
        {
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
