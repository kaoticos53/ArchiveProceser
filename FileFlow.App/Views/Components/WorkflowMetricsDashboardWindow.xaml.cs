using System.Windows;
using System.Windows.Input;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class WorkflowMetricsDashboardWindow : Window
{
    public WorkflowMetricsDashboardWindow(WorkflowMetricsDashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
