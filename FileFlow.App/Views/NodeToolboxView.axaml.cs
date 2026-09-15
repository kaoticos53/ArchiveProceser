using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views;

public partial class NodeToolboxView : UserControl
{
    private Point? _dragStartPoint;
    private NodeToolboxItem? _dragItem;
    private PointerPressedEventArgs? _triggerPointerEventArgs;
    private bool _isDragging;

    public NodeToolboxView()
    {
        InitializeComponent();
    }

    private void NodeItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) return;

        // Ignore if clicking on button (e.g. favorite star button)
        if (e.Source is Button || (e.Source is Visual visual && visual.FindAncestorOfType<Button>() != null))
        {
            return;
        }

        if (sender is Control element && element.DataContext is NodeToolboxItem item && !string.IsNullOrEmpty(item.TypeName))
        {
            _dragStartPoint = e.GetPosition(this);
            _dragItem = item;
            _triggerPointerEventArgs = e;
        }
    }

    private void NodeItem_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragStartPoint = null;
        _dragItem = null;
        _triggerPointerEventArgs = null;
        _isDragging = false;
    }

    private async void NodeItem_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging || _dragStartPoint == null || _dragItem == null || _triggerPointerEventArgs == null)
            return;

        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed)
        {
            _dragStartPoint = null;
            _dragItem = null;
            _triggerPointerEventArgs = null;
            return;
        }

        var currentPoint = e.GetPosition(this);
        var diff = _dragStartPoint.Value - currentPoint;

        if (Math.Abs(diff.X) > 6 || Math.Abs(diff.Y) > 6)
        {
            _isDragging = true;
            var triggerEvent = _triggerPointerEventArgs;
            try
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.CreateText(_dragItem.TypeName));
                await DragDrop.DoDragDropAsync(triggerEvent, data, DragDropEffects.Copy);
            }
            finally
            {
                _isDragging = false;
                _dragStartPoint = null;
                _dragItem = null;
                _triggerPointerEventArgs = null;
            }
        }
    }
}
