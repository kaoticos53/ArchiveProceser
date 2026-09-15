using Avalonia.Controls;
using Avalonia.Input;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class NodeCardView : UserControl
{
    public NodeCardView()
    {
        InitializeComponent();
        
        var titleBox = this.FindControl<TextBox>("TitleEditBox");
        if (titleBox != null)
        {
            titleBox.KeyDown += TitleEditBox_KeyDown;
            titleBox.LostFocus += TitleEditBox_LostFocus;
        }

        PointerPressed += NodeCardView_PointerPressed;
        DoubleTapped += NodeCardView_DoubleTapped;
    }

    private void NodeCardView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            node.ParentEditor?.BringToFront(node);
        }
    }

    private void NodeCardView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            node.InspectNode();
            e.Handled = true;
        }
    }

    private void ResizeThumb_DragDelta(object? sender, VectorEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            node.UpdateWidth(node.Width + e.Vector.X);
        }
    }

    private void TitleEditBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            if (e.Key == Key.Enter)
            {
                node.CommitTitleRename();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                node.CancelTitleRename();
                e.Handled = true;
            }
        }
    }

    private void TitleEditBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is NodeViewModel node && node.IsEditingTitle)
        {
            node.CommitTitleRename();
        }
    }
}
