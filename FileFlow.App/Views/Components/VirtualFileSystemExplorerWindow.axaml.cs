using Avalonia.Controls;

namespace FileFlow.App.Views.Components;

public partial class VirtualFileSystemExplorerWindow : Window
{
    public VirtualFileSystemExplorerWindow()
    {
        InitializeComponent();
    }

    public VirtualFileSystemExplorerWindow(FileFlow.Sdk.VirtualFileSystem.IVirtualFileSystemStore store) : this()
    {
        DataContext = new ViewModels.VirtualFileSystemExplorerViewModel(store);
    }
}
