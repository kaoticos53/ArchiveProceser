using System.Reflection;
using FileFlow.Sdk;

namespace FileFlow.Core.Plugins;

public class PluginLoader
{
    private readonly List<PluginAssemblyLoadContext> _loadContexts = [];
    private readonly Dictionary<string, Type> _discoveredNodeTypes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, Type> DiscoveredNodeTypes => _discoveredNodeTypes;

    public void LoadPluginDirectory(string pluginsDirectory)
    {
        if (Directory.Exists(pluginsDirectory))
        {
            string[] dllFiles = Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories);
            foreach (string dllPath in dllFiles)
            {
                LoadPluginAssembly(dllPath);
            }
        }

        // Also scan loaded assemblies in current AppDomain for builtin/referenced plugins
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            RegisterNodeTypesFromAssembly(asm);
        }
    }

    public void LoadPluginAssembly(string dllPath)
    {
        string fileName = Path.GetFileName(dllPath);
        if (fileName.Equals("FileFlow.Sdk.dll", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("FileFlow.Core.dll", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var alc = new PluginAssemblyLoadContext(dllPath);
            Assembly asm = alc.LoadFromUnlockedFile(dllPath);
            _loadContexts.Add(alc);
            RegisterNodeTypesFromAssembly(asm);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load plugin assembly '{dllPath}': {ex.Message}");
        }
    }

    public void RegisterNodeTypesFromAssembly(Assembly asm)
    {
        try
        {
            string asmName = asm.GetName().Name ?? string.Empty;
            if (asmName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                asmName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                asmName.StartsWith("WindowsBase", StringComparison.OrdinalIgnoreCase) ||
                asmName.StartsWith("Presentation", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (Type type in asm.GetTypes())
            {
                bool isFlowNode = (typeof(IFlowNode).IsAssignableFrom(type) ||
                                   type.GetInterfaces().Any(i => i.Name.Equals(nameof(IFlowNode), StringComparison.OrdinalIgnoreCase))) &&
                                   !type.IsAbstract && !type.IsInterface;

                if (isFlowNode)
                {
                    string key = type.FullName ?? type.Name;
                    _discoveredNodeTypes[key] = type;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Assembly node registration error: {ex.Message}");
        }
    }

    public void RegisterNodeType<T>() where T : IFlowNode, new()
    {
        Type type = typeof(T);
        string key = type.FullName ?? type.Name;
        _discoveredNodeTypes[key] = type;
    }

    public IFlowNode? CreateNodeInstance(string typeName)
    {
        if (_discoveredNodeTypes.TryGetValue(typeName, out Type? type))
        {
            return (IFlowNode?)Activator.CreateInstance(type);
        }

        // Try matching by simple class name if FullName fails
        var kvp = _discoveredNodeTypes.FirstOrDefault(x => x.Value.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
        if (kvp.Value != null)
        {
            return (IFlowNode?)Activator.CreateInstance(kvp.Value);
        }

        return null;
    }

    public void UnloadAll()
    {
        _discoveredNodeTypes.Clear();
        foreach (var alc in _loadContexts)
        {
            try
            {
                alc.Unload();
            }
            catch { }
        }
        _loadContexts.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
