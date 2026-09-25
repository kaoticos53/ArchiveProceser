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
    /// <summary>
    /// Garantiza que el estudio se abra <b>siempre con su view model</b>, incluso cuando quien lo abre no
    /// lo inyecta.
    ///
    /// El síntoma que esto evita: la ventana se mostraba sin <see cref="DataContext"/> y ningún
    /// <c>{Binding}</c> resolvía contra nada, de modo que el catálogo de temas salía vacío, el editor por
    /// secciones no se generaba y ningún botón (nuevo, duplicar, eliminar, aplicar) hacía nada. El estudio
    /// no puede depender de que el llamante se acuerde de conectarlo.
    ///
    /// Se resuelve al <b>abrir</b> y no en el constructor a propósito: así, quien sí inyecta su view model
    /// (las pruebas, las capturas y la barra de control) no paga el coste de construir uno de descarte.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is null)
        {
            DataContext = new ThemeCustomizerViewModel();
        }
    }

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
            _viewModel.LivePreviewUpdated += OnLivePreviewUpdated;
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

    private void OnLivePreviewUpdated()
    {
        // Los valores del diccionario portable cambiaron en el sitio; el ResourceDictionary de
        // Avalonia no los ve (los structs se copiaron), así que se re-publican.
        AttachPreviewResources();
    }

    private void AttachPreviewResources()
    {
        if (_viewModel == null || PreviewHost == null)
        {
            return;
        }

        var dict = new ResourceDictionary();
        foreach (var (key, value) in _viewModel.LivePreviewResources)
        {
            if (value is Avalonia.Media.IBrush or Avalonia.Media.Color or string or double
                or Avalonia.CornerRadius or Avalonia.Media.BoxShadows)
            {
                dict[key] = value;
            }
        }
        PreviewHost.Resources = dict;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
