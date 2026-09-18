using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
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

        AddHandler(InputElement.GotFocusEvent, (sender, e) =>
        {
            if (e.Source is Avalonia.Visual visual)
            {
                var acb = visual.FindAncestorOfType<AutoCompleteBox>() ?? (visual is AutoCompleteBox box ? box : null);
                if (acb != null && acb.MinimumPrefixLength == 0 && !acb.IsDropDownOpen)
                {
                    acb.IsDropDownOpen = true;
                }
            }
        });
    }

    private static bool IsInteractiveVisual(object? source)
    {
        if (source is not Avalonia.Visual visual) return false;

        return visual.FindAncestorOfType<ComboBox>() != null ||
               visual is ComboBox ||
               visual.FindAncestorOfType<ComboBoxItem>() != null ||
               visual is ComboBoxItem ||
               visual.FindAncestorOfType<AutoCompleteBox>() != null ||
               visual is AutoCompleteBox ||
               visual.FindAncestorOfType<Button>() != null ||
               visual is Button ||
               visual.FindAncestorOfType<ToggleButton>() != null ||
               visual is ToggleButton ||
               visual.FindAncestorOfType<TextBox>() != null ||
               visual is TextBox ||
               visual.FindAncestorOfType<ToggleSwitch>() != null ||
               visual is ToggleSwitch ||
               visual.FindAncestorOfType<Slider>() != null ||
               visual is Slider ||
               visual.FindAncestorOfType<NumericUpDown>() != null ||
               visual is NumericUpDown ||
               visual.FindAncestorOfType<ListBox>() != null ||
               visual is ListBox ||
               visual.FindAncestorOfType<ListBoxItem>() != null ||
               visual is ListBoxItem ||
               visual.FindAncestorOfType<ScrollViewer>() != null ||
               visual is ScrollViewer;
    }

    private void NodeCardView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        // Clic derecho: el nodo debe mostrar su menú contextual.
        // Marcar como manejado evita que el evento burbujee a NodifyEditor, donde
        // el gesto de paneo (RightClick) capturaría el puntero y cancelaría el
        // ContextRequested. En su lugar, abrimos el ContextMenu manualmente.
        if (props.IsRightButtonPressed)
        {
            if (DataContext is NodeViewModel nodeRightClick)
            {
                nodeRightClick.ParentEditor?.BringToFront(nodeRightClick);
            }
            // Marcar handled para que NodifyEditor no inicie el paneo sobre este nodo.
            // Avalonia no dispara ContextRequested si el PointerPressed fue handled,
            // así que abrimos el ContextMenu directamente.
            e.Handled = true;
            if (ContextMenu is { } menu)
            {
                menu.Open(this);
            }
            return;
        }

        if (IsInteractiveVisual(e.Source))
        {
            // Traer al frente y seleccionar el nodo para que pase a primer plano
            // de inmediato al interactuar con cualquier parámetro o control.
            if (DataContext is NodeViewModel interactiveNode)
            {
                if (!interactiveNode.IsSelected)
                {
                    interactiveNode.IsSelected = true;
                }
                else
                {
                    interactiveNode.ParentEditor?.BringToFront(interactiveNode);
                }
            }

            // Marcar como manejado para evitar que el evento burbujee a ItemContainer de Nodify,
            // lo cual capturaría el puntero (e.Pointer.Capture) e interrumpiría la apertura o
            // interacción de ComboBox, AutoCompleteBox y demás controles interactivos en la tarjeta.
            e.Handled = true;
            return;
        }

        if (DataContext is NodeViewModel node)
        {
            node.ParentEditor?.BringToFront(node);
        }
    }

    private void NodeCardView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (IsInteractiveVisual(e.Source))
        {
            return;
        }

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
