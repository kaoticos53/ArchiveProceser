using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Views;

public partial class EditorView : UserControl
{
    private Point? _lastRightClickPosition;
    private Point? _lastPointerPosition;
    private readonly Dictionary<NodeViewModel, Point> _nodeDragStartPositions = [];

    static EditorView()
    {
        // El paneo del lienzo se activa con el botón derecho del ratón en el fondo del canvas.
        // Cuando el usuario hace clic derecho sobre un nodo, NodeCardView intercepta el evento
        // (e.Handled = true) y muestra el menú contextual del nodo. Los clics derechos que llegan
        // a NodifyEditor son los del fondo libre del canvas, que inician el paneo.
        Nodify.Avalonia.EditorGestures.Mappings.Editor.Pan.Value =
            new Nodify.Avalonia.Helpers.Gestures.PointerGesture(
                Nodify.Avalonia.Helpers.Gestures.MouseAction.RightClick);
    }

    public EditorView()
    {
        InitializeComponent();
        
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, Editor_Drop);
        AddHandler(DragDrop.DragOverEvent, Editor_DragOver);
        
        // Usar fase de túnel (Tunnel) para interceptar clics derechos
        // sobre conexiones ANTES de que NodifyEditor los procese como inicio de pan.
        AddHandler(InputElement.PointerPressedEvent, EditorView_TunnelingPointerPressed, 
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, EditorView_PointerReleased,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble);
        
        PointerPressed += EditorView_PointerPressed;
        PointerMoved += EditorView_PointerMoved;
        KeyDown += EditorView_KeyDown;
        DoubleTapped += EditorView_DoubleTapped;
        DataContextChanged += EditorView_DataContextChanged;
    }

    private void EditorView_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            vm.PropertyChanged -= ViewModel_PropertyChanged;
            vm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.IsSpotlightOpen))
        {
            if (DataContext is EditorViewModel { IsSpotlightOpen: true })
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SpotlightSearchBox?.Focus();
                    SpotlightSearchBox?.SelectAll();
                }, DispatcherPriority.Input);
            }
        }
    }

    private void Editor_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void Editor_Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            var typeName = e.DataTransfer.TryGetText();
            if (!string.IsNullOrEmpty(typeName))
            {
                Point screenPoint = e.GetPosition(NodifyCanvas);
                double zoom = vm.ViewportZoom > 0 ? vm.ViewportZoom : 1.0;
                Point canvasPoint = new Point(
                    vm.ViewportLocation.X + (screenPoint.X / zoom),
                    vm.ViewportLocation.Y + (screenPoint.Y / zoom)
                );
                vm.AddNode(typeName, canvasPoint);
                e.Handled = true;
            }
        }
    }

    private void EditorView_PointerMoved(object? sender, PointerEventArgs e)
    {
        _lastPointerPosition = e.GetPosition(NodifyCanvas);
    }

    private void EditorView_TunnelingPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && DataContext is EditorViewModel vm)
        {
            _nodeDragStartPositions.Clear();
            foreach (var node in vm.Nodes)
            {
                _nodeDragStartPositions[node] = node.Location;
            }
        }

        // Este handler se ejecuta en fase de túnel (antes que NodifyEditor).
        // Si el clic derecho cae sobre un ConnectionContainer que tiene ContextMenu,
        // lo abrimos nosotros y marcamos Handled para que NodifyEditor no inicie el pan.
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;

        if (e.Source is Avalonia.Visual src)
        {
            // Buscar el ConnectionContainer más cercano en el árbol visual
            var connContainer = src.FindAncestorOfType<Nodify.Avalonia.Connections.ConnectionContainer>()
                ?? (src is Nodify.Avalonia.Connections.ConnectionContainer cc ? cc : null);

            if (connContainer?.ContextMenu is { } connMenu)
            {
                e.Handled = true;
                connMenu.Open(connContainer);
                return;
            }

            // Si el clic está dentro de un NodeCardView, NodeCardView_PointerPressed ya lo maneja.
            // No intervenir aquí para las tarjetas de nodo.
        }
    }

    private void EditorView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not EditorViewModel vm) return;

        if (_nodeDragStartPositions.Count > 0)
        {
            var movedNodes = new List<FileFlow.App.Services.UndoRedo.NodeMoveItem>();
            foreach (var (node, oldPos) in _nodeDragStartPositions)
            {
                if (node.Location != oldPos)
                {
                    movedNodes.Add(new FileFlow.App.Services.UndoRedo.NodeMoveItem(node, oldPos, node.Location));
                }
            }

            if (movedNodes.Count > 0)
            {
                vm.UndoRedoService.Record(new FileFlow.App.Services.UndoRedo.MoveNodesAction(movedNodes));
            }
            _nodeDragStartPositions.Clear();
        }
    }

    private void EditorView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        _lastPointerPosition = e.GetPosition(NodifyCanvas);
        if (props.IsRightButtonPressed)
        {
            _lastRightClickPosition = e.GetPosition(this);
        }
    }

    private void EditorView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not EditorViewModel vm) return;

        // If double tapping directly on canvas background (not on a node card or interactive element)
        if (e.Source is not TextBox and not Button && e.Source is Control sourceCtrl)
        {
            // Only open if double clicking editor background or canvas
            var point = e.GetPosition(NodifyCanvas);
            double zoom = vm.ViewportZoom > 0 ? vm.ViewportZoom : 1.0;
            var canvasPoint = new Point(
                vm.ViewportLocation.X + (point.X / zoom),
                vm.ViewportLocation.Y + (point.Y / zoom)
            );
            vm.OpenSpotlight(canvasPoint);
            e.Handled = true;
        }
    }

    private void EditorView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not EditorViewModel vm) return;

        // If spotlight is currently open, let spotlight handlers handle navigation/escape
        if (vm.IsSpotlightOpen)
        {
            if (e.Key == Key.Escape)
            {
                vm.CloseSpotlight();
                e.Handled = true;
            }
            return;
        }

        if (e.Source is TextBox)
        {
            return;
        }

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        // Blender / ComfyUI Quick-Add: Shift+A or Space
        if ((shift && e.Key == Key.A) || (e.Key == Key.Space && !ctrl))
        {
            Point canvasPoint;
            double zoom = vm.ViewportZoom > 0 ? vm.ViewportZoom : 1.0;
            if (_lastPointerPosition.HasValue)
            {
                canvasPoint = new Point(
                    vm.ViewportLocation.X + (_lastPointerPosition.Value.X / zoom),
                    vm.ViewportLocation.Y + (_lastPointerPosition.Value.Y / zoom)
                );
            }
            else
            {
                canvasPoint = new Point(
                    vm.ViewportLocation.X + 250,
                    vm.ViewportLocation.Y + 200
                );
            }

            vm.OpenSpotlight(canvasPoint);
            e.Handled = true;
            return;
        }

        if (ctrl && !shift && e.Key == Key.Z)
        {
            if (vm.UndoCommand.CanExecute(null))
            {
                vm.UndoCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }
        else if ((ctrl && e.Key == Key.Y) || (ctrl && shift && e.Key == Key.Z))
        {
            if (vm.RedoCommand.CanExecute(null))
            {
                vm.RedoCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

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

    private void SpotlightSearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not EditorViewModel vm) return;

        if (e.Key == Key.Down)
        {
            if (vm.FilteredSpotlightItems.Count > 0)
            {
                int currentIndex = vm.SelectedSpotlightItem != null
                    ? vm.FilteredSpotlightItems.IndexOf(vm.SelectedSpotlightItem)
                    : -1;
                int nextIndex = (currentIndex + 1) % vm.FilteredSpotlightItems.Count;
                vm.SelectedSpotlightItem = vm.FilteredSpotlightItems[nextIndex];
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Up)
        {
            if (vm.FilteredSpotlightItems.Count > 0)
            {
                int currentIndex = vm.SelectedSpotlightItem != null
                    ? vm.FilteredSpotlightItems.IndexOf(vm.SelectedSpotlightItem)
                    : 0;
                int prevIndex = (currentIndex - 1 + vm.FilteredSpotlightItems.Count) % vm.FilteredSpotlightItems.Count;
                vm.SelectedSpotlightItem = vm.FilteredSpotlightItems[prevIndex];
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            vm.ConfirmSpotlightSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CloseSpotlight();
            e.Handled = true;
        }
    }

    private void SpotlightList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not EditorViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            vm.ConfirmSpotlightSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CloseSpotlight();
            e.Handled = true;
        }
    }

    private void SpotlightList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            vm.ConfirmSpotlightSelection();
            e.Handled = true;
        }
    }
}

