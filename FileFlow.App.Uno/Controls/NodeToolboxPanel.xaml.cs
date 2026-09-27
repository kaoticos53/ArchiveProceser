using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
            if (_vm is not null)
            {
                // El modo compacto/detallado vive en el VM (con persistencia en preferencias); la
                // vista reacciona por PropertyChanged — el x:Bind de una DataTemplate de WinUI no
                // alcanza la página (la lección que dejó el pendiente declarado en el plan).
                _vm.PropertyChanged -= OnVmPropertyChanged;
                _vm.PropertyChanged += OnVmPropertyChanged;
                _vm.CategoryGroups.CollectionChanged -= OnGroupsChanged;
                _vm.CategoryGroups.CollectionChanged += OnGroupsChanged;
            }

            Bindings.Update();
            ApplyViewMode();
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

    /// <summary>Vista compacta (sólo nombre) o detallada (con insignia de rol y descripción), la
    /// propiedad del VM del núcleo con su persistencia en preferencias (hito 246).</summary>
    public bool IsCompact => _vm?.IsCompactMode ?? true;

    /// <summary>
    /// Fija la clave del título y aplica la localización vigente.
    /// </summary>
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
        var toggle = FindDescendantByName(this, "ViewModeToggle") as FrameworkElement;
        if (toggle is not null)
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle,
                loc.GetString("Uno_ToolboxViewMode", "Compact / detailed view"));
        }
    }

    private void OnLanguageChanged(object? sender, System.Globalization.CultureInfo e) => ApplyLocalization();

    /// <summary>El conmutador del modo por el MISMO comando del VM que el botón del escritorio.</summary>
    private void OnViewModeToggleClicked(object sender, RoutedEventArgs e)
    {
        _vm?.ToggleViewModeCommand.Execute(null);
    }

    /// <summary>El VM conmuta IsCompactMode (con persistencia); la vista reacciona por PropertyChanged.</summary>
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "IsCompactMode" or "")
        {
            _ = DispatcherQueue.TryEnqueue(ApplyViewMode);
        }
    }

    /// <summary>El refresco del catálogo reemplaza los grupos: re-aplicar el modo a lo nuevo.</summary>
    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(ApplyViewMode);
    }

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
    /// Aplica el modo del VM al árbol de ítems: en compacto, el bloque detallado (insignia de rol
    /// + descripción) de cada ítem colapsa a <see cref="Visibility.Collapsed"/> — el x:Bind de la
    /// DataTemplate no alcanza la página, así que es el recorrido del árbol el que reacciona
    /// (hito 246). Los contenedores que se materialicen DESPUÉS (scroll, regeneración) se aplican
    /// solos en su <c>Loading</c>. Sin VM (deselección) aplica compacto, el valor de fábrica.
    /// </summary>
    private void ApplyViewMode()
    {
        bool compact = _vm?.IsCompactMode ?? true;
        Visibility detailsVisibility = compact ? Visibility.Collapsed : Visibility.Visible;
        try
        {
            int affected = 0;
            ApplyToDetailsBlocks(this, detailsVisibility, ref affected);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // El recorrido en plena regeneración de contenedores (el refresco del catálogo) lanza
            // COMException — la medición del 246 lo cazó. Los bloques ya cargados toman su estado
            // en el próximo Loading; los vivos, en el reintento.
        }
    }

    /// <summary>El recorrido del árbol que aplica la visibilidad a cada bloque detallado.</summary>
    private static void ApplyToDetailsBlocks(DependencyObject root, Visibility visibility, ref int affected)
    {
        int count;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(root);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return; // la rama se invalidó (generación en curso): se abandona este subtree
        }

        for (int i = 0; i < count; i++)
        {
            DependencyObject child;
            try
            {
                child = VisualTreeHelper.GetChild(root, i);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                continue;
            }

            if (child is StackPanel { Tag: "ToolboxItemDetails" } details)
            {
                details.Visibility = visibility;
                affected++;
                continue; // el bloque no contiene otros bloques: rama terminada
            }

            ApplyToDetailsBlocks(child, visibility, ref affected);
        }
    }

    /// <summary>
    /// Cada bloque detallado toma SU estado al materializarse el contenedor: los ítems que entran
    /// por scroll o regeneración nacen con la visibilidad del modo vigente, sin esperar al
    /// siguiente recorrido (hito 246).
    /// </summary>
    private void OnToolboxItemDetailsLoading(FrameworkElement sender, object args)
    {
        if (sender is StackPanel details)
        {
            details.Visibility = (_vm?.IsCompactMode ?? true) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static DependencyObject? FindDescendantByName(DependencyObject root, string name)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { Name: var n } && n == name)
            {
                return child;
            }

            if (FindDescendantByName(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
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
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.CategoryGroups.CollectionChanged -= OnGroupsChanged;
        }
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

    /// <summary>Conmuta el modo por el MISMO comando que el botón (la sonda del selfcheck, hito 246).</summary>
    internal void ToggleViewModeViaCommand()
    {
        _vm?.ToggleViewModeCommand.Execute(null);
    }

    /// <summary>El primer grupo con ítems para expandirlo desde la sonda (los acordeones colapsados no materializan).</summary>
    internal ToolboxCategoryGroup? FirstGroupWithItemsForProbe()
    {
        return _vm?.CategoryGroups.FirstOrDefault(g => g.Items.Count > 0);
    }

    /// <summary>
    /// Los bloques detallados del árbol con el modo aplicado (compacto = ocultos). Reintenta tras
    /// asentar el dispatcher: el recorrido en plena regeneración de contenedores lanza
    /// COMException (la medición del 246) — con el asentamiento, los contadores son los reales.
    /// </summary>
    internal (int Total, int Hidden, int Visible) ProbeDetailsBlocks()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            int total = 0, hidden = 0, visible = 0;
            var walked = 0;
            int stackPanels = 0, itemRoots = 0;
            try
            {
                // La cura medida del 246: los contenedores preparados mientras el ItemsControl del
                // grupo estaba COLAPSADO no materializan su contenido de plantilla ni al hacerse
                // visibles (la medición: 10 ContentPresenters vacíos tras expandir) — el empuje
                // explícito de materialización (medir cada contenedor vacío) los despierta.
                try
                {
                    ForceItemTemplates();
                    UpdateLayout();
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }

                CountDetailsBlocks(this, ref total, ref hidden, ref visible, ref walked, ref stackPanels, ref itemRoots);

                if (total > 0)
                {
                    return (total, hidden, visible);
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // regeneración de contenedores en curso: la pausa breve deja pasar el pase
            }

            System.Threading.Thread.Sleep(200);
        }

        int lastTotal = 0, lastHidden = 0, lastVisible = 0;
        int lastWalked = 0, lastPanels = 0, lastRoots = 0;
        try
        {
            CountDetailsBlocks(this, ref lastTotal, ref lastHidden, ref lastVisible, ref lastWalked, ref lastPanels, ref lastRoots);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }

        return (lastTotal, lastHidden, lastVisible);
    }

    /// <summary>
    /// El empuje de materialización: cada ContentPresenter de ítem que no tenga contenido, fuerza
    /// su ApplyTemplate (los contenedores preparados en colapso no materializan solos al
    /// visibilizarse — la medición del 246). Devuelve los contenedores empujados.
    /// </summary>
    internal int ForceItemTemplates()
    {
        int forced = 0;
        ForceTemplatesRecursive(this, ref forced);
        return forced;
    }

    private static void ForceTemplatesRecursive(DependencyObject node, ref int forced)
    {
        int count;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(node);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            DependencyObject child;
            try
            {
                child = VisualTreeHelper.GetChild(node, i);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                continue;
            }

            if (child is ContentPresenter presenter && presenter.Content is not null && presenter.ContentTemplate is not null)
            {
                try
                {
                    int before = VisualTreeHelper.GetChildrenCount(presenter);
                    if (before == 0)
                    {
                        // No hay ApplyTemplate en ContentPresenter: medir (no materializa el pase
                        // de layout no lo hará si el contenedor nació sin medir) fuerza la fábrica.
                        presenter.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                        forced++;
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }
            }

            ForceTemplatesRecursive(child, ref forced);
        }
    }

    private static void CountDetailsBlocks(DependencyObject root, ref int total, ref int hidden, ref int visible, ref int walked, ref int stackPanels, ref int itemRoots)
    {
        int count;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(root);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            DependencyObject child;
            try
            {
                child = VisualTreeHelper.GetChild(root, i);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                continue;
            }

            walked++;
            if (child is StackPanel)
            {
                stackPanels++;
            }

            if (child is FrameworkElement { Name: "ToolboxItemRoot" })
            {
                itemRoots++;
            }
            if (child is StackPanel { Tag: "ToolboxItemDetails" } details)
            {
                total++;
                if (details.Visibility == Visibility.Collapsed)
                {
                    hidden++;
                }
                else if (details.Visibility == Visibility.Visible)
                {
                    visible++;
                }

                continue;
            }

            CountDetailsBlocks(child, ref total, ref hidden, ref visible, ref walked, ref stackPanels, ref itemRoots);
        }
    }

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
