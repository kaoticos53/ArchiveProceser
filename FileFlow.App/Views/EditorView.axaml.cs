using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views;

public partial class EditorView : UserControl
{
    private Point? _lastRightClickPosition;

    public EditorView()
    {
        InitializeComponent();
        KeyDown += EditorView_KeyDown;
        AddHandler(PointerPressedEvent, EditorView_PointerPressed, RoutingStrategies.Tunnel);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void EditorView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
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
