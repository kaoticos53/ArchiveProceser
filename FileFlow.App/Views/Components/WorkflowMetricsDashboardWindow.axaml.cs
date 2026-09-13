using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class WorkflowMetricsDashboardWindow : Window
{
    public WorkflowMetricsDashboardWindow()
    {
        InitializeComponent();
    }

    public WorkflowMetricsDashboardWindow(WorkflowMetricsDashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
