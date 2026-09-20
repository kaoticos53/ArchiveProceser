using System;
using Avalonia.Controls;
using FileFlow.App;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views;
using FileFlow.Core.Engine;
using FileFlow.Sdk;
using FileFlow.Sdk.Telemetry;

namespace FileFlow.Tests.TestHelpers;

/// <summary>Superficie de la aplicación que se puede capturar.</summary>
public enum AppSurface
{
    /// <summary>La ventana principal completa: barra de control, paneles, lienzo, consola y barra de estado.</summary>
    Shell,

    /// <summary>El lienzo con un grafo de ejemplo.</summary>
    Editor,

    /// <summary>El panel de nodos disponibles.</summary>
    Toolbox,

    /// <summary>El inspector de datos del nodo seleccionado.</summary>
    Inspector,

    /// <summary>La consola de ejecución con registros de ejemplo.</summary>
    LogConsole,

    /// <summary>La barra de estado.</summary>
    StatusBar,

    /// <summary>La barra de control superior.</summary>
    ControlBar,

    /// <summary>La ventana principal con el cajón lateral desplegado.</summary>
    Drawer
}

/// <summary>
/// Aplicación de muestra, reproducible, para las capturas de las vistas clave.
///
/// Monta los view models <b>reales</b> con dobles de sus puertos (preferencias, monitor de rendimiento,
/// almacén de logs, almacenamiento de flujos), no una imitación de la interfaz: así una captura detecta
/// cambios en las plantillas, en las clases de estilo y en los datos que las vistas pintan.
///
/// Todo lo que en la aplicación real es «ambiente» queda fijado aquí, porque una línea base sólo sirve si es
/// idéntica en cualquier equipo:
/// <list type="bullet">
///   <item>Preferencias en memoria: ni favoritos, ni contadores de uso, ni tema o idioma del usuario.</item>
///   <item>Monitor de rendimiento congelado: sin cifras de CPU/RAM/GPU que cambien entre ejecuciones.</item>
///   <item>Consola con registros de marca de tiempo fija: el reloj no puede entrar en la imagen.</item>
///   <item>Grafo de ejemplo cargado desde el modelo de datos (no con <c>AddNode</c>, que además escribe en
///   las preferencias del usuario).</item>
/// </list>
/// </summary>
/// <remarks>
/// Implementa <see cref="IDisposable"/> y hay que disponerla: los view models se suscriben a
/// <c>LocalizationManager</c>, <c>ModelSessionRegistry</c> y las preferencias —singletons de proceso— y
/// la consola arranca un <c>DispatcherTimer</c>. Sin esta limpieza, esos suscriptores y ese timer
/// sobreviven al test, reaccionan al singleton cuando otra colección está corriendo en paralelo y
/// provocan fallos de afinidad de hilo («The calling thread cannot access this object») en pruebas
/// que nada tienen que ver con esta fixture. Con el <c>Dispose</c>, la muestra muere con el test que
/// la creó.
/// </remarks>
public sealed class AppVisualFixture : IDisposable
{
    /// <summary>Tamaño por defecto de la ventana principal (el mismo que declara MainWindow).</summary>
    public const int ShellWidth = 1340;
    public const int ShellHeight = 850;

    /// <summary>Anchura del lienzo cuando se captura solo.</summary>
    public const int EditorWidth = 980;

    /// <summary>Anchura de la barra de estado y de la barra de control cuando se capturan solas.</summary>
    public const int BarWidth = 1340;

    /// <summary>Anchura de las consolas de la barra inferior cuando se capturan solas.</summary>
    public const int ConsoleWidth = 1180;

    private const string OutputDirectory = "/workflow/output";

    private readonly InMemoryUserPreferencesService _preferences;
    private readonly FrozenPerformanceMonitor _monitor;

    private AppVisualFixture(
        MainViewModel shell,
        InMemoryUserPreferencesService preferences,
        FrozenPerformanceMonitor monitor)
    {
        Shell = shell;
        _preferences = preferences;
        _monitor = monitor;
    }

    public MainViewModel Shell { get; }

    public EditorViewModel Editor => Shell.Editor;

    public ToolboxViewModel Toolbox => Shell.Toolbox;

    public NodeInspectorViewModel Inspector => Shell.NodeInspector;

    public LogViewModel LogConsole => Shell.LogConsole;

    public StatusBarViewModel StatusBar => Shell.StatusBar;

    public ControlBarViewModel ControlBar => Shell.ControlBar;

    /// <summary>
    /// Libera los view models que se suscribieron a singletons del proceso y detiene el timer de la
    /// consola. Idempotente: los <c>Dispose</c> de los view models ya lo son.
    ///
    /// Se ejecuta en el hilo de UI aunque quien disponga esté en el hilo del runner: el timer de la consola
    /// es un <c>DispatcherTimer</c> y los controles creados para la captura tienen afinidad de hilo.
    /// </summary>
    public void Dispose()
    {
        AvaloniaTestHelper.RunOnUI(() =>
        {
            LogConsole.Dispose();
            ControlBar.Dispose();
            Toolbox.Dispose();
            Editor.Dispose();
        });
    }

    /// <summary>Preferencias en memoria que alimentan la muestra (para el test que quiera inspeccionarlas).</summary>
    public IUserPreferencesService Preferences => _preferences;

    /// <summary>Monitor de rendimiento congelado (nunca publica métricas por su cuenta).</summary>
    public FrozenPerformanceMonitor Monitor => _monitor;

    /// <summary>
    /// Construye la muestra. Debe llamarse en el hilo de UI: crea view models que registran
    /// <c>DispatcherTimer</c> y se suscriben a eventos de la aplicación.
    /// </summary>
    public static AppVisualFixture Create()
    {
        AvaloniaTestHelper.RequireUIThread($"{nameof(AppVisualFixture)}.{nameof(Create)}");

        var preferences = new InMemoryUserPreferencesService();
        var monitor = new FrozenPerformanceMonitor();
        var logs = new LogViewModel(new InMemoryLogStore());
        var pluginLoader = PluginRegistryHelper.CreateConfiguredLoader();
        var fileDialog = new NullFileDialogService();
        var storage = new InMemoryWorkflowStorageService();

        var editor = new EditorViewModel(pluginLoader, userPreferencesService: preferences);
        var toolbox = new ToolboxViewModel(pluginLoader, preferences);
        var inspector = new NodeInspectorViewModel(editor, fileDialog, logs);
        var controlBar = new ControlBarViewModel(
            editor, pluginLoader, logs, inspector, fileDialog, storage, preferences);
        var statusBar = new StatusBarViewModel(editor, controlBar, monitor, logs);

        var shell = new MainViewModel(
            pluginLoader, editor, toolbox, inspector, controlBar, logs, statusBar, monitor, fileDialog, storage);

        var fixture = new AppVisualFixture(shell, preferences, monitor);
        fixture.Freeze();
        return fixture;
    }

    /// <summary>Construye la superficie pedida. Debe llamarse en el hilo de UI.</summary>
    public Control Build(AppSurface surface)
    {
        AvaloniaTestHelper.RequireUIThread($"{nameof(AppVisualFixture)}.{nameof(Build)}({surface})");

        return surface switch
        {
            AppSurface.Shell => BuildShell(),
            AppSurface.Editor => new EditorView { DataContext = Editor },
            AppSurface.Toolbox => new NodeToolboxView { DataContext = Toolbox },
            AppSurface.Inspector => new NodeInspectorPanelView { DataContext = Inspector },
            AppSurface.LogConsole => new LogView { DataContext = LogConsole },
            AppSurface.StatusBar => new StatusBarView { DataContext = StatusBar },
            AppSurface.ControlBar => new ControlBarView { DataContext = ControlBar },
            AppSurface.Drawer => BuildDrawer(),
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Superficie no soportada.")
        };
    }

    /// <summary>
    /// La ventana principal con el <b>cajón abierto</b>: es la superficie por la que se alcanza el diseñador de
    /// conjuntos de datos sintéticos y el resto de herramientas, y hasta ahora no tenía captura —un botón del
    /// cajón podía desaparecer o salir recortado sin que ninguna prueba se enterara—.
    /// </summary>
    private Control BuildDrawer()
    {
        ControlBar.IsMenuOpen = true;
        return BuildShell();
    }

    /// <summary>
    /// Contenido real de la ventana principal, sin la ventana. Se extrae en lugar de reconstruir el layout a
    /// mano: si alguien mueve un panel o cambia una columna en <c>MainWindow.axaml</c>, la captura lo ve.
    /// </summary>
    private Control BuildShell()
    {
        var window = new MainWindow(Shell);
        var content = (Control)window.Content!;

        // La ventana real no se usa como contenedor: la captura crea la suya sin decoración. El contenido
        // heredaba el DataContext de la ventana, así que se le asigna explícitamente al desengancharlo.
        window.Content = null;
        content.DataContext = Shell;

        return content;
    }

    /// <summary>
    /// Fija (o re-fija) el estado de partida: sin él, la imagen dependería del azar y del reloj.
    ///
    /// Idempotente y pensado para llamarse <b>antes de cada captura</b> de una fixture compartida por
    /// clase de pruebas (<c>IClassFixture</c>): recarga el grafo (el <c>LoadFromGraphModel</c> pasa por
    /// <c>ClearGraph</c>, que dispone los nodos viejos), vuelve a sembrar la consola tras vaciarla y
    /// re-afija la barra de estado — de modo que aunque la prueba anterior hubiera mutado view models,
    /// la siguiente captura parte del mismo estado que una fixture recién creada.
    /// Debe llamarse en el hilo de UI (p. ej. dentro de la fábrica de <c>VisualSnapshot.Capture</c>).
    /// </summary>
    public void EnsureFrozen()
    {
        AvaloniaTestHelper.RequireUIThread($"{nameof(AppVisualFixture)}.{nameof(EnsureFrozen)}");

        // El cajón arranca cerrado: la captura que lo abre es la única que lo despliega, y así el orden de las
        // pruebas de la clase no cambia lo que se ve en ninguna de las demás.
        ControlBar.IsMenuOpen = false;

        LoadSampleGraph();
        SeedLogConsole();
        ReleaseAiModels();
        FreezeStatusBar();
        FreezeToolbox();

        // El inspector abierto es el estado en el que se trabaja: la captura debe cubrir su panel.
        var inspected = Editor.Nodes.Count > 0 ? Editor.Nodes[0] : null;
        if (inspected != null)
        {
            Inspector.InspectNode(inspected, autoOpen: true);
        }
    }

    /// <summary>Fija el estado de partida de la caja de herramientas.</summary>
    private void FreezeToolbox()
    {
        Toolbox.SearchText = string.Empty;
        Toolbox.SelectedCategoryFilter = "Todas";
        Toolbox.CurrentPerspective = ToolboxPerspective.ByCategory;
        Toolbox.IsCompactMode = true;
        Toolbox.RefreshToolbox();

        for (int i = 0; i < Toolbox.CategoryGroups.Count; i++)
        {
            Toolbox.CategoryGroups[i].IsExpanded = (i == 0);
        }
    }

    /// <summary>Fija el estado de partida de una fixture recién creada.</summary>
    private void Freeze() => EnsureFrozen();

    /// <summary>
    /// Grafo de ejemplo: origen de carpeta, filtro lógico y destino, con una anotación y un grupo. Se carga
    /// desde el modelo de datos para no pasar por <c>AddNode</c>, que incrementa el contador de uso en las
    /// preferencias reales del usuario.
    /// </summary>
    private void LoadSampleGraph()
    {
        var graph = new WorkflowGraph
        {
            Name = "muestra.fileflow",
            GlobalOutputDir = OutputDirectory,
            Nodes =
            {
                new WorkflowNode
                {
                    Id = "source",
                    NodeTypeName = "FolderSourceNode",
                    X = 60,
                    Y = 90,
                    Parameters =
                    {
                        ["SourcePath"] = "/workflow/input",
                        ["ExtensionFilter"] = "*.jpg, *.png",
                        ["Recursive"] = true
                    }
                },
                new WorkflowNode
                {
                    Id = "filter",
                    NodeTypeName = "ExpressionFilterNode",
                    X = 380,
                    Y = 90,
                    Parameters =
                    {
                        ["Property"] = "SizeMB",
                        ["Operator"] = ">",
                        ["ComparisonValue"] = "10"
                    }
                },
                new WorkflowNode
                {
                    Id = "sink",
                    NodeTypeName = "DestinationSinkNode",
                    X = 700,
                    Y = 90,
                    Parameters =
                    {
                        ["DestinationRoot"] = OutputDirectory
                    }
                }
            },
            Edges =
            {
                new WorkflowEdge
                {
                    SourceNodeId = "source",
                    SourcePortName = "Out",
                    TargetNodeId = "filter",
                    TargetPortName = "In"
                },
                new WorkflowEdge
                {
                    SourceNodeId = "filter",
                    SourcePortName = "True",
                    TargetNodeId = "sink",
                    TargetPortName = "In"
                }
            },
            Annotations =
            {
                new WorkflowAnnotation
                {
                    Id = "note",
                    Title = "Nota de diseño",
                    Content = "El origen sólo emite imágenes; el filtro descarta las menores de 10 MB.",
                    X = 60,
                    Y = 380,
                    Width = 300,
                    Height = 150,
                    Color = "#FEF08A"
                }
            },
            Groups =
            {
                new WorkflowGroup
                {
                    Id = "group",
                    Title = "Ingesta y filtrado",
                    X = 30,
                    Y = 40,
                    Width = 720,
                    Height = 320,
                    Color = "#3B82F6",
                    NodeIds = { "source", "filter" }
                }
            }
        };

        Editor.LoadFromGraphModel(graph);
    }

    /// <summary>
    /// Consola con registros de marca de tiempo fija. El constructor de <c>MainViewModel</c> encola su
    /// registro de arranque (con la hora actual), así que primero se vacía y después se siembra la muestra
    /// por la vía real —<c>AddStructuredLog</c>— para que los contadores de nivel cuadren con las filas.
    /// </summary>
    private void SeedLogConsole()
    {
        LogConsole.FlushAllPendingLogs();
        LogConsole.Logs.Clear();
        LogConsole.TotalLogsCount = 0;
        LogConsole.ErrorCount = 0;
        LogConsole.WarningCount = 0;
        LogConsole.InfoCount = 0;
        LogConsole.DebugCount = 0;

        foreach (var record in SampleLogRecords())
        {
            LogConsole.AddStructuredLog(record);
        }

        LogConsole.FlushAllPendingLogs();
    }

    private static StructuredLogRecord[] SampleLogRecords() =>
    [
        new StructuredLogRecord(
            Id: 1,
            ExecutionId: "demo",
            Timestamp: new DateTime(2026, 1, 1, 9, 30, 12, 145),
            Level: LogLevel.Information,
            NodeId: "source",
            NodeName: "Folder Source",
            ItemId: "a1b2c3d4e5f60718",
            FilePath: "/workflow/input/foto-001.jpg",
            FileName: "foto-001.jpg",
            FileSizeBytes: 2_413_000,
            DurationMs: 12.4,
            Message: "Origen de carpeta: 3 elementos detectados."),

        new StructuredLogRecord(
            Id: 2,
            ExecutionId: "demo",
            Timestamp: new DateTime(2026, 1, 1, 9, 30, 12, 402),
            Level: LogLevel.Information,
            NodeId: "filter",
            NodeName: "Filtro por Condición Lógica",
            ItemId: "a1b2c3d4e5f60718",
            FilePath: "/workflow/input/foto-001.jpg",
            FileName: "foto-001.jpg",
            FileSizeBytes: 2_413_000,
            DurationMs: 3.8,
            Message: "Condición 'SizeMB > 10' evaluada como TRUE → rama 'True'.",
            DetailsJson: "{\"property\":\"SizeMB\",\"operator\":\">\",\"targetValue\":\"10\",\"result\":true}"),

        new StructuredLogRecord(
            Id: 3,
            ExecutionId: "demo",
            Timestamp: new DateTime(2026, 1, 1, 9, 30, 12, 631),
            Level: LogLevel.Warning,
            NodeId: "sink",
            NodeName: "Destination Sink",
            ItemId: "b2c3d4e5f6071829",
            FilePath: "/workflow/input/escaneo-002.png",
            FileName: "escaneo-002.png",
            FileSizeBytes: 5_120_000,
            DurationMs: 41.2,
            Message: "El archivo de destino ya existía: se renombra de forma incremental."),

        new StructuredLogRecord(
            Id: 4,
            ExecutionId: "demo",
            Timestamp: new DateTime(2026, 1, 1, 9, 30, 13, 004),
            Level: LogLevel.Error,
            NodeId: "sink",
            NodeName: "Destination Sink",
            ItemId: "c3d4e5f607182930",
            FilePath: "/workflow/input/corrupto.jpg",
            FileName: "corrupto.jpg",
            FileSizeBytes: 0,
            DurationMs: 88.9,
            Message: "No se pudo escribir el archivo de destino: el origen está bloqueado.")
    ];

    /// <summary>
    /// Vacía el registro de sesiones de modelos antes de fijar la barra de estado.
    ///
    /// Es un singleton del proceso y la barra se suscribe a sus notificaciones: si una prueba anterior dejó
    /// un modelo cargado, cualquier aviso de cambio de estado vuelve a mostrar la isla de «modelos en
    /// memoria» —incluso después de haberla ocultado— y con ella se desplaza media barra. El resultado era
    /// una captura que dependía de lo que hubiera corrido antes: pasaba sola y fallaba dentro del suite
    /// completo, y sólo a veces. Con el registro vacío, el cálculo da 0 pase lo que pase.
    /// </summary>
    private static void ReleaseAiModels() => FileFlow.Sdk.ModelSessionRegistry.ClearAllSessions();

    /// <summary>
    /// Barra de estado con cifras y estado fijos.
    ///
    /// El monitor real daría valores distintos en cada ejecución, y el registro de sesiones de IA es un
    /// singleton del proceso: si una prueba anterior cargó un modelo, la barra mostraría la isla de «modelos
    /// en memoria» y la captura dejaría de ser reproducible —fallaba sólo al ejecutar el suite entero—. La
    /// muestra se declara, por tanto, sin modelos cargados, que además es el estado de partida de la
    /// aplicación.
    /// </summary>
    private void FreezeStatusBar()
    {
        StatusBar.GlobalOutputDir = OutputDirectory;
        StatusBar.RamText = "412.0 MB";
        StatusBar.CpuText = "7%";
        StatusBar.GpuText = "0%";

        StatusBar.LoadedAiModelsCount = 0;
        StatusBar.HasLoadedAiModels = false;
        StatusBar.LoadedAiModelsText = string.Empty;
        StatusBar.LoadedAiModelsToolTip = string.Empty;
    }
}
