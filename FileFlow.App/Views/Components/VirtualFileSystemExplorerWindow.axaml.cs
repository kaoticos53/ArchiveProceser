using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.VirtualFileSystem;

namespace FileFlow.App.Views.Components;

public partial class VirtualFileSystemExplorerWindow : Window
{
    public VirtualFileSystemExplorerWindow()
    {
        InitializeComponent();
    }

    public VirtualFileSystemExplorerWindow(IVirtualFileSystemStore store)
    {
        InitializeComponent();
        DataContext = new VirtualFileSystemExplorerViewModel(store);
    }

    public VirtualFileSystemExplorerWindow(VirtualFileSystemExplorerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
