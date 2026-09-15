using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FileFlow.App.Views.Components;

public partial class WorkflowMetricsDashboardWindow : Window
{
    public WorkflowMetricsDashboardWindow()
    {
        InitializeComponent();
    }

    public WorkflowMetricsDashboardWindow(ViewModels.WorkflowMetricsDashboardViewModel vm) : this()
    {
        DataContext = vm;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
