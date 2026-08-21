using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views;

public partial class EditorView : UserControl
{
    public EditorView()
    {
        InitializeComponent();
    }

    private void Editor_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("NodeTypeName") && DataContext is EditorViewModel vm)
        {
            string typeName = (string)e.Data.GetData("NodeTypeName");
            Point dropPoint = e.GetPosition(this);
            vm.AddNode(typeName, dropPoint);
        }
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is NodeViewModel node)
        {
            node.UpdateWidth(node.Width + e.HorizontalChange);
        }
    }

    private void Node_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is NodeViewModel node)
        {
            node.InspectNode();
            e.Handled = true;
        }
    }
}
