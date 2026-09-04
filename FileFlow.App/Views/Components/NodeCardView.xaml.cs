using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class NodeCardView : UserControl
{
    public NodeCardView()
    {
        InitializeComponent();
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            node.UpdateWidth(node.Width + e.HorizontalChange);
        }
    }

    private void UserControl_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            node.ParentEditor?.BringToFront(node);
        }
    }

    private void Node_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            node.InspectNode();
            e.Handled = true;
        }
    }
}
