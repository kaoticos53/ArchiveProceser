using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views;

public partial class EditorView : UserControl
{
    private Point? _lastRightClickPosition;

    public EditorView()
    {
        InitializeComponent();
        
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, Editor_Drop);
        AddHandler(DragDrop.DragOverEvent, Editor_DragOver);
        
        PointerPressed += EditorView_PointerPressed;
        KeyDown += EditorView_KeyDown;
    }

    private void Editor_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
    }

    private void Editor_Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            if (e.DataTransfer.TryGetValue(DataFormat.Text) is string typeName && !string.IsNullOrEmpty(typeName))
            {
                Point dropPoint = e.GetPosition(this);
                vm.AddNode(typeName, dropPoint);
            }
        }
    }

    private void EditorView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsRightButtonPressed)
        {
            _lastRightClickPosition = e.GetPosition(this);
        }
    }

    private void EditorView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox)
        {
            return;
        }

        if (DataContext is not EditorViewModel vm) return;

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (ctrl && e.Key == Key.C)
        {
            vm.CopySelectedNodesCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.V)
        {
            vm.PasteNodesCommand.Execute(_lastRightClickPosition);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.X)
        {
            vm.CutSelectedNodesCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.D)
        {
            vm.DuplicateSelectedNodesCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            vm.DeleteSelectedNodesCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            var selected = vm.Nodes.FirstOrDefault(n => n.IsSelected);
            if (selected != null)
            {
                selected.StartRenaming();
                e.Handled = true;
            }
        }
    }
}
