using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFlow.App.Services;
using FileFlow.Core.Plugins;

namespace FileFlow.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public PluginLoader PluginLoader { get; }
    public EditorViewModel Editor { get; }
    public ToolboxViewModel Toolbox { get; }
    public NodeInspectorViewModel NodeInspector { get; }
    public ControlBarViewModel ControlBar { get; }
    public LogViewModel LogConsole { get; }
    public StatusBarViewModel StatusBar { get; }
    public SystemPerformanceMonitor PerformanceMonitor { get; }
    public IFileDialogService FileDialogService { get; }
    public IWorkflowStorageService WorkflowStorageService { get; }

    public MainViewModel()
    {
        PluginLoader = new PluginLoader();

        // Register all built-in referenced plugin assemblies
        PluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.FileSystem.FolderSourceNode).Assembly);
        PluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Archives.SmartUnpackNode).Assembly);
        PluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Images.ImageOptimizerNode).Assembly);
        PluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.SwitchCaseNode).Assembly);
        PluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Hashing.HashCalculatorNode).Assembly);
        PluginLoader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Integrations.CliExecutionNode).Assembly);

        // Dynamically load any additional external plugin assemblies in /Plugins directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string pluginsDirectory = Path.Combine(baseDir, "Plugins");

        if (!Directory.Exists(pluginsDirectory))
        {
            Directory.CreateDirectory(pluginsDirectory);
        }

        PluginLoader.LoadPluginDirectory(pluginsDirectory);

        FileDialogService = new FileDialogService();
        WorkflowStorageService = new WorkflowStorageService();
        PerformanceMonitor = new SystemPerformanceMonitor();
        LogConsole = new LogViewModel();
        Editor = new EditorViewModel(PluginLoader);
        Toolbox = new ToolboxViewModel(PluginLoader);
        NodeInspector = new NodeInspectorViewModel(Editor, FileDialogService);
        ControlBar = new ControlBarViewModel(Editor, PluginLoader, LogConsole, NodeInspector, FileDialogService, WorkflowStorageService);
        StatusBar = new StatusBarViewModel(Editor, ControlBar, PerformanceMonitor, LogConsole);

        LogConsole.AddLog(Sdk.LogLevel.Information, $"FileFlow Studio initialized with {PluginLoader.DiscoveredNodeTypes.Count} active plugin nodes.");
    }
}
