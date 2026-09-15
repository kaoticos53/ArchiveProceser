using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FileFlow.App.ViewModels;

namespace FileFlow.App;

public partial class MainWindow : Window
{
    public MainWindow() : this(App.Services?.GetService(typeof(ViewModels.MainViewModel)) as ViewModels.MainViewModel)
    {
    }

    public MainWindow(ViewModels.MainViewModel? mainViewModel)
    {
        InitializeComponent();
        if (mainViewModel != null)
        {
            DataContext = mainViewModel;
        }
        Services.WindowThemeHelper.ApplyThemeToWindow(this);

        Services.ThemeManager.Instance.ThemeChanged += (theme) =>
        {
            Services.WindowThemeHelper.ApplyThemeToWindow(this);
        };
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
