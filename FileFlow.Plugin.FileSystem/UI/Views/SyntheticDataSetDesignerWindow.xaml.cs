using System.Windows;
using FileFlow.Plugin.FileSystem.UI.ViewModels;

namespace FileFlow.Plugin.FileSystem.UI.Views;

public partial class SyntheticDataSetDesignerWindow : Window
{
    private readonly SyntheticDataSetDesignerViewModel _viewModel;

    public SyntheticDataSetDesignerWindow(SyntheticDataSetDesignerViewModel? viewModel = null)
    {
        InitializeComponentSafe();
        _viewModel = viewModel ?? new SyntheticDataSetDesignerViewModel();
        DataContext = _viewModel;
    }

    private void InitializeComponentSafe()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception)
        {
            var uri = new Uri("/FileFlow.Plugin.FileSystem;component/ui/views/syntheticdatasetdesignerwindow.xaml", UriKind.Relative);
            Application.LoadComponent(this, uri);
        }
    }
}
