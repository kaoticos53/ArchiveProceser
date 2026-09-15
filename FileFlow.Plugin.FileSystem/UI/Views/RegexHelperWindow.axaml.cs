using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.Plugin.FileSystem.UI.ViewModels;

namespace FileFlow.Plugin.FileSystem.UI.Views;

public partial class RegexHelperWindow : Window
{
    private readonly RegexHelperViewModel _viewModel;

    public string ResultPattern => _viewModel.Pattern;
    public string ResultReplacement => _viewModel.Replacement;

    public RegexHelperWindow() : this("", "", "") { }

    public RegexHelperWindow(string initialPattern = "", string initialReplacement = "", string initialSampleText = "")
    {
        InitializeComponent();
        _viewModel = new RegexHelperViewModel(initialPattern, initialReplacement, initialSampleText);
        DataContext = _viewModel;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
