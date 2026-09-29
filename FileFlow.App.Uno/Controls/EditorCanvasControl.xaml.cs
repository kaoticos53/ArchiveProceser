using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using FileFlow.App.Services;
using FileFlow.App.Services.UndoRedo;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El lienzo del editor en el host Uno (fase 3.1 del plan): tarjetas en modo lectura posicionadas por
/// <see cref="NodeViewModel.Location"/> —proyectada con <see cref="UnoPointConverter"/>, la regla del 217—,
/// cables estáticos dibujados con <see cref="ConnectionGeometry"/> (la Bézier del núcleo, la misma que pinta
/// Nodify en el escritorio) y pan/zoom/acotar.
///
/// <para><b>Por qué la posición va por código y no por Setter con Binding</b>: el motor XAML de WinUI no
/// evalúa enlaces dentro de <c>Setter.Value</c> (silenciosamente no hacen nada), así que el aplicador
/// <see cref="ApplyNodePosition"/> lee la posición proyectada del adaptador y la escribe en el Canvas —
/// pasando por el conversor, que es lo que la guardia de geometría censura en este fichero.</para>
///
/// <para><b>Modo lectura</b>: sin selección, arrastre ni puertos vivos (fases 3.2/3.3).</para>
/// </summary>
public sealed partial class EditorCanvasControl : UserControl
{
    /// <summary>El paso del grid, el doble del lienzo de Avalonia dibujado sutil para no robar contraste.</summary>
    private const double GridStep = 50.0;

    private const double MinZoom = 0.2;
    private const double MaxZoom = 2.5;

    private EditorViewModel? _editor;
    private bool _isPanning;
    private Windows.Foundation.Point _panStart;

    // Los contenedores materializados, indexados por su tarjeta: el arrastre reposiciona sin esperar al
    // pase de layout, y el drag descubre los contenedores de la selección entera.
    private readonly Dictionary<NodeCardViewModel, ContentPresenter> _containers = new();
    private readonly Dictionary<NodeViewModel, NodeCardViewModel> _cardsByNode = new();

    public EditorCanvasControl()
    {
        InitializeComponent();
        DrawBackgroundGrid();

        // La materialización de los contenedores del ItemsControl ocurre en el pase de layout, DESPUÉS de
        // cualquier Rebuild() síncrono (el setter de Editor incluido): aplicar posiciones ahí llega a un
        // árbol sin contenedores y las tarjetas quedan en (0,0). Cada pase de layout del host con
        // reconstrucción pendiente re-aplica — un flag barato que cubre carga inicial, ejemplo cargado en
        // el arranque y nodos añadidos en caliente.
        NodesHost.LayoutUpdated += OnNodesHostLayoutUpdated;
        KeyDown += OnKeyDown;

        // El rastro del foco (hito 252): con FILEFLOW_CANVAS_TRACE=1, una sesión manual deja escrito quién
        // tiene el foco al clicar y qué teclas llegan — la vía para medir un defecto de foco sin puntero
        // inyectable. Apagado por defecto: sin la variable no se toca el disco.
        GotFocus += (_, _) => CanvasFocusTrace.Write($"GotFocus  enfocado={DescribeFocused()}");
        LostFocus += (_, _) =>
        {
            // El ladrón, con ficha (hito 253): si se lleva el foco un elemento fuera del árbol visual —el
            // caso medido, con su GotFocus sin burbujear a la ventana— lo único que lo identifica es su
            // estado (cargado, tamaño, padre LÓGICO, contenido), así que se imprime entero y se re-lee un
            // tick después, cuando un elemento recién creado puede haberse enganchado ya al árbol.
            CanvasFocusTrace.Write($"LostFocus enfocado={DescribeThief()} | {DescribeOpenPopups()}");
            DispatcherQueue.TryEnqueue(() =>
            {
                CanvasFocusTrace.Write($"  perdida +tick enfocado={DescribeThief()} | {DescribeOpenPopups()}");

                // La otra mitad del arreglo del 253: si el que se lo llevó es ajeno al editor y el clic es
                // reciente, el teclado vuelve. Se hace en el tick siguiente a propósito: el envoltorio del
                // framework necesita su pase de layout para quedar como dueño, y reclamar antes sería una
                // carrera que a veces se perdería.
                ReclaimKeyboardIfStolenByFramework();
            });
        };
    }

    /// <summary>
    /// La cadena del origen de un evento, con el <c>DataContext</c> de cada eslabón (hito 278): es lo que
    /// identifica al elemento que se quedó con una pulsación cuando la conducta no es la esperada — así se
    /// midió que el pulsado del puerto caía en la cara de la tarjeta o en el fondo del lienzo. Va al rastro de
    /// sesión, junto al resto del instrumento del foco.
    /// </summary>
    private static string DescribeChain(object? source)
    {
        var parts = new List<string>();
        DependencyObject? node = source as DependencyObject;
        for (int i = 0; node is not null && i < 14; i++)
        {
            var fe = node as FrameworkElement;
            parts.Add($"{node.GetType().Name}#{fe?.Name ?? "-"}[dc={fe?.DataContext?.GetType().Name ?? "null"}]");
            node = node is UIElement ui ? VisualTreeHelper.GetParent(ui) : null;
        }

        return string.Join(" <- ", parts);
    }

    /// <summary>Quién tiene el foco, en palabras (tipo, nombre y cadena de ancestros), para el rastro del
    /// hito 252. La cadena es lo que identifica al ladrón cuando el que recibe el foco es anónimo (un
    /// <c>ScrollViewer</c> del inspector): con ella el rastro dice a qué panel pertenece.</summary>
    private string DescribeFocused() => CanvasFocusTrace.Describe(
        Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot), ancestors: 5);

    /// <summary>
    /// La ficha del elemento que tiene el foco, para identificarlo cuando NO está en el árbol visual: tipo y
    /// nombre, si está cargado, lo que mide, su padre LÓGICO (que existe aunque el visual no) y, si es un
    /// <c>ScrollViewer</c>, qué lleva dentro y si ese contenido está cargado. Nació en el hito 253, porque
    /// «un <c>ScrollViewer</c> anónimo» no es una identificación.
    /// </summary>
    private string DescribeThief()
    {
        object? focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot);
        if (focused is not FrameworkElement element)
        {
            return CanvasFocusTrace.Describe(focused, ancestors: 3);
        }

        var parts = new List<string>
        {
            $"{element.GetType().Name}#{element.Name}",
            $"cargado={element.IsLoaded}",
            $"mide={element.ActualWidth:F0}x{element.ActualHeight:F0}",
            $"padreLogico={CanvasFocusTrace.Describe(element.Parent, ancestors: 2)}",
            element.XamlRoot is null ? "sin XamlRoot" : "con XamlRoot"
        };

        parts.Add("datacontext=" + DescribeDataContext(element));

        if (element is ScrollViewer scroll)
        {
            parts.Add("contenido=" + CanvasFocusTrace.Describe(scroll.Content, ancestors: 2));
            parts.Add("contenidoCargado="
                + (scroll.Content is FrameworkElement inner ? inner.IsLoaded.ToString() : "n/a"));

            // Un envoltorio de scroll suele llevar dentro un Border con el panel de verdad: el tipo y el
            // DataContext de su HIJO es lo que dice a qué parte de la aplicación pertenece el ladrón.
            if (scroll.Content is FrameworkElement wrapper)
            {
                parts.Add("hijo=" + CanvasFocusTrace.Describe(
                    wrapper is ContentControl cc ? cc.Content : null, ancestors: 1));
                parts.Add("hijoDatacontext=" + DescribeDataContext(wrapper));
            }
        }

        return string.Join(" | ", parts);
    }

    /// <summary>El tipo del DataContext de un elemento: lo que ata un ladrón anónimo a su panel.</summary>
    private static string DescribeDataContext(FrameworkElement? element) =>
        element?.DataContext?.GetType().Name ?? "sin DataContext";

    /// <summary>
    /// Los popups abiertos en este momento, con su contenido y su cadena. Existe por el ladrón del foco del
    /// 252: el <c>ScrollViewer</c> que se lleva el foco ~0,5 s después del clic NO tiene ancestros en el
    /// árbol visual de la ventana (por eso el rastro sólo imprime su tipo), y eso es la firma de un popup —
    /// un <c>ToolTip</c>, por ejemplo. Sin esta línea el ladrón queda como «un ScrollViewer anónimo».
    /// </summary>
    private string DescribeOpenPopups()
    {
        try
        {
            var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
            if (popups.Count == 0)
            {
                return "popups=ninguno";
            }

            var described = popups.Select(p => p.Child is null
                ? "sin contenido"
                : $"{p.Child.GetType().Name}[{CanvasFocusTrace.Describe(p.Child, ancestors: 4)}]");
            return "popups=" + string.Join(" + ", described);
        }
        catch (Exception ex)
        {
            return "popups=no se pudo consultar (" + ex.GetType().Name + ")";
        }
    }

    /// <summary>Queda al menos una reconstrucción cuyos contenedores aún no recibieron su posición.</summary>
    private bool _positionsPending = true;

    private void OnNodesHostLayoutUpdated(object sender, object e)
    {
        if (!_positionsPending)
        {
            return;
        }

        // El pase se consume sólo cuando ya se posicionaron todos los contenedores esperados; mientras,
        // el siguiente pase de layout reintenta (la materialización puede tardar más de un pase).
        if (ApplyAllNodePositions() >= (_editor?.Nodes.Count ?? 0))
        {
            _positionsPending = false;

            // Las tarjetas ya existen (hito 278): el cableado de sus sockets se hace AQUÍ, no en Rebuild.
            // Al asignar el ItemsSource los contenedores todavía no se han materializado, así que Rebuild
            // no encontraba ninguna vista y el evento se quedaba SIN suscriptor: pulsar un puerto entraba
            // al handler de la tarjeta y moría ahí — «no pasa nada», el síntoma que reportó el usuario.
            WireCardEvents();

            // Con contenedores y tarjetas materializados, las anclas de los puertos ya son reales: el
            // write-back inicial y el primer trazado de cables con las anclas del árbol (fase 3.3).
            if (_editor is not null && _editor.Connections.Count > 0)
            {
                WriteBackAnchors();            DrawWires();
        }
    }
    }

    /// <summary>
    /// El peer de automatización del lienzo: sin él, un contenedor (UserControl + Grid) no expone
    /// nada por UIA — el árbol del hito 237 llegaba a las tarjetas pero NO a la superficie que recibe
    /// el foco y el teclado. Con peer enfocable, la observación externa (sondas UIA sin UIAccess)
    /// encuentra el lienzo por su AutomationId y puede entregarle el foco real — el paso que el
    /// acotamiento del 237 dejó en el puntero del usuario.
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new CanvasAutomationPeer(this);

    /// <summary>El peer del lienzo: control y contenido, para que la observación externa lo vea.</summary>
    private sealed class CanvasAutomationPeer(EditorCanvasControl owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }

    /// <summary>El ViewModel del editor que el lienzo pinta.</summary>
    public EditorViewModel? Editor
    {
        get => _editor;
        set
        {
            if (ReferenceEquals(_editor, value))
            {
                return;
            }

            if (_editor is not null)
            {
                ((INotifyCollectionChanged)_editor.Nodes).CollectionChanged -= OnNodesChanged;

                // Simetría del contrato de vida (hito 225): la suscripción que añade el editor entrante
                // para Connections tiene que soltarse también para el saliente, o cada reasignación del
                // Editor deja un lienzo fantasma redibujando sobre un VM que ya no pinta.
                ((INotifyCollectionChanged)_editor.Connections).CollectionChanged -= OnConnectionsChanged;

                // La misma simetría para la capa de decoradores de la fase 3.4.
                ((INotifyCollectionChanged)_editor.CanvasDecorators).CollectionChanged -= OnDecoratorsChanged;

                _editor.SelectedConnections.CollectionChanged -= OnSelectedConnectionsChanged;
            }

            _editor = value;

            if (_editor is not null)
            {
                // Fase 3.4: notas y grupos entran por CanvasDecorators (y sus propias colecciones);
                // sin esta suscripción la capa de decoradores no se entera de AddAnnotation/AddGroup.
                // (Antes de Nodes/Connections: el fragmento contiguo que la mutación del 227 vigila queda intacto.)
                ((INotifyCollectionChanged)_editor.CanvasDecorators).CollectionChanged += OnDecoratorsChanged;

                ((INotifyCollectionChanged)_editor.Nodes).CollectionChanged += OnNodesChanged;
                ((INotifyCollectionChanged)_editor.Connections).CollectionChanged += OnConnectionsChanged;

                // La MARCA de los cables vive en una colección (con Ctrl se añaden): su cambio es lo que
                // repinta el resalte, así que se escucha como las demás colecciones del editor.
                _editor.SelectedConnections.CollectionChanged += OnSelectedConnectionsChanged;
            }

            // El aviso de cables perdidos (y cualquier texto del VM) llega por PropertyChanged: suscrito
            // FUERA del bloque de suscripciones de colecciones para no alterar los fragmentos que las
            // mutaciones del catálogo vigilan literalmente.
            if (_editor is not null)
            {
                _editor.PropertyChanged += OnEditorPropertyChanged;
            }

            Rebuild();
            RebuildDecorators();
            RefreshCanvasNotice();
            RefreshBreadcrumbs();
        }
    }

    /// <summary>Doble clic en el fondo: abre el spotlight en ese punto (el estándar del editor).</summary>
    private void OnCanvasDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_editor is null)
        {
            return;
        }

        var point = e.GetPosition(RootGrid);
        if (CardAt(point) is null && !HitsInteractiveControl(point))
        {
            _editor.OpenSpotlight(GraphPointFromScreen(point));
            e.Handled = true;
        }
    }

    // ── Drag & drop del cajón de herramientas: soltar crea el nodo en el punto del grafo ──

    private void OnCanvasDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.Handled = true;
    }

    private async void OnCanvasDrop(object sender, DragEventArgs e)
    {
        try
        {
            if (_editor is null || !e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                return;
            }

            string typeName = await e.DataView.GetTextAsync();
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return;
            }

            var graphPoint = GraphPointFromScreen(e.GetPosition(RootGrid));
            _editor.AddNode(typeName, graphPoint);
            e.Handled = true;
        }
        catch
        {
            // Un soltado malformado no puede tumbar el lienzo: sin tipo legible, no hay nodo.
        }
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    /// <summary>
    /// Los cables llegan DESPUÉS de los nodos (el importador añade todos los nodos y luego las aristas),
    /// así que el Rebuild del último nodo corrió con la colección de conexiones aún vacía: sin esta
    /// suscripción, un flujo cargado desde disco dibujaba tarjetas pero cero cables.
    /// </summary>
    private void OnConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => DrawWires();

    /// <summary>La marca de los cables cambió (el clic, el Ctrl que añade, el Supr que los borra, el undo).</summary>
    private void OnSelectedConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => DrawWires();

    /// <summary>
    /// Notas y grupos (fase 3.4): AddAnnotation/AddGroup/DeleteAnnotation/DeleteGroup del núcleo
    /// escriben CanvasDecorators; la capa se reconstruye igual que las tarjetas lo hacen con Nodes.
    /// </summary>
    private void OnDecoratorsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildDecorators();

    /// <summary>El aviso de cables perdidos (y cualquier texto del VM) llega por PropertyChanged.</summary>
    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorViewModel.CanvasNotice) or nameof(EditorViewModel.HasCanvasNoticeFixes))
        {
            RefreshCanvasNotice();
        }
        else if (e.PropertyName is nameof(EditorViewModel.IsSpotlightOpen))
        {
            if (_editor?.IsSpotlightOpen is true)
            {
                ShowSpotlight();
            }
            else
            {
                HideSpotlight();
            }
        }
        else if (e.PropertyName is nameof(EditorViewModel.HasBreadcrumbs) or nameof(EditorViewModel.CurrentWorkflowTitle))
        {
            RefreshBreadcrumbs();
        }
        else if (e.PropertyName is nameof(EditorViewModel.FilteredSpotlightItems))
        {
            if (_editor?.IsSpotlightOpen is true)
            {
                SpotlightList.ItemsSource = _editor.FilteredSpotlightItems;
                SpotlightList.SelectedItem = _editor.SelectedSpotlightItem;
            }
        }
    }

    /// <summary>
    /// La superficie de sonda de la fase 3.2: el selfcheck del host (y sólo él) ejercita selección,
    /// borrado y deshacer con valores reales del grafo de ejemplo, sin interacción. El estado queda
    /// RESTAURADO con el propio undo (que así también queda probado). Todo lo que necesita puntero o
    /// foco real (arrastre, rubber band, atajos de teclado) no es ejecutable por esta vía y queda para
    /// la prueba manual del criterio de salida.
    /// </summary>
    internal (int NodesBefore, int NodesAfterDelete, int NodesAfterUndo, bool SelectionStuck, bool GlowContainerExists) ProbeSelectionRoundTrip()
    {
        if (_editor is null || _editor.Nodes.Count == 0)
        {
            return (0, 0, 0, false, false);
        }

        int nodesBefore = _editor.Nodes.Count;
        var target = _editor.Nodes[0];

        // Click selecciona: escribir IsSelected es exactamente lo que hace el lienzo al pinchar.
        target.IsSelected = true;
        bool selectionStuck = target.IsSelected && ReferenceEquals(_editor.SelectedNode, target);
        bool glowContainerExists = _cardsByNode.TryGetValue(target, out var card)
            && _containers.ContainsKey(card);

        // Delete con la selección puesta (el mismo comando que el atajo ejecuta), y undo para restaurar.
        _editor.DeleteSelectedNodesCommand.Execute(null);
        int nodesAfterDelete = _editor.Nodes.Count;

        _editor.UndoRedoService.Undo();
        int nodesAfterUndo = _editor.Nodes.Count;

        // Cura de contaminación entre intentos del sondeo: el undo restaura el nodo CON IsSelected=true
        // (el Delete lo capturó seleccionado). El reintento del sondeo heredaría esa selección y borraría
        // más nodos de los suyos (3 -> 0). Desseleccionar aquí es exactamente el clic en el fondo que el
        // gesto real implica después de soltar el Delete.
        target.IsSelected = false;

        return (nodesBefore, nodesAfterDelete, nodesAfterUndo, selectionStuck, glowContainerExists);
    }

    /// <summary>
    /// La REGLA DE SELECCIÓN del lienzo, medida en la app viva por los MISMOS métodos que los gestos:
    /// <b>pulsar REEMPLAZA</b> (lo de antes se suelta, nodos y cables) y <b>Ctrl AÑADE</b> a lo que ya estaba.
    /// Uno elegido, luego otro → queda UNO; y con Ctrl → quedan los dos. Un cable suelta los nodos (la
    /// selección del lienzo es UNA) y dos cables marcados con Ctrl se borran con **un solo** deshacer.
    /// </summary>
    /// <returns>Las cuatro mitades de la regla y si el borrado del conjunto se deshace de una vez.</returns>
    internal (bool NodeReplaces, bool NodeAdds, bool WireReplaces, bool WireAdds, bool BatchOneUndo, string Detail)
        ProbeSelectionRule()
    {
        if (_editor is null || _editor.Nodes.Count < 2 || _editor.Connections.Count < 2)
        {
            return (false, false, false, false, false, "el grafo de la sonda no tiene dos nodos y dos cables");
        }

        var first = _editor.Nodes[0];
        var second = _editor.Nodes[1];
        var wireA = _editor.Connections[0];
        var wireB = _editor.Connections[1];
        int connectionsBefore = _editor.Connections.Count;

        // Pulsar la segunda SUELTA la primera, y con Ctrl las dos quedan elegidas.
        _editor.SelectNode(first);
        _editor.SelectNode(second);
        bool nodeReplaces = second.IsSelected && !first.IsSelected
            && _editor.Nodes.Count(n => n.IsSelected) == 1;

        _editor.SelectNode(first, add: true);
        bool nodeAdds = first.IsSelected && second.IsSelected;

        // Un cable reemplaza (los nodos se sueltan) y el segundo se AÑADE con Ctrl.
        _editor.SelectConnection(wireA);
        bool wireReplaces = _editor.SelectedConnections.Count == 1 && _editor.Nodes.All(n => !n.IsSelected);

        _editor.SelectConnection(wireB, add: true);
        bool wireAdds = _editor.SelectedConnections.Count == 2
            && WireLayerIsPainting(2);

        // El Supr (la tabla del núcleo) borra el CONJUNTO, y UN deshacer lo devuelve entero.
        bool deleted = EditorKeyboardShortcuts.Execute(EditorKeyboardShortcuts.ShortcutKey.Delete, _editor)
            && _editor.Connections.Count == connectionsBefore - 2;

        _editor.UndoRedoService.Undo();
        bool oneUndo = _editor.Connections.Count == connectionsBefore;

        _editor.ClearSelection();

        string detail = $"nodos: reemplaza={nodeReplaces} añade={nodeAdds} | cables: reemplaza={wireReplaces} "
            + $"añade={wireAdds} | borrado en una sola operación={deleted && oneUndo} "
            + $"(cables {connectionsBefore}->{connectionsBefore - 2}->{_editor.Connections.Count})";

        return (nodeReplaces, nodeAdds, wireReplaces, wireAdds, deleted && oneUndo, detail);
    }

    /// <summary>
    /// Sonda del RECTÁNGULO de selección (hito 286): un rectángulo que encierra TODAS las tarjetas tiene que
    /// elegir sus nodos <b>y marcar sus cables</b> —las dos cosas a la vez, que es lo que lo distingue del clic—,
    /// y el modificador decide lo de FUERA: con Ctrl no se suelta nada, sin Ctrl manda el área (y se suelta
    /// todo). Corre por los MISMOS tres tiempos que el gesto (arrancar, mover, soltar) y con puntos de la RAÍZ,
    /// que es por donde entra el puntero: el cruce de espacios lo mide el propio rectángulo. El estado queda
    /// como al entrar.
    /// </summary>
    internal (int Nodes, int Wires, bool MarksBoth, bool CtrlKeepsOutside, bool PlainReleases, string Detail)
        ProbeRubberBand()
    {
        UpdateLayout();
        ApplyAllNodePositions();

        int nodes = _editor?.Nodes.Count ?? 0;
        int wires = _editor?.Connections.Count ?? 0;
        if (_editor is null || nodes == 0 || wires == 0 || _containers.Count == 0)
        {
            return (nodes, wires, false, false, false, "el grafo de la sonda no tiene tarjetas materializadas y cables");
        }

        var (left, top, right, bottom) = RubberBandAroundEverything();

        // 1. Un rectángulo que encierra el grafo entero: los nodos Y los cables que caen dentro.
        _editor.ClearSelection();
        BeginRubberBand(new Windows.Foundation.Point(left, top), add: false);
        UpdateRubberBand(new Windows.Foundation.Point(right, bottom));
        bool marksBoth = _editor.Nodes.All(n => n.IsSelected)
            && _editor.SelectedConnections.Count == wires;
        EndRubberBand();

        // 2. El mismo rectángulo, lejos de todo: con Ctrl no suelta lo de fuera... Los puntos del gesto son de
        //    la RAÍZ —el espacio del puntero, que es lo que un `Windows.Foundation.Point` es aquí—: el cruce al
        //    espacio del grafo lo hace el propio rectángulo por dentro (GraphPointFromScreen), no esta sonda.
        double awayLeft = right + 600, awayTop = bottom + 600;
        var away = new Windows.Foundation.Point(awayLeft, awayTop);
        var awayEnd = new Windows.Foundation.Point(awayLeft + 300, awayTop + 300);
        BeginRubberBand(away, add: true);
        UpdateRubberBand(awayEnd);
        bool ctrlKeeps = _editor.Nodes.Count(n => n.IsSelected) == nodes
            && _editor.SelectedConnections.Count == wires;
        EndRubberBand();

        // 3. ...y sin Ctrl reemplaza: manda el área, que está vacía, así que suelta todo.
        BeginRubberBand(away, add: false);
        UpdateRubberBand(awayEnd);
        bool plainReleases = _editor.Nodes.All(n => !n.IsSelected)
            && _editor.SelectedConnections.Count == 0
            && _editor.SelectedNode is null;
        EndRubberBand();

        string detail = $"rectángulo ({left:F0},{top:F0})-({right:F0},{bottom:F0}) de la raíz: "
            + $"encierra {nodes} nodos y {wires} cables; con Ctrl lo de fuera se queda={ctrlKeeps}; "
            + $"sin Ctrl suelta todo={plainReleases}";
        return (nodes, wires, marksBoth, ctrlKeeps, plainReleases, detail);
    }

    /// <summary>
    /// Sonda del BORRADO MIXTO (hito 287): una selección con un NODO y un CABLE —justo lo que deja el rectángulo
    /// de selección— se borra entera con Supr y <b>un solo</b> deshacer la devuelve. Antes el Supr se llevaba
    /// los cables y <b>dejaba los nodos</b>, así que esa misma selección había que borrarla en dos tandas (y
    /// deshacerla otras dos). El estado queda como al entrar, por el propio undo — que es lo que se mide.
    /// </summary>
    internal (bool BothGone, bool OneUndoRestoresBoth, string Detail) ProbeMixedDeletion()
    {
        if (_editor is null || _editor.Nodes.Count < 2 || _editor.Connections.Count == 0)
        {
            return (false, false, "el grafo de la sonda no tiene dos nodos y un cable");
        }

        // El ÚLTIMO nodo y su cable: no se toca el que miran las otras sondas y el undo lo devuelve a su sitio.
        var node = _editor.Nodes[^1];
        var wire = _editor.Connections.FirstOrDefault(c => ReferenceEquals(c.Source.NodeOwner, node)
                                                        || ReferenceEquals(c.Target.NodeOwner, node));
        if (wire is null)
        {
            return (false, false, "el último nodo de la sonda no tiene cables");
        }

        int nodes = _editor.Nodes.Count;
        int wires = _editor.Connections.Count;

        _editor.ApplyRubberSelection([node], [wire], add: false, [], []);
        EditorKeyboardShortcuts.Execute(EditorKeyboardShortcuts.ShortcutKey.Delete, _editor);

        bool bothGone = !_editor.Nodes.Contains(node) && !_editor.Connections.Contains(wire)
                     && _editor.Nodes.Count == nodes - 1;

        _editor.UndoRedoService.Undo();

        bool restored = _editor.Nodes.Contains(node) && _editor.Connections.Contains(wire)
                     && _editor.Nodes.Count == nodes && _editor.Connections.Count == wires;

        _editor.ClearSelection();

        string detail = $"{node.Title} + su cable: borrado de golpe={bothGone}; un solo deshacer devuelve el "
            + $"grafo entero={restored} (nodos {nodes}->{nodes - 1}->{_editor.Nodes.Count}, cables {wires}->"
            + $"{wires - 1}->{_editor.Connections.Count})";
        return (bothGone, restored, detail);
    }

    /// <summary>
    /// El rectángulo que encierra el grafo ENTERO, en espacio de la RAÍZ y medido del árbol: el centro dibujado
    /// de cada tarjeta (el mismo que mide la sonda del área de clic) más su media caja escalada, con un margen.
    /// </summary>
    private (double Left, double Top, double Right, double Bottom) RubberBandAroundEverything()
    {
        double left = double.MaxValue, top = double.MaxValue;
        double right = double.MinValue, bottom = double.MinValue;

        foreach (var pair in _containers)
        {
            var centre = TransformToVisualCenter(pair.Value, RootGrid);
            double halfWidth = pair.Key.Width * CanvasTransform.ScaleX / 2;
            double halfHeight = RubberCardHeight * CanvasTransform.ScaleY / 2;

            left = Math.Min(left, centre.X - halfWidth);
            top = Math.Min(top, centre.Y - halfHeight);
            right = Math.Max(right, centre.X + halfWidth);
            bottom = Math.Max(bottom, centre.Y + halfHeight);
        }

        return (left - 20, top - 20, right + 20, bottom + 20);
    }

    /// <summary>¿La capa pinta tantos cables con el trazo del marcado como se piden? Lo comparten la sonda y sus renglones.</summary>
    private bool WireLayerIsPainting(int marked)
    {
        return WireLayer.Children
            .OfType<Microsoft.UI.Xaml.Shapes.Path>()
            .Count(p => AutomationProperties.GetAutomationId(p) == WireAnchor
                     && p.StrokeThickness >= WireSelectedThickness) == marked;
    }

    /// <summary>
    /// El punto de la RAÍZ que espera <see cref="VisualTreeHelper.FindElementsInHostCoordinates"/>: el
    /// «host» de esa API es la raíz del contenido, NO el subárbol del lienzo. Pasarle el punto relativo al
    /// lienzo desplazaba la sonda la posición del control en la ventana —medido en el hito 247 con puntero
    /// real: (280, 41), la columna del cajón y la barra superior— y con ella el área de clic de TODAS las
    /// tarjetas. El cruce se hace en UN solo sitio, como el resto de cruces de puntos del host.
    /// </summary>
    private Windows.Foundation.Point PointInHostSpace(Windows.Foundation.Point canvasPoint)
    {
        var toRoot = RootGrid.TransformToVisual(null);
        return toRoot is null ? canvasPoint : toRoot.TransformPoint(canvasPoint);
    }

    /// <summary>
    /// Sonda del ÁREA DE CLIC (hito 249): para cada tarjeta materializada, el centro de su caja DIBUJADA
    /// —medido del árbol visual, en el espacio del lienzo— entra por el MISMO <see cref="CardAt"/> que usan
    /// los handlers, y tiene que resolver ESA tarjeta. Es el defecto que ni la suite ni las sondas veían,
    /// porque el hit-testing vive en el árbol visual: la sesión con puntero real del hito 247 lo midió como
    /// un desplazamiento del área de clic igual a la posición del lienzo en la ventana, y esta sonda es la
    /// que lo caza sin puntero.
    /// </summary>
    /// <returns>(tarjetas medidas, coincidencias, detalle legible)</returns>
    internal (int Measured, int Matched, string Detail) ProbeHitAreas()
    {
        // Los contenedores pueden estar vacíos si un Rebuild acaba de ocurrir y no ha pasado layout:
        // forzarlo los rellena (la cura que ya usa la sonda de conexión).
        UpdateLayout();
        ApplyAllNodePositions();

        var origin = PointInHostSpace(new Windows.Foundation.Point(0, 0));
        int measured = 0;
        int matched = 0;
        string firstMiss = "";

        foreach (var pair in _containers)
        {
            measured++;

            var centre = TransformToVisualCenter(pair.Value, RootGrid);
            var hit = CardAt(centre);
            if (ReferenceEquals(hit, pair.Key))
            {
                matched++;
                continue;
            }

            if (firstMiss.Length == 0)
            {
                string what = hit is null
                    ? "nada (fondo)"
                    : $"OTRA tarjeta (#{_editor?.Nodes.IndexOf(hit.Node) ?? -1})";
                firstMiss = $"el centro dibujado ({centre.X:F0},{centre.Y:F0}) resolvió {what}; "
                          + $"el lienzo está en ({origin.X:F0},{origin.Y:F0}) de la raíz";
            }
        }

        string detail = measured == 0
            ? "sin tarjetas materializadas"
            : firstMiss.Length > 0
                ? firstMiss
                : $"{matched}/{measured} tarjetas resuelven por su centro dibujado "
                  + $"(el lienzo está en ({origin.X:F0},{origin.Y:F0}) de la raíz)";

        return (measured, matched, detail);
    }

    /// <summary>
    /// Sonda de la fase 3.3: el ciclo COMPLETO de conexión por los mismos métodos que usan los handlers
    /// — anclas write-back reales (calculadas del árbol), StartConnection, FinishConnection vía CreateConnection,
    /// estados de puerto refrescados, desconexión por comando y restauración exacta por undo. El estado del
    /// grafo queda como al entrar (las tres undos devuelven también la conexión que CreateConnection sustituyó).
    /// </summary>
    internal (bool AnchorsReal, bool ConnectedViaCommands, bool StatesRefreshed, bool DisconnectedViaCommand, bool RestoredByUndo) ProbeConnectionRoundTrip()
    {
        if (_editor is null || _editor.Nodes.Count < 2)
        {
            return (false, false, false, false, false);
        }

        // Los contenedores pueden estar vacíos si un Rebuild acaba de ocurrir (p. ej. el undo de la sonda
        // anterior) y no ha pasado layout: forzarlo rellena _containers y hace las anclas reales.
        UpdateLayout();
        ApplyAllNodePositions();

        // Par cualquiera: la primera salida del primer nodo y la primera entrada de otro. CreateConnection
        // SUSTITUYE la conexión previa de la entrada (por diseño del núcleo) y la apila para el undo.
        var output = _editor.Nodes[0].OutputPorts.FirstOrDefault();
        var input = _editor.Nodes.Skip(1).SelectMany(n => n.InputPorts).FirstOrDefault();
        if (output is null || input is null)
        {
            return (false, false, false, false, false);
        }

        var replaced = _editor.Connections.FirstOrDefault(c => c.Target == input);
        int connectionsBefore = _editor.Connections.Count;

        bool anchorsReal = AnchorOf(output) is { } a1 && AnchorOf(input) is { } a2
            && a1 != default && a2 != default && a1 != a2;

        // El ciclo que hacen los handlers: write-back → start → finish (create) → refresh.
        WriteBackAnchors();
        _editor.StartConnectionCommand.Execute(output);
        bool pendingStarted = _editor.PendingConnection?.Source == output;
        _editor.FinishConnectionCommand.Execute(input);
        UpdatePortStatesAndWires();

        bool connected = _editor.Connections.Any(c => c.Source == output && c.Target == input);
        bool statesRefreshed = output.IsConnected && input.IsConnected;

        // Desconexión por comando (el mismo que el click derecho del socket).
        _editor.DisconnectConnectorCommand.Execute(input);
        bool disconnected = !_editor.Connections.Any(c => c.Source == output && c.Target == input);

        // Restauración EXACTA: undo#1 re-añade la conexión de la sonda; undo#2 la quita; undo#3 devuelve
        // la conexión sustituida (si la había). El grafo queda con su pila de undo vacía y sus cables.
        _editor.UndoRedoService.Undo();
        bool restored = _editor.Connections.Any(c => c.Source == output && c.Target == input);
        _editor.UndoRedoService.Undo();
        _editor.UndoRedoService.Undo();
        bool originalBack = replaced is null
            ? _editor.Connections.Count == connectionsBefore
            : _editor.Connections.Contains(replaced);

        _ = pendingStarted; // informativo: el pendiente existió durante el ciclo
        return (anchorsReal, connected && originalBack, statesRefreshed, disconnected, restored);
    }

    /// <summary>
    /// Sonda del GESTO DEL CABLE (hito 278): recorre los tres tiempos del gesto por los MISMOS métodos que
    /// ejecutan los handlers —pulsar un puerto, mover el puntero y soltar— y mide los desenlaces del encargo:
    /// (1) pulsar arranca el cable sin armar el arrastre de la tarjeta, (2) el extremo sigue al puntero, (3)
    /// soltar sobre un destino compatible crea la conexión, y (4) soltar en el vacío o sobre un destino
    /// incompatible CANCELA dejando el estado limpio (sin cable fantasma y sin pendiente colgado).
    ///
    /// <para><b>Por qué aquí y no en la suite</b>: el host Uno es WinUI y no se materializa en la sesión de
    /// pruebas —sus guardias censan la fuente—, y el puntero inyectado entrega pulsaciones pero no movimientos:
    /// medir el gesto necesita las anclas reales y las tarjetas materializadas, que es lo que da la app viva.
    /// El grafo queda como estaba: la conexión que crea el gesto la quita el undo del núcleo, el mismo camino
    /// que usa la sonda del ciclo de conexión.</para>
    /// </summary>
    /// <returns>(arranca sin arrastrar, sigue al puntero, conecta al soltar en compatible, cancela lo que no
    /// vale, detalle legible)</returns>
    internal (bool Started, bool Followed, bool Connected, bool Cancelled, string Detail) ProbeSocketGesture()
    {
        if (_editor is null || _editor.Nodes.Count < 2)
        {
            return (false, false, false, false, "sin editor o con menos de dos nodos: no hay gesto que medir");
        }

        UpdateLayout();
        ApplyAllNodePositions();
        WriteBackAnchors();

        // Un par REAL del flujo cargado: la primera salida del primer nodo y la primera entrada de otro con
        // la que el producto permita conectar. No se inventa el escenario: el gesto se mide sobre el grafo vivo.
        var source = _editor.Nodes[0].OutputPorts.FirstOrDefault();
        var target = source is null
            ? null
            : _editor.Nodes.Skip(1).SelectMany(n => n.InputPorts)
                .FirstOrDefault(p => PortViewModel.CanConnect(source, p));
        if (source is null || target is null)
        {
            return (false, false, false, false, "el flujo cargado no trae un par de puertos conectable");
        }

        if (AnchorOf(source) is not { } sourceAnchor || AnchorOf(target) is not { } targetAnchor)
        {
            return (false, false, false, false, "las anclas de los puertos no se pudieron medir (¿layout pendiente?)");
        }

        var screenSource = ScreenPointOfAnchor(sourceAnchor);
        var screenTarget = ScreenPointOfAnchor(targetAnchor);
        var replaced = _editor.Connections.FirstOrDefault(c => c.Target == target);
        int connectionsBefore = _editor.Connections.Count;

        // 0) ANTES DE NADA: cada tarjeta materializada tiene que estar CABLEADA. El cableado se hacía en
        //    Rebuild —donde el ItemsSource acaba de asignarse y todavía no hay ninguna vista— y buscaba la
        //    vista en el Content del contenedor (que es el ViewModel), así que el evento se quedaba sin
        //    suscriptor y pulsar un puerto no hacía nada: medido con el ratón inyectado sobre la ventana real.
        bool cardsWired = _containers.Count > 0 && _socketWiring.Count == _containers.Count;

        // 1) PULSAR el puerto: el cable arranca atado a su ancla, ya dibujado, y la tarjeta NO se arma.
        BeginSocketGesture(source);
        bool started = cardsWired
            && _editor.PendingConnection?.Source == source
            && _pendingWirePath is not null
            && !WouldArmCardDrag(screenSource);

        // 2) MOVER el puntero: el extremo libre sigue al cursor y el puerto bajo el cursor queda por destino.
        var midpoint = new Windows.Foundation.Point(
            (screenSource.X + screenTarget.X) / 2,
            (screenSource.Y + screenTarget.Y) / 2);
        UpdateSocketGesture(midpoint);
        var midpointGraph = GraphPointFromScreen(midpoint);
        bool followed = _editor.PendingConnection is { } pending
            && _pendingWirePath is not null
            && Math.Abs(pending.TargetLocation.X - midpointGraph.X) < 0.001
            && Math.Abs(pending.TargetLocation.Y - midpointGraph.Y) < 0.001;

        UpdateSocketGesture(screenTarget);
        bool snapped = ReferenceEquals(_pendingHoverPort, target);

        // 3) SOLTAR sobre un destino COMPATIBLE: conecta y deja el estado limpio.
        EndSocketGesture(_pendingHoverPort);
        bool connected = snapped
            && _editor.Connections.Any(c => c.Source == source && c.Target == target)
            && _editor.PendingConnection is null
            && _pendingWirePath is null;
        string connectedWhy = $"destino apuntado={snapped}"
            + $", conexion presente={_editor.Connections.Any(c => c.Source == source && c.Target == target)}"
            + $", pendiente={_editor.PendingConnection is not null}, cable={_pendingWirePath is not null}"
            + $", conexiones {connectionsBefore}->{_editor.Connections.Count}, sustituye={replaced is not null}";

        // El grafo vuelve a como estaba: el undo del núcleo deshace lo que creó el gesto (y devuelve la
        // conexión que sustituyó, si la había).
        _editor.UndoRedoService.Undo();
        if (replaced is not null)
        {
            _editor.UndoRedoService.Undo();
        }

        bool restored = replaced is null
            ? _editor.Connections.Count == connectionsBefore
            : _editor.Connections.Contains(replaced);

        // 4) CANCELAR: soltar en el VACÍO y sobre un destino INCOMPATIBLE (una salida, con el cable saliendo
        //    de una salida) dejan lo mismo — ni conexión, ni pendiente, ni cable en la capa.
        var (cancelledOnEmpty, whyEmpty) = TrySocketGestureEndingAt(new Windows.Foundation.Point(2, 2), source);
        var incompatible = _editor.Nodes.Skip(1).SelectMany(n => n.OutputPorts).FirstOrDefault();
        var (cancelledOnIncompatible, whyIncompatible) = incompatible is not null
            && AnchorOf(incompatible) is { } incompatibleAnchor
            ? TrySocketGestureEndingAt(ScreenPointOfAnchor(incompatibleAnchor), source)
            : (false, "el flujo no trae un segundo nodo con salida");

        string detail = $"origen '{source.NodeOwner.Title}.{source.DisplayName}' -> destino '{target.NodeOwner.Title}.{target.DisplayName}': "
            + $"las tarjetas materializadas estan cableadas ({_socketWiring.Count} de {_containers.Count})={cardsWired}; "
            + $"pulsar arranca el cable y no arma el arrastre de la tarjeta={started}; "
            + $"el extremo sigue al puntero={followed}; "
            + $"soltar sobre el puerto compatible conecta y deja el estado limpio={connected} ({connectedWhy}); "
            + $"el grafo queda restaurado={restored}; "
            + $"soltar en el vacio cancela={cancelledOnEmpty} ({whyEmpty}); "
            + $"soltar sobre un destino incompatible cancela={cancelledOnIncompatible} ({whyIncompatible})";

        return (started, followed, connected && restored, cancelledOnEmpty && cancelledOnIncompatible, detail);
    }

    /// <summary>
    /// Recorre un gesto COMPLETO que debe CANCELAR: pulsar <paramref name="port"/>, mover hasta
    /// <paramref name="screenPoint"/> y soltar donde el movimiento resolvió. Devuelve si no quedó conexión
    /// nueva, ni pendiente, ni cable en la capa —y POR QUÉ, para que el informe del sondeo se lea sin abrir
    /// el código cuando el desenlace no es el esperado.
    /// </summary>
    private (bool Cancelled, string Why) TrySocketGestureEndingAt(
        Windows.Foundation.Point screenPoint, PortViewModel port)
    {
        if (_editor is null)
        {
            return (false, "sin editor");
        }

        int before = _editor.Connections.Count;
        BeginSocketGesture(port);
        UpdateSocketGesture(screenPoint);
        var hover = _pendingHoverPort;
        EndSocketGesture(hover);

        string why = $"destino apuntado={(hover is null ? "nadie" : hover.DisplayName)}"
            + $", pendiente={_editor.PendingConnection is not null}, cable={_pendingWirePath is not null}"
            + $", conexiones {before}->{_editor.Connections.Count}";

        return (hover is null
            && _editor.PendingConnection is null
            && _pendingWirePath is null
            && _editor.Connections.Count == before, why);
    }

    /// <summary>
    /// Sonda de la fase 3.4: edición completa del flujo sin puntero — nota creada y movida (arrastre por
    /// los mismos deltas que el gesto), grupo creado y borrado, spotlight que añade un nodo real en el
    /// punto del grafo, y migas de subflujo navegadas. Todo por los mismos métodos que los handlers.
    /// </summary>
    internal (bool NoteCreatedAndMoved, bool GroupCreatedAndDeleted, bool SpotlightAddedNode, bool BreadcrumbNavigated) ProbeDecoratorsRoundTrip()
    {
        if (_editor is null)
        {
            return (false, false, false, false);
        }

        // La sonda no puede morir en silencio: un crash stowed de WinRT mata el proceso sin pasar por el
        // catch del sondeo, así que cada fase avanza un marcador en un fichero (el último dice dónde).
        void Progress(string stage)
        {
            try
            {
                File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "probe34-progress.txt"), stage + Environment.NewLine);
            }
            catch { }
        }

        Progress("inicio");

        // 1. Nota: crear, comprobar que la capa la pintó, moverla por su Location y borrarla.
        Progress("nota: crear");
        int notesBefore = _editor.Annotations.Count;
        var note = _editor.AddAnnotation(new Sdk.Point(50, 400), "Sonda", "contenido", "#FEF08A");
        bool noteRendered = _decoratorBindings.ContainsKey(note);
        note.Location = new Sdk.Point(120, 420);
        if (_decoratorBindings.TryGetValue(note, out var noteCard)
            && noteCard.Parent is ContentPresenter noteContainer)
        {
            PositionDecorator(noteContainer, note);
        }

        bool noteMoved = note.Location == new Sdk.Point(120, 420);
        _editor.DeleteAnnotation(note);
        bool noteCreatedAndMoved = _editor.Annotations.Count == notesBefore && noteRendered && noteMoved;

        // 2. Grupo: crear y borrar (el undo queda apilado; se limpia al final).
        Progress("grupo: crear");
        int groupsBefore = _editor.Groups.Count;
        var group = _editor.AddGroup(new Sdk.Point(50, 450), "Grupo sonda", 300, 200);
        bool groupRendered = _decoratorBindings.ContainsKey(group);
        _editor.DeleteGroup(group);
        bool groupCreatedAndDeleted = _editor.Groups.Count == groupsBefore && groupRendered;

        // 3. Spotlight: abrir en un punto, seleccionar el primer ítem y confirmar (AddNode real).
        Progress("spotlight: abrir");
        int nodesBefore = _editor.Nodes.Count;
        _editor.OpenSpotlight(new Sdk.Point(700, 400));
        var firstItem = _editor.FilteredSpotlightItems.FirstOrDefault();
        _editor.SelectedSpotlightItem = firstItem;
        Progress("spotlight: confirmar");
        ConfirmSpotlight();
        Progress("spotlight: confirmado");
        var addedNode = _editor.Nodes.LastOrDefault();
        bool spotlightAdded = _editor.Nodes.Count == nodesBefore + 1
            && addedNode is not null
            && addedNode.Location == new Sdk.Point(700, 400);

        // 4. Migas: el nodo añadido no es subflujo, así que la navegación se ejercita con la raíz si la hay
        //    (la lista queda intacta y el comando corre); el añadido se elimina para no dejar rastro.
        bool breadcrumbNavigated = true;
        if (_editor.Breadcrumbs.Count > 1)
        {
            Progress("migas: navegar");
            var target = _editor.Breadcrumbs[0];
            _editor.NavigateToBreadcrumbCommand.Execute(target);
            breadcrumbNavigated = _editor.CurrentWorkflowTitle == target.Name;
        }

        if (addedNode is not null && _editor.Nodes.Contains(addedNode))
        {
            _editor.RemoveNodeWithConnections(addedNode);
        }

        _editor.UndoRedoService.Clear();
        Progress("fin");
        return (noteCreatedAndMoved, groupCreatedAndDeleted, spotlightAdded, breadcrumbNavigated);
    }

    /// <summary>
    /// El pincel de un token Canvas* resuelto desde App.xaml (los tokens únicos de la fase 3.5 viven
    /// ahí; la indexación directa de <c>Resources[key]</c> NO encadena a Application.Resources y
    /// lanzaría KeyNotFound al instanciar el control).
    /// </summary>
    private static Brush CanvasBrush(string key)
    {
        return (Brush)Application.Current.Resources[key];
    }

    /// <summary>
    /// El punto del grafo en el centro del viewport (rebanada 4): es donde el cajón de herramientas
    /// añade con doble clic — el mismo espacio de pantalla que consume el ratón (RootGrid llena el
    /// control y el cursor que el lienzo traduce con <see cref="GraphPointFromScreen"/>).
    /// </summary>
    internal Sdk.Point GraphPointAtViewportCenter()
    {
        return GraphPointFromScreen(new Windows.Foundation.Point(ActualWidth / 2, ActualHeight / 2));
    }

    /// <summary>
    /// Sonda de la fase 3.6 — el rendimiento MEDIDO con el grafo de referencia (40 nodos + 40 cables,
    /// la densidad del banco de ejemplos): (1) construir el grafo completo con materialización de
    /// tarjetas y cables; (2) re-posicionar TODO el grafo (el coste de un frame de arrastre); (3) un
    /// drag real de la selección entera por el mismo camino que el gesto, con su DrawWires por frame.
    /// Umbrales del plan (sin tirones perceptibles): build < 5 s, re-position < 60 ms, frame < 33 ms.
    /// Restauración exacta: undo apilado + pila limpia ygrafo como al entrar.
    /// </summary>
    internal (int NodesBuilt, int WiresDrawn, double BuildMs, double RepositionMs, double DragFrameMs) ProbePerformanceGraph40()
    {
        if (_editor is null)
        {
            return (0, 0, 0, 0, 0);
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var original = _editor.Nodes.ToList();
        var graph = new List<NodeViewModel>();
        try
        {
            // 1. Construir 40 nodos en rejilla 8x5 con cables encadenados (la densidad del banco).
            for (int i = 0; i < 40; i++)
            {
                string typeName = original[i % original.Count].NodeTypeName;
                var added = _editor.AddNode(typeName, new Point(100 + (i % 8) * 320.0, 100 + (i / 8) * 300.0));
                if (added is null)
                {
                    return (graph.Count, 0, 0, 0, 0);
                }

                graph.Add(added);
            }

            clock.Restart();
            int links = 0;
            for (int i = 1; i < graph.Count; i++)
            {
                var source = graph[i - 1].OutputPorts.FirstOrDefault();
                var target = graph[i].InputPorts.FirstOrDefault();
                if (source is null || target is null)
                {
                    continue; // los fuentes no tienen entrada: el encadenado salta al siguiente par válido
                }

                _editor.StartConnectionCommand.Execute(source);
                _editor.FinishConnectionCommand.Execute(target);
                links++;
            }
            double buildMs = clock.Elapsed.TotalMilliseconds;
            int wires = _editor.Connections.Count;
            _ = links;

            // 2. Re-posicionar todo: el coste por frame de arrastrar la selección entera.
            clock.Restart();
            foreach (var node in graph)
            {
                node.Location = new Point(node.Location.X + 10, node.Location.Y + 10);
            }
            ApplyAllNodePositions();
            DrawWires();
            double repositionMs = clock.Elapsed.TotalMilliseconds;

            // 3. Un frame de drag real (el mismo camino que el gesto: Location + Reposition + DrawWires).
            graph[0].IsSelected = true;
            _drag = _editor.Nodes.Where(n => n.IsSelected)
                .Select(n => new DragItem(_cardsByNode[n], n, n.Location))
                .ToList();
            _dragScreenStart = new Windows.Foundation.Point(0, 0);
            clock.Restart();
            foreach (var node in graph)
            {
                node.Location = new Point(node.Location.X + 1, node.Location.Y + 1);
            }
            ApplyAllNodePositions();
            DrawWires();
            double dragFrameMs = clock.Elapsed.TotalMilliseconds;
            _drag = null;

            return (graph.Count, wires, Math.Round(buildMs, 1), Math.Round(repositionMs, 1), Math.Round(dragFrameMs, 1));
        }
        finally
        {
            // Restauración EXPLÍCITA (no por undo): RemoveNodeWithConnections retira cada nodo añadido
            // con sus cables y es determinista entre intentos del sondeo — el undo dependía de qué
            // graba AddNode y dejó nodos huérfanos que contaminaban la sonda 3.2 del intento siguiente.
            foreach (var node in graph.AsEnumerable().Reverse())
            {
                node.IsSelected = false;
                if (_editor.Nodes.Contains(node))
                {
                    _editor.RemoveNodeWithConnections(node);
                }
            }

            _editor.UndoRedoService.Clear();
        }
    }

    /// <summary>
    /// Sonda de la fase 3.5: cambiar el tema por la API del núcleo (SetThemeById) tiene que
    /// re-tematizar el lienzo EN CALIENTE — el fondo del plano y la cara de una tarjeta cambian de
    /// color porque los pinceles republicados por UnoThemeHost llegan a los ThemeResource ya
    /// evaluados del XAML. La vuelta devuelve el tema que estaba ACTIVO al entrar.
    ///
    /// <para><b>El tema de la prueba se elige por ser distinto del activo, y la vuelta devuelve el de la
    /// entrada</b>: medido en el playtest del 255, con el tema GUARDADO del usuario en claro
    /// (<c>pastel_spring</c>, <c>#FFF8FA</c>) esta sonda cantaba dos fallos —el tema de prueba era el mismo
    /// que ya estaba puesto y la «restauración» comparaba contra un <c>dark_fluent</c> fijo que no era el de
    /// la entrada—. La sonda medía su propia suposición (que la aplicación arranca oscura), que era cierta
    /// sólo mientras el arranque IGNORABA el tema guardado: el defecto que la superficie de ajustes mide.</para>
    /// </summary>
    internal (bool BackgroundChanged, bool CardChanged, bool VariantChanged, bool Restored, string Colors) ProbeThemeRepublish()
    {
        if (_editor is null)
        {
            return (false, false, false, false, "sin editor");
        }

        var themeManager = FileFlow.App.Services.ThemeManager.Instance;
        var original = themeManager.ActiveThemeDefinition;

        // El tema de la prueba: el CONTRARIO del que está activo (y el de la entrada, para volver a él).
        bool probeIsDark = !(original?.IsDark ?? themeManager.IsCurrentThemeDark);
        string probeThemeId = probeIsDark ? "dark_fluent" : "light_studio";
        string entryThemeId = original?.Id ?? themeManager.CurrentThemeId;

        try
        {
            Windows.UI.Color ColorOf(Brush? brush) => brush is SolidColorBrush solid
                ? solid.Color
                : default;

            var backgroundBefore = ColorOf(RootGrid.Background);
            var cardBefore = ColorOf(FindFirstCardBodyBrush());

            themeManager.SetThemeById(probeThemeId);

            var backgroundAfter = ColorOf(RootGrid.Background);
            var cardAfter = ColorOf(FindFirstCardBodyBrush());

            bool backgroundChanged = backgroundAfter != backgroundBefore;
            bool cardChanged = cardAfter != cardBefore;

            // La variante publicada por el host llega a este control HEREDADA (ActualTheme).
            bool variantChanged = ActualTheme == (probeIsDark ? ElementTheme.Dark : ElementTheme.Light);

            themeManager.SetThemeById(entryThemeId);
            var backgroundRestored = ColorOf(RootGrid.Background);

            // Los COLORES crudos de la medición: sin ellos, un FALLO de esta sonda dice que algo no cambió
            // y deja sin saber QUÉ valor había (la lección del renglón crudo de la sonda de cables: el
            // detalle es lo que separa «no cambió» de «cambió a lo mismo»).
            static string Hex(Windows.UI.Color color) =>
                $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            string colors = "fondo " + Hex(backgroundBefore) + "->" + Hex(backgroundAfter)
                + " tarjeta " + Hex(cardBefore) + "->" + Hex(cardAfter)
                + " restaurado " + Hex(backgroundRestored) + " contra " + Hex(backgroundBefore);

            return (backgroundChanged, cardChanged, variantChanged,
                backgroundRestored == backgroundBefore, colors);
        }
        finally
        {
            if (original is not null && themeManager.ActiveThemeDefinition?.Id != original.Id)
            {
                themeManager.SetTheme(original);
            }
        }
    }

    /// <summary>El pincel de la cara de la PRIMERA tarjeta (el Border que la pinta), del árbol real.</summary>
    private Brush? FindFirstCardBodyBrush()
    {
        return FindCardBodyBorder(this)?.Background;
    }

    private static Microsoft.UI.Xaml.Controls.Border? FindCardBodyBorder(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Microsoft.UI.Xaml.Controls.Border { Background: SolidColorBrush } border
                && border.CornerRadius.TopLeft > 4)
            {
                // El cuerpo de la tarjeta (CornerRadius 8); los glows (10) no tienen Background.
                return border;
            }

            var found = FindCardBodyBorder(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void Rebuild()
    {
        // Los contenedores de este ItemsSource aún no existen: el primer posicionamiento ocurre en el
        // pase de layout (OnNodesHostLayoutUpdated), cuando la materialización ya ocurrió.
        _positionsPending = true;

        var cards = _editor?.Nodes.Select(node => new NodeCardViewModel(node)).ToList();
        _containers.Clear();
        _cardsByNode.Clear();
        if (cards is not null)
        {
            foreach (var card in cards)
            {
                _cardsByNode[card.Node] = card;
            }
        }

        NodesHost.ItemsSource = cards;
        WireCardEvents();
        DrawWires();
    }

    /// <summary>
    /// Cablea (y descablea) los eventos de socket de las tarjetas <b>materializadas</b>.
    ///
    /// <para><b>Cuándo se llama</b>: el pase de layout que ya tiene contenedores —no <see cref="Rebuild"/>,
    /// donde el <c>ItemsSource</c> acaba de asignarse y aún no hay ninguna vista que enganchar— y cada
    /// reconstrucción, que suelta lo que hubiera antes de que el layout vuelva a materializar (hito 278).</para>
    /// </summary>
    private void WireCardEvents()
    {
        foreach (var entry in _socketWiring)
        {
            entry.View.SocketRequested -= OnCardSocketRequested;
            entry.View.DisconnectRequested -= OnCardDisconnectRequested;
        }

        _socketWiring.Clear();

        // Los eventos viven en la VISTA (NodeCardView), y la vista sólo existe con su contenedor ya
        // materializado: se busca en su ÁRBOL VISUAL, no en su Content (que es el ViewModel — buscarla en el
        // Content era el segundo motivo por el que el evento se quedaba sin suscriptor: la comparación no
        // podía dar true nunca).
        foreach (var pair in _containers)
        {
            if (FirstDescendant<NodeCardView>(pair.Value) is not { } view
                || _socketWiring.Any(entry => ReferenceEquals(entry.View, view)))
            {
                continue;
            }

            view.SocketRequested += OnCardSocketRequested;
            view.DisconnectRequested += OnCardDisconnectRequested;
            _socketWiring.Add((view, OnCardSocketRequested, OnCardDisconnectRequested));
        }
    }

    /// <summary>El primer descendiente del árbol visual del tipo pedido, o <c>null</c> si no hay ninguno.</summary>
    private static T? FirstDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FirstDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private readonly List<(NodeCardView View, EventHandler<PortViewModel> Socket, EventHandler<PortViewModel> Disconnect)> _socketWiring = new();

    /// <summary>
    /// Aplica la posición proyectada de un nodo al Canvas. La lectura pasa por
    /// <see cref="NodeCardViewModel.Position"/>, que viene del <see cref="UnoPointConverter"/> — la
    /// proyección explícita que la guardia de geometría exige en cada enlace equivalente.
    /// </summary>
    private void ApplyNodePosition(ContentPresenter container, NodeCardViewModel card)
    {
        Canvas.SetLeft(container, card.Position.X);
        Canvas.SetTop(container, card.Position.Y);
    }

    /// <summary>Aplica la posición proyectada a cada contenedor materializado; devuelve cuántas aplicó.</summary>
    private int ApplyAllNodePositions()
    {
        int count = Math.Min(VisualTreeHelper.GetChildrenCount(NodesHost), 1);
        if (count == 0)
        {
            return 0;
        }

        if (VisualTreeHelper.GetChild(NodesHost, 0) is not ItemsPresenter presenter)
        {
            return 0;
        }

        return RecurseContainers(presenter);
    }

    private int RecurseContainers(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        int applied = 0;

        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is ContentPresenter container && container.Content is NodeCardViewModel card)
            {
                ApplyNodePosition(container, card);
                _containers[card] = container;
                applied++;
            }
            else
            {
                // La posición es una propiedad adjunta del Canvas: no cambia con el layout interno de la
                // tarjeta, así que no hace falta re-suscribirse a LayoutUpdated por contenedor (además,
                // cada suscripción anónima era una fuga en cada Rebuild).
                applied += RecurseContainers(child);
            }
        }

        return applied;
    }
    // ─────────────────────────────────────────────────────────────────────────────
    // Pan, zoom y encuadre
    // ─────────────────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────────────────
    // Fase 3.2: selección, arrastre de nodos y teclado, sobre el pan/zoom del 3.1.
    // Las CLAVES salen de la tabla compartida del núcleo (EditorKeyboardShortcuts): es la única
    // fuente, y la guardia de atajos compara el uso de los dos hosts contra ella.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Un nodo arrastrado: su tarjeta (para reposicionar el contenedor) y su posición de partida.</summary>
    private sealed record DragItem(NodeCardViewModel Card, NodeViewModel Node, Point GraphStart);

    private List<DragItem>? _drag;
    private Windows.Foundation.Point _dragScreenStart;
    private Windows.Foundation.Point _lastPointerPosition;

    /// <summary>La tecla modificadora Ctrl, portable (la tabla vive en el núcleo).</summary>
    private const EditorKeyboardShortcuts.Modifiers Ctrl = EditorKeyboardShortcuts.Modifiers.Control;

    /// <summary>El punto del grafo bajo un punto de pantalla: el inverso del mapeo compartido (zoom + translate).</summary>
    private Sdk.Point GraphPointFromScreen(Windows.Foundation.Point screenPoint)
    {
        double zoom = CanvasTransform.ScaleX;
        if (zoom <= 0)
        {
            return UnoPointProjection.ToSdk(screenPoint.X, screenPoint.Y);
        }

        return UnoPointProjection.ToSdk(
            (screenPoint.X - CanvasTransform.TranslateX) / zoom,
            (screenPoint.Y - CanvasTransform.TranslateY) / zoom);
    }

    /// <summary>
    /// La tarjeta bajo el puntero, o null si el punto cae en el fondo (el hit-testing de WinUI respeta
    /// IsHitTestVisible y la geometría real del árbol).
    ///
    /// <para><b>El espacio importa</b>: el punto entra en el espacio del LIENZO —el mismo de los handlers
    /// y de las sondas— y el cruce a la raíz lo hace <see cref="PointInHostSpace"/>, porque la API de
    /// hit-testing espera el de la RAÍZ. Sin ese cruce el área de clic caía desplazada la posición del
    /// lienzo en la ventana: es el defecto que la sesión con puntero real midió en el hito 247 y el que
    /// atrapa la sonda <see cref="ProbeHitAreas"/>.</para>
    /// </summary>
    private NodeCardViewModel? CardAt(Windows.Foundation.Point position)
    {
        return VisualTreeHelper.FindElementsInHostCoordinates(PointInHostSpace(position), this)
            .OfType<NodeCardView>()
            .FirstOrDefault()?.DataContext as NodeCardViewModel;
    }

    /// <summary>¿El punto cae sobre un control interactivo (la barra de zoom)? Ahí ni arrastre ni pan.
    /// El punto viene en el espacio del lienzo y cruza a la raíz igual que <see cref="CardAt"/>.</summary>
    private bool HitsInteractiveControl(Windows.Foundation.Point position)
    {
        return VisualTreeHelper.FindElementsInHostCoordinates(PointInHostSpace(position), this).OfType<Button>().Any();
    }

    /// <summary>
    /// Entrega el foco al LIENZO — al control con <c>IsTabStop</c>, que es el dueño de
    /// <see cref="OnKeyDown"/> — tal y como lo hace el clic del puntero.
    ///
    /// <para><b>Por qué no vale enfocar «el elemento del handler»</b>: el hito 250 midió con puntero real
    /// que <c>Ctrl+Z</c>, <c>Ctrl+Y</c> y <c>Supr</c> no llegaban al lienzo aunque la app fuera el primer
    /// plano. La causa estaba aquí: se enfocaba <c>RootGrid</c> —un <c>Grid</c>, que NO es focusable— y el
    /// valor de retorno se descartaba, así que el foco se quedaba donde estuviera. En la sesión UIA del
    /// 238 los atajos sí funcionaban porque el <c>set_focus</c> externo enfoca el control: ese es el
    /// elemento que hay que enfocar también con el puntero.</para>
    ///
    /// <para><b>El único dueño del teclado que se respeta es un cuadro de texto</b> (el buscador del
    /// spotlight, la caja de renombrado): ahí el teclado es del cuadro, y es la misma cortesía que ya
    /// aplica <see cref="OnKeyDown"/> al ignorar sus teclas. Los botones NO entran en esa lista aunque el
    /// punto caiga sobre uno: las tarjetas traen los suyos (los toggles de la cabecera, el Ejecutar) y
    /// excluir «punto sobre control» dejaba sin foco justo el gesto que importa — clicar la cara de una
    /// tarjeta. Lo midió la sonda <see cref="ProbePointerFocus"/> en este hito: la primera versión de
    /// este helper declinaba el foco ahí y el teclado seguía sin llegar al lienzo con puntero real.</para>
    /// </summary>
    /// <param name="source">El origen del puntero, para saber si el clic era de un cuadro de texto.</param>
    /// <returns>¿El foco quedó en el lienzo?</returns>
    internal bool FocusCanvasForShortcuts(DependencyObject? source = null)
    {
        if (IsTextInput(source))
        {
            return false;
        }

        return this.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Un clic en el lienzo toma el teclado y lo declara suyo durante una ventana corta (hito 253).
    ///
    /// <para><b>Por qué una ventana y no un estado</b>: el foco se pierde DESPUÉS del gesto. El rastro con
    /// puntero real mide que el clic deja el foco en el lienzo y que, 78–141 ms más tarde, un envoltorio de
    /// scroll de la plantilla de ventana del framework (sin nombre, sin DataContext, del tamaño del área de
    /// contenido) se lo lleva; el robo ocurre clicando el fondo (`src=Grid`) igual que una tarjeta
    /// (`src=Border`), así que no es de la selección. La ventana acota la reclamación al gesto que la
    /// justifica: pasados unos cientos de milisegundos, si otro se lleva el teclado por su cuenta, el lienzo
    /// no discute.</para>
    /// </summary>
    private void BeginKeyboardOwnership()
    {
        _keyboardOwnedUntil = Environment.TickCount64 + KeyboardOwnershipMs;
    }

    /// <summary>Cuánto dura la propiedad del teclado declarada por un clic (ms).</summary>
    private const long KeyboardOwnershipMs = 700;

    /// <summary>
    /// Recupera el teclado si el framework se lo llevó justo después del clic, y sólo entonces. Las cuatro
    /// guardias están en orden de importancia: (1) la ventana de propiedad tiene que estar viva —sin clic
    /// reciente esto no es un robo, es un cambio de dueño—; (2) un cuadro de texto manda en su teclado;
    /// (3) un elemento DENTRO del lienzo no necesita reclamación (las teclas ya le llegan por burbujeo); y
    /// (4) los paneles del editor (cajón, inspector) tampoco: si el usuario acaba de clicar ahí, el teclado
    /// es suyo. Lo que queda —un elemento ajeno, sin texto, fuera del lienzo y de los paneles— es el caso
    /// medido, y ahí el lienzo vuelve a quedarse con el teclado.
    /// </summary>
    /// <returns>¿Reclamó el teclado?</returns>
    private bool ReclaimKeyboardIfStolenByFramework()
    {
        if (Environment.TickCount64 > _keyboardOwnedUntil)
        {
            return false;
        }

        if (HoldsFocus())
        {
            return false;
        }

        var owner = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        if (IsTextInput(owner) || IsInsideSelf(owner) || IsInsideEditorPanel(owner))
        {
            return false;
        }

        if (!FocusCanvasForShortcuts())
        {
            return false;
        }

        CanvasFocusTrace.Write($"foco RECUPERADO del envoltorio ajeno ({CanvasFocusTrace.Describe(owner, 2)})");
        return true;
    }

    /// <summary>Hasta cuándo el teclado es del lienzo por un clic (ms monótonos).</summary>
    private long _keyboardOwnedUntil;

    /// <summary>¿El elemento es este lienzo o algo suyo (dentro del subárbol del control)?</summary>
    private bool IsInsideSelf(DependencyObject? node) => IsInside(node, this);

    /// <summary>¿El elemento vive dentro de los paneles del editor (cajón o inspector)?</summary>
    private static bool IsInsideEditorPanel(DependencyObject? node) =>
        IsInside(node, null, typeof(NodeToolboxPanel), typeof(NodeInspectorPanel));

    /// <summary>
    /// ¿El elemento desciende de <paramref name="ancestor"/> (visual o lógicamente) o de alguno de los tipos
    /// indicados? Se camina por las DOS vías porque un elemento recién creado puede no estar aún en el árbol
    /// visual pero sí tener padre lógico —y el ladrón medido es justamente de ese tipo—.
    /// </summary>
    private static bool IsInside(
        DependencyObject? node,
        DependencyObject? ancestor = null,
        params Type[] ancestorTypes)
    {
        DependencyObject? current = node;
        int guard = 0;
        while (current is not null && guard++ < 64)
        {
            if (ReferenceEquals(current, ancestor) || ancestorTypes.Contains(current.GetType()))
            {
                return true;
            }

            current = current is UIElement element
                ? VisualTreeHelper.GetParent(element) ?? (current as FrameworkElement)?.Parent
                : (current as FrameworkElement)?.Parent;
        }

        return false;
    }

    /// <summary>¿El origen del puntero está dentro de un cuadro de texto (el teclado es del cuadro)?</summary>
    private static bool IsTextInput(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is TextBox)
            {
                return true;
            }

            current = current is UIElement element ? VisualTreeHelper.GetParent(element) : null;
        }

        return false;
    }

    /// <summary>
    /// Sonda del SEGUIMIENTO de los cables (hito 254): el extremo dibujado del cable tiene que TOCAR el
    /// socket dibujado, medido los dos en el espacio de la raíz —con el pan y el zoom ya dentro, que es lo
    /// que el usuario ve—, y tiene que seguir tocándolo después de mover el plano y de cambiar el zoom.
    ///
    /// <para>Se mide en la RAÍZ a propósito: comparar las dos cosas en espacio de grafo daría 0 aunque el
    /// dibujo estuviera desplazado, porque el error de un espacio mal cruzado se cancela cuando los dos
    /// lados se miden en el mismo sitio equivocado. Es el defecto que el usuario reportó: mover o ajustar el
    /// zoom desplazaba los cables fuera de su socket.</para>
    /// </summary>
    /// <returns>(toca antes del gesto, toca después, la forma aguanta el hueco estrecho, detalle)</returns>
    internal (bool Before, bool After, bool Crowded, string Detail) ProbeWireTracking()
    {
        if (_editor is null || _editor.Connections.Count == 0)
        {
            return (true, true, true, "sin cables que medir (no hay conexiones en el grafo)");
        }

        double before = WireToSocketDistance(0, out string beforeDetail);

        // Los gestos, por los MISMOS mandos que usan los handlers y los botones: el pan del arrastre y el zoom
        // de la rueda. Separados a propósito: el usuario reportó los dos, y hay que saber cuál descuadra.
        double savedTx = CanvasTransform.TranslateX;
        double savedTy = CanvasTransform.TranslateY;
        double savedScale = CanvasTransform.ScaleX;
        CanvasTransform.TranslateX += 140;
        CanvasTransform.TranslateY += 90;
        double afterPan = WireToSocketDistance(0, out string panDetail);
        ZoomBy(1.25);
        double afterZoom = WireToSocketDistance(0, out string zoomDetail);

        CanvasTransform.TranslateX = savedTx;
        CanvasTransform.TranslateY = savedTy;
        CanvasTransform.ScaleX = savedScale;
        CanvasTransform.ScaleY = savedScale;
        ZoomText.Text = $"{Math.Round(savedScale * 100)} %";
        DrawWires();

        const double tolerance = 1.5;
        bool beforeOk = before <= tolerance;
        bool afterOk = afterPan <= tolerance && afterZoom <= tolerance;
        bool crowdedOk = CrowdedShapeFitsTheHueco(out string crowdedDetail);
        string detail = $"antes {before:F1} px ({beforeDetail}); tras pan (+140,+90) {afterPan:F1} px ({panDetail}); "
                      + $"tras zoom x1,25 {afterZoom:F1} px ({zoomDetail}); hueco estrecho {crowdedDetail}";

        return (beforeOk, afterOk, crowdedOk, detail);
    }

    /// <summary>
    /// La FORMA del cable en el hueco estrecho: el caso que el usuario vio como «la parte recta es demasiado
    /// grande y se ve mal» al arrastrar una tarjeta hasta dejarla cerca de otra. La regla compartida
    /// (<see cref="ConnectionGeometry.BezierControlPoints"/>) tiene que dejar el cuello DENTRO del hueco: ni
    /// la retirada de las puntas ni los puntos de control pueden salirse, porque salirse es doblar la curva
    /// hacia atrás y dibujar el rulo con forma de «2».
    ///
    /// <para>Se mide la regla y no un arrastre de verdad a propósito: el sondeo corre en un único callback del
    /// hilo de UI y no hay pase de layout entre mover la tarjeta y medir, así que simular el arrastre mediría
    /// posiciones viejas. Lo que la app dibuja con la regla ya lo miden las otras dos mitades de esta sonda
    /// (los extremos contra los sockets reales).</para>
    /// </summary>
    private static bool CrowdedShapeFitsTheHueco(out string detail)
    {
        // Las anclas del caso reportado: el hueco horizontal se queda en 70 px mientras la tarjeta arrastrada
        // baja 120 px. El cable tiene que nacer en las anclas y quedarse dentro del hueco.
        const double hueco = 70;
        var crowded = ConnectionGeometry.BuildWire(new Sdk.Point(0, 0), new Sdk.Point(hueco, 120));

        double exit = crowded.Exit.X;
        double arrival = crowded.Arrival.X;
        bool fits = exit <= hueco + 0.001
            && arrival >= -0.001
            && crowded.Trace[0] == new Sdk.Point(0, 0)
            && crowded.Trace[^1] == new Sdk.Point(hueco, 120);

        detail = $"cuello {exit:F1} y {arrival:F1} en un hueco de {hueco:F0}"
               + (fits ? " — nace y muere en las anclas, sin salirse" : " — SE SALE del hueco (el rulo)");
        return fits;
    }

    /// <summary>
    /// La distancia, en el espacio de la RAÍZ, entre los dos extremos dibujados del cable
    /// <paramref name="index"/> y los centros dibujados de sus sockets (el mayor de los dos extremos).
    /// </summary>
    private double WireToSocketDistance(int index, out string detail)
    {
        detail = "sin poder medir";
        if (_editor is null || index >= _editor.Connections.Count || index >= WireLayer.Children.Count)
        {
            return 0;
        }

        if (WireLayer.Children[index] is not Microsoft.UI.Xaml.Shapes.Path path
            || path.Data is not PathGeometry geometry
            || geometry.Figures.Count == 0
            || !TryFigureEnds(geometry.Figures[0], out var rawStart, out var rawEnd, out bool hasBezier)
            || !hasBezier)
        {
            detail = "el cable dibujado no tiene geometría de Bézier";
            return 0;
        }

        var connection = _editor.Connections[index];
        var source = SocketElementOf(connection.Source);
        var target = SocketElementOf(connection.Target);
        if (source is null || target is null || path.TransformToVisual(RootGrid) is not { } wireTransform)
        {
            detail = "sin socket o sin transformación al árbol";
            return 0;
        }

        var drawnStart = wireTransform.TransformPoint(rawStart);
        var drawnEnd = wireTransform.TransformPoint(rawEnd);
        var socketStart = TransformToVisualCenter(source, RootGrid);
        var socketEnd = TransformToVisualCenter(target, RootGrid);

        // La geometría vive en el espacio del PLANO (el cable no lleva transform propia): sus puntos crudos
        // son espacio de grafo, igual que lo que devuelve AnchorOf. Comparar los dos dice si el cable se
        // dibujó con las anclas vivas o con otra cosa.
        var anchorSource = AnchorOf(connection.Source);
        var anchorTarget = AnchorOf(connection.Target);

        double startGap = Distance(drawnStart, socketStart);
        double endGap = Distance(drawnEnd, socketEnd);
        detail = $"inicio {startGap:F1} px, fin {endGap:F1} px"
               + $" | plano T=({CanvasTransform.TranslateX:F0},{CanvasTransform.TranslateY:F0}) S={CanvasTransform.ScaleX:F2}"
               + $" | '{connection.Source.NodeOwner.Title}'.{connection.Source.Name} -> '{connection.Target.NodeOwner.Title}'.{connection.Target.Name}"
               + $" | cable(grafo) {rawStart.X:F0},{rawStart.Y:F0}->{rawEnd.X:F0},{rawEnd.Y:F0}"
               + $" | ancla(grafo) {Fmt(anchorSource)}->{Fmt(anchorTarget)}"
               + $" | cable(raiz) {drawnStart.X:F0},{drawnStart.Y:F0}->{drawnEnd.X:F0},{drawnEnd.Y:F0}"
               + $" | socket(raiz) {socketStart.X:F0},{socketStart.Y:F0}->{socketEnd.X:F0},{socketEnd.Y:F0}"
               + $" | tarjeta(grafo) {ContainerPosition(connection.Source.NodeOwner)}->{ContainerPosition(connection.Target.NodeOwner)}"
               + $" | tamSocket {source.ActualWidth:F1}x{source.ActualHeight:F1}";
        return Math.Max(startGap, endGap);
    }

    /// <summary>El elemento del socket de un puerto, o null si su tarjeta no está materializada.</summary>
    private FrameworkElement? SocketElementOf(PortViewModel port) =>
        _cardsByNode.TryGetValue(port.NodeOwner, out var card) && _containers.TryGetValue(card, out var container)
            ? FindSocketElement(container, port)
            : null;

    /// <summary>Dónde tiene el lienzo la tarjeta de ese nodo (espacio de grafo), para el detalle de la sonda.</summary>
    private string ContainerPosition(NodeViewModel node) =>
        _cardsByNode.TryGetValue(node, out var card) && _containers.TryGetValue(card, out var container)
            ? $"{Canvas.GetLeft(container):F1},{Canvas.GetTop(container):F1}"
            : "(sin tarjeta)";

    /// <summary>Un punto del SDK en texto compacto, para el detalle de las sondas.</summary>
    private static string Fmt(Sdk.Point? point) =>
        point is { } p ? $"{p.X:F0},{p.Y:F0}" : "(sin ancla)";

    /// <summary>
    /// Los dos extremos de la figura del cable: dónde la abre y dónde la cierra el trazo <b>entero</b>. Se
    /// leen del primer y del último segmento en vez de asumir que el primero es la Bézier, porque la figura
    /// es la de Nodify —ancla, tramo recto, curva, tramo recto, ancla— y el extremo dibujado es la ÚLTIMA
    /// parada, no el último punto de control.
    /// </summary>
    private static bool TryFigureEnds(
        PathFigure figure,
        out Windows.Foundation.Point start,
        out Windows.Foundation.Point end,
        out bool hasBezier)
    {
        start = figure.StartPoint;
        end = figure.StartPoint;
        hasBezier = false;

        if (figure.Segments.Count == 0)
        {
            return false;
        }

        foreach (var segment in figure.Segments)
        {
            switch (segment)
            {
                case LineSegment line:
                    end = line.Point;
                    break;
                case BezierSegment bezier:
                    hasBezier = true;
                    end = bezier.Point3;
                    break;
                default:
                    // Un segmento que no es de estos dos tipos dejaría la medida a medias: mejor declararlo.
                    return false;
            }
        }

        return true;
    }

    /// <summary>Distancia entre dos puntos (en el mismo espacio).</summary>
    private static double Distance(Windows.Foundation.Point a, Windows.Foundation.Point b) =>
        Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    /// <summary>
    /// Sonda de la RECLAMACIÓN del teclado (hito 253): con la ventana de propiedad de un clic abierta —lo
    /// que arma <see cref="BeginKeyboardOwnership"/> en el gesto—, si un elemento AJENO al editor se lleva
    /// el foco el lienzo lo recupera; y NO lo hace cuando el nuevo dueño es un cuadro de texto (manda en su
    /// teclado), algo DENTRO del lienzo (las teclas ya le llegan) o un panel del editor (el usuario acaba de
    /// clicar ahí). Los cuatro casos entran por el MISMO camino que el gesto real.
    ///
    /// <para>El puntero no se puede inyectar en este entorno: lo que el rastro mide con dedos de verdad es
    /// que el envoltorio de la plantilla de ventana se lleva el foco 78–141 ms después del clic; aquí se
    /// simula ese robo con elementos reales de la ventana, que es lo que este entorno sí puede hacer.</para>
    /// </summary>
    /// <returns>(reclama del ajeno, respeta a los otros dueños, detalle)</returns>
    internal (bool Reclaimed, bool RespectsOwners, string Detail) ProbeKeyboardReclaim()
    {
        var root = WalkToWindowRoot(RootGrid);

        // Los objetivos son elementos REALES de la ventana, y se evita el cuadro de texto donde no se mide la
        // cortesía específica del cuadro: cada caso debe probar UNA razón de declinar, no dos a la vez.
        var foreign = FirstFocusable(root, this, c => !IsInsideEditorPanel(c) && c is not TextBox);
        var insideCanvas = FirstButton(RootGrid);
        var insidePanel = FirstFocusable(root, this, c => IsInsideEditorPanel(c) && c is not TextBox);
        var textInput = FirstTextInput(root);
        var failures = new List<string>();

        bool Case(string what, DependencyObject? thief, bool expectReclaim)
        {
            if (thief is null)
            {
                // Sin objetivo no hay caso: se declara en el detalle en vez de darlo por bueno.
                return true;
            }

            if (!FocusCanvasForShortcuts() || !HoldsFocus())
            {
                failures.Add($"no se pudo entregar el foco al lienzo para '{what}'");
                return false;
            }

            // El clic que justifica la reclamación...
            BeginKeyboardOwnership();

            if (thief is not Control control || !control.Focus(FocusState.Programmatic))
            {
                failures.Add($"no se pudo simular el robo por '{what}'");
                return false;
            }

            bool reclaimed = ReclaimKeyboardIfStolenByFramework();
            if (reclaimed != expectReclaim)
            {
                failures.Add(what);
                return false;
            }

            if (expectReclaim && !HoldsFocus())
            {
                failures.Add($"el lienzo dijo reclamar por '{what}' pero el foco no es suyo");
                return false;
            }

            if (!expectReclaim && HoldsFocus())
            {
                failures.Add($"el lienzo se llevó el teclado de '{what}'");
                return false;
            }

            return true;
        }

        bool reclaimed = Case("un elemento ajeno al editor (la barra de estado)", foreign, expectReclaim: true);
        bool respectsText = Case("un cuadro de texto", textInput, expectReclaim: false);
        bool respectsPanels = Case("un control de un panel del editor", insidePanel, expectReclaim: false);
        bool respectsCanvas = Case("un control dentro del lienzo", insideCanvas, expectReclaim: false);

        // Los objetivos van SIEMPRE en el detalle: sin ellos, un caso declarado «sin objetivo» se leería como
        // medido —y una sonda que no dice con qué midió es una sonda que miente por omisión—.
        string targets = $"objetivos: ajeno={DescribeTarget(foreign)}, cuadro={DescribeTarget(textInput)}, "
                       + $"panel={DescribeTarget(insidePanel)}, lienzo={DescribeTarget(insideCanvas)}";
        string detail = failures.Count == 0
            ? "con la propiedad del clic abierta, el lienzo recupera el teclado de un dueño ajeno y lo respeta "
              + $"de un cuadro de texto, de un control suyo y de un panel del editor ({targets})"
            : $"la reclamación del teclado falló en {failures.Count} caso(s): {string.Join("; ", failures)} ({targets})";

        return (reclaimed, respectsText && respectsPanels && respectsCanvas, detail);
    }

    /// <summary>El tipo y el nombre de un objetivo de la sonda, para que el renglón diga con qué midió.</summary>
    private static string DescribeTarget(DependencyObject? node) =>
        node is FrameworkElement element ? $"{element.GetType().Name}#{element.Name}" : "nadie";

    /// <summary>Sube hasta la raíz de la ventana (visual primero, lógica después) para explorar hermanos.</summary>
    private static DependencyObject WalkToWindowRoot(DependencyObject node)
    {
        DependencyObject current = node;
        int guard = 0;
        while (guard++ < 64)
        {
            var parent = current is UIElement element
                ? VisualTreeHelper.GetParent(element) ?? (current as FrameworkElement)?.Parent
                : (current as FrameworkElement)?.Parent;

            if (parent is null)
            {
                return current;
            }

            current = parent;
        }

        return current;
    }

    /// <summary>
    /// El primer elemento focusable de la ventana que cumpla <paramref name="accept"/>, saltando el subárbol
    /// <paramref name="excluded"/> (el lienzo). El recorrido BAJA por todos los contenedores —podar por
    /// «bando» dejaría sin visitar los paneles, que cuelgan de una reja que no es de ningún panel— y el
    /// filtro decide qué candidato vale.
    /// </summary>
    private static DependencyObject? FirstFocusable(
        DependencyObject root, DependencyObject excluded, Func<DependencyObject, bool> accept)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is not DependencyObject child
                || ReferenceEquals(child, excluded))
            {
                continue;
            }

            if (accept(child) && IsFocusable(child))
            {
                return child;
            }

            if (FirstFocusable(child, excluded, accept) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>El primer cuadro de texto de la ventana (el dueño legítimo del teclado más delicado).</summary>
    private static DependencyObject? FirstTextInput(DependencyObject root)
    {
        if (root is TextBox)
        {
            return root;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child
                && FirstTextInput(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>¿El elemento puede recibir el foco por programa?</summary>
    private static bool IsFocusable(DependencyObject node) =>
        node is Control control && control.IsTabStop && control.IsEnabled;

    /// <summary>
    /// Sonda del FOCO del puntero (hito 252): el mismo camino que ejecuta un clic real del lienzo tiene
    /// que dejar el foco en el CONTROL —el que sostiene <see cref="OnKeyDown"/>— en los TRES sitios donde
    /// el usuario clica (el fondo, la cara de una tarjeta y la barra de zoom), y no robarle el suyo a un
    /// cuadro de texto. Es el defecto que el 250 midió con dedos de verdad; aquí se mide sin puntero, que
    /// es lo que este entorno puede hacer.
    ///
    /// <para>La cara de la tarjeta se mide a propósito: la primera versión del helper declinaba el foco
    /// cuando el punto caía sobre un <c>Button</c> —y las tarjetas traen los suyos—, así que el clic del
    /// 250 seguía dejando el teclado sin destinatario. La sonda lo caza porque pasa por el mismo camino.</para>
    /// </summary>
    /// <returns>(foco entregado al lienzo, los otros dueños del teclado conservan el suyo, detalle)</returns>
    internal (bool FocusDelivered, bool RespectsOtherOwners, string Detail) ProbePointerFocus()
    {
        var other = FirstButton(RootGrid);
        var card = _containers.Values.FirstOrDefault();
        var failures = new List<string>();
        bool movedAway = false;

        // Cada caso mide la TRANSICIÓN (el selfcheck corre con el lienzo ya enfocado en otras sondas):
        // se le da el foco a otro control focusable del lienzo y se comprueba dónde acaba el clic.
        bool Delivers(string what, DependencyObject? source)
        {
            if (other is not null)
            {
                movedAway |= other.Focus(FocusState.Programmatic);
            }

            if (FocusCanvasForShortcuts(source) && HoldsFocus())
            {
                return true;
            }

            failures.Add(what);
            return false;
        }

        bool background = Delivers("el fondo del lienzo no deja el foco en el lienzo", null);
        bool cardFace = card is null || Delivers("la cara de una tarjeta no deja el foco en el lienzo", card);
        bool zoomBar = other is null || Delivers("la barra de zoom no deja el foco en el lienzo", other);

        // La cortesía: un clic en un cuadro de texto no se lleva su teclado (el cuadro se mide con una
        // instancia: la decisión depende del TIPO del origen, no de una caja concreta del árbol).
        bool textInputDeclined = !FocusCanvasForShortcuts(new TextBox());

        // Diagnóstico de la causa que este hito cazó: la cara de la tarjeta tiene botones PROPIOS (los
        // toggles de su cabecera, el Ejecutar), así que la regla «punto sobre control interactivo no se
        // enfoca» declinaba justo el gesto del 250. Se mide para que el renglón lo cante.
        string cardDiagnostic = card is null
            ? "; sin tarjetas materializadas"
            : $"; botón bajo la cara de la tarjeta={HitsInteractiveControl(TransformToVisualCenter(card, RootGrid))}";

        bool delivered = background && cardFace && zoomBar;
        string detail;
        if (!delivered)
        {
            detail = $"el clic del puntero NO deja el foco en el lienzo en {failures.Count} de 3 sitios "
                   + $"({string.Join("; ", failures)})";
        }
        else if (!textInputDeclined)
        {
            detail = "el foco llega al lienzo pero se lleva por delante el teclado de un cuadro de texto";
        }
        else
        {
            detail = $"el foco del puntero queda en el lienzo clicando el fondo, la cara de una tarjeta o "
                   + $"la barra de zoom (transición medida: movedAway={movedAway}) y el cuadro de texto "
                   + "conserva el suyo" + cardDiagnostic;
        }

        return (delivered, textInputDeclined, detail);
    }

    /// <summary>¿El FOCO está en el lienzo (el control que sostiene <see cref="OnKeyDown"/>)?</summary>
    /// <remarks>Se pregunta al gestor de foco por el elemento enfocado: los dos hosts comparten la
    /// semántica «el foco del lienzo es el de su control», y el atajo sólo llega si es así.</remarks>
    private bool HoldsFocus() => ReferenceEquals(
        Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot), this);

    /// <summary>El primer <see cref="Button"/> del subárbol (la barra de zoom), o null si no hay ninguno.</summary>
    private static Button? FirstButton(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is DependencyObject child)
            {
                if (child is Button button)
                {
                    return button;
                }

                if (FirstButton(child) is { } nested)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private void OnCanvasPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RootGrid).Position;
        var properties = e.GetCurrentPoint(RootGrid).Properties;

        // El FOCO del lienzo lo entrega el puntero (hito 252): los atajos viven en OnKeyDown y sin foco no
        // llega ninguno — lo midió la sesión humana del 250 (Ctrl+Z, Ctrl+Y y Supr sin efecto con la app
        // en primer plano). Antes se enfocaba aquí el elemento del handler (`RootGrid`, un Grid) que no es
        // focusable; ahora se enfoca el control, que es quien sostiene el handler del teclado.
        bool focused = FocusCanvasForShortcuts(e.OriginalSource as DependencyObject);
        if (focused)
        {
            // El clic declara suyo el teclado una ventana corta: el framework se lo lleva ~100 ms después.
            BeginKeyboardOwnership();
        }

        CanvasFocusTrace.Write($"press src={e.OriginalSource?.GetType().Name ?? "nadie"} "
                             + $"punto=({point.X:F0},{point.Y:F0}) foco={focused} enfocado={DescribeFocused()}");
        CanvasFocusTrace.Write("press CADENA: " + DescribeChain(e.OriginalSource));

        // 1. Selección y arrastre: botón izquierdo sobre una tarjeta — SALVO que un GESTO DE PUERTO tenga la
        //    pulsación (hito 278): el cable en la mano no puede arrastrar la tarjeta que lo arrancó. El socket
        //    consume su propia pulsación, pero eso no basta: su fila cae dentro de la caja de la tarjeta, así
        //    que cualquier hueco del gesto acababa moviendo el nodo (lo que reportó el usuario).
        if (properties.IsLeftButtonPressed && WouldArmCardDrag(point))
        {
            var card = CardAt(point);
            if (card is not null)
            {
                // La REGLA DE SELECCIÓN, decidida en el núcleo: pulsar REEMPLAZA (los demás se sueltan, y
                // también los cables marcados) y Ctrl AÑADE a lo que ya estaba. El modificador lo lee aquí
                // porque es del teclado; el estado lo gobierna el núcleo.
                _editor?.SelectNode(card.Node, add: IsKeyDown(Windows.System.VirtualKey.Control));

                // Arrastra la selección entera: el undo del escritorio mueve el bloque con
                // MoveNodesAction, y aquí se registra igual al soltar.
                _drag = _editor?.Nodes.Where(n => n.IsSelected)
                    .Select(n => new DragItem(_cardsByNode[n], n, n.Location))
                    .ToList();
                _dragScreenStart = point;
                ((FrameworkElement)sender).CapturePointer(e.Pointer);
                return;
            }
        }

        // 2. Rubber band (botón izquierdo en el fondo): selección por rectángulo, como el escritorio. Marca
        //    los NODOS y los CABLES que caen dentro con la MISMA regla que el clic —sin Ctrl REEMPLAZA (lo
        //    elegido se suelta al empezar) y con Ctrl AÑADE—: el modificador se lee aquí, al pulsar.
        if (properties.IsLeftButtonPressed && !HitsInteractiveControl(point))
        {
            BeginRubberBand(point, add: IsKeyDown(Windows.System.VirtualKey.Control));
            ((FrameworkElement)sender).CapturePointer(e.Pointer);
            return;
        }

        // 3. Pan (fondo del lienzo, botón derecho): nunca sobre tarjetas ni controles.
        if (properties.IsRightButtonPressed && !HitsInteractiveControl(point))
        {
            _isPanning = true;
            _panStart = e.GetCurrentPoint(CanvasPlane).Position;
            ((FrameworkElement)sender).CapturePointer(e.Pointer);
        }
    }

    private void OnCanvasMoved(object sender, PointerRoutedEventArgs e)
    {
        _lastPointerPosition = e.GetCurrentPoint(RootGrid).Position;

        if (_isRubberBanding)
        {
            UpdateRubberBand(_lastPointerPosition);
            return;
        }

        // Cable pendiente: el extremo móvil sigue al cursor en espacio de grafo (el VM guarda
        // TargetLocation en Sdk.Point; el lienzo proyecta y redibuja).
        if (_editor?.PendingConnection is not null)
        {
            UpdateSocketGesture(_lastPointerPosition);
            return;
        }

        if (_drag is { } drag)
        {
            double zoom = CanvasTransform.ScaleX;
            if (zoom > 0)
            {
                double dx = (_lastPointerPosition.X - _dragScreenStart.X) / zoom;
                double dy = (_lastPointerPosition.Y - _dragScreenStart.Y) / zoom;
                foreach (var item in drag)
                {
                    item.Node.Location = UnoPointProjection.ToSdk(item.GraphStart.X + dx, item.GraphStart.Y + dy);
                    Reposition(item.Card);
                }

                DrawWires();
            }

            return;
        }

        if (!_isPanning)
        {
            return;
        }

        var current = e.GetCurrentPoint(CanvasPlane).Position;
        CanvasTransform.TranslateX += current.X - _panStart.X;
        CanvasTransform.TranslateY += current.Y - _panStart.Y;
        _panStart = current;
    }

    private void OnCanvasReleased(object sender, PointerRoutedEventArgs e)
    {
        // Soltar el cable pendiente: sobre un socket compatible conecta; en el vacío, cancela.
        if (_editor?.PendingConnection is not null)
        {
            // El destino lo resolvió el último movimiento; soltar lo cierra ahí o cancela (hito 278).
            EndSocketGesture(_pendingHoverPort);
            return;
        }

        if (_isRubberBanding)
        {
            EndRubberBand();
        }

        if (_drag is { } drag)
        {
            var moves = drag
                .Where(m => m.Node.Location != m.GraphStart)
                .Select(m => new NodeMoveItem(m.Node, m.GraphStart, m.Node.Location))
                .ToList();

            if (moves.Count > 0)
            {
                _editor?.UndoRedoService.Record(new MoveNodesAction(moves));
            }

            _drag = null;
        }

        _isPanning = false;
        ((FrameworkElement)sender).ReleasePointerCapture(e.Pointer);
    }

    // ── Rubber band: el rectángulo de selección y el conjunto que va atrapando ──

    private bool _isRubberBanding;

    /// <summary>¿El rectángulo que se está arrastrando AÑADE (Ctrl) o REEMPLAZA? Se decide al pulsar.</summary>
    private bool _rubberAdditive;

    /// <summary>Lo que estaba elegido al empezar el rectángulo: con Ctrl, eso se queda aunque el rectángulo no lo toque.</summary>
    private HashSet<NodeViewModel> _rubberBaseNodes = [];

    /// <summary>Lo mismo para los CABLES marcados: con Ctrl, el rectángulo no suelta la marca de fuera.</summary>
    private HashSet<ConnectionViewModel> _rubberBaseConnections = [];

    /// <summary>
    /// Las anclas de los cables, medidas <b>una vez</b> al empezar el rectángulo (mientras se arrastra, ni el
    /// plano ni el árbol se mueven): decidir si un cable cae dentro no puede costar dos recorridos del árbol de
    /// sockets por cada movimiento del puntero. Un cable sin sus dos anclas medidas no entra: el rectángulo no adivina.
    /// </summary>
    private Dictionary<ConnectionViewModel, (Sdk.Point Source, Sdk.Point Target)> _rubberAnchors = [];

    private Windows.Foundation.Point _rubberStart;

    /// <summary>El alto de referencia de una tarjeta: el mismo con el que el lienzo la encuadra y mide sus cajas.</summary>
    private const double RubberCardHeight = 140;

    /// <summary>
    /// PULSAR en el vacío con el botón izquierdo: arranca el rectángulo. Sin Ctrl REEMPLAZA —lo elegido se suelta
    /// ya, para que se vea que manda el rectángulo— y con Ctrl AÑADE, así que lo anterior se guarda como base. El
    /// punto llega en espacio de la RAÍZ (el del puntero) y el rectángulo se decide en espacio de GRAFO, que es
    /// donde viven las tarjetas y las anclas.
    /// </summary>
    private void BeginRubberBand(Windows.Foundation.Point rootPoint, bool add)
    {
        _isRubberBanding = true;
        _rubberStart = rootPoint;
        _rubberAdditive = add;
        _rubberBaseNodes = _editor?.Nodes.Where(n => n.IsSelected).ToHashSet() ?? [];
        _rubberBaseConnections = _editor?.SelectedConnections.ToHashSet() ?? [];
        _rubberAnchors = MeasureWireAnchors();

        if (!add)
        {
            _editor?.ClearSelection();
        }

        ShowRubberBand(rootPoint, rootPoint);
    }

    /// <summary>MOVER: el rectángulo crece y la selección se recalcula con lo que va quedando dentro.</summary>
    private void UpdateRubberBand(Windows.Foundation.Point rootPoint)
    {
        _lastPointerPosition = rootPoint;
        ShowRubberBand(_rubberStart, rootPoint);
        ApplyRubberBand();
    }

    /// <summary>
    /// SOLTAR: el rectángulo desaparece. Sin arrastre fue un CLIC en el vacío, y eso suelta TODO lo elegido (nodos
    /// y cables), como el clic en el fondo del escritorio.
    /// </summary>
    private void EndRubberBand()
    {
        _isRubberBanding = false;
        RubberLayer.Children.Clear();

        bool wasClick = Math.Abs(_lastPointerPosition.X - _rubberStart.X) < 3
                     && Math.Abs(_lastPointerPosition.Y - _rubberStart.Y) < 3;
        if (wasClick)
        {
            _editor?.ClearSelection();
        }
    }

    /// <summary>
    /// La decisión la toma el NÚCLEO (<see cref="EditorViewModel.ApplyRubberSelection"/>): aquí sólo se decide
    /// QUÉ quedó dentro, con la misma vara para las dos cosas —una tarjeta entra por su CENTRO y un cable entra
    /// con sus DOS anclas dentro, que son las que trazan su curva—.
    /// </summary>
    private void ApplyRubberBand()
    {
        if (_editor is null)
        {
            return;
        }

        var (left, top, right, bottom) = RubberBandInGraph();

        var nodes = _containers
            .Where(pair => CardCenterIsInside(pair.Value, pair.Key, left, top, right, bottom))
            .Select(pair => pair.Key.Node)
            .ToList();

        var connections = _rubberAnchors
            .Where(pair => IsInside(pair.Value.Source, left, top, right, bottom)
                        && IsInside(pair.Value.Target, left, top, right, bottom))
            .Select(pair => pair.Key)
            .ToList();

        _editor.ApplyRubberSelection(nodes, connections, _rubberAdditive, _rubberBaseNodes, _rubberBaseConnections);
    }

    /// <summary>
    /// El rectángulo en espacio de GRAFO: los dos puntos del gesto vienen de la raíz (el puntero) y el plano los
    /// mapea con el MISMO inverso que todo lo demás (<see cref="GraphPointFromScreen"/>). Compararlos en crudo
    /// contra las posiciones de las tarjetas —que son del grafo— sólo acertaba con el plano sin mover.
    /// </summary>
    private (double Left, double Top, double Right, double Bottom) RubberBandInGraph()
    {
        var from = GraphPointFromScreen(_rubberStart);
        var to = GraphPointFromScreen(_lastPointerPosition);
        return (
            Math.Min(from.X, to.X),
            Math.Min(from.Y, to.Y),
            Math.Max(from.X, to.X),
            Math.Max(from.Y, to.Y));
    }

    /// <summary>El centro DIBUJADO de la tarjeta, en espacio de grafo: `Canvas.Left/Top` ya son del grafo (el pan
    /// y el zoom los lleva el plano, no las posiciones) y el ancho es el del nodo.</summary>
    private static bool CardCenterIsInside(
        FrameworkElement container, NodeCardViewModel card,
        double left, double top, double right, double bottom)
        => IsInside(
            Canvas.GetLeft(container) + card.Width / 2,
            Canvas.GetTop(container) + RubberCardHeight / 2,
            left, top, right, bottom);

    private static bool IsInside(double x, double y, double left, double top, double right, double bottom)
        => x >= left && x <= right && y >= top && y <= bottom;

    private static bool IsInside(Sdk.Point point, double left, double top, double right, double bottom)
        => IsInside(point.X, point.Y, left, top, right, bottom);

    /// <summary>Dibuja el rectángulo de selección en pantalla, del punto de partida al actual.</summary>
    private void ShowRubberBand(Windows.Foundation.Point from, Windows.Foundation.Point to)
    {
        RubberLayer.Children.Clear();
        RubberLayer.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = Math.Abs(to.X - from.X),
            Height = Math.Abs(to.Y - from.Y),
            Stroke = CanvasBrush("CanvasWireBrush"),
            StrokeThickness = 1.5,
            Fill = CanvasBrush("CanvasWireBrush"),
            Opacity = 0.9
        });

        if (RubberLayer.Children[0] is Microsoft.UI.Xaml.Shapes.Rectangle rect)
        {
            Canvas.SetLeft(rect, Math.Min(from.X, to.X));
            Canvas.SetTop(rect, Math.Min(from.Y, to.Y));
        }
    }

    /// <summary>Reaplica la posición proyectada de una tarjeta cuyo nodo se movió (sin esperar al pase de layout).</summary>
    private void Reposition(NodeCardViewModel card)
    {
        if (_containers.TryGetValue(card, out var container))
        {
            Canvas.SetLeft(container, card.Position.X);
            Canvas.SetTop(container, card.Position.Y);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Fase 3.3: puertos vivos — anclas write-back, cable pendiente y desconexión
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ancla de un puerto en coordenadas del CANVAS PLANE (espacio de grafo), calculada del árbol visual:
    /// el centro del socket transformado al plano, el write-back que Nodify hacía en el escritorio. Los
    /// cruces de puntos pasan por la proyección explícita del 217.
    /// </summary>
    private Sdk.Point? AnchorOf(PortViewModel port)
    {
        if (!_cardsByNode.TryGetValue(port.NodeOwner, out var card)
            || !_containers.TryGetValue(card, out var container))
        {
            return null;
        }

        // El socket vive en el árbol de la tarjeta: se busca por su DataContext (el PortViewModel).
        var socket = FindSocketElement(container, port);
        if (socket is null)
        {
            return null;
        }

        // Centro del socket en coordenadas de la ventana → espacio de grafo (inverso del mapeo).
        var center = TransformToVisualCenter(socket, this);
        return GraphPointFromScreen(center);
    }

    /// <summary>El elemento del árbol cuya DataContext es el puerto buscado.</summary>
    private static FrameworkElement? FindSocketElement(DependencyObject root, PortViewModel port)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { DataContext: PortViewModel p } && ReferenceEquals(p, port))
            {
                return (FrameworkElement)child;
            }

            var found = FindSocketElement(child, port);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// El centro del elemento, medido en el espacio de <paramref name="relativeTo"/>.
    ///
    /// <para><b>El centro se transforma, no se suma</b>: el punto que hay que llevar por la cadena es el
    /// centro <i>local</i> del elemento <c>(w/2, h/2)</c>. La primera versión transformaba el vértice
    /// <c>(0, 0)</c> y le <i>sumaba</i> la mitad del tamaño en el espacio de destino, lo que ignora la
    /// <b>escala</b> de la cadena: con el plano al 125 % el centro medido se quedaba corto
    /// <c>0,25 · (w/2)</c> —unos 5 px con el socket de este árbol— y esa ancla corta viajaba al cable, que
    /// aparecía desplazado en cuanto se tocaba el zoom. Con el plano al 100 %, o con un pan (que sólo
    /// traslada), el error es cero: por eso el defecto sólo se ve al ajustar el zoom, que es como lo reportó
    /// el usuario.</para>
    ///
    /// <para>El centro pasa por la proyección (la guardia del 217 censura el cruce hecho a mano con .X/.Y):
    /// la pareja se envuelve con la misma regla que el resto de cruces del host.</para>
    /// </summary>
    private static Windows.Foundation.Point TransformToVisualCenter(FrameworkElement element, UIElement relativeTo)
    {
        var localCenter = UnoPointProjection.ToSdk(element.ActualWidth / 2, element.ActualHeight / 2);
        (double lx, double ly) = UnoPointProjection.ToUno(localCenter);

        var center = element.TransformToVisual(relativeTo).TransformPoint(new Windows.Foundation.Point(lx, ly));
        var projected = UnoPointProjection.ToSdk(center.X, center.Y);
        (double cx, double cy) = UnoPointProjection.ToUno(projected);
        return new Windows.Foundation.Point(cx, cy);
    }

    /// <summary>El write-back de todas las anclas, tras layout y tras cada movimiento.</summary>
    private void WriteBackAnchors()
    {
        if (_editor is null)
        {
            return;
        }

        foreach (var node in _editor.Nodes)
        {
            foreach (var port in node.InputPorts.Concat(node.OutputPorts))
            {
                if (AnchorOf(port) is { } anchor)
                {
                    port.Anchor = anchor;
                }
            }
        }
    }

    /// <summary>Un socket de una tarjeta pidió iniciar (o terminar) un cable: habla con los comandos del núcleo.</summary>
    private void OnCardSocketRequested(object? sender, PortViewModel port)
    {
        if (_editor is null)
        {
            return;
        }

        if (_editor.PendingConnection is null)
        {
            BeginSocketGesture(port);
        }
        else
        {
            // Pulsar otro puerto con el cable en la mano lo cierra ahí: es el tercer tiempo del gesto, por el
            // mismo camino que el soltar.
            EndSocketGesture(port);
        }
    }

    // ── El GESTO DEL CABLE (hito 278): pulsar un puerto lo arranca, mover lo dibuja, soltar lo cierra ──
    //
    // Son TRES métodos y no un bloque dentro de cada handler porque el gesto tiene tres tiempos y cada uno
    // llega por un camino distinto —la tarjeta sube la pulsación, el lienzo recibe los movimientos y el soltar
    // llega también por la pérdida de captura—. La sonda del gesto los recorre en ese orden, que es el MISMO
    // que ejecuta el puntero: medir por otro camino certificaría un comportamiento que nadie recorre.

    /// <summary>
    /// Radio (en píxeles de pantalla) dentro del cual soltar el cable cuelga del puerto más cercano. Es la
    /// diana del gesto: el socket dibujado mide ~13 px y exigir puntería exacta convierte cada conexión en un
    /// ejercicio de precisión (el hito 250 lo midió dos veces con dedos de verdad).
    /// </summary>
    private const double SocketDropTolerance = 48.0;

    /// <summary>
    /// ¿Un GESTO DE PUERTO tiene la pulsación? Mientras hay un cable pendiente la pulsación es suya: no arma el
    /// arrastre de la tarjeta ni el rectángulo de selección. Es el único dueño de esa decisión, y lo citan el
    /// handler de la pulsación y la sonda del gesto.
    /// </summary>
    private bool SocketGestureOwnsThePress => _editor?.PendingConnection is not null;

    /// <summary>¿Una pulsación en este punto armaría el ARRASTRE de una tarjeta? (un gesto de puerto lo impide)</summary>
    private bool WouldArmCardDrag(Windows.Foundation.Point point)
        => !SocketGestureOwnsThePress && CardAt(point) is not null;

    /// <summary>Pulsar un puerto arranca el gesto: el cable queda pendiente, atado a su ancla y ya dibujado.</summary>
    private void BeginSocketGesture(PortViewModel port)
    {
        WriteBackAnchors();
        _editor?.StartConnectionCommand.Execute(port);
        _pendingSourceAnchor = port.Anchor;
        _pendingHoverPort = null;
        DrawPendingWire();
        CanvasFocusTrace.Write($"gesto del cable: arranca en {SocketName(port)}"
            + $" (cable en la capa={_pendingWirePath is not null})");
    }

    /// <summary>Mover el puntero dibuja el extremo libre y busca el puerto bajo el cursor: el destino del snapping.</summary>
    private void UpdateSocketGesture(Windows.Foundation.Point screenPoint)
    {
        if (_editor?.PendingConnection is not { } pending)
        {
            return;
        }

        pending.TargetLocation = GraphPointFromScreen(screenPoint);
        DrawPendingWire();

        // El rastro anota SÓLO los cambios de destino (un movimiento llega por píxel): es la traza que deja
        // una sesión con el puntero —el mismo instrumento del foco del hito 252— y la que dice si el snapping
        // encontró puerto o si el soltar va a cancelar.
        var hover = FindHoverPort(screenPoint);
        if (!ReferenceEquals(hover, _pendingHoverPort))
        {
            _pendingHoverPort = hover;
            CanvasFocusTrace.Write($"gesto del cable: destino bajo el cursor = {(hover is null ? "ninguno" : SocketName(hover))}");
        }
    }

    /// <summary>
    /// Soltar cierra el gesto: con destino COMPATIBLE conecta; sin él —vacío, destino incompatible o Escape—
    /// CANCELA. Los dos desenlaces dejan el estado limpio: sin cable fantasma en la capa y sin destino colgado.
    /// </summary>
    private void EndSocketGesture(PortViewModel? target)
    {
        int connectionsBefore = _editor?.Connections.Count ?? 0;
        _pendingHoverPort = null;
        if (target is not null)
        {
            _editor?.FinishConnectionCommand.Execute(target);
        }
        else
        {
            _editor?.CancelConnectionCommand.Execute(null);
        }

        // El cable en la mano se saca de la capa AQUÍ: es el final del gesto, y sin esto quedaba un cable
        // fantasma colgado del último punto del arrastre (lo cazó la sonda del sondeo en la app viva, que
        // mide el estado limpio del desenlace).
        ClearPendingWire();
        UpdatePortStatesAndWires();
        CanvasFocusTrace.Write($"gesto del cable: cierra destino={(target is null ? "ninguno" : SocketName(target))}"
            + $" conexiones {connectionsBefore}->{_editor?.Connections.Count ?? 0}"
            + $" cable en la capa={_pendingWirePath is not null}");
    }

    /// <summary>«Nodo.Puerto», para el rastro: el nombre que un humano reconoce en el informe de la sesión.</summary>
    private static string SocketName(PortViewModel port) => $"'{port.NodeOwner.Title}.{port.DisplayName}'";

    /// <summary>Quita de la capa el cable pendiente, si lo hay: el único sitio que lo saca.</summary>
    private void ClearPendingWire()
    {
        if (_pendingWirePath is null)
        {
            return;
        }

        WireLayer.Children.Remove(_pendingWirePath);
        _pendingWirePath = null;
    }

    private void OnCardDisconnectRequested(object? sender, PortViewModel port)
    {
        if (_editor is null)
        {
            return;
        }

        _editor.DisconnectConnectorCommand.Execute(port);
        UpdatePortStatesAndWires();
    }

    /// <summary>Estados de conexión (tooltips/LED) y redibujado, tras cualquier cambio en las conexiones.</summary>
    private void UpdatePortStatesAndWires()
    {
        _editor?.UpdatePortConnectionStates();
        WriteBackAnchors();
        DrawWires();
    }

    private Sdk.Point _pendingSourceAnchor;
    private PortViewModel? _pendingHoverPort;

    /// <summary>
    /// El puerto compatible bajo el cursor durante el arrastre de un cable: es el objetivo del snapping.
    /// Sin compatibilidad (o sin puerto a tiro), no hay objetivo y soltar cancela.
    ///
    /// <para><b>Qué cambió en el hito 278</b>: antes se miraba SÓLO la tarjeta bajo el puntero y con 20 px de
    /// diana (400 px²), así que soltar unos píxeles corto —fuera de la caja de la tarjeta o lejos del punto—
    /// cancelaba una conexión que el usuario creía hecha. Ahora se mira toda tarjeta cuya caja (o su borde de
    /// tolerancia) contiene el punto, y se elige el puerto más cercano dentro de <see cref="SocketDropTolerance"/>.
    /// Sólo entran los destinos que el producto considera conectables (<see cref="PortViewModel.CanConnect"/>):
    /// los que el arrastre muestra en aviso de tipo SÍ conectan —es la regla del escritorio— y los atenuados no.
    /// </para>
    /// </summary>
    private PortViewModel? FindHoverPort(Windows.Foundation.Point screenPoint)
    {
        if (_editor?.PendingConnection?.Source is not { } source)
        {
            return null;
        }

        PortViewModel? best = null;
        double bestDistance = SocketDropTolerance * SocketDropTolerance;
        foreach (var (card, container) in _containers)
        {
            if (!CardBoxIsWithin(container, screenPoint))
            {
                continue;
            }

            var candidates = source.Direction == PortDirection.Output
                ? card.Node.InputPorts
                : card.Node.OutputPorts;

            foreach (var port in candidates)
            {
                if (!PortViewModel.CanConnect(source, port))
                {
                    continue;
                }

                if (AnchorOf(port) is not { } anchor)
                {
                    continue;
                }

                var screen = ScreenPointOfAnchor(anchor);
                double dx = screen.X - screenPoint.X;
                double dy = screen.Y - screenPoint.Y;
                double distance = (dx * dx) + (dy * dy);

                if (distance <= bestDistance)
                {
                    best = port;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// ¿La caja de <paramref name="container"/> contiene el punto, con el borde de
    /// <see cref="SocketDropTolerance"/> de margen? La caja se pregunta al árbol visual —el mismo cruce que la
    /// medida de un ancla, con la escala del plano dentro—: el margen es lo que permite soltar el cable unos
    /// píxeles fuera de la tarjeta destino sin que el gesto se cancele.
    /// </summary>
    private bool CardBoxIsWithin(FrameworkElement container, Windows.Foundation.Point screenPoint)
    {
        var box = container.TransformToVisual(RootGrid)
            .TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));
        double margin = SocketDropTolerance;

        return screenPoint.X >= box.X - margin
            && screenPoint.X <= box.X + box.Width + margin
            && screenPoint.Y >= box.Y - margin
            && screenPoint.Y <= box.Y + box.Height + margin;
    }

    /// <summary>El punto de pantalla de un ancla de grafo (aplica el mapeo compartido: zoom + translate).</summary>
    private Windows.Foundation.Point ScreenPointOfAnchor(Sdk.Point graphAnchor)
    {
        (double x, double y) = UnoPointProjection.ToUno(graphAnchor);
        var projected = UnoPointProjection.ToSdk(
            (x * CanvasTransform.ScaleX) + CanvasTransform.TranslateX,
            (y * CanvasTransform.ScaleY) + CanvasTransform.TranslateY);

        // El envoltorio del framework se construye con la MISMA proyección (la guardia del 217 censura
        // cualquier cruce hecho a mano desde .X/.Y): un solo camino para el par de coordenadas.
        (double sx, double sy) = UnoPointProjection.ToUno(projected);
        return new Windows.Foundation.Point(sx, sy);
    }

    /// <summary>El cable pendiente dibujado sobre la capa de cables, si hay arrastre activo.</summary>
    private void DrawPendingWire()
    {
        ClearPendingWire();

        var pending = _editor?.PendingConnection;
        if (pending is null || pending.Source is null)
        {
            return;
        }

        // El extremo móvil sigue al cursor; la geometría es la misma Bézier compartida.
        var target = pending.TargetLocation;
        var wire = ConnectionGeometry.BuildWire(
            _pendingSourceAnchor, target,
            pending.Source.Direction == PortDirection.Output
                ? ConnectionGeometry.FlowDirection.Forward
                : ConnectionGeometry.FlowDirection.Backward);

        _pendingWirePath = new Microsoft.UI.Xaml.Shapes.Path
        {
            Stroke = CanvasBrush("CanvasWireBrush"),
            StrokeThickness = 2.5,
            Opacity = 0.85,
            Data = CreateWireGeometry(wire)
        };
        WireLayer.Children.Add(_pendingWirePath);
    }

    private Microsoft.UI.Xaml.Shapes.Path? _pendingWirePath;

    /// <summary>
    /// El handler del teclado del CONTROL: registra el rastro y delega en
    /// <see cref="TryHandleShortcutKey"/>, que es el único resolver — así el atajo que entra por el foco del
    /// lienzo y el que entra enrutado por la ventana no pueden divergir.
    /// </summary>
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        CanvasFocusTrace.Write($"tecla={e.Key} src={e.OriginalSource?.GetType().Name ?? "nadie"} "
                             + $"enfocado={DescribeFocused()}");

        if (TryHandleShortcutKey(e.Key, e.OriginalSource))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Los atajos del editor, con las mismas claves que el escritorio: la tabla compartida resuelve la
    /// combinación y ejecuta el comando canónico del núcleo. La caja de renombrado (y cualquier cuadro de
    /// texto) consume sus teclas: no se las secuestra.
    ///
    /// <para><b>Por qué el teclado no depende del FOCO</b> (hito 252): el rastro con puntero real midió que
    /// el clic SÍ entregaba el foco al control (<c>GotFocus enfocado=EditorCanvasControl#Canvas</c>) y que
    /// ~0,5 s después se lo llevaba un <c>ScrollViewer</c> de un panel que reacciona a la selección, con
    /// <c>Ctrl+Z</c>, <c>Ctrl+Y</c>, <c>Supr</c> y <c>F2</c> muriendo con él. Aquí el defecto no se puede
    /// provocar sin puntero (el sondeo selecciona un nodo en el mismo camino y el foco no se mueve), así que
    /// el arreglo no persigue al ladrón: hace que los atajos no necesiten ser dueños del foco. La ventana
    /// enruta a este método las teclas que nadie consumió —el burbujeo que el escritorio ya usaba en su
    /// vista de editor— y el handler del control llama al MISMO método.</para>
    /// </summary>
    /// <param name="key">La tecla física.</param>
    /// <param name="source">El origen del evento: un cuadro de texto (o algo suyo) manda en su teclado.</param>
    /// <returns>¿La tecla quedó consumida por el editor?</returns>
    internal bool TryHandleShortcutKey(Windows.System.VirtualKey key, object? source)
    {
        if (_editor is null || IsTextInput(source as DependencyObject) || _editor.Nodes.Any(n => n.IsEditingTitle))
        {
            return false;
        }

        var physical = MapKey(key);
        if (physical is null)
        {
            return false;
        }

        var modifiers = EditorKeyboardShortcuts.Modifiers.None;
        if (IsKeyDown(Windows.System.VirtualKey.Control)) modifiers |= Ctrl;
        if (IsKeyDown(Windows.System.VirtualKey.Shift)) modifiers |= EditorKeyboardShortcuts.Modifiers.Shift;

        var command = EditorKeyboardShortcuts.Resolve(physical.Value, modifiers);
        if (command is null)
        {
            return false;
        }

        // Escape con cable pendiente lo CANCELA (y limpia la capa), antes que cualquier otra semántica.
        if (command == EditorKeyboardShortcuts.ShortcutKey.Escape && _editor.PendingConnection is not null)
        {
            // Escape CANCELA el gesto por el mismo camino que soltar en el vacío (hito 278): una sola casa
            // para "dejar el estado limpio".
            EndSocketGesture(null);
            return true;
        }

        // El spotlight lo abre la VISTA (necesita el punto del grafo bajo el cursor guardado en el VM):
        // la tabla resuelve el comando, la vista hace el resto.
        if (command == EditorKeyboardShortcuts.ShortcutKey.Spotlight)
        {
            var spotlightPoint = GraphPointFromScreen(
                _lastPointerPosition == default ? new Windows.Foundation.Point(250, 200) : _lastPointerPosition);
            _editor.OpenSpotlight(spotlightPoint);
            return true;
        }

        // La posición de referencia (pegar, spotlight) es el punto del grafo bajo el cursor.
        var cursor = _lastPointerPosition == default ? new Windows.Foundation.Point(250, 200) : _lastPointerPosition;
        return EditorKeyboardShortcuts.Execute(command.Value, _editor, GraphPointFromScreen(cursor));
    }

    /// <summary>
    /// Sonda del ENRUTADO de atajos (hito 252): el atajo tiene que resolverse AUNQUE EL FOCO NO ESTÉ EN EL
    /// LIENZO. Es la forma que el defecto tiene sin puntero inyectable: lo medido con dedos de verdad fue que
    /// un panel se lleva el foco ~0,5 s después del clic, y con él morían los atajos. Aquí se entrega el foco
    /// a otro control del lienzo (la barra de zoom) y se resuelven teclas por el MISMO método que enruta la
    /// ventana: <c>Espacio</c> abre el buscador y <c>Escape</c> lo cierra — las dos sin modificador, porque
    /// un modificador exige la tecla físicamente pulsada y el sondeo no puede inyectarla.
    /// </summary>
    /// <returns>(el atajo se resolvió sin foco, un cuadro de texto conserva su teclado, detalle)</returns>
    internal (bool ResolvedWithoutFocus, bool RespectsTextInput, string Detail) ProbeShortcutResolution()
    {
        if (_editor is null)
        {
            return (false, false, "sin editor montado");
        }

        var other = FirstButton(RootGrid);
        bool movedAway = other is null || other.Focus(FocusState.Programmatic);
        bool focusAway = !HoldsFocus();

        bool opened = TryHandleShortcutKey(Windows.System.VirtualKey.Space, other)
                   && _editor.IsSpotlightOpen;
        bool closed = TryHandleShortcutKey(Windows.System.VirtualKey.Escape, other)
                   && !_editor.IsSpotlightOpen;

        // La cortesía: al cuadro de texto no se le secuestra el teclado (misma regla que el handler).
        bool declinedTextInput = !TryHandleShortcutKey(Windows.System.VirtualKey.Space, new TextBox())
                              && !_editor.IsSpotlightOpen;

        bool resolved = opened && closed;
        string detail = resolved
            ? $"con el foco FUERA del lienzo (movedAway={movedAway}, focusAway={focusAway}) el atajo se "
              + "resuelve igual: Espacio abre el buscador y Escape lo cierra"
            : $"el atajo NO se resuelve sin foco (abra el buscador={opened}, ciérrelo={closed}, "
              + $"movedAway={movedAway}, focusAway={focusAway})";

        return (resolved, declinedTextInput, detail);
    }

    private static EditorKeyboardShortcuts.PhysicalKey? MapKey(Windows.System.VirtualKey key) => key switch
    {
        Windows.System.VirtualKey.A => EditorKeyboardShortcuts.PhysicalKey.A,
        Windows.System.VirtualKey.Z => EditorKeyboardShortcuts.PhysicalKey.Z,
        Windows.System.VirtualKey.Y => EditorKeyboardShortcuts.PhysicalKey.Y,
        Windows.System.VirtualKey.C => EditorKeyboardShortcuts.PhysicalKey.C,
        Windows.System.VirtualKey.V => EditorKeyboardShortcuts.PhysicalKey.V,
        Windows.System.VirtualKey.X => EditorKeyboardShortcuts.PhysicalKey.X,
        Windows.System.VirtualKey.D => EditorKeyboardShortcuts.PhysicalKey.D,
        Windows.System.VirtualKey.Delete => EditorKeyboardShortcuts.PhysicalKey.Delete,
        Windows.System.VirtualKey.Back => EditorKeyboardShortcuts.PhysicalKey.Back,
        Windows.System.VirtualKey.F2 => EditorKeyboardShortcuts.PhysicalKey.F2,
        Windows.System.VirtualKey.Space => EditorKeyboardShortcuts.PhysicalKey.Space,
        Windows.System.VirtualKey.Escape => EditorKeyboardShortcuts.PhysicalKey.Escape,
        _ => null
    };

    private static bool IsKeyDown(Windows.System.VirtualKey key)
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Aviso de cables perdidos (fase 3.3): el VM del núcleo ya compone las filas; el host las pinta y
    // ejecuta sus dos botones — ir al nodo (centrarlo) y reconectar (CreateConnection con los LiveEnds).
    // ─────────────────────────────────────────────────────────────────────────────

    private EditorViewModel? _notifiedEditor;

    /// <summary>Refresca el banner con el estado del VM del núcleo (texto + filas de arreglo).</summary>
    private void RefreshCanvasNotice()
    {
        if (_editor is null)
        {
            return;
        }

        _notifiedEditor = _editor;
        bool hasNotice = _editor.HasCanvasNotice || _editor.HasCanvasNoticeFixes;

        CanvasNoticeBanner.Visibility = hasNotice ? Visibility.Visible : Visibility.Collapsed;
        CanvasNoticeText.Text = _editor.CanvasNotice;
        CanvasNoticeFixes.ItemsSource = _editor.HasCanvasNoticeFixes ? _editor.CanvasNoticeFixes : null;
    }

    private void OnDismissCanvasNotice(object sender, RoutedEventArgs e)
    {
        _notifiedEditor?.DismissCanvasNoticeCommand.Execute(null);
        RefreshCanvasNotice();
    }

    private void OnGoToFixNode(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DroppedConnectionFixViewModel fix)
        {
            fix.GoToNodeCommand.Execute(null);
        }
    }

    private void OnReconnectFix(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DroppedConnectionFixViewModel fix && fix.CanReconnect)
        {
            fix.ReconnectCommand.Execute(null);
            UpdatePortStatesAndWires();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Fase 3.4: decoradores (notas/grupos), drag & drop, spotlight y migas de subflujos
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Reconstruye la capa de decoradores: grupos AL FONDO (z menor), notas delante.</summary>
    private void RebuildDecorators()
    {
        DecoratorLayer.Children.Clear();
        _decoratorBindings.Clear();
        if (_editor is null)
        {
            return;
        }

        // El mismo orden del CanvasDecorators del núcleo: grupos insertados en 0 (detrás), notas al final.
        foreach (var decorator in _editor.CanvasDecorators)
        {
            ContentPresenter container;
            switch (decorator)
            {
                case GroupViewModel group:
                    container = WrapDecorator(BuildGroupCard(group), group);
                    DecoratorLayer.Children.Insert(0, container);
                    break;
                case AnnotationViewModel annotation:
                    container = WrapDecorator(BuildAnnotationCard(annotation), annotation);
                    DecoratorLayer.Children.Add(container);
                    break;
            }
        }
    }

    private readonly Dictionary<object, FrameworkElement> _decoratorBindings = new();

    private ContentPresenter WrapDecorator(FrameworkElement card, object decorator)
    {
        var container = new ContentPresenter { Content = card };
        PositionDecorator(container, decorator);
        _decoratorBindings[decorator] = card;
        return container;
    }

    private void PositionDecorator(ContentPresenter container, object decorator)
    {
        // La posición sale proyectada por el mismo conversor del 217 (jamás de .X/.Y crudos).
        var projected = UnoPointConverter.Instance.Convert(DecoratorLocation(decorator), typeof(Windows.Foundation.Point), null!, "en-US");
        if (projected is Windows.Foundation.Point p)
        {
            Canvas.SetLeft(container, p.X);
            Canvas.SetTop(container, p.Y);
        }
    }

    private static Sdk.Point DecoratorLocation(object decorator) => decorator switch
    {
        GroupViewModel g => g.Location,
        AnnotationViewModel a => a.Location,
        _ => new Sdk.Point(0, 0)
    };

    /// <summary>La nota: cara de color, título y contenido; botones de recolorear y borrar.</summary>
    private FrameworkElement BuildAnnotationCard(AnnotationViewModel annotation)
    {
        var colorBrush = new SolidColorBrush(NodeCardViewModel.ParseHex(annotation.Color));
        var title = new TextBlock
        {
            Text = annotation.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
        };
        var content = new TextBlock
        {
            Text = annotation.Content,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
        };
        var delete = new Button { Content = "✕", Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(4, 0, 4, 0) };
        delete.Click += (_, _) =>
        {
            _editor?.DeleteAnnotationCommand.Execute(annotation);
            RebuildDecorators();
        };

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(title);
        header.Children.Add(delete);

        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(header);
        body.Children.Add(content);

        var card = new Border
        {
            Background = colorBrush,
            BorderBrush = new SolidColorBrush(NodeCardViewModel.ParseHex("#30363D")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Width = annotation.Width,
            Child = body,
            Tag = annotation
        };
        HookDecoratorDrag(card, annotation);
        return card;
    }

    /// <summary>El grupo: contorno de color translúcido con el título; engloba a sus nodos.</summary>
    private FrameworkElement BuildGroupCard(GroupViewModel group)
    {
        var accent = NodeCardViewModel.ParseHex(group.Color);
        var title = new TextBlock
        {
            Text = group.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(NodeCardViewModel.ParseHex("#F0F6FC"))
        };
        var delete = new Button { Content = "✕", Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(4, 0, 4, 0), Foreground = new SolidColorBrush(NodeCardViewModel.ParseHex("#F0F6FC")) };
        delete.Click += (_, _) =>
        {
            _editor?.DeleteGroupCommand.Execute(group);
            RebuildDecorators();
        };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        header.Children.Add(title);
        header.Children.Add(delete);

        var card = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, accent.R, accent.G, accent.B)),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(10),
            Width = group.Width,
            Height = group.Height,
            Child = header,
            Tag = group,
            IsHitTestVisible = true
        };
        HookDecoratorDrag(card, group);
        return card;
    }

    /// <summary>Arrastre de decoradores: el puntero mueve la Location del VM (Sdk.Point) y repinta.</summary>
    private void HookDecoratorDrag(FrameworkElement card, object decorator)
    {
        bool dragging = false;
        Windows.Foundation.Point start = default;
        Sdk.Point graphStart = default;

        card.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
            {
                return;
            }

            dragging = true;
            start = e.GetCurrentPoint(RootGrid).Position;
            graphStart = DecoratorLocation(decorator);
            card.CapturePointer(e.Pointer);
            e.Handled = true;
        };
        card.PointerMoved += (_, e) =>
        {
            if (!dragging)
            {
                return;
            }

            var current = e.GetCurrentPoint(RootGrid).Position;
            double zoom = CanvasTransform.ScaleX;
            if (zoom <= 0)
            {
                return;
            }

            double dx = (current.X - start.X) / zoom;
            double dy = (current.Y - start.Y) / zoom;
            SetDecoratorLocation(decorator, UnoPointProjection.ToSdk(graphStart.X + dx, graphStart.Y + dy));

            if (card.Parent is ContentPresenter container)
            {
                PositionDecorator(container, decorator);
            }

            e.Handled = true;
        };
        card.PointerReleased += (_, e) =>
        {
            dragging = false;
            card.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        };
    }

    private static void SetDecoratorLocation(object decorator, Sdk.Point value)
    {
        switch (decorator)
        {
            case GroupViewModel g: g.Location = value; break;
            case AnnotationViewModel a: a.Location = value; break;
        }
    }

    // ── Spotlight: añadir nodo por teclado; el núcleo filtra, el host pinta y confirma ──

    private void ShowSpotlight()
    {
        SpotlightPopup.Visibility = Visibility.Visible;
        SpotlightList.ItemsSource = _editor?.FilteredSpotlightItems;
        SpotlightSearchBox.Text = _editor?.SpotlightSearchText ?? string.Empty;
        _ = DispatcherQueue.TryEnqueue(() => SpotlightSearchBox.Focus(FocusState.Programmatic));
    }

    private void HideSpotlight()
    {
        SpotlightPopup.Visibility = Visibility.Collapsed;
        SpotlightList.ItemsSource = null;
    }

    private void OnSpotlightBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_editor is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Down:
                StepSpotlightSelection(+1);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Up:
                StepSpotlightSelection(-1);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Enter:
                ConfirmSpotlight();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Escape:
                _editor.CloseSpotlight();
                e.Handled = true;
                break;
            default:
                // El texto llega al VM por el binding TwoWay; el filtro se recalcula allí.
                break;
        }
    }

    private void OnSpotlightListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_editor is null)
        {
            return;
        }

        if (e.Key is Windows.System.VirtualKey.Enter)
        {
            ConfirmSpotlight();
            e.Handled = true;
        }
        else if (e.Key is Windows.System.VirtualKey.Escape)
        {
            _editor.CloseSpotlight();
            e.Handled = true;
        }
    }

    private void OnSpotlightListDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ConfirmSpotlight();
    }

    private void StepSpotlightSelection(int delta)
    {
        if (_editor is null || _editor.FilteredSpotlightItems.Count == 0)
        {
            return;
        }

        var items = _editor.FilteredSpotlightItems;
        int index = _editor.SelectedSpotlightItem is { } current ? items.IndexOf(current) : -1;
        index = (index + delta + items.Count) % items.Count;
        _editor.SelectedSpotlightItem = items[index];
        SpotlightList.SelectedItem = items[index];
    }

    /// <summary>Confirma la selección del spotlight: AddNode en el punto del grafo guardado al abrir.</n>
    private void ConfirmSpotlight()
    {
        if (_editor?.SelectedSpotlightItem is not { } item || _editor.IsSpotlightOpen is false)
        {
            return;
        }

        var position = _editor.SpotlightCanvasPosition;
        _editor.AddNode(item.TypeName, position);
        _editor.CloseSpotlight();
        RebuildDecorators();
    }

    // ── Migas de subflujos ──

    private void RefreshBreadcrumbs()
    {
        if (_editor is null)
        {
            return;
        }

        BreadcrumbsHost.ItemsSource = _editor.Breadcrumbs;
        BreadcrumbsBar.Visibility = _editor.HasBreadcrumbs ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBreadcrumbClicked(object sender, RoutedEventArgs e)
    {
        if (_editor is null || (sender as FrameworkElement)?.DataContext is not BreadcrumbItem crumb)
        {
            return;
        }

        _editor.NavigateToBreadcrumbCommand.Execute(crumb);
        Rebuild();
        RebuildDecorators();
        RefreshBreadcrumbs();
    }

    private void OnWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(RootGrid).Properties.MouseWheelDelta;
        ZoomBy(delta > 0 ? 1.1 : 1 / 1.1);
    }

    private void OnZoomIn(object sender, RoutedEventArgs e) => ZoomBy(1.1);

    private void OnZoomOut(object sender, RoutedEventArgs e) => ZoomBy(1 / 1.1);

    private void ZoomBy(double factor)
    {
        double zoom = Math.Clamp(CanvasTransform.ScaleX * factor, MinZoom, MaxZoom);
        CanvasTransform.ScaleX = zoom;
        CanvasTransform.ScaleY = zoom;
        ZoomText.Text = $"{Math.Round(zoom * 100)} %";
    }

    private void OnFitToScreen(object sender, RoutedEventArgs e)
    {
        if (_editor is null || _editor.Nodes.Count == 0)
        {
            return;
        }

        // El mismo calculador del núcleo que usa el escritorio: un solo «ajustar a pantalla» para los dos hosts.
        var (zoom, location) = EditorViewportCalculator.CalculateFitToScreen(_editor.Nodes);

        CanvasTransform.ScaleX = zoom;
        CanvasTransform.ScaleY = zoom;
        CanvasTransform.TranslateX = -location.X * zoom;
        CanvasTransform.TranslateY = -location.Y * zoom;
        ZoomText.Text = $"{Math.Round(zoom * 100)} %";
    }

    /// <summary>
    /// La sonda de la superficie UIA (hito 238): lo que una observación EXTERNA (pywinauto, sin
    /// UIAccess) puede alcanzar del lienzo. Tres medidas en la app viva: la ancla explícita llega al
    /// árbol con su valor y el peer está expuesto; el foco programático (la vía del SetFocus de UIA,
    /// misma API que llama el peer) entra sin puntero y deja IsFocused en el árbol; y el estado del
    /// zoom es OBSERVABLE — cambiarlo por la vía de los botones deja el nivel escrito en la ancla de
    /// la barra, restaurando el 100 % al salir.
    /// </summary>
    /// <returns>(la ancla expuesta, el foco entró, el estado observado y restaurado)</returns>
    internal (bool AnchorExposed, bool FocusEntered, bool ZoomStateObservable) ProbeUiAccessibility()
    {
        string? anchor = AutomationProperties.GetAutomationId(this);
        bool anchorExposed = anchor == "CanvasRoot"
            && FrameworkElementAutomationPeer.CreatePeerForElement(this) is not null;

        bool focusEntered = this.Focus(FocusState.Programmatic);

        double before = CanvasTransform.ScaleX;
        string beforeLabel = ZoomText.Text;
        ZoomBy(1.25);
        double after = CanvasTransform.ScaleX;
        string afterLabel = ZoomText.Text;
        ZoomBy(before / after);
        bool zoomStateObservable = Math.Abs(after - before * 1.25) < 0.001
            && afterLabel != beforeLabel
            && ZoomText.Text == beforeLabel;

        return (anchorExposed, focusEntered, zoomStateObservable);
    }

    private void DrawBackgroundGrid()
    {
        for (double x = 0; x <= 2400; x += GridStep)
        {
            GridLayer.Children.Add(new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = 2400,
                Stroke = CanvasBrush("CanvasGridBrush"),
                StrokeThickness = x % (GridStep * 2) == 0 ? 1 : 0.5
            });
        }

        for (double y = 0; y <= 2400; y += GridStep)
        {
            GridLayer.Children.Add(new Line
            {
                X1 = 0, Y1 = y, X2 = 2400, Y2 = y,
                Stroke = CanvasBrush("CanvasGridBrush"),
                StrokeThickness = y % (GridStep * 2) == 0 ? 1 : 0.5
            });
        }
    }
}
