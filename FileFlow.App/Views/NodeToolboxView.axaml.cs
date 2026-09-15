using Avalonia.Controls;
using Avalonia.Input;
using FileFlow.App.Models;

namespace FileFlow.App.Views;

public partial class NodeToolboxView : UserControl
{
    public NodeToolboxView()
    {
        InitializeComponent();
    }

    private async void NodeItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control element && element.DataContext is NodeToolboxItem item && !string.IsNullOrEmpty(item.TypeName))
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(item.TypeName));
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
        }
    }
}
