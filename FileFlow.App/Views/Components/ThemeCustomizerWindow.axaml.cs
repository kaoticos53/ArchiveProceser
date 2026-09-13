using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class ThemeCustomizerWindow : Window
{
    private readonly ThemeCustomizerViewModel _viewModel;

    public ThemeCustomizerWindow() : this(new ThemeCustomizerViewModel())
    {
    }

    public ThemeCustomizerWindow(ThemeCustomizerViewModel viewModel)
    {
        InitializeComponent();
        WindowThemeHelper.ApplyThemeToWindow(this);

        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
