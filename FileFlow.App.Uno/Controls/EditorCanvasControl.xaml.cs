using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using FileFlow.App.Services;
using FileFlow.App.Services.UndoRedo;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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

            // Con contenedores y tarjetas materializados, las anclas de los puertos ya son reales: el
            // write-back inicial y el primer trazado de cables con las anclas del árbol (fase 3.3).
            if (_editor is not null && _editor.Connections.Count > 0)
            {
                WriteBackAnchors();
                DrawWires();
            }
        }
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
    /// Sonda de la fase 3.5: cambiar el tema por la API del núcleo (SetThemeById) tiene que
    /// re-tematizar el lienzo EN CALIENTE — el fondo del plano y la cara de una tarjeta cambian de
    /// color porque los pinceles republicados por UnoThemeHost llegan a los ThemeResource ya
    /// evaluados del XAML. Restauración: el dark_fluent vuelve a estar activo al salir.
    /// </summary>
    internal (bool BackgroundChanged, bool CardChanged, bool VariantChanged, bool Restored) ProbeThemeRepublish()
    {
        if (_editor is null)
        {
            return (false, false, false, false);
        }

        var themeManager = FileFlow.App.Services.ThemeManager.Instance;
        var original = themeManager.ActiveThemeDefinition;
        try
        {
            Windows.UI.Color ColorOf(Brush? brush) => brush is SolidColorBrush solid
                ? solid.Color
                : default;

            var backgroundBefore = ColorOf(RootGrid.Background);
            var cardBefore = ColorOf(FindFirstCardBodyBrush());

            themeManager.SetThemeById("light_studio");

            var backgroundAfter = ColorOf(RootGrid.Background);
            var cardAfter = ColorOf(FindFirstCardBodyBrush());

            bool backgroundChanged = backgroundAfter != backgroundBefore;
            bool cardChanged = cardAfter != cardBefore;

            // La variante publicada por el host llega a este control HEREDADA (ActualTheme).
            bool variantChanged = ActualTheme == ElementTheme.Light;

            themeManager.SetThemeById("dark_fluent");
            var backgroundRestored = ColorOf(RootGrid.Background);

            return (backgroundChanged, cardChanged, variantChanged,
                backgroundRestored == backgroundBefore);
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
        WireCardEvents(cards);
        DrawWires();
    }

    /// <summary>Cablea (y descablea) los eventos de socket de cada tarjeta.</summary>
    private void WireCardEvents(List<NodeCardViewModel>? cards)
    {
        foreach (var entry in _socketWiring)
        {
            entry.View.SocketRequested -= OnCardSocketRequested;
            entry.View.DisconnectRequested -= OnCardDisconnectRequested;
        }

        _socketWiring.Clear();
        if (cards is null)
        {
            return;
        }

        // Los eventos viven en la VISTA (NodeCardView); se alcanzan desde los contenedores materializados.
        foreach (var pair in _containers)
        {
            if (pair.Value.Content is NodeCardView view)
            {
                view.SocketRequested += OnCardSocketRequested;
                view.DisconnectRequested += OnCardDisconnectRequested;
                _socketWiring.Add((view, OnCardSocketRequested, OnCardDisconnectRequested));
            }
        }
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
    // Los cables: ConnectionGeometry, la misma curva que pinta Nodify en el escritorio
    // ─────────────────────────────────────────────────────────────────────────────

    private void DrawWires()
    {
        WireLayer.Children.Clear();

        if (_editor is null)
        {
            return;
        }

        foreach (var connection in _editor.Connections)
        {
            // Fase 3.3: las anclas REALES (write-back por árbol visual) mandan; la estimación del centro
            // de tarjeta (Location.Y + 40) queda como respaldo si el árbol aún no materializó el socket.
            var source = AnchorOf(connection.Source)
                ?? new Sdk.Point(
                    connection.Source.NodeOwner.Location.X + connection.Source.NodeOwner.Width,
                    connection.Source.NodeOwner.Location.Y + 40);
            var target = AnchorOf(connection.Target)
                ?? new Sdk.Point(
                    connection.Target.NodeOwner.Location.X,
                    connection.Target.NodeOwner.Location.Y + 40);

            var curve = ConnectionGeometry.BezierControlPoints(source, target, ConnectionGeometry.DefaultSpacing);

            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = CanvasBrush("CanvasWireBrush"),
                StrokeThickness = 3.5,
                Data = CreateBezierGeometry(curve)
            };

            WireLayer.Children.Add(path);
        }
    }

    private static Geometry CreateBezierGeometry(ConnectionGeometry.CubicBezier curve)
    {
        var geometry = new PathGeometry
        {
            Figures =
            {
                new PathFigure
                {
                    StartPoint = ToWindowsPoint(curve.P0),
                    IsFilled = false,
                    Segments =
                    {
                        new BezierSegment
                        {
                            Point1 = ToWindowsPoint(curve.P1),
                            Point2 = ToWindowsPoint(curve.P2),
                            Point3 = ToWindowsPoint(curve.P3)
                        }
                    }
                }
            }
        };

        return geometry;
    }

    /// <summary>
    /// La proyección del cable: <see cref="UnoPointProjection.ToUno"/> (núcleo portable), el mismo corazón
    /// que <see cref="UnoPointConverter"/> — la geometría del cable se calcula en espacio de grafo
    /// (<see cref="Sdk.Point"/>) y sólo se proyecta al dibujar, como en el escritorio.
    /// </summary>
    private static Windows.Foundation.Point ToWindowsPoint(Sdk.Point point)
    {
        (double x, double y) = UnoPointProjection.ToUno(point);
        return new Windows.Foundation.Point(x, y);
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
    /// </summary>
    private NodeCardViewModel? CardAt(Windows.Foundation.Point position)
    {
        return VisualTreeHelper.FindElementsInHostCoordinates(position, this)
            .OfType<NodeCardView>()
            .FirstOrDefault()?.DataContext as NodeCardViewModel;
    }

    /// <summary>¿El punto cae sobre un control interactivo (la barra de zoom)? Ahí ni arrastre ni pan.</summary>
    private bool HitsInteractiveControl(Windows.Foundation.Point position)
    {
        return VisualTreeHelper.FindElementsInHostCoordinates(position, this).OfType<Button>().Any();
    }

    private void OnCanvasPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RootGrid).Position;
        var properties = e.GetCurrentPoint(RootGrid).Properties;
        ((FrameworkElement)sender).Focus(FocusState.Programmatic);

        // 1. Selección y arrastre: botón izquierdo sobre una tarjeta.
        if (properties.IsLeftButtonPressed)
        {
            var card = CardAt(point);
            if (card is not null)
            {
                // Click selecciona: el núcleo reacciona a IsSelected (SelectedNode + BringToFront +
                // contador); si ya estaba seleccionada, BringToFront la sube sin romper la selección.
                if (!card.Node.IsSelected)
                {
                    card.Node.IsSelected = true;
                }
                else
                {
                    _editor?.BringToFront(card.Node);
                }

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

        // 2. Rubber band (botón izquierdo en el fondo): selección por rectángulo, como el escritorio.
        if (properties.IsLeftButtonPressed && !HitsInteractiveControl(point))
        {
            _isRubberBanding = true;
            _rubberStart = point;
            ShowRubberBand(point, point);
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
        _lastPointerPosition = e.GetCurrentPoint(RootGrid).Position;        if (_isRubberBanding)
        {
            ShowRubberBand(_rubberStart, _lastPointerPosition);
            UpdateRubberSelection();
            return;
        }

        // Cable pendiente: el extremo móvil sigue al cursor en espacio de grafo (el VM guarda
        // TargetLocation en Sdk.Point; el lienzo proyecta y redibuja).
        if (_editor?.PendingConnection is { } pending)
        {
            if (_pendingWirePath is not null)
            {
                WireLayer.Children.Remove(_pendingWirePath);
            }

            var graphPoint = GraphPointFromScreen(_lastPointerPosition);
            pending.TargetLocation = graphPoint;
            DrawPendingWire();

            // ¿Hay un socket compatible bajo el cursor? Solta ahí al levantar ( snapping del escritorio).
            var hoverCard = CardAt(_lastPointerPosition);
            if (hoverCard is not null)
            {
                _pendingHoverPort = FindHoverPort(hoverCard, _lastPointerPosition);
            }
            else
            {
                _pendingHoverPort = null;
            }

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
            var target = _pendingHoverPort;
            _pendingHoverPort = null;
            if (_pendingWirePath is not null)
            {
                WireLayer.Children.Remove(_pendingWirePath);
                _pendingWirePath = null;
            }

            if (target is not null)
            {
                _editor.FinishConnectionCommand.Execute(target);
            }
            else
            {
                _editor.CancelConnectionCommand.Execute(null);
            }

            UpdatePortStatesAndWires();
            return;
        }

        if (_isRubberBanding)
        {
            _isRubberBanding = false;
            RubberLayer.Children.Clear();

            // Un clic sin arrastre en el fondo deselecciona: el estándar del editor (y lo que Nodify
            // hacía en el escritorio). Un rectángulo de menos de 3 px no es una selección.
            bool isClick = Math.Abs(_lastPointerPosition.X - _rubberStart.X) < 3
                        && Math.Abs(_lastPointerPosition.Y - _rubberStart.Y) < 3;
            if (isClick && _editor is not null)
            {
                foreach (var node in _editor.Nodes.Where(n => n.IsSelected))
                {
                    node.IsSelected = false;
                }
            }
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
    private Windows.Foundation.Point _rubberStart;

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

    /// <summary>
    /// Los nodos cuyo CENTRO cae dentro del rectángulo se marcan como seleccionados: el estado del VM
    /// repinta el glow de selección de la tarjeta (el binding ya existía de la fase 3.1).
    /// </summary>
    private void UpdateRubberSelection()
    {
        if (_editor is null)
        {
            return;
        }

        double left = Math.Min(_rubberStart.X, _lastPointerPosition.X);
        double top = Math.Min(_rubberStart.Y, _lastPointerPosition.Y);
        double right = Math.Max(_rubberStart.X, _lastPointerPosition.X);
        double bottom = Math.Max(_rubberStart.Y, _lastPointerPosition.Y);

        foreach (var pair in _containers)
        {
            var card = pair.Key;
            double width = card.Width * CanvasTransform.ScaleX;
            double height = 140 * CanvasTransform.ScaleY;
            double x = Canvas.GetLeft(pair.Value);
            double y = Canvas.GetTop(pair.Value);

            bool inside = x + width / 2 >= left && x + width / 2 <= right
                       && y + height / 2 >= top && y + height / 2 <= bottom;

            if (inside)
            {
                card.Node.IsSelected = true;
            }
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

    private static Windows.Foundation.Point TransformToVisualCenter(FrameworkElement element, UIElement relativeTo)
    {
        var transform = element.TransformToVisual(relativeTo);
        var topLeft = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

        // El centro pasa por la proyección (la guardia del 217 censura el cruce hecho a mano con .X/.Y):
        // el par (x+w/2, y+h/2) se envuelve con la misma regla que el resto de cruces del host.
        var center = UnoPointProjection.ToSdk(topLeft.X + (element.ActualWidth / 2), topLeft.Y + (element.ActualHeight / 2));
        (double cx, double cy) = UnoPointProjection.ToUno(center);
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
            WriteBackAnchors();
            _editor.StartConnectionCommand.Execute(port);
            _pendingSourceAnchor = port.Anchor;
        }
        else
        {
            _editor.FinishConnectionCommand.Execute(port);
            UpdatePortStatesAndWires();
        }
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
    /// Sin compatibilidad (o sin socket bajo el cursor), no hay objetivo y soltar cancela.
    /// </summary>
    private PortViewModel? FindHoverPort(NodeCardViewModel card, Windows.Foundation.Point screenPoint)
    {
        if (_editor?.PendingConnection?.Source is not { } source)
        {
            return null;
        }

        var candidates = source.Direction == PortDirection.Output
            ? card.Node.InputPorts
            : card.Node.OutputPorts;

        PortViewModel? best = null;
        double bestDistance = double.MaxValue;
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

            if (distance < bestDistance && distance <= 400)
            {
                best = port;
                bestDistance = distance;
            }
        }

        return best;
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
        var pending = _editor?.PendingConnection;
        if (pending is null || pending.Source is null)
        {
            return;
        }

        // El extremo móvil sigue al cursor; la geometría es la misma Bézier compartida.
        var target = pending.TargetLocation;
        var curve = ConnectionGeometry.BezierControlPoints(
            _pendingSourceAnchor, target, ConnectionGeometry.DefaultSpacing,
            pending.Source.Direction == PortDirection.Output
                ? ConnectionGeometry.FlowDirection.Forward
                : ConnectionGeometry.FlowDirection.Backward);

        _pendingWirePath = new Microsoft.UI.Xaml.Shapes.Path
        {
            Stroke = CanvasBrush("CanvasWireBrush"),
            StrokeThickness = 2.5,
            Opacity = 0.85,
            Data = CreateBezierGeometry(curve)
        };
        WireLayer.Children.Add(_pendingWirePath);
    }

    private Microsoft.UI.Xaml.Shapes.Path? _pendingWirePath;

    /// <summary>
    /// Los atajos del lienzo, con las mismas claves que el escritorio: la tabla compartida resuelve la
    /// combinación y ejecuta el comando canónico del núcleo. La caja de renombrado (y cualquier TextBox)
    /// consume sus teclas: no se las secuestra.
    /// </summary>
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_editor is null || e.OriginalSource is TextBox || _editor.Nodes.Any(n => n.IsEditingTitle))
        {
            return;
        }

        var key = MapKey(e.Key);
        if (key is null)
        {
            return;
        }

        var modifiers = EditorKeyboardShortcuts.Modifiers.None;
        if (IsKeyDown(Windows.System.VirtualKey.Control)) modifiers |= Ctrl;
        if (IsKeyDown(Windows.System.VirtualKey.Shift)) modifiers |= EditorKeyboardShortcuts.Modifiers.Shift;

        var command = EditorKeyboardShortcuts.Resolve(key.Value, modifiers);
        if (command is null)
        {
            return;
        }

        // Escape con cable pendiente lo CANCELA (y limpia la capa), antes que cualquier otra semántica.
        if (command == EditorKeyboardShortcuts.ShortcutKey.Escape && _editor.PendingConnection is not null)
        {
            if (_pendingWirePath is not null)
            {
                WireLayer.Children.Remove(_pendingWirePath);
                _pendingWirePath = null;
            }

            _editor.CancelConnectionCommand.Execute(null);
            UpdatePortStatesAndWires();
            e.Handled = true;
            return;
        }

        // El spotlight lo abre la VISTA (necesita el punto del grafo bajo el cursor guardado en el VM):
        // la tabla resuelve el comando, la vista hace el resto.
        if (command == EditorKeyboardShortcuts.ShortcutKey.Spotlight)
        {
            var spotlightPoint = GraphPointFromScreen(
                _lastPointerPosition == default ? new Windows.Foundation.Point(250, 200) : _lastPointerPosition);
            _editor.OpenSpotlight(spotlightPoint);
            e.Handled = true;
            return;
        }

        // La posición de referencia (pegar, spotlight) es el punto del grafo bajo el cursor.
        var cursor = _lastPointerPosition == default ? new Windows.Foundation.Point(250, 200) : _lastPointerPosition;
        if (EditorKeyboardShortcuts.Execute(command.Value, _editor, GraphPointFromScreen(cursor)))
        {
            e.Handled = true;
        }
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
