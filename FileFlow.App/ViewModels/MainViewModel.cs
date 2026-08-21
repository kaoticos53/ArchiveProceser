using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
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

    public MainViewModel()
    {
        PluginLoader = new PluginLoader();

        // Dynamically load all plugin assemblies in /Plugins directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string pluginsDirectory = Path.Combine(baseDir, "Plugins");

        if (!Directory.Exists(pluginsDirectory))
        {
            Directory.CreateDirectory(pluginsDirectory);
        }

        PluginLoader.LoadPluginDirectory(pluginsDirectory);

        LogConsole = new LogViewModel();
        Editor = new EditorViewModel(PluginLoader);
        Toolbox = new ToolboxViewModel(PluginLoader);
        NodeInspector = new NodeInspectorViewModel(Editor);
        ControlBar = new ControlBarViewModel(Editor, PluginLoader, LogConsole, NodeInspector);

        LogConsole.AddLog(Sdk.LogLevel.Information, $"FileFlow Studio initialized with {PluginLoader.DiscoveredNodeTypes.Count} active plugin nodes.");
    }
}
