using System;
using System.Windows;
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
        InitializeComponentSafe();
        _viewModel = viewModel ?? new MultimodalVlmConfigViewModel();
        _viewModel.RequestClose = () =>
        {
            DialogResult = true;
            Close();
        };
        DataContext = _viewModel;
    }

    private void InitializeComponentSafe()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MultimodalVlmConfigWindow] Primary InitializeComponent failed, trying fallback: {ex.Message}");
            try
            {
                var uri = new Uri("/FileFlow.Plugin.AI;component/ui/multimodalvlmconfigwindow.xaml", UriKind.Relative);
                Application.LoadComponent(this, uri);
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"[MultimodalVlmConfigWindow] Fallback LoadComponent failed: {fallbackEx.Message}");
            }
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
