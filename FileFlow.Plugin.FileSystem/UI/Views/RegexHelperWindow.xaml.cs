using System.Windows;
using FileFlow.Plugin.FileSystem.UI.ViewModels;

namespace FileFlow.Plugin.FileSystem.UI.Views;

public partial class RegexHelperWindow : Window
{
    private readonly RegexHelperViewModel _viewModel;

    public string ResultPattern => _viewModel.Pattern;
    public string ResultReplacement => _viewModel.Replacement;

    public RegexHelperWindow(string initialPattern = "", string initialReplacement = "", string initialSampleText = "")
    {
        InitializeComponentSafe();
        _viewModel = new RegexHelperViewModel(initialPattern, initialReplacement, initialSampleText);
        DataContext = _viewModel;
    }

    private void InitializeComponentSafe()
    {
        try
        {
            InitializeComponent();
        }
        catch (System.Exception)
        {
            var uri = new System.Uri("/FileFlow.Plugin.FileSystem;component/ui/views/regexhelperwindow.xaml", System.UriKind.Relative);
            System.Windows.Application.LoadComponent(this, uri);
        }
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
