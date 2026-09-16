using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class ThemeCustomizerWindow : Window
{
    private ThemeCustomizerViewModel? _viewModel;

    public ThemeCustomizerWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Engancha el diccionario de tokens en edición a la zona de previsualización.
    ///
    /// Es lo que hace que el Theme Studio sea «en vivo»: <c>PreviewHost</c> resuelve sus
    /// <c>DynamicResource</c> contra el tema que se está editando, mientras el resto de la ventana sigue
    /// mostrando el tema activo de la aplicación. La instancia del diccionario es estable (ver
    /// <see cref="ThemeCustomizerViewModel.LivePreviewResources"/>), así que basta engancharla una vez:
    /// cada ajuste reemplaza valores en el sitio y los recursos se vuelven a resolver solos.
    /// </summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ThemeCustomizerViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        AttachPreviewResources();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ThemeCustomizerViewModel.SelectedTheme))
        {
            // Cambiar de tema reconstruye el editor y regenera los tokens de la previsualización.
            AttachPreviewResources();
        }
    }

    private void AttachPreviewResources()
    {
        if (_viewModel == null || PreviewHost == null)
        {
            return;
        }

        PreviewHost.Resources = _viewModel.LivePreviewResources;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
