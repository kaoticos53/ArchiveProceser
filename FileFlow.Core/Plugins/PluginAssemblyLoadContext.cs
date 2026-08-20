using System.Reflection;
using System.Runtime.Loader;

namespace FileFlow.Core.Plugins;

public class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginPath;

    public PluginAssemblyLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _pluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Shared contract assemblies (like FileFlow.Sdk and FileFlow.Core) MUST be loaded by Default ALC!
        if (assemblyName.Name != null &&
            (assemblyName.Name.Equals("FileFlow.Sdk", StringComparison.OrdinalIgnoreCase) ||
             assemblyName.Name.Equals("FileFlow.Core", StringComparison.OrdinalIgnoreCase)))
        {
            return null; // Return null to fallback to AssemblyLoadContext.Default
        }

        // Try resolving through dependency resolver first
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null && File.Exists(assemblyPath))
        {
            return LoadFromUnlockedFile(assemblyPath);
        }

        // Search in plugin directory
        string pluginDir = Path.GetDirectoryName(_pluginPath) ?? string.Empty;
        string candidatePath = Path.Combine(pluginDir, $"{assemblyName.Name}.dll");
        if (File.Exists(candidatePath))
        {
            return LoadFromUnlockedFile(candidatePath);
        }

        // Search in main application BaseDirectory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string baseCandidatePath = Path.Combine(baseDir, $"{assemblyName.Name}.dll");
        if (File.Exists(baseCandidatePath))
        {
            return LoadFromUnlockedFile(baseCandidatePath);
        }

        return null;
    }

    public Assembly LoadFromUnlockedFile(string path)
    {
        byte[] assemblyBytes = File.ReadAllBytes(path);
        string pdbPath = Path.ChangeExtension(path, ".pdb");
        if (File.Exists(pdbPath))
        {
            byte[] pdbBytes = File.ReadAllBytes(pdbPath);
            using var pdbStream = new MemoryStream(pdbBytes);
            using var asmStream = new MemoryStream(assemblyBytes);
            return LoadFromStream(asmStream, pdbStream);
        }

        using var stream = new MemoryStream(assemblyBytes);
        return LoadFromStream(stream);
    }
}
