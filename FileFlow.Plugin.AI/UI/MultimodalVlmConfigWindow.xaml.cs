using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FileFlow.Plugin.AI.ViewModels;

namespace FileFlow.Plugin.AI.UI;

/// <summary>
/// Ventana modal de configuración técnica avanzada para el nodo de IA Multimodal (VLM).
/// </summary>
public partial class MultimodalVlmConfigWindow : Window
{
    private readonly MultimodalVlmConfigViewModel _viewModel;

    public MultimodalVlmConfigViewModel ViewModel => _viewModel;

    public MultimodalVlmConfigWindow(MultimodalVlmConfigViewModel? viewModel = null)
    {
        _viewModel = viewModel ?? new MultimodalVlmConfigViewModel();
        _viewModel.RequestClose = () =>
        {
            Close();
        };
        DataContext = _viewModel;
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
