using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FileFlow.App.Models;

namespace FileFlow.App.Views;

public partial class NodeToolboxView : UserControl
{
    public NodeToolboxView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void NodeItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control element && element.DataContext is NodeToolboxItem item)
        {
            var dragData = new DataObject();
            dragData.Set("NodeTypeName", item.TypeName);
            await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy);
        }
    }
}
