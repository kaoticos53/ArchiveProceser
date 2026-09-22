using System.Reflection;
using System.Resources;
using System.Runtime.Loader;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Plugins;

namespace FileFlow.Core.Plugins;

public class PluginLoader
{
    private readonly List<PluginAssemblyLoadContext> _loadContexts = [];
    private readonly Dictionary<string, Type> _discoveredNodeTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Conjunto de nombres simples de ensamblados ya procesados para evitar re-escaneos.</summary>
    private readonly HashSet<string> _registeredAssemblyNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Lock exclusivo para acceso concurrente a _discoveredNodeTypes y _registeredAssemblyNames.</summary>
    private readonly Lock _dictLock = new();

    public IReadOnlyDictionary<string, Type> DiscoveredNodeTypes
    {
        get
        {
            lock (_dictLock)
            {
                return new Dictionary<string, Type>(_discoveredNodeTypes, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Colección de tipos de nodo únicos descubiertos. Devuelve una snapshot inmutable bajo lock
    /// para evitar condiciones de carrera entre el hilo de carga de plugins y el hilo de UI.
    /// </summary>
    public IReadOnlyList<Type> UniqueNodeTypes
    {
        get
        {
            lock (_dictLock)
            {
                return _discoveredNodeTypes.Values
                    .DistinctBy(t => t.FullName ?? t.Name)
                    .ToList();
            }
        }
    }

    /// <summary>Cantidad de nodos únicos descubiertos sin contar duplicados de nombres cortos o ALC.</summary>
    public int DiscoveredNodesCount
    {
        get
        {
            lock (_dictLock)
            {
                return _discoveredNodeTypes.Values
                    .DistinctBy(t => t.FullName ?? t.Name)
                    .Count();
            }
        }
    }

    public void LoadPluginDirectory(string pluginsDirectory)
    {
        if (!Directory.Exists(pluginsDirectory)) return;

        string[] dllFiles = Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories);
        foreach (string dllPath in dllFiles)
        {
            // Saltar DLLs cuyo ensamblado ya fue registrado (p.ej. por RegisterBuiltInAssemblies)
            string asmSimpleName = Path.GetFileNameWithoutExtension(dllPath);
            lock (_dictLock)
            {
                if (_registeredAssemblyNames.Contains(asmSimpleName))
                {
                    System.Diagnostics.Debug.WriteLine($"[PluginLoader] Skipping already-registered assembly: {asmSimpleName}");
                    continue;
                }
            }
            LoadPluginAssembly(dllPath);
        }
        // NOTA: ScanCurrentAppDomain() ya NO se llama aquí.
        // Debe invocarse explícitamente desde CreateConfiguredLoader() una sola vez al final.
    }

    public void ScanCurrentAppDomain()
    {
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

        // If the assembly is already loaded in the default AppDomain (e.g. referenced by the host app),
        // use the existing assembly rather than creating an isolated AssemblyLoadContext.
        // This avoids duplicating types and prevents WPF pack URI / BAML resolution errors.
        string asmSimpleName = Path.GetFileNameWithoutExtension(dllPath);
        Assembly? existing = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, asmSimpleName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            RegisterNodeTypesFromAssembly(existing);
            return;
        }

        // Check if the assembly can be loaded into the default context directly
        try
        {
            var defaultAsm = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(asmSimpleName));
            if (defaultAsm != null)
            {
                RegisterNodeTypesFromAssembly(defaultAsm);
                return;
            }
        }
        catch
        {
            // Not accessible in default context; fallback to isolated ALC
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

            // Marcar este ensamblado como ya procesado para que LoadPluginDirectory y ScanCurrentAppDomain lo salten
            lock (_dictLock)
            {
                // Si ya fue procesado (p.ej. por RegisterBuiltInAssemblies), no repetir el trabajo
                if (!_registeredAssemblyNames.Add(asmName))
                {
                    return;
                }
            }

            // 1. Auto-discover and register Plugin Resources (Strings.resx / embedded .resources)
            RegisterPluginResources(asm);

            // 2. Discover and instantiate IPluginInitializer if present; collect node types
            var nodeTypesToRegister = new List<(string fullName, string shortName, Type type)>();

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
                    nodeTypesToRegister.Add((type.FullName ?? type.Name, type.Name, type));
                }
            }

            // Escritura batch bajo lock para máxima thread-safety
            lock (_dictLock)
            {
                foreach (var (fullName, shortName, type) in nodeTypesToRegister)
                {
                    // Prioridad Default ALC: no sobreescribir un tipo del ALC por defecto con uno de un ALC aislado
                    if (_discoveredNodeTypes.TryGetValue(fullName, out var existingType))
                    {
                        var existingAlc = AssemblyLoadContext.GetLoadContext(existingType.Assembly);
                        var newAlc = AssemblyLoadContext.GetLoadContext(type.Assembly);
                        if (existingAlc == AssemblyLoadContext.Default && newAlc != AssemblyLoadContext.Default)
                        {
                            continue;
                        }
                    }

                    if (_discoveredNodeTypes.TryGetValue(shortName, out var existingShortType))
                    {
                        var existingAlc = AssemblyLoadContext.GetLoadContext(existingShortType.Assembly);
                        var newAlc = AssemblyLoadContext.GetLoadContext(type.Assembly);
                        if (existingAlc == AssemblyLoadContext.Default && newAlc != AssemblyLoadContext.Default)
                        {
                            // Registrar solo el FullName (no sobreescribir el shortName)
                            _discoveredNodeTypes[fullName] = type;
                            continue;
                        }
                    }

                    _discoveredNodeTypes[fullName] = type;
                    _discoveredNodeTypes[shortName] = type;
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
                    if (baseName.EndsWith(".es", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

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
        lock (_dictLock)
        {
            _discoveredNodeTypes[fullName] = type;
            _discoveredNodeTypes[type.Name] = type;
        }
    }

    public IFlowNode? CreateNodeInstance(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;

        // Obtener snapshot bajo lock para la búsqueda inicial
        Type? type;
        lock (_dictLock)
        {
            _discoveredNodeTypes.TryGetValue(typeName, out type);
        }
        if (type != null)
        {
            return (IFlowNode?)Activator.CreateInstance(type);
        }

        // Try matching by simple class name if FullName fails
        Type? shortMatchType;
        lock (_dictLock)
        {
            var kvp = _discoveredNodeTypes.FirstOrDefault(x => x.Value.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
            shortMatchType = kvp.Value;
        }
        if (shortMatchType != null)
        {
            return (IFlowNode?)Activator.CreateInstance(shortMatchType);
        }

        // Fallback: search AppDomain loaded types
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var matchedType = asm.GetType(typeName, throwOnError: false, ignoreCase: true);
                if (matchedType == null)
                {
                    try
                    {
                        matchedType = asm.GetTypes().FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) && typeof(IFlowNode).IsAssignableFrom(t));
                    }
                    catch
                    {
                        // Ignore reflection type load exceptions for non-relevant assemblies
                    }
                }

                if (matchedType != null && typeof(IFlowNode).IsAssignableFrom(matchedType) && !matchedType.IsAbstract && !matchedType.IsInterface)
                {
                    lock (_dictLock)
                    {
                        _discoveredNodeTypes[typeName] = matchedType;
                        _discoveredNodeTypes[matchedType.Name] = matchedType;
                    }
                    return (IFlowNode?)Activator.CreateInstance(matchedType);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PluginLoader] Fallback type resolution error for '{typeName}': {ex.Message}");
        }

        return null;
    }

    public void UnloadAll()
    {
        lock (_dictLock)
        {
            _discoveredNodeTypes.Clear();
            _registeredAssemblyNames.Clear();
        }
        foreach (var alc in _loadContexts)
        {
            try
            {
                alc.Unload();
            }
            catch { }
        }
        _loadContexts.Clear();

        // La descarga de AssemblyLoadContext es cooperativa: el runtime solo libera los ensamblados
        // cuando ya no quedan referencias vivas y se ejecuta una recolección completa seguida de los
        // finalizadores pendientes. Es el único punto del código donde GC.Collect() está justificado,
        // porque UnloadAll() es una operación explícita de recarga de plugins, nunca una ruta crítica.
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
