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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SyntheticDataSetDesignerWindow] Primary InitializeComponent failed, trying fallback: {ex.Message}");
            try
            {
                var uri = new Uri("/FileFlow.Plugin.FileSystem;component/ui/views/syntheticdatasetdesignerwindow.xaml", UriKind.Relative);
                Application.LoadComponent(this, uri);
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SyntheticDataSetDesignerWindow] Fallback LoadComponent failed: {fallbackEx.Message}");
            }
        }
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is SyntheticDataSetDesignerViewModel vm && e.NewValue is SyntheticTreeNodeItem node)
        {
            vm.SelectedTreeNode = node;
        }
    }
}
