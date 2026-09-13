using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.Plugin.FileSystem.UI.ViewModels;

namespace FileFlow.Plugin.FileSystem.UI.Views;

public partial class SyntheticDataSetDesignerWindow : Window
{
    private readonly SyntheticDataSetDesignerViewModel _viewModel;

    public SyntheticDataSetDesignerWindow(SyntheticDataSetDesignerViewModel? viewModel = null)
    {
        _viewModel = viewModel ?? new SyntheticDataSetDesignerViewModel();
        DataContext = _viewModel;
    }

    private void TreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SyntheticDataSetDesignerViewModel vm && sender is TreeView tv && tv.SelectedItem is SyntheticTreeNodeItem node)
        {
            vm.SelectedTreeNode = node;
        }
    }
}
