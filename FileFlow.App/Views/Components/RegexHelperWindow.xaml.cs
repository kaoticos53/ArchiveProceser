using System.Windows;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class RegexHelperWindow : Window
{
    private readonly RegexHelperViewModel _viewModel;

    public string ResultPattern => _viewModel.Pattern;
    public string ResultReplacement => _viewModel.Replacement;

    public RegexHelperWindow(string initialPattern = "", string initialReplacement = "", string initialSampleText = "")
    {
        InitializeComponent();
        _viewModel = new RegexHelperViewModel(initialPattern, initialReplacement, initialSampleText);
        DataContext = _viewModel;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
