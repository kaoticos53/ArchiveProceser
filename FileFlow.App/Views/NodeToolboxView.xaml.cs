using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileFlow.App.Models;

namespace FileFlow.App.Views;

public partial class NodeToolboxView : UserControl
{
    public NodeToolboxView()
    {
        InitializeComponent();
    }

    private void NodeItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is NodeToolboxItem item)
        {
            var dragData = new DataObject("NodeTypeName", item.TypeName);
            DragDrop.DoDragDrop(element, dragData, DragDropEffects.Copy);
        }
    }
}
