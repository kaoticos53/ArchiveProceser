using System.Reflection;
using System.Resources;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Plugins;

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

            // 1. Auto-discover and register Plugin Resources (Strings.resx / embedded .resources)
            RegisterPluginResources(asm);

            // 2. Discover and instantiate IPluginInitializer if present
            foreach (Type type in asm.GetTypes())
            {
                if (typeof(IPluginInitializer).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                {
                    try
                    {
                        var initializer = (IPluginInitializer?)Activator.CreateInstance(type);
                        initializer?.Initialize();
                    }
                    catch (Exception exInit)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error initializing plugin '{type.FullName}': {exInit.Message}");
                    }
                }

                bool isFlowNode = (typeof(IFlowNode).IsAssignableFrom(type) ||
                                   type.GetInterfaces().Any(i => i.Name.Equals(nameof(IFlowNode), StringComparison.OrdinalIgnoreCase))) &&
                                   !type.IsAbstract && !type.IsInterface;

                if (isFlowNode)
                {
                    string fullName = type.FullName ?? type.Name;
                    _discoveredNodeTypes[fullName] = type;
                    _discoveredNodeTypes[type.Name] = type;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Assembly node registration error: {ex.Message}");
        }
    }

    private static void RegisterPluginResources(Assembly asm)
    {
        try
        {
            // 1. Check generated Strongly-Typed Resource classes (e.g., Resources.Strings)
            foreach (Type type in asm.GetTypes())
            {
                if (type.Name.Equals("Strings", StringComparison.OrdinalIgnoreCase) ||
                    type.Name.EndsWith("Resources", StringComparison.OrdinalIgnoreCase))
                {
                    PropertyInfo? prop = type.GetProperty("ResourceManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (prop?.GetValue(null) is ResourceManager rm)
                    {
                        LocalizationManager.Instance.RegisterResourceManager(rm);
                    }
                }
            }

            // 2. Also check embedded resource manifest names (e.g. MyPlugin.Resources.Strings.resources)
            string[] manifestNames = asm.GetManifestResourceNames();
            foreach (string name in manifestNames)
            {
                if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase)) // exclude BAML/XAML generated resources
                {
                    string baseName = name[..^10]; // Strip ".resources"
                    try
                    {
                        var rm = new ResourceManager(baseName, asm);
                        LocalizationManager.Instance.RegisterResourceManager(rm);
                    }
                    catch
                    {
                        // Fallback ignore if already registered
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error registering plugin resources for '{asm.FullName}': {ex.Message}");
        }
    }

    public void RegisterNodeType<T>() where T : IFlowNode, new()
    {
        Type type = typeof(T);
        string fullName = type.FullName ?? type.Name;
        _discoveredNodeTypes[fullName] = type;
        _discoveredNodeTypes[type.Name] = type;
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
