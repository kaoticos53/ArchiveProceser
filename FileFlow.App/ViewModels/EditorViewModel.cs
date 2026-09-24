using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.Services.UndoRedo;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using Material.Icons;

namespace FileFlow.App.ViewModels;

public sealed record BreadcrumbItem(string Name, string? NodeId, WorkflowGraph Graph);

public partial class EditorViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly PluginLoader _pluginLoader;
    private readonly Services.IVariableDiscoveryService _variableDiscoveryService;
    private readonly Services.INodeClipboardService _clipboardService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ILocalizationService _loc;
    private readonly IDialogService _dialogService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly LogViewModel? _logViewModel;

    /// <summary>Reloj del que cuelgan las duraciones con semántica (hoy, el fin del pulso de energía).</summary>
    private readonly TimeProvider _timeProvider;
    private readonly Action _preferencesChangedHandler;

    /// <summary>
    /// El latido que detecta que un subflujo abierto cambió en disco por fuera del lienzo. Ver
    /// <see cref="RefreshSubflowsChangedOnDisk"/>. Lo declara el registro
    /// (<see cref="FileFlow.App.Services.HeartbeatService"/>), que pone el reloj, el despacho y la entrega.
    /// </summary>
    private readonly FileFlow.App.Services.IHeartbeat _subflowWatchBeat;

    /// <summary>Despacha al hilo de la interfaz; en línea cuando no hay aplicación (pruebas, apagado).</summary>
    private readonly IUiDispatcher _ui;

    public Services.INodeClipboardService ClipboardService => _clipboardService;
    public Services.IVariableDiscoveryService VariableDiscoveryService => _variableDiscoveryService;
    public IUndoRedoService UndoRedoService => _undoRedoService;

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = [];
    public ObservableCollection<AnnotationViewModel> Annotations { get; } = [];
    public ObservableCollection<GroupViewModel> Groups { get; } = [];
    public ObservableCollection<object> CanvasDecorators { get; } = [];

    // --- Spotlight Quick-Add Search ---
    [ObservableProperty]
    private bool _isSpotlightOpen;

    [ObservableProperty]
    private string _spotlightSearchText = string.Empty;

    [ObservableProperty]
    private Point _spotlightScreenPosition = new(200, 200);

    [ObservableProperty]
    private Point _spotlightCanvasPosition = new(200, 200);

    [ObservableProperty]
    private NodeToolboxItem? _selectedSpotlightItem;

    public ObservableCollection<NodeToolboxItem> FilteredSpotlightItems { get; } = [];
    private readonly List<NodeToolboxItem> _allSpotlightItems = [];

    [ObservableProperty]
    private string _currentWorkflowTitle = "Root Workflow";

    [ObservableProperty]
    private string _globalOutputDir = @"C:\FileFlowOutput";

    [ObservableProperty]
    private PendingConnectionViewModel? _pendingConnection;

    [ObservableProperty]
    private Point _viewportLocation;

    [ObservableProperty]
    private Size _viewportSize;

    [ObservableProperty]
    private double _viewportZoom = 1.0;

    [RelayCommand]
    public void ZoomIn()
    {
        ViewportZoom = Math.Min(2.5, Math.Round(ViewportZoom + 0.05, 2));
    }

    [RelayCommand]
    public void ZoomOut()
    {
        ViewportZoom = Math.Max(0.2, Math.Round(ViewportZoom - 0.05, 2));
    }

    [RelayCommand]
    public void ResetZoom()
    {
        FitToScreen();
    }

    [RelayCommand]
    public void FitToScreen()
    {
        var (zoom, location) = EditorViewportCalculator.CalculateFitToScreen(Nodes);
        ViewportZoom = zoom;
        ViewportLocation = location;
    }

    [ObservableProperty]
    private bool _showGrid = true;

    /// <summary>
    /// Lo que la última acción dejó a medias y el usuario tiene que saber <b>aquí</b>, donde está el grafo y
    /// donde se puede arreglar: hoy, los cables que no se pudieron reconstruir al abrir un flujo, al pegar o
    /// al duplicar. Es efímero en el sentido de que se retira solo en cuanto la acción deja de estar —se
    /// deshace el pegado, se abre otro flujo, el usuario lo descarta— y nunca con un temporizador: esconder un
    /// aviso de pérdida por reloj deja al usuario sin la noticia justo cuando iba a leerla.
    ///
    /// <para>
    /// No es la única superficie del mismo hecho: la consola guarda el registro, que sobrevive al cartel. Y no
    /// es sólo un texto: cada cable perdido es una fila con su arreglo (ver <see cref="CanvasNoticeFixes"/>).
    /// </para>
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCanvasNotice))]
    private string? _canvasNotice;

    /// <summary>Si hay algo que el lienzo tenga que contar de la última acción.</summary>
    public bool HasCanvasNotice => !string.IsNullOrWhiteSpace(CanvasNotice);

    /// <summary>
    /// Los cables perdidos de la última acción que se pueden arreglar aquí: cada uno con el nodo al que hay que
    /// ir y la reconexión a un clic.
    ///
    /// Van <b>con</b> el aviso y no aparte, y por eso se ponen con <see cref="SetCanvasNotice"/>: el texto de
    /// una pérdida y las filas de otra no pueden acabar juntos, que es lo que pasaría si fueran dos estados.
    /// </summary>
    public ObservableCollection<DroppedConnectionFixViewModel> CanvasNoticeFixes { get; } = [];

    /// <summary>Si el aviso trae algo que hacer, además de algo que leer.</summary>
    public bool HasCanvasNoticeFixes => CanvasNoticeFixes.Count > 0;

    /// <summary>
    /// Cables que la última acción perdió y que <b>siguen</b> sin reconstruir: los que tienen fila —mientras no
    /// se arreglen— más los que no la tienen, que son los que el lienzo no puede arreglar porque su nodo no
    /// está. Baja al reconectar un cable y desaparece con el aviso.
    ///
    /// Se cuenta <b>aquí</b> y no en quien lo mira —la barra de estado— porque el dato sale de lo mismo que
    /// hace el aviso: las filas que hay y las pérdidas que no pudieron tener fila se saben en el momento de
    /// ponerlo, así que nadie más tiene que llevar la cuenta de nada.
    /// </summary>
    public int UnrebuiltConnectionsCount => CanvasNoticeFixes.Count + _lostWithoutFixCount;

    /// <summary>Si queda algún cable perdido por el que hacer algo.</summary>
    public bool HasUnrebuiltConnections => UnrebuiltConnectionsCount > 0;

    /// <summary>Pérdidas de la última acción que no pudieron tener fila, fijadas al poner el aviso.</summary>
    private int _lostWithoutFixCount;

    private void OnCanvasNoticeFixesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasCanvasNoticeFixes));
        NotifyUnrebuiltConnections();
    }

    private void NotifyUnrebuiltConnections()
    {
        OnPropertyChanged(nameof(UnrebuiltConnectionsCount));
        OnPropertyChanged(nameof(HasUnrebuiltConnections));
    }

    /// <summary>Retira el aviso del lienzo, que es cosa del usuario: se queda hasta que lo lea.</summary>
    [RelayCommand]
    public void DismissCanvasNotice() => ClearCanvasNotice();

    /// <summary>
    /// Pone el aviso y, con él, lo que se puede hacer de él: recibe las conexiones que se perdieron —el dato, no
    /// las filas ya hechas— y saca de ahí las dos cosas que el resto del mundo mira, las filas que se pueden
    /// arreglar y cuántas no pueden tenerlas.
    ///
    /// Recibir el <b>dato</b> y no las filas es lo que impide que un aviso acabe con las filas de otro o con un
    /// recuento que no le corresponde: es la única puerta, y quien la cruza no puede traer una cuenta y un
    /// detalle que no cuadren.
    /// </summary>
    private void SetCanvasNotice(string? message, IReadOnlyList<DroppedConnection>? lostConnections = null)
    {
        var lost = lostConnections ?? [];
        var fixes = BuildFixes(lost);

        _lostWithoutFixCount = lost.Count - fixes.Count;

        CanvasNoticeFixes.Clear();

        foreach (var fix in fixes)
        {
            CanvasNoticeFixes.Add(fix);
        }

        CanvasNotice = message;

        // Un aviso que sustituye a otro con el mismo texto y sin filas nuevas no dispara ninguna de las dos
        // notificaciones de arriba —el texto no cambió y la colección tampoco—, así que el recuento se avisa
        // siempre: es un número, repetirlo no cuesta nada y no hacerlo deja la barra de estado contando la
        // pérdida anterior.
        NotifyUnrebuiltConnections();
    }

    private void ClearCanvasNotice() => SetCanvasNotice(null);

    [ObservableProperty]
    private int _selectedNodesCount;

    public int TotalNodesCount => Nodes.Count;
    public int ConnectionsCount => Connections.Count;
    public string FormattedLocation => $"{ViewportLocation.X:F1}, {ViewportLocation.Y:F1}";
    public string FormattedZoom => $"{ViewportZoom:F2}x";

    partial void OnViewportLocationChanged(Point value)
    {
        OnPropertyChanged(nameof(FormattedLocation));
    }

    partial void OnViewportZoomChanged(double value)
    {
        OnPropertyChanged(nameof(FormattedZoom));
    }

    public void UpdateSelectedCount()
    {
        SelectedNodesCount = Nodes.Count(n => n.IsSelected);
    }

    private readonly Dictionary<string, List<ConnectionViewModel>> _connectionLookup = new(StringComparer.OrdinalIgnoreCase);

    public EditorViewModel(
        PluginLoader pluginLoader,
        Services.IVariableDiscoveryService? variableDiscoveryService = null,
        Services.INodeClipboardService? clipboardService = null,
        IUserPreferencesService? userPreferencesService = null,
        ILocalizationService? localizationService = null,
        IDialogService? dialogService = null,
        IUndoRedoService? undoRedoService = null,
        LogViewModel? logViewModel = null,
        TimeProvider? timeProvider = null,
        IUiDispatcher? uiDispatcher = null,
        FileFlow.App.Services.IHeartbeatService? heartbeats = null)
    {
        _pluginLoader = pluginLoader;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _ui = uiDispatcher ?? AvaloniaUiDispatcher.Instance;
        _variableDiscoveryService = variableDiscoveryService ?? new Services.VariableDiscoveryService();
        _clipboardService = clipboardService ?? new Services.NodeClipboardService(_pluginLoader);
        _userPreferencesService = userPreferencesService ?? UserPreferencesService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;
        _dialogService = dialogService ?? AvaloniaDialogService.Instance;
        _undoRedoService = undoRedoService ?? new UndoRedoService();
        _logViewModel = logViewModel;
        _globalOutputDir = _userPreferencesService.Preferences.DefaultGlobalOutputDir;
        CanvasNoticeFixes.CollectionChanged += OnCanvasNoticeFixesChanged;

        _preferencesChangedHandler = () =>
        {
            GlobalOutputDir = _userPreferencesService.Preferences.DefaultGlobalOutputDir;
        };
        _userPreferencesService.PreferencesChanged += _preferencesChangedHandler;

        _undoRedoService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IUndoRedoService.CanUndo) || e.PropertyName == nameof(IUndoRedoService.CanRedo))
            {
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        };

        Connections.CollectionChanged += (s, e) =>
        {
            RebuildConnectionLookup();
            UpdatePortConnectionStates();
            RefreshAllNodeFileVersions();
            OnPropertyChanged(nameof(ConnectionsCount));
        };
        Nodes.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (NodeViewModel node in e.NewItems)
                {
                    node.PropertyChanged += (ns, ne) =>
                    {
                        if (ne.PropertyName == nameof(NodeViewModel.IsSelected))
                        {
                            UpdateSelectedCount();
                        }
                    };
                }
            }
            UpdatePortConnectionStates();
            RefreshAllNodeFileVersions();
            OnPropertyChanged(nameof(TotalNodesCount));
            UpdateSelectedCount();
        };

        // Un subflujo puede editarse por fuera de este lienzo —en otra pestaña del editor, en otro
        // programa— y el contenedor vivo se quedaba con la frontera vieja hasta que alguien le preguntara.
        // El latido es la pregunta, y es barato porque no lee el archivo: sólo compara su huella.
        // Y eso es todo: el latido se declara y se arranca. El reloj inyectable, el despacho al hilo de la
        // interfaz y la entrega protegida los pone el registro, que es el único sitio del producto donde vive la
        // fontanería de un latido (antes: cuatro copias del mismo ritual).
        _subflowWatchBeat = (heartbeats ?? new FileFlow.App.Services.HeartbeatService(_timeProvider, _ui))
            .Declare(SubflowWatchBeat, SubflowWatchInterval, RunSubflowWatchTick)
            .Start();
    }

    public bool CanUndo => _undoRedoService.CanUndo;
    public bool CanRedo => _undoRedoService.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo()
    {
        if (_undoRedoService.CanUndo)
        {
            _undoRedoService.Undo();

            // El aviso contaba lo que hizo la acción que se acaba de deshacer: si el pegado ya no está, el
            // aviso tampoco puede quedar ahí, contando algo que el usuario ya revirtió.
            ClearCanvasNotice();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo()
    {
        if (_undoRedoService.CanRedo)
        {
            _undoRedoService.Redo();
        }
    }

    private void RebuildConnectionLookup()
    {
        _connectionLookup.Clear();
        foreach (var conn in Connections)
        {
            string key = $"{conn.Source.NodeOwner.Id}:{conn.Source.Name}";
            if (!_connectionLookup.TryGetValue(key, out var list))
            {
                list = [];
                _connectionLookup[key] = list;
            }
            list.Add(conn);
        }
    }

    public void UpdatePortConnectionStates()
    {
        foreach (var node in Nodes)
        {
            foreach (var inPort in node.InputPorts)
            {
                var connectedSources = Connections
                    .Where(c => c.Target == inPort)
                    .Select(c => $"{c.Source.NodeOwner.Title} (\"{c.Source.DisplayName}\")")
                    .ToList();
                inPort.UpdateConnectionState(connectedSources.Count > 0, string.Join(", ", connectedSources));
            }

            foreach (var outPort in node.OutputPorts)
            {
                var connectedTargets = Connections
                    .Where(c => c.Source == outPort)
                    .Select(c => $"{c.Target.NodeOwner.Title} (\"{c.Target.DisplayName}\")")
                    .ToList();
                outPort.UpdateConnectionState(connectedTargets.Count > 0, string.Join(", ", connectedTargets));
            }
        }
    }

    /// <summary>
    /// Descarta los cables del nodo que hayan quedado colgando: una conexión sólo es válida mientras sus dos
    /// extremos sigan siendo puertos que el nodo expone. Cuando un nodo reconstruye su topología (renombrar
    /// los puertos de un subflujo, quitar un caso de un switch, cambiar los puertos de un script) los puertos
    /// desaparecidos se llevan por delante el cable, en lugar de dejar una arista que apuntaría a un puerto
    /// inexistente y que el motor no volvería a trazar al reabrir el flujo.
    ///
    /// No pasa por el historial de deshacer: el cable no lo quita el usuario sino un cambio de topología, y
    /// los cambios de parámetro que lo provocan tampoco son reversibles desde el editor.
    /// </summary>
    public void RevalidateConnections(NodeViewModel node)
    {
        if (node == null) return;

        var related = Connections
            .Where(c => ReferenceEquals(c.Source.NodeOwner, node) || ReferenceEquals(c.Target.NodeOwner, node))
            .ToList();

        foreach (var connection in related)
        {
            bool stillExposed = connection.Source.NodeOwner.OutputPorts.Contains(connection.Source)
                                && connection.Target.NodeOwner.InputPorts.Contains(connection.Target);

            if (!stillExposed)
            {
                Connections.Remove(connection);
            }
        }
    }

    /// <summary>
    /// Cada cuánto se le pregunta a los contenedores de subflujo si su definición cambió en disco. La
    /// comprobación es una huella —fecha y tamaño del archivo, sin leerlo—, así que un segundo es barato; y es
    /// corto a propósito, porque editar el subflujo en otra pestaña y volver a mirarlo es el caso normal.
    ///
    /// Público para que la prueba de cadencia avance el reloj contra <b>este</b> periodo y no contra una copia.
    /// </summary>
    public static readonly TimeSpan SubflowWatchInterval = TimeSpan.FromSeconds(1);

    /// <summary>Nombre del latido en el registro: con él se busca, se mide su cadencia y se sabe cuál falló.</summary>
    public const string SubflowWatchBeat = "subflow-watch";

    /// <summary>
    /// El <b>latido</b> del vigilante de subflujos: pregunta a los contenedores vivos si su definición cambió
    /// en disco.
    ///
    /// <para><b>Público y sin argumentos para poder ejercitarlo desde las pruebas</b>, como el paso del barrido
    /// de la splash (hito 169): el temporizador sólo corre en la aplicación, así que sin una entrada alcanzable
    /// el camino del tick no se ejecuta nunca en el suite y una regresión ahí sólo se ve usando el producto.
    /// Es el <b>mismo</b> método que el reloj entrega —va nombrado en la propia programación del latido, ver el
    /// constructor—, no una copia que pueda divergir.</para>
    /// </summary>
    public void RunSubflowWatchTick() => RefreshSubflowsChangedOnDisk();

    /// <summary>
    /// Refresca los contenedores de subflujo cuya definición cambió en disco, para que el lienzo deje de
    /// mostrar una frontera que ya no existe: aparecen los puertos nuevos, se van los que ya no están con sus
    /// cables, y <b>los que siguen existiendo conservan los suyos</b> —los puertos se emparejan por nombre y
    /// los que sobreviven conservan su instancia, que es de quien cuelga el cable—.
    ///
    /// <para>
    /// Se pregunta en vez de vigilar el sistema de archivos: la huella que el resolutor ya necesita para no
    /// releer el archivo en cada pulsación de tecla responde «¿cambió?» sin leerlo, así que no hace falta un
    /// vigilante del sistema operativo por contenedor —ni sus fallos de red, ni su limpieza— para saber algo
    /// que ya se sabe preguntar. El refresco en sí pasa por <see cref="NodeViewModel.SyncSubflowPorts"/>, la
    /// misma puerta que usa el inspector: una sola regla decide qué puertos expone un contenedor.
    /// </para>
    ///
    /// <para>
    /// Lo que se pierde no se predice: se mide por diferencia contra el estado anterior a refrescar, porque
    /// quien descarta los cables huérfanos es la revalidación del lienzo y adivinar qué hará sería escribir
    /// esa regla por segunda vez.
    /// </para>
    ///
    /// <para>
    /// Se cuenta y se devuelve lo que cambió <b>para quien mira</b> —la topología del contenedor—, no lo que
    /// cambió en el disco: un origen que no se puede resolver, como un subflujo que se movió de sitio, responde
    /// «cambió» en cada latido porque el resolutor no memoriza lo que no pudo leer. Si cada intento contara, la
    /// consola se llenaría de un aviso por segundo sobre un contenedor que sigue exponiendo exactamente los
    /// mismos puertos.
    /// </para>
    /// </summary>
    public IReadOnlyList<NodeViewModel> RefreshSubflowsChangedOnDisk()
    {
        var refreshed = new List<NodeViewModel>();

        foreach (var node in Nodes.ToList())
        {
            if (!node.HasSubflowDefinitionChanged()) continue;

            var topologyBefore = PortNamesOf(node);
            var boundBefore = Connections.ToList();

            // Materializa y anuncia: el lienzo reconcilia sus puertos y revalida en el acto, así que al
            // volver de aquí el grafo ya es el nuevo —cables conservados, huérfanos fuera—.
            node.SyncSubflowPorts();

            if (SameTopology(topologyBefore, PortNamesOf(node)))
            {
                // El origen cambió —o dejó de resolverse— pero el contenedor expone lo mismo que antes: no hay
                // nada que contarle a nadie, y los cables no tienen por qué enterarse.
                continue;
            }

            refreshed.Add(node);
            AnnounceSubflowDefinitionChanged(node, [.. boundBefore.Where(connection => !Connections.Contains(connection))]);
        }

        return refreshed;
    }

    /// <summary>Nombres de puerto que el lienzo muestra, en su orden: lo que cambia para quien mira.</summary>
    private static (List<string> Inputs, List<string> Outputs) PortNamesOf(NodeViewModel node) =>
        ([.. node.InputPorts.Select(port => port.Name)], [.. node.OutputPorts.Select(port => port.Name)]);

    private static bool SameTopology(
        (List<string> Inputs, List<string> Outputs) left,
        (List<string> Inputs, List<string> Outputs) right) =>
        left.Inputs.SequenceEqual(right.Inputs) && left.Outputs.SequenceEqual(right.Outputs);

    /// <summary>
    /// Cuenta que la topología de un contenedor cambió porque su subflujo cambió en disco. En la consola
    /// <b>siempre</b> —es el registro de lo que le fue pasando al flujo sin que el usuario lo tocara— y en el
    /// lienzo <b>sólo</b> cuando el cambio se llevó por delante algún cable.
    ///
    /// La distinción no es cosmética: que un subflujo cambie por fuera es lo normal —se está editando en otra
    /// pestaña, o en otro editor—, y poner un cartel en cada guardado por un cambio que no rompió nada enseña
    /// a ignorar el cartel, que es justo lo que existe para evitar. Cuando sí rompió algo, el cable se cuenta
    /// con la misma frase que un puerto que falta, porque es el mismo hecho y quien lo lee tiene que poder
    /// reconectarlo.
    /// </summary>
    private void AnnounceSubflowDefinitionChanged(NodeViewModel node, IReadOnlyList<ConnectionViewModel> lostConnections)
    {
        _logViewModel?.AddLog(LogLevel.Information, _loc.GetFormattedString(
            "LogSubflowDefinitionChanged",
            "🔄 El subflujo '{0}' cambió en disco: el contenedor volvió a calcular sus puertos.",
            node.Title));

        if (lostConnections.Count == 0)
        {
            // El contenedor se puso al día solo y no rompió nada: queda el registro, y el cartel se reserva
            // para el cambio que sí hay que leer.
            return;
        }

        var lost = lostConnections.Select(DropDescriptionOf).ToList();

        foreach (var connection in lost)
        {
            LogLostConnection("LogConnectionLostWithSubflowChange", "🔌 La conexión {0} se perdió: {1}", connection);
        }

        // El contenedor sí se nombra en la cabecera —es el contexto del cambio, y no deja de ser verdad al
        // arreglar un cable—, pero los detalles se van a las filas, con su botón.
        SetCanvasNotice(
            _loc.GetFormattedString(
                "CanvasNoticeSubflowDefinitionChanged",
                "🔄 El subflujo '{0}' cambió en disco: se perdieron estas conexiones",
                node.Title),
            lost);
    }

    /// <summary>
    /// Cuenta en la consola un cable perdido: <b>una línea por motivo</b>, cada una con el nodo al que hay que
    /// ir para arreglarlo.
    ///
    /// Una línea por motivo y no una por cable porque un cable que falla por sus dos extremos son dos nodos
    /// los que hay que arreglar, y una sola frase con los dos motivos no puede llevar dos nodos: la fila de la
    /// consola abre el nodo que lleva, así que con dos motivos juntos habría que elegir a cuál se renuncia.
    /// </summary>
    private void LogLostConnection(string key, string fallback, DroppedConnection connection)
    {
        string endpoints = DroppedConnectionText.DescribeEndpoints(connection);

        foreach (var impediment in connection.Impediments)
        {
            var target = DroppedConnectionText.NodeToPointAt(impediment);

            _logViewModel?.AddNodeLog(
                LogLevel.Warning,
                _loc.GetFormattedString(key, fallback, endpoints, DroppedConnectionText.DescribeImpediment(_loc, impediment)),
                target?.NodeId,
                target?.NodeName);
        }
    }

    /// <summary>
    /// El cable que el refresco se llevó, con la forma que ya sabe contar <see cref="DroppedConnectionText"/>:
    /// sus dos extremos y el puerto que desapareció como motivo.
    ///
    /// El extremo culpable se <b>mide</b> —preguntando si el nodo sigue exponiendo ese puerto— en lugar de
    /// darse por sabido, y por eso el motivo que se cuenta es exactamente el que descartó el cable.
    /// </summary>
    private static DroppedConnection DropDescriptionOf(ConnectionViewModel connection) => new(
        EndpointOf(connection.Source),
        EndpointOf(connection.Target));

    private static DroppedConnectionEnd EndpointOf(PortViewModel port)
    {
        var exposed = port.Direction == PortDirection.Output ? port.NodeOwner.OutputPorts : port.NodeOwner.InputPorts;

        return new DroppedConnectionEnd(
            port.NodeOwner.Id,
            port.NodeOwner.Title,
            port.Name,
            exposed.Contains(port) ? DroppedConnectionEndProblem.None : DroppedConnectionEndProblem.MissingPort);
    }

    public void CreateConnection(PortViewModel source, PortViewModel target)
    {
        if (source == null || target == null || source == target) return;
        if (source.NodeOwner == target.NodeOwner) return;

        // Ensure Source is Output and Target is Input
        PortViewModel outputPort = source.Direction == PortDirection.Output ? source : target;
        PortViewModel inputPort = source.Direction == PortDirection.Output ? target : source;

        if (outputPort.Direction != PortDirection.Output || inputPort.Direction != PortDirection.Input)
            return;

        if (Connections.Any(c => c.Source == outputPort && c.Target == inputPort))
            return;

        using var tx = _undoRedoService.BeginTransaction($"Conectar {outputPort.NodeOwner.Title} -> {inputPort.NodeOwner.Title}");

        // Remove any existing connection to the same input port
        var existing = Connections.FirstOrDefault(c => c.Target == inputPort);
        if (existing != null)
        {
            Connections.Remove(existing);
            _undoRedoService.Record(new DeleteConnectionAction(this, existing));
        }

        var newConn = new ConnectionViewModel(outputPort, inputPort);
        Connections.Add(newConn);
        _undoRedoService.Record(new AddConnectionAction(this, newConn));
    }

    private static (PortViewModel? Source, PortViewModel? Target) ExtractPortsFromParameter(object? param)
    {
        if (param is PortViewModel singlePort)
        {
            return (null, singlePort);
        }

        if (param is System.Runtime.CompilerServices.ITuple tuple && tuple.Length > 0)
        {
            PortViewModel? p1 = tuple[0] as PortViewModel;
            PortViewModel? p2 = tuple.Length > 1 ? tuple[1] as PortViewModel : null;
            return (p1, p2);
        }

        return (null, null);
    }

    [RelayCommand]
    public void StartConnection(object? source)
    {
        var (p1, p2) = ExtractPortsFromParameter(source);
        var port = p1 ?? p2;
        if (port != null)
        {
            PendingConnection = new PendingConnectionViewModel(port);
            ApplyPortCompatibilityHighlight(port);
        }
    }

    [RelayCommand]
    public void FinishConnection(object? target)
    {
        var (p1, p2) = ExtractPortsFromParameter(target);
        PortViewModel? sourcePort = p1 ?? PendingConnection?.Source;
        PortViewModel? targetPort = p2;

        if (p1 != null && p2 == null)
        {
            if (PendingConnection?.Source != null && PendingConnection.Source != p1)
            {
                sourcePort = PendingConnection.Source;
                targetPort = p1;
            }
            else
            {
                targetPort = p1;
            }
        }

        if (sourcePort != null && targetPort != null && sourcePort != targetPort)
        {
            CreateConnection(sourcePort, targetPort);
        }
        if (PendingConnection != null)
        {
            PendingConnection.IsVisible = false;
        }
        PendingConnection = null;
        ClearPortCompatibilityHighlight();
    }

    [RelayCommand]
    public void CancelConnection()
    {
        if (PendingConnection != null)
        {
            PendingConnection.IsVisible = false;
        }
        PendingConnection = null;
        ClearPortCompatibilityHighlight();
    }

    /// <summary>
    /// Marca cada puerto del lienzo con su compatibilidad respecto al puerto que se está arrastrando, para
    /// que la tarjeta pueda resaltar los destinos válidos y atenuar el resto mientras se dibuja el cable.
    /// </summary>
    private void ApplyPortCompatibilityHighlight(PortViewModel source)
    {
        foreach (var port in AllPorts())
        {
            port.IsDragActive = true;
            port.ApplyDragHighlight(source);
        }
    }

    /// <summary>Devuelve todos los puertos del lienzo al estado de reposo (fin o cancelación del arrastre).</summary>
    public void ClearPortCompatibilityHighlight()
    {
        foreach (var port in AllPorts())
        {
            port.ClearDragHighlight();
        }
    }

    private IEnumerable<PortViewModel> AllPorts()
        => Nodes.SelectMany(n => n.InputPorts.Concat(n.OutputPorts));

    [RelayCommand]
    public void DisconnectConnector(object? connector)
    {
        var (p1, p2) = ExtractPortsFromParameter(connector);
        var port = p1 ?? p2;

        if (p1 != null && p2 != null)
        {
            var specificConn = Connections.FirstOrDefault(c => (c.Source == p1 && c.Target == p2) || (c.Source == p2 && c.Target == p1));
            if (specificConn != null)
            {
                Connections.Remove(specificConn);
                _undoRedoService.Record(new DeleteConnectionAction(this, specificConn));
                return;
            }
        }

        if (port != null)
        {
            var removeList = Connections.Where(c => c.Source == port || c.Target == port).ToList();
            if (removeList.Count > 0)
            {
                using var tx = _undoRedoService.BeginTransaction("Desconectar puerto");
                foreach (var conn in removeList)
                {
                    Connections.Remove(conn);
                    _undoRedoService.Record(new DeleteConnectionAction(this, conn));
                }
            }
        }
    }

    [RelayCommand]
    public void DeleteNode(object? nodeParam)
    {
        if (nodeParam is NodeViewModel node)
        {
            RemoveNodeWithConnections(node);
        }
    }

    [RelayCommand]
    public void DeleteConnection(object? connectionParam)
    {
        if (connectionParam is ConnectionViewModel conn)
        {
            Connections.Remove(conn);
            _undoRedoService.Record(new DeleteConnectionAction(this, conn));
        }
    }

    private int _maxZIndex = 0;

    public void BringToFront(NodeViewModel node)
    {
        if (node == null) return;
        if (node.ZIndex == _maxZIndex && _maxZIndex > 0) return;
        node.ZIndex = ++_maxZIndex;
    }

    private void OnNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is NodeViewModel nodeVm && e.PropertyName == nameof(NodeViewModel.IsSelected) && nodeVm.IsSelected)
        {
            SelectedNode = nodeVm;
            BringToFront(nodeVm);
        }
    }

    public void RemoveNodeWithConnections(NodeViewModel node)
    {
        if (node == null) return;
        var relatedConnections = Connections
            .Where(c => c.Source.NodeOwner == node || c.Target.NodeOwner == node)
            .ToList();

        foreach (var conn in relatedConnections)
        {
            Connections.Remove(conn);
        }

        node.PropertyChanged -= OnNodePropertyChanged;
        Nodes.Remove(node);
        _undoRedoService.Record(new DeleteNodesAction(this, [node], relatedConnections));
    }

    private List<NodeViewModel> ResolveTargetNodes(object? parameter)
    {
        if (parameter is NodeViewModel singleNode)
        {
            if (singleNode.IsSelected)
            {
                var selected = Nodes.Where(n => n.IsSelected).ToList();
                if (selected.Count > 1 && selected.Contains(singleNode))
                {
                    return selected;
                }
            }
            return [singleNode];
        }

        var targets = Nodes.Where(n => n.IsSelected).ToList();
        if (targets.Count == 0 && SelectedNode != null)
        {
            targets.Add(SelectedNode);
        }
        return targets;
    }

    [RelayCommand]
    public void DeleteSelectedNodes(object? parameter = null)
    {
        var targets = ResolveTargetNodes(parameter);
        if (targets.Count == 0) return;

        var targetIds = new HashSet<string>(targets.Select(t => t.Id), StringComparer.OrdinalIgnoreCase);
        var relatedConnections = Connections
            .Where(c => targetIds.Contains(c.Source.NodeOwner.Id) || targetIds.Contains(c.Target.NodeOwner.Id))
            .ToList();

        foreach (var conn in relatedConnections)
        {
            Connections.Remove(conn);
        }

        foreach (var node in targets)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            Nodes.Remove(node);
        }

        _undoRedoService.Record(new DeleteNodesAction(this, targets, relatedConnections));
    }

    [RelayCommand]
    public void CopySelectedNodes(object? parameter = null)
    {
        var targets = ResolveTargetNodes(parameter);
        if (targets.Count > 0)
        {
            _clipboardService.Copy(targets, Connections);
        }
    }

    [RelayCommand]
    public void CutSelectedNodes(object? parameter = null)
    {
        var targets = ResolveTargetNodes(parameter);
        if (targets.Count > 0)
        {
            _clipboardService.Copy(targets, Connections);
            DeleteSelectedNodes(targets.Count == 1 ? targets[0] : null);
        }
    }

    [RelayCommand]
    public void PasteNodes(object? positionParam = null)
    {
        Point? targetPoint = null;
        if (positionParam is Point pt)
        {
            targetPoint = pt;
        }

        using var tx = _undoRedoService.BeginTransaction("Pegar Nodos");
        var result = _clipboardService.Paste(this, targetPoint);
        RecordPastedNodes(result.Nodes, "Pegar Nodos");
        AnnounceWhatCouldNotBeRebuilt(result.Report);
        if (result.Nodes.Count > 0)
        {
            SelectedNode = result.Nodes[^1];
        }
    }

    [RelayCommand]
    public void DuplicateSelectedNodes(object? parameter = null)
    {
        var targets = ResolveTargetNodes(parameter);
        if (targets.Count > 0)
        {
            using var tx = _undoRedoService.BeginTransaction("Duplicar Nodos");
            var result = _clipboardService.Duplicate(targets, Connections, this);
            RecordPastedNodes(result.Nodes, "Duplicar Nodos");
            AnnounceWhatCouldNotBeRebuilt(result.Report);
            if (result.Nodes.Count > 0)
            {
                SelectedNode = result.Nodes[^1];
            }
        }
    }

    /// <summary>
    /// Inscribe en el historial los nodos que acaban de entrar al lienzo por un pegado o una duplicación,
    /// como una sola acción.
    ///
    /// Sin esto el pegado no dejaba nada que deshacer por los nodos: los cables que reconstruye se
    /// registraban solos —pasan por <see cref="CreateConnection"/>—, pero los nodos entraban al lienzo sin
    /// registro, así que un Ctrl+Z tras un Ctrl+V deshacía <b>la acción anterior</b> y dejaba los nodos
    /// pegados donde estaban. Los cables no se repiten aquí: <see cref="AddNodesAction.Undo"/> retira
    /// además los que toquen a estos nodos.
    /// </summary>
    private void RecordPastedNodes(IReadOnlyList<NodeViewModel> pastedNodes, string description)
    {
        if (pastedNodes.Count > 0)
        {
            _undoRedoService.Record(new AddNodesAction(this, pastedNodes, description: description));
        }
    }

    /// <summary>
    /// Cuenta los cables que una reconstrucción no pudo rehacer —al abrir un flujo, al pegar y al duplicar— en
    /// el lienzo, que es donde está el grafo y donde se pueden arreglar, y en la consola, que es el registro
    /// que queda.
    ///
    /// El cartel lleva sólo la <b>cabecera</b> —qué pasó— y el detalle de cada cable vive en su fila, junto al
    /// botón que lo arregla. Antes el texto enumeraba los cables perdidos, y eso tiene un defecto que se ve en
    /// cuanto se arregla uno: la frase sigue contando lo que ya no es verdad, y reescribirla es llevar la
    /// cuenta en dos sitios.
    ///
    /// Un resultado sano <b>retira</b> el aviso anterior en vez de dejar el de la vez pasada: el aviso cuenta
    /// la última acción, y uno viejo sobre un grafo que ya no es el que se ve es una mentira.
    /// </summary>
    private void AnnounceWhatCouldNotBeRebuilt(ConnectionRebuildReport report)
    {
        if (report.IsComplete)
        {
            ClearCanvasNotice();
            return;
        }

        foreach (var connection in report.DroppedConnections)
        {
            LogLostConnection("LogDroppedConnection", "🔌 No se pudo reconstruir la conexión {0}: {1}", connection);
        }

        SetCanvasNotice(
            _loc.GetString("CanvasNoticeLostConnections", "🔌 No se pudieron reconstruir estas conexiones"),
            report.DroppedConnections);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El arreglo de un cable perdido, en el propio aviso
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Convierte cada cable perdido en una fila que se puede pulsar: el nodo cuyo puerto falta, el puerto
    /// vigente que más se le parece y los dos botones.
    ///
    /// Sólo se ofrece lo que se puede cumplir. Hay fila si el nodo del puerto que falta está en el lienzo —a un
    /// nodo que no se pudo crear no se puede ir—; hay botón de reconectar si además el otro extremo existe o se
    /// le puede proponer un puerto; y hay propuesta si algún puerto vigente se parece lo suficiente como para no
    /// ser una trampa (ver <see cref="PortNameProposal"/>).
    /// </summary>
    private IReadOnlyList<DroppedConnectionFixViewModel> BuildFixes(IReadOnlyList<DroppedConnection> lostConnections)
    {
        var fixes = new List<DroppedConnectionFixViewModel>();
        var lookup = Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var connection in lostConnections)
        {
            var output = ResolveEnd(connection.Source, isOutput: true, lookup);
            var input = ResolveEnd(connection.Target, isOutput: false, lookup);

            // La fila se ancla en el primer extremo cuyo puerto falta: es el que hay que mirar. Si el cable no
            // perdió ningún puerto —el problema es un nodo que no está— no hay fila, porque no hay a dónde ir.
            if (output is not { Existing: null } && input is not { Existing: null })
            {
                continue;
            }

            var anchor = output is { Existing: null } ? output : input!;

            PortViewModel? proposedOutput = output is { Existing: null } ? ProposedPortFor(output) : null;
            PortViewModel? proposedInput = input is { Existing: null } ? ProposedPortFor(input) : null;
            PortViewModel? liveOutput = output?.Existing ?? proposedOutput;
            PortViewModel? liveInput = input?.Existing ?? proposedInput;
            bool canReconnect = liveOutput != null && liveInput != null;

            fixes.Add(new DroppedConnectionFixViewModel(
                anchor.Node,
                anchor.PortName,
                DroppedConnectionText.Describe(_loc, connection),
                (anchor.IsOutput ? proposedOutput : proposedInput)?.Name,
                node => FocusNode(node),
                ReconnectLostConnection,
                _loc)
            {
                CanReconnect = canReconnect,
                LiveEnds = canReconnect ? (liveOutput!, liveInput!) : null
            });
        }

        return fixes;
    }

    /// <summary>
    /// El extremo del cable tal y como está hoy en el lienzo: su nodo —si sigue ahí—, el puerto que nombraba y,
    /// si ese puerto existe, el puerto mismo. Un puerto que falta se queda sin resolver a propósito: proponer
    /// uno es decisión de <see cref="BuildFixes"/>, no de esta lectura.
    /// </summary>
    private static LostEnd? ResolveEnd(
        DroppedConnectionEnd end,
        bool isOutput,
        IReadOnlyDictionary<string, NodeViewModel> lookup)
    {
        if (!lookup.TryGetValue(end.NodeId, out var node))
        {
            return null;
        }

        var ports = isOutput ? node.OutputPorts : node.InputPorts;

        return new LostEnd(
            node,
            end.PortName,
            isOutput,
            ports.FirstOrDefault(port => port.Name.Equals(end.PortName, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// El puerto vigente más parecido al que falta, de entre los que se pueden usar: una entrada que ya tiene
    /// cable no es candidata, porque reconectar ahí tiraría el cable que ya estaba y el usuario no lo pidió.
    /// </summary>
    private PortViewModel? ProposedPortFor(LostEnd end)
    {
        var candidates = (end.IsOutput ? end.Node.OutputPorts : end.Node.InputPorts)
            .Where(port => port.Direction == PortDirection.Output || Connections.All(connection => connection.Target != port))
            .ToList();

        string? proposed = PortNameProposal.Suggest(end.PortName, candidates.Select(port => port.Name));

        return proposed == null
            ? null
            : candidates.First(port => port.Name.Equals(proposed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Lleva la vista al nodo: lo selecciona y lo centra. Es la mitad del aviso que no depende de adivinar
    /// nada —el puerto que falta se ve al llegar— y la que queda cuando no hay nada que proponer.
    /// </summary>
    [RelayCommand]
    public void FocusNode(NodeViewModel? node)
    {
        if (node == null) return;

        foreach (var other in Nodes)
        {
            other.IsSelected = ReferenceEquals(other, node);
        }

        SelectedNode = node;
        BringToFront(node);
        ViewportLocation = EditorViewportCalculator.CenterOn(node, ViewportZoom);
    }

    /// <summary>
    /// Vuelve a trazar el cable perdido contra los puertos de la fila y retira esa fila. Si no se pudo trazar
    /// —el motor del lienzo rechaza una conexión por sus puertos— la fila <b>se queda</b>: quitarla sería decir
    /// que se arregló.
    /// </summary>
    public void ReconnectLostConnection(DroppedConnectionFixViewModel fix)
    {
        if (fix?.LiveEnds is not { } ends) return;

        CreateConnection(ends.Output, ends.Input);

        if (!Connections.Any(connection => connection.Source == ends.Output && connection.Target == ends.Input))
        {
            return;
        }

        CanvasNoticeFixes.Remove(fix);

        // Cuando ya no queda ningún cable perdido, el aviso tampoco: quedarse contando lo que ya está
        // arreglado es la misma mentira que un aviso viejo sobre otro grafo.
        //
        // Que no queden <b>filas</b> no es que no queden pérdidas: un cable cuyo nodo no está se pierde sin
        // ofrecer nada que pulsar, y retirar el aviso al arreglar el último arreglable escondería justo el
        // cable que el usuario no puede recuperar. El aviso se retira cuando ya no queda nada perdido.
        if (CanvasNoticeFixes.Count == 0 && _lostWithoutFixCount == 0)
        {
            ClearCanvasNotice();
        }
    }

    /// <summary>Un extremo del cable perdido, ya resuelto contra el lienzo.</summary>
    private sealed record LostEnd(NodeViewModel Node, string PortName, bool IsOutput, PortViewModel? Existing);

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    [RelayCommand]
    public void ClearGraph()
    {
        Connections.Clear();
        foreach (var node in Nodes)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            node.Dispose();
        }
        Nodes.Clear();
        Annotations.Clear();
        Groups.Clear();
        CanvasDecorators.Clear();
        SelectedNode = null;
        ClearCanvasNotice();
        _undoRedoService.Clear();
    }

    public AnnotationViewModel AddAnnotation(Point? position = null, string title = "Nota", string content = "", string color = "#FEF08A")
    {
        var loc = position ?? new Point(Math.Max(50, -ViewportLocation.X + 150), Math.Max(50, -ViewportLocation.Y + 150));
        var annotation = new AnnotationViewModel(title, content, loc, color: color)
        {
            ParentEditor = this
        };
        Annotations.Add(annotation);
        CanvasDecorators.Add(annotation);
        _undoRedoService.Record(new AddAnnotationAction(this, annotation));
        return annotation;
    }

    [RelayCommand]
    public void AddNewAnnotation()
    {
        AddAnnotation();
    }

    [RelayCommand]
    public void DeleteAnnotation(AnnotationViewModel? annotation)
    {
        if (annotation != null)
        {
            Annotations.Remove(annotation);
            CanvasDecorators.Remove(annotation);
            _undoRedoService.Record(new DeleteAnnotationAction(this, annotation));
        }
    }

    public GroupViewModel AddGroup(Point? position = null, string title = "Grupo de Nodos", double width = 450, double height = 320, string color = "#3B82F6", IEnumerable<string>? nodeIds = null)
    {
        var loc = position ?? new Point(Math.Max(50, -ViewportLocation.X + 100), Math.Max(50, -ViewportLocation.Y + 100));
        var group = new GroupViewModel(title, loc, width, height, color, nodeIds)
        {
            ParentEditor = this
        };
        Groups.Add(group);
        CanvasDecorators.Insert(0, group);
        _undoRedoService.Record(new AddGroupAction(this, group));
        return group;
    }

    [RelayCommand]
    public void AddNewGroup()
    {
        AddGroup();
    }

    [RelayCommand]
    public void GroupSelectedNodes()
    {
        var selected = Nodes.Where(n => n.IsSelected).ToList();
        if (selected.Count == 0)
        {
            AddNewGroup();
            return;
        }

        double minX = selected.Min(n => n.Location.X) - 30;
        double minY = selected.Min(n => n.Location.Y) - 50;
        double maxX = selected.Max(n => n.Location.X + n.Width) + 30;
        double maxY = selected.Max(n => n.Location.Y + 250) + 30;

        AddGroup(new Point(minX, minY), "Grupo", Math.Max(300, maxX - minX), Math.Max(200, maxY - minY), "#3B82F6", selected.Select(n => n.Id));
    }

    [RelayCommand]
    public void DeleteGroup(GroupViewModel? group)
    {
        if (group != null)
        {
            Groups.Remove(group);
            CanvasDecorators.Remove(group);
            _undoRedoService.Record(new DeleteGroupAction(this, group));
        }
    }

    public NodeViewModel? AddNode(string nodeTypeName, Point position)
    {
        IFlowNode? nodeInstance = _pluginLoader.CreateNodeInstance(nodeTypeName);
        if (nodeInstance == null) return null;

        var nodeVm = new NodeViewModel(nodeInstance, position)
        {
            ParentEditor = this
        };
        nodeVm.PropertyChanged += OnNodePropertyChanged;
        Nodes.Add(nodeVm);
        UserPreferencesService.Instance.IncrementNodeUsage(nodeTypeName);
        _undoRedoService.Record(new AddNodesAction(this, [nodeVm]));
        return nodeVm;
    }

    public void ClearDebugStates()
    {
        foreach (var node in Nodes)
        {
            node.ClearDebugData();
        }
    }

    public void ResetAllNodeMetrics()
    {
        foreach (var node in Nodes)
        {
            node.UpdateTelemetryStats(FileFlow.Sdk.Telemetry.NodeTelemetryStats.Empty(node.Id));
        }
    }

    [RelayCommand]
    public async Task OpenWorkflowSettings()
    {
        try
        {
            var win = new Views.Components.WorkflowSettingsWindow(GlobalOutputDir);
            var result = App.MainWindow != null ? await win.ShowDialog<bool>(App.MainWindow) : false;
            if (result)
            {
                GlobalOutputDir = win.GlobalOutputDir;
            }
        }
        catch (Exception ex)
        {
            string msg = string.Format(_loc.GetString("Msg_OpenSettingsError", "Error al abrir la Configuración del Flujo: {0}"), ex.Message);
            string title = _loc.GetString("Error", "Error");
            _dialogService.ShowError(msg, title);
        }
    }

    [RelayCommand]
    public void BrowseGlobalOutputDir()
    {
        var fileDialogService = new FileDialogService();
        var selectedFolder = fileDialogService.ShowFolderBrowserDialog("Seleccionar Ruta de Salida Global");
        if (!string.IsNullOrWhiteSpace(selectedFolder))
        {
            GlobalOutputDir = selectedFolder;
        }
    }

    public WorkflowGraph ExportToGraphModel(string name = "FileFlow Workflow")
    {
        return WorkflowGraphSerializer.Export(Nodes, Connections, GlobalOutputDir, name, Annotations, Groups);
    }

    /// <summary>
    /// Reconstruye el grafo en el lienzo y devuelve lo que no se pudo reconstruir: un cable cuyo puerto ya no
    /// existe se descarta, y este es el punto por el que el editor lo sabe. Contárselo al usuario no es cosa
    /// del lienzo —los grafos que carga desde memoria (subflujos, migas de pan) los exportó esta misma sesión
    /// y no pierden cables—, sino de quien abre un archivo.
    /// </summary>
    public ConnectionRebuildReport LoadFromGraphModel(WorkflowGraph graph)
    {
        ClearGraph();

        if (!string.IsNullOrWhiteSpace(graph.GlobalOutputDir))
        {
            GlobalOutputDir = graph.GlobalOutputDir;
        }

        var importResult = WorkflowGraphSerializer.Import(
            graph,
            _pluginLoader,
            this,
            registerNodeCallback: nodeVm =>
            {
                nodeVm.PropertyChanged += OnNodePropertyChanged;
                Nodes.Add(nodeVm);
            },
            registerConnectionCallback: conn =>
            {
                Connections.Add(conn);
            },
            registerAnnotationCallback: annotVm =>
            {
                Annotations.Add(annotVm);
                CanvasDecorators.Add(annotVm);
            },
            registerGroupCallback: groupVm =>
            {
                Groups.Add(groupVm);
                CanvasDecorators.Insert(0, groupVm);
            }
        );

        // Abrir un archivo es donde más cables se pierden —el flujo viene de otra máquina, o su subflujo
        // cambió de sitio— y hasta ahora sólo lo contaba la consola, porque «no había acción del lienzo a la
        // que apuntar». Con el arreglo a un clic, la hay: el informe se cuenta donde se puede hacer algo.
        AnnounceWhatCouldNotBeRebuilt(importResult);

        RefreshAllNodeFileVersions();
        _undoRedoService.Clear();

        return importResult;
    }

    public void RefreshAllNodeFileVersions()
    {
        foreach (var node in Nodes)
        {
            foreach (var param in node.Parameters)
            {
                if (param.IsFileVersionSelector)
                {
                    param.RefreshAvailableVersions();
                }
            }
        }
    }

    public List<FileFlow.App.Models.VariableGroupItem> GetUpstreamAvailableVariables(NodeViewModel targetNode)
    {
        return _variableDiscoveryService.GetAvailableVariables(targetNode, Connections);
    }

    [RelayCommand]
    public void OpenSubWorkflow(NodeViewModel node)
    {
        if (node == null) return;

        // Save current graph state into breadcrumb
        var currentGraph = ExportToGraphModel();
        Breadcrumbs.Add(new BreadcrumbItem(CurrentWorkflowTitle, node.Id, currentGraph));

        CurrentWorkflowTitle = node.Title;

        // Load inner graph if exists, or start fresh sub-graph
        if (!string.IsNullOrWhiteSpace(node.InnerGraphJson))
        {
            try
            {
                // Por el mismo lector que todo lo demás: un grafo que viaja dentro de un flujo es un flujo, y
                // leerlo con opciones propias era tener un segundo lector del mismo formato.
                var innerGraph = WorkflowGraph.FromJson(node.InnerGraphJson);
                if (innerGraph != null)
                {
                    LoadFromGraphModel(innerGraph);
                    return;
                }
            }
            catch
            {
                // Fallback to clear
            }
        }

        ClearGraph();
    }

    [RelayCommand]
    public void NavigateBreadcrumb(BreadcrumbItem target)
    {
        if (target == null) return;

        int index = Breadcrumbs.IndexOf(target);
        if (index < 0) return;

        // Restore target graph
        LoadFromGraphModel(target.Graph);
        CurrentWorkflowTitle = target.Name;

        // Remove all subsequent breadcrumbs
        while (Breadcrumbs.Count > index)
        {
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }
    }

    public void UpdateEdgeDispatched(string sourceNodeId, string portName, int count)
    {
        string key = $"{sourceNodeId}:{portName}";
        if (_connectionLookup.TryGetValue(key, out var list))
        {
            foreach (var conn in list)
            {
                conn.UpdateCount(count);
                PulseConnectionEnergy(conn);
            }
        }
    }

    /// <summary>
    /// Energiza un cable durante unos instantes (flujo de energía animado mientras los datos viajan) y lo
    /// devuelve a reposo. Reutiliza un único temporizador por cable para que ráfagas consecutivas no dejen
    /// animaciones colgadas.
    ///
    /// <para>Devuelve el vencimiento del pulso. Nadie en la aplicación necesita esperarlo —el cable se apaga
    /// solo—, pero quien sí lo necesita es el test: esperar esa tarea es lo único que convierte «el vencimiento
    /// obsoleto no apagó el pulso nuevo» en una comprobación en lugar de una carrera contra el reloj.</para>
    /// </summary>
    public Task PulseConnectionEnergy(ConnectionViewModel connection, int durationMs = 900)
    {
        if (durationMs <= 0)
        {
            connection.IsExecuting = false;
            return Task.CompletedTask;
        }

        connection.LastDispatchedCount++;
        connection.IsExecuting = true;

        int generation = connection.LastDispatchedCount;
        return ExpireConnectionPulseAsync(connection, generation, durationMs);
    }

    /// <summary>
    /// Apaga el pulso cuando su tiempo se acaba.
    ///
    /// <para>La espera usa el reloj <b>inyectado</b> y no <c>Task.Delay</c> a secas porque la duración es
    /// semántica —«el cable se apaga cuando los datos han dejado de pasar»— y con el reloj del sistema probarla
    /// cuesta la espera entera por caso, así que no se probaba: el vencimiento programado era lo único del pulso
    /// sin red. Con una fuente de tiempo manual, avanzar el reloj <i>es</i> el paso que se mide.</para>
    /// </summary>
    private async Task ExpireConnectionPulseAsync(ConnectionViewModel connection, int generation, int durationMs)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(durationMs), _timeProvider).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Un reloj que no puede programar no puede dejar el cable encendido para siempre: se deja el pulso
            // como está (lo apagará ClearConnectionEnergy al terminar la ejecución) en lugar de propagar.
            System.Diagnostics.Debug.WriteLine($"[EditorViewModel] No se pudo programar el fin del pulso: {ex.Message}");
            return;
        }

        _ui.Post(() => CompleteConnectionPulse(connection, generation));
    }

    /// <summary>
    /// Apaga la energía de un cable al vencer su pulso... salvo que ya haya empezado otro más reciente. La
    /// comparación de generación es lo que evita que una ráfaga de datos deje el cable apagado antes de
    /// tiempo (o encendido para siempre) cuando los pulsos se solapan.
    /// </summary>
    public static void CompleteConnectionPulse(ConnectionViewModel connection, int generation)
    {
        if (connection.LastDispatchedCount == generation)
        {
            connection.IsExecuting = false;
        }
    }

    /// <summary>Apaga el flujo de energía de todos los cables (fin de ejecución o parada).</summary>
    public void ClearConnectionEnergy()
    {
        foreach (var connection in Connections)
        {
            connection.IsExecuting = false;
        }
    }

    public void PopulateSpotlightItems()
    {
        _allSpotlightItems.Clear();
        var types = _pluginLoader.UniqueNodeTypes.ToList();
        foreach (var type in types)
        {
            string typeName = type.FullName ?? type.Name;
            IFlowNode? sampleInstance = null;
            try
            {
                sampleInstance = _pluginLoader.CreateNodeInstance(typeName);
            }
            catch { }

            var defAttr = type.GetCustomAttribute<NodeDefinitionAttribute>();
            string name = _loc.GetString(type.Name + "_Name", sampleInstance?.Name ?? defAttr?.Name ?? type.Name);
            if (name.EndsWith("_Name", StringComparison.OrdinalIgnoreCase) && sampleInstance != null && !string.IsNullOrWhiteSpace(sampleInstance.Name))
            {
                name = sampleInstance.Name;
            }

            string category = sampleInstance?.Category ?? defAttr?.Category ?? "General";
            string locCategory = _loc.GetString($"Category_{category}", category);

            string description = _loc.GetString(type.Name + "_Desc", sampleInstance?.Description ?? defAttr?.Description ?? string.Empty);
            if (description.EndsWith("_Desc", StringComparison.OrdinalIgnoreCase) && sampleInstance != null && !string.IsNullOrWhiteSpace(sampleInstance.Description))
            {
                description = sampleInstance.Description;
            }

            MaterialIconKind icon = NodeIconResolver.GetIconForNodeType(typeName);
            var role = defAttr?.Role ?? PipelineRole.Transform;
            var tags = defAttr?.Tags ?? Array.Empty<string>();
            var subCategory = defAttr?.SubCategory ?? string.Empty;
            string localizedRole = _loc.GetString($"Role_{role}", role.ToString());

            var item = new NodeToolboxItem(
                name,
                locCategory,
                description,
                typeName,
                icon,
                false,
                0,
                role,
                tags,
                subCategory,
                localizedRole
            );
            _allSpotlightItems.Add(item);
        }
        UpdateFilteredSpotlightItems();
    }

    partial void OnSpotlightSearchTextChanged(string value)
    {
        UpdateFilteredSpotlightItems();
    }

    private void UpdateFilteredSpotlightItems()
    {
        FilteredSpotlightItems.Clear();
        var query = SpotlightSearchText?.Trim() ?? string.Empty;
        var matches = string.IsNullOrEmpty(query)
            ? _allSpotlightItems
            : _allSpotlightItems.Where(i =>
                i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (i.Tags != null && i.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))));

        foreach (var item in matches.Take(30))
        {
            FilteredSpotlightItems.Add(item);
        }

        SelectedSpotlightItem = FilteredSpotlightItems.FirstOrDefault();
    }

    [RelayCommand]
    public void OpenSpotlight(Point? canvasPosition = null)
    {
        PopulateSpotlightItems();
        SpotlightCanvasPosition = canvasPosition ?? new Point(
            ViewportLocation.X + (ViewportSize.Width > 0 ? (ViewportSize.Width / (2 * (ViewportZoom > 0 ? ViewportZoom : 1.0))) : 200),
            ViewportLocation.Y + (ViewportSize.Height > 0 ? (ViewportSize.Height / (2 * (ViewportZoom > 0 ? ViewportZoom : 1.0))) : 200)
        );
        SpotlightSearchText = string.Empty;
        IsSpotlightOpen = true;
    }

    [RelayCommand]
    public void CloseSpotlight()
    {
        IsSpotlightOpen = false;
        SpotlightSearchText = string.Empty;
    }

    public bool HasBreadcrumbs => Breadcrumbs.Count > 1;

    public void InitializeBreadcrumbs()
    {
        Breadcrumbs.Clear();
        var rootGraph = WorkflowGraphSerializer.Export(Nodes, Connections, GlobalOutputDir, CurrentWorkflowTitle, Annotations, Groups);
        Breadcrumbs.Add(new BreadcrumbItem(CurrentWorkflowTitle, null, rootGraph));
        OnPropertyChanged(nameof(HasBreadcrumbs));
    }

    [RelayCommand]
    public void OpenSubflow(NodeViewModel subflowNodeVm)
    {
        if (subflowNodeVm == null) return;

        // Si la lista de breadcrumbs está vacía, inicializar la raíz
        if (Breadcrumbs.Count == 0)
        {
            var rootGraph = WorkflowGraphSerializer.Export(Nodes, Connections, GlobalOutputDir, CurrentWorkflowTitle, Annotations, Groups);
            Breadcrumbs.Add(new BreadcrumbItem(CurrentWorkflowTitle, null, rootGraph));
        }
        else
        {
            // Guardar el estado actual en el breadcrumb superior
            var currentGraph = WorkflowGraphSerializer.Export(Nodes, Connections, GlobalOutputDir, CurrentWorkflowTitle, Annotations, Groups);
            var top = Breadcrumbs.Last();
            int topIndex = Breadcrumbs.Count - 1;
            Breadcrumbs[topIndex] = new BreadcrumbItem(top.Name, top.NodeId, currentGraph);
        }

        // Resolver el grafo del subflujo
        WorkflowGraph? innerGraph = null;
        if (subflowNodeVm.NodeInstance is ISubflowNode sn)
        {
            if (sn.EmbedDefinition && !string.IsNullOrWhiteSpace(sn.SubflowDefinitionJson))
            {
                innerGraph = WorkflowGraph.FromJson(sn.SubflowDefinitionJson);
            }
            else if (!string.IsNullOrWhiteSpace(sn.SubflowDefinitionJson))
            {
                try { innerGraph = WorkflowGraph.FromJson(sn.SubflowDefinitionJson); } catch { }
            }

            if (innerGraph == null && !string.IsNullOrWhiteSpace(sn.SubflowPath) && File.Exists(sn.SubflowPath))
            {
                try
                {
                    string json = File.ReadAllText(sn.SubflowPath);
                    innerGraph = WorkflowGraph.FromJson(json);
                }
                catch { }
            }
        }

        // Si no tiene grafo interno aún, crear uno predeterminado con SubflowInputNode y SubflowOutputNode
        if (innerGraph == null || innerGraph.Nodes.Count == 0)
        {
            innerGraph = new WorkflowGraph
            {
                Name = subflowNodeVm.Title,
                GlobalOutputDir = GlobalOutputDir
            };

            var inputNode = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                NodeTypeName = "FileFlow.Plugin.Subflows.SubflowInputNode",
                CustomTitle = "Entrada",
                X = 100,
                Y = 200,
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "In" }
            };

            var outputNode = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                NodeTypeName = "FileFlow.Plugin.Subflows.SubflowOutputNode",
                CustomTitle = "Salida",
                X = 600,
                Y = 200,
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["PortNames"] = "Out" }
            };

            innerGraph.Nodes.Add(inputNode);
            innerGraph.Nodes.Add(outputNode);
        }

        // Limpiar el lienzo actual e importar el grafo interno
        ClearCanvas();
        WorkflowGraphSerializer.Import(
            innerGraph,
            _pluginLoader,
            this,
            n => Nodes.Add(n),
            c => Connections.Add(c),
            a => Annotations.Add(a),
            g => Groups.Add(g));

        CurrentWorkflowTitle = subflowNodeVm.Title;
        Breadcrumbs.Add(new BreadcrumbItem(subflowNodeVm.Title, subflowNodeVm.Id, innerGraph));
        OnPropertyChanged(nameof(HasBreadcrumbs));
        FitToScreen();
    }

    [RelayCommand]
    public void NavigateToBreadcrumb(BreadcrumbItem targetItem)
    {
        if (targetItem == null || Breadcrumbs.Count == 0) return;
        if (Breadcrumbs.LastOrDefault() == targetItem) return;

        int targetIndex = Breadcrumbs.IndexOf(targetItem);
        if (targetIndex < 0) return;

        // Guardar el grafo actual del nivel que abandonamos
        var currentLevelGraph = WorkflowGraphSerializer.Export(Nodes, Connections, GlobalOutputDir, CurrentWorkflowTitle, Annotations, Groups);
        var currentTop = Breadcrumbs.Last();

        // Si el nivel que abandonamos correspondía a un nodo de subflujo, actualizar su SubflowDefinitionJson en el grafo padre
        if (!string.IsNullOrWhiteSpace(currentTop.NodeId) && Breadcrumbs.Count >= 2)
        {
            var parentBreadcrumb = Breadcrumbs[Breadcrumbs.Count - 2];
            var targetNodeDto = parentBreadcrumb.Graph.Nodes.FirstOrDefault(n => n.Id == currentTop.NodeId);
            if (targetNodeDto != null)
            {
                targetNodeDto.Parameters["SubflowDefinitionJson"] = currentLevelGraph.ToJson();
                targetNodeDto.Parameters["EmbedDefinition"] = true;
            }
        }

        // Eliminar todos los breadcrumbs posteriores al targetIndex
        while (Breadcrumbs.Count > targetIndex + 1)
        {
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }

        // Cargar el grafo del target
        ClearCanvas();
        WorkflowGraphSerializer.Import(
            targetItem.Graph,
            _pluginLoader,
            this,
            n => Nodes.Add(n),
            c => Connections.Add(c),
            a => Annotations.Add(a),
            g => Groups.Add(g));

        CurrentWorkflowTitle = targetItem.Name;
        OnPropertyChanged(nameof(HasBreadcrumbs));
        FitToScreen();
    }

    private void ClearCanvas()
    {
        Connections.Clear();
        Nodes.Clear();
        Annotations.Clear();
        Groups.Clear();
        CanvasDecorators.Clear();
    }

    [RelayCommand]
    public void CollapseSelectionToSubflow()
    {
        var selectedNodes = Nodes.Where(n => n.IsSelected).ToList();
        if (selectedNodes.Count == 0) return;

        using var tx = _undoRedoService.BeginTransaction("Colapsar a Subflujo");

        var selectedSet = selectedNodes.ToHashSet();

        // Identificar conexiones entrantes (desde nodos externos hacia la selección)
        var incomingConns = Connections
            .Where(c => selectedSet.Contains(c.Target.NodeOwner) && !selectedSet.Contains(c.Source.NodeOwner))
            .ToList();

        // Identificar conexiones salientes (desde la selección hacia nodos externos)
        var outgoingConns = Connections
            .Where(c => selectedSet.Contains(c.Source.NodeOwner) && !selectedSet.Contains(c.Target.NodeOwner))
            .ToList();

        // Identificar conexiones internas
        var internalConns = Connections
            .Where(c => selectedSet.Contains(c.Source.NodeOwner) && selectedSet.Contains(c.Target.NodeOwner))
            .ToList();

        var inPortNames = incomingConns.Select(c => c.Target.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (inPortNames.Count == 0) inPortNames.Add("In");

        var outPortNames = outgoingConns.Select(c => c.Source.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (outPortNames.Count == 0) outPortNames.Add("Out");

        // Coordenadas para calcular el centro
        double minX = selectedNodes.Min(n => n.Location.X);
        double minY = selectedNodes.Min(n => n.Location.Y);
        double maxX = selectedNodes.Max(n => n.Location.X);
        double maxY = selectedNodes.Max(n => n.Location.Y);
        double centerX = (minX + maxX) / 2.0;
        double centerY = (minY + maxY) / 2.0;

        // Construir el subgrafo interno
        var subflowGraph = new WorkflowGraph
        {
            Name = "Subflujo Compuesto",
            GlobalOutputDir = GlobalOutputDir
        };

        var inputBoundary = new WorkflowNode
        {
            Id = Guid.NewGuid().ToString(),
            NodeTypeName = "FileFlow.Plugin.Subflows.SubflowInputNode",
            CustomTitle = "Entrada",
            X = minX - 300,
            Y = minY,
            Parameters = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PortNames"] = string.Join(";", inPortNames)
            }
        };
        subflowGraph.Nodes.Add(inputBoundary);

        var outputBoundary = new WorkflowNode
        {
            Id = Guid.NewGuid().ToString(),
            NodeTypeName = "FileFlow.Plugin.Subflows.SubflowOutputNode",
            CustomTitle = "Salida",
            X = maxX + 300,
            Y = minY,
            Parameters = new(StringComparer.OrdinalIgnoreCase)
            {
                ["PortNames"] = string.Join(";", outPortNames)
            }
        };
        subflowGraph.Nodes.Add(outputBoundary);

        // Añadir nodos seleccionados al subgrafo
        foreach (var node in selectedNodes)
        {
            var nodeDto = new WorkflowNode
            {
                Id = node.Id,
                NodeTypeName = node.NodeTypeName,
                CustomTitle = node.CustomTitle,
                X = node.Location.X,
                Y = node.Location.Y,
                HasBreakpoint = node.HasBreakpoint,
                IsLoggingEnabled = node.IsLoggingEnabled,
                Parameters = node.Parameters
                    .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                    .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase)
            };
            subflowGraph.Nodes.Add(nodeDto);
        }

        // Añadir aristas internas
        foreach (var c in internalConns)
        {
            subflowGraph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = c.Source.NodeOwner.Id,
                SourcePortName = c.Source.Name,
                TargetNodeId = c.Target.NodeOwner.Id,
                TargetPortName = c.Target.Name
            });
        }

        // Conectar SubflowInputNode a los nodos internos destino
        foreach (var inConn in incomingConns)
        {
            subflowGraph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = inputBoundary.Id,
                SourcePortName = inConn.Target.Name,
                TargetNodeId = inConn.Target.NodeOwner.Id,
                TargetPortName = inConn.Target.Name
            });
        }

        // Conectar los nodos internos origen a SubflowOutputNode
        foreach (var outConn in outgoingConns)
        {
            subflowGraph.Edges.Add(new WorkflowEdge
            {
                SourceNodeId = outConn.Source.NodeOwner.Id,
                SourcePortName = outConn.Source.Name,
                TargetNodeId = outputBoundary.Id,
                TargetPortName = outConn.Source.Name
            });
        }

        string subflowJson = subflowGraph.ToJson();

        // Crear la instancia del nodo SubflowNode en el lienzo padre
        IFlowNode? subflowInstance = _pluginLoader.CreateNodeInstance("FileFlow.Plugin.Subflows.SubflowNode")
                                  ?? _pluginLoader.CreateNodeInstance("SubflowNode");
        if (subflowInstance == null) return;

        subflowInstance.Parameters["EmbedDefinition"] = true;
        subflowInstance.Parameters["SubflowDefinitionJson"] = subflowJson;
        subflowInstance.Parameters["SubflowName"] = "Subflujo Compuesto";

        if (subflowInstance is ISubflowNode snNode)
        {
            snNode.RefreshDynamicPorts(inPortNames, outPortNames);
        }

        var subflowNodeVm = new NodeViewModel(subflowInstance, new Point(centerX, centerY))
        {
            ParentEditor = this,
            Title = "Subflujo Compuesto"
        };
        subflowNodeVm.SyncSubflowPorts();

        // 1. Eliminar conexiones incidentes de los nodos seleccionados
        var allIncidentConns = Connections
            .Where(c => selectedSet.Contains(c.Source.NodeOwner) || selectedSet.Contains(c.Target.NodeOwner))
            .ToList();

        foreach (var c in allIncidentConns)
        {
            _undoRedoService.Record(new DeleteConnectionAction(this, c));
            Connections.Remove(c);
        }

        // 2. Eliminar nodos seleccionados
        _undoRedoService.Record(new DeleteNodesAction(this, selectedNodes, allIncidentConns));
        foreach (var n in selectedNodes)
        {
            Nodes.Remove(n);
        }

        // 3. Añadir el nuevo nodo subflujo
        _undoRedoService.Record(new AddNodesAction(this, [subflowNodeVm]));
        Nodes.Add(subflowNodeVm);

        // 4. Reconectar aristas externas al nuevo nodo subflujo
        foreach (var inConn in incomingConns)
        {
            var targetPort = subflowNodeVm.InputPorts.FirstOrDefault(p => p.Name.Equals(inConn.Target.Name, StringComparison.OrdinalIgnoreCase))
                             ?? subflowNodeVm.InputPorts.FirstOrDefault();
            if (targetPort != null)
            {
                var newConn = new ConnectionViewModel(inConn.Source, targetPort);
                _undoRedoService.Record(new AddConnectionAction(this, newConn));
                Connections.Add(newConn);
            }
        }

        foreach (var outConn in outgoingConns)
        {
            var sourcePort = subflowNodeVm.OutputPorts.FirstOrDefault(p => p.Name.Equals(outConn.Source.Name, StringComparison.OrdinalIgnoreCase))
                             ?? subflowNodeVm.OutputPorts.FirstOrDefault();
            if (sourcePort != null)
            {
                var newConn = new ConnectionViewModel(sourcePort, outConn.Target);
                _undoRedoService.Record(new AddConnectionAction(this, newConn));
                Connections.Add(newConn);
            }
        }

        subflowNodeVm.IsSelected = true;
    }

    [RelayCommand]
    public void ConfirmSpotlightSelection()
    {
        if (SelectedSpotlightItem != null)
        {
            AddNode(SelectedSpotlightItem.TypeName, SpotlightCanvasPosition);
            CloseSpotlight();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _subflowWatchBeat.Dispose();
        _userPreferencesService.PreferencesChanged -= _preferencesChangedHandler;
        GC.SuppressFinalize(this);
    }
}
