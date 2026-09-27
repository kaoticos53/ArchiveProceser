using System;
using System.Linq;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El cajón de herramientas del host Uno, montado sobre el <see cref="ToolboxViewModel"/> del núcleo
/// portable (el mismo que el escritorio): búsqueda, filtros de categoría, grupos acordeón con
/// expansión exclusiva gestionada por el VM, favoritos y doble clic para añadir el nodo en el centro
/// del viewport (el mismo <c>EditorViewModel.AddNode</c> que consume el Drop del lienzo). El gesto de
/// arrastre fino queda pendiente de la sesión con puntero real, igual que el cajón del escritorio en
/// su día.
/// </summary>
public sealed partial class NodeToolboxPanel : UserControl
{
    private ToolboxViewModel? _vm;
    private EditorViewModel? _editor;
    private string _titleKey = "Uno_ToolboxTitle";

    public NodeToolboxPanel()
    {
        InitializeComponent();
        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>El VM del núcleo que la vista consume; el x:Bind de la vista se ata a él.</summary>
    public ToolboxViewModel? Vm
    {
        get => _vm;
        set
        {
            if (ReferenceEquals(_vm, value))
            {
                return;
            }

            _vm = value;
            Bindings.Update();
        }
    }

    /// <summary>El editor que recibe el nodo al hacer doble clic (el mismo del lienzo).</summary>
    public EditorViewModel? Editor
    {
        get => _editor;
        set
        {
            if (ReferenceEquals(_editor, value))
            {
                return;
            }

            _editor = value;
        }
    }

    /// <summary>Vista compacta (sólo nombre) o detallada (con insignia de rol). El toggle del
    /// escritorio queda pendiente: el x:Bind de una DataTemplate de WinUI no alcanza la página.</summary>
    public bool IsCompact { get; private set; } = true;

    /// <summary>Fija la clave del título y aplica la localización vigente.</summary>
    public void ApplyLocalization(string? titleKey = null)
    {
        _titleKey = string.IsNullOrWhiteSpace(titleKey) ? "Uno_ToolboxTitle" : titleKey!;
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var loc = LocalizationManager.Instance;
        TitleText.Text = loc.GetString(_titleKey, "Nodes");
        SearchBox.PlaceholderText = loc.GetString("Uno_ToolboxSearch", "Buscar nodo… (Ctrl+F)");
    }

    private void OnLanguageChanged(object? sender, System.Globalization.CultureInfo e) => ApplyLocalization();

    private void OnCategoryChipClicked(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        if ((sender as FrameworkElement)?.Tag is not string key || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        // El VM conmuta el filtro y refresca: RefreshToolbox -> UpdateAvailableCategories actualiza
        // el IsSelected de cada chip, y el binding TwoWay repinta los ToggleButton.
        _vm.SetCategoryFilter(key);
    }

    private void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is NodeToolboxItem item)
        {
            TryAddItem(item);
        }
    }

    /// <summary>
    /// El gesto del doble clic reducido a método: añade el tipo del ítem en el centro del viewport por
    /// el MISMO <see cref="EditorViewModel.AddNode"/> que consume el Drop del lienzo. Internal para que
    /// el sondeo en runtime (--selfcheck) recorra el mismo camino que el handler.
    /// </summary>
    internal bool TryAddItem(NodeToolboxItem item)
    {
        if (_editor is null || item is null || string.IsNullOrWhiteSpace(item.TypeName))
        {
            return false;
        }

        // El centro del viewport es donde el escritorio añade con el spotlight; aquí con doble clic.
        return _editor.AddNode(item.TypeName, GraphCenterOfCanvas()) is not null;
    }

    /// <summary>
    /// El punto del grafo en el centro del lienzo: el lienzo resuelve su propio viewport (mismo
    /// control que consume el ratón), el panel lo localiza por el árbol visual de la ventana.
    /// </summary>
    private Sdk.Point GraphCenterOfCanvas()
    {
        var canvas = FindCanvas();
        return canvas is null ? new Sdk.Point(0, 0) : canvas.GraphPointAtViewportCenter();
    }

    private EditorCanvasControl? FindCanvas()
    {
        // Del panel a la raíz de la ventana y de vuelta: el lienzo es hermano del panel.
        DependencyObject current = this;
        while (VisualTreeHelper.GetParent(current) is { } parent)
        {
            current = parent;
        }

        return FindDescendant<EditorCanvasControl>(current);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : class
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is T nested)
            {
                return nested;
            }
        }

        return null;
    }

    public void Dispose()
    {
        LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
    }

    // ── Superficie interna para el sondeo en runtime (--selfcheck), sin tocar el árbol visual ──

    /// <summary>Cuenta de nodos del editor antes/después de añadir (la sonda compara).</summary>
    internal int EditorNodeCount => _editor?.Nodes.Count ?? -1;

    /// <summary>Añade el primer ítem de un grupo por el MISMO método que el doble clic (la sonda).</summary>
    internal bool TryAddFirstItemOfGroupForProbe()
    {
        var group = _vm?.CategoryGroups.FirstOrDefault(g => g.Items.Count > 0);
        var item = group?.Items[0];
        return item is not null && TryAddItem(item);
    }

    /// <summary>Escribe un término en SearchText por el MISMO setter que el binding (la sonda).</summary>
    internal void SearchForProbe(string term)
    {
        if (_vm is not null)
        {
            _vm.SearchText = term;
        }
    }

    /// <summary>Cuenta de ítems visibles del catálogo con el filtro vigente (la sonda compara).</summary>
    internal int VisibleItemCount =>
        (_vm?.CategoryGroups.Sum(g => g.Items.Count) ?? 0);

    /// <summary>
    /// Conmuta el favorito de un ítem por el MISMO comando que la estrella (la sonda). Devuelve el
    /// estado del ítem con ese TypeName DESPUÉS del refresco del catálogo — el refresco reemplaza la
    /// instancia del ítem, así que la referencia anterior queda obsoleta y no sirve para comparar.
    /// </summary>
    internal bool ToggleFavoriteViaCommand()
    {
        var item = _vm?.CategoryGroups.SelectMany(g => g.Items).FirstOrDefault();
        if (_vm is null || item is null)
        {
            return false;
        }

        string typeName = item.TypeName;
        bool before = item.IsFavorite;
        _vm.ToggleFavoriteCommand.Execute(item);

        var after = _vm.CategoryGroups.SelectMany(g => g.Items)
            .FirstOrDefault(i => string.Equals(i.TypeName, typeName, StringComparison.OrdinalIgnoreCase));
        return after is not null && after.IsFavorite != before;
    }
}
