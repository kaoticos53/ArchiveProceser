using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFlow.App.Services;
using FileFlow.Core.Plugins;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly LogInspectorSyncService _logInspectorSync;

    public PluginLoader PluginLoader { get; }
    public EditorViewModel Editor { get; }
    public ToolboxViewModel Toolbox { get; }
    public NodeInspectorViewModel NodeInspector { get; }
    public ControlBarViewModel ControlBar { get; }
    public LogViewModel LogConsole { get; }
    public StatusBarViewModel StatusBar { get; }
    public ISystemPerformanceMonitor PerformanceMonitor { get; }
    public IFileDialogService FileDialogService { get; }
    public IWorkflowStorageService WorkflowStorageService { get; }
    public ILocalizationService LocalizationService { get; }

    /// <summary>
    /// El <b>registro de latidos</b> de la aplicación: los cuatro latidos del producto se declaran en él, así que
    /// aquí es donde se puede preguntar cuáles existen y con qué periodo. En el constructor por defecto es el
    /// servicio que comparten los cuatro; con contenedor, el que se haya registrado (ver
    /// <c>ServiceCollectionExtensions</c>).
    /// </summary>
    public IHeartbeatService Heartbeats { get; }
    public string AppVersionDisplay => FileFlow.Sdk.AppVersionInfo.DisplayVersion;

    public MainViewModel(
        PluginLoader pluginLoader,
        EditorViewModel editor,
        ToolboxViewModel toolbox,
        NodeInspectorViewModel nodeInspector,
        ControlBarViewModel controlBar,
        LogViewModel logConsole,
        StatusBarViewModel statusBar,
        ISystemPerformanceMonitor performanceMonitor,
        IFileDialogService fileDialogService,
        IWorkflowStorageService workflowStorageService,
        ILocalizationService? localizationService = null,
        IHeartbeatService? heartbeats = null)
    {
        PluginLoader = pluginLoader;
        Editor = editor;
        Toolbox = toolbox;
        NodeInspector = nodeInspector;
        ControlBar = controlBar;
        LogConsole = logConsole;
        StatusBar = statusBar;
        PerformanceMonitor = performanceMonitor;
        FileDialogService = fileDialogService;
        WorkflowStorageService = workflowStorageService;
        LocalizationService = localizationService ?? FileFlow.Sdk.Localization.LocalizationManager.Instance;
        Heartbeats = heartbeats ?? HeartbeatService.Shared;

        _logInspectorSync = new LogInspectorSyncService(LogConsole, NodeInspector);

        LogConsole.AddLog(Sdk.LogLevel.Information, LocalizationService.GetFormattedString("Log_AppInitialized", "FileFlow Studio initialized with {0} active plugin nodes.", PluginLoader.DiscoveredNodesCount));
    }

    /// <summary>
    /// Constructor por defecto para compatibilidad con diseñadores XAML o inicializaciones sin contenedor directo.
    /// </summary>
    public MainViewModel()
    {
        PluginLoader = PluginRegistryHelper.CreateConfiguredLoader();

        FileDialogService = new FileDialogService();
        WorkflowStorageService = new WorkflowStorageService();

        // Un solo registro para los cuatro latidos: es lo que hace que «los latidos de la aplicación» sea una
        // lista consultable en lugar de cuatro temporizadores que nadie puede enumerar.
        Heartbeats = new HeartbeatService();

        PerformanceMonitor = new SystemPerformanceMonitor(heartbeats: Heartbeats);
        LogConsole = new LogViewModel(heartbeats: Heartbeats);
        Editor = new EditorViewModel(PluginLoader, logViewModel: LogConsole, heartbeats: Heartbeats);
        Toolbox = new ToolboxViewModel(PluginLoader);
        NodeInspector = new NodeInspectorViewModel(Editor, FileDialogService, LogConsole);
        ControlBar = new ControlBarViewModel(Editor, PluginLoader, LogConsole, NodeInspector, FileDialogService, WorkflowStorageService, heartbeats: Heartbeats);
        StatusBar = new StatusBarViewModel(Editor, ControlBar, PerformanceMonitor, LogConsole);
        LocalizationService = FileFlow.Sdk.Localization.LocalizationManager.Instance;

        _logInspectorSync = new LogInspectorSyncService(LogConsole, NodeInspector);

        LogConsole.AddLog(Sdk.LogLevel.Information, LocalizationService.GetFormattedString("Log_AppInitialized", "FileFlow Studio initialized with {0} active plugin nodes.", PluginLoader.DiscoveredNodesCount));
    }

    /// <summary>
    /// Desuscribe la sincronización entre consola de logs e inspector de nodos.
    /// </summary>
    public void Dispose()
    {
        _logInspectorSync.Dispose();
    }
}
