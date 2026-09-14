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



    private void TitleEditBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.Visibility == Visibility.Visible)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void TitleEditBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                node.CommitTitleRename();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                node.CancelTitleRename();
                e.Handled = true;
            }
        }
    }

    private void TitleEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is NodeViewModel node && node.IsEditingTitle)
        {
            node.CommitTitleRename();
        }
    }
}
