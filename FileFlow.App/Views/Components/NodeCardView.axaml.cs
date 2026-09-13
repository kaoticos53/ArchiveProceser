using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views.Components;

public partial class NodeCardView : UserControl
{
    public NodeCardView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, UserControl_PointerPressed, RoutingStrategies.Tunnel);
        DoubleTapped += Node_DoubleTapped;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ResizeThumb_DragDelta(object? sender, VectorEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            node.UpdateWidth(node.Width + e.Vector.X);
        }
    }

    private void UserControl_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            node.ParentEditor?.BringToFront(node);
        }
    }

    private void Node_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is NodeViewModel node)
        {
            node.InspectNode();
            e.Handled = true;
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

    private void TitleEditBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NodeViewModel node && node.IsEditingTitle)
        {
            node.CommitTitleRename();
        }
    }
}
