using System.Windows;
using System.Windows.Controls;
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

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.LivePreviewResources) || e.PropertyName == nameof(_viewModel.EditingTheme))
            {
                ApplyLivePreviewResources();
            }
        };

        Loaded += (s, e) => ApplyLivePreviewResources();
    }

    private void ApplyLivePreviewResources()
    {
        if (LivePreviewContainer != null && _viewModel.LivePreviewResources != null)
        {
            LivePreviewContainer.Resources.MergedDictionaries.Clear();
            LivePreviewContainer.Resources.MergedDictionaries.Add(_viewModel.LivePreviewResources);
        }
    }

    private void ColorPicker_ColorChanged(object sender, RoutedPropertyChangedEventArgs<string> e)
    {
        _viewModel?.UpdateLivePreview();
        ApplyLivePreviewResources();
    }

    private void Input_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel?.UpdateLivePreview();
        ApplyLivePreviewResources();
    }

    private void Combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel?.UpdateLivePreview();
        ApplyLivePreviewResources();
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _viewModel?.UpdateLivePreview();
        ApplyLivePreviewResources();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
