using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views;

public partial class EditorView : UserControl
{
    private Point? _lastRightClickPosition;

    public EditorView()
    {
        InitializeComponent();
        PreviewKeyDown += EditorView_PreviewKeyDown;
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

    private void NodifyEditor_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _lastRightClickPosition = e.GetPosition(this);
    }

    private void EditorView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Si el usuario está escribiendo en un TextBox, PasswordBox o similar, respetar la entrada normal
        if (e.OriginalSource is TextBox || e.OriginalSource is PasswordBox)
        {
            return;
        }

        if (DataContext is not EditorViewModel vm) return;

        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

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
    }
}

