using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void DrawerBackdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ControlBar.IsMenuOpen = false;
        }
    }
}
