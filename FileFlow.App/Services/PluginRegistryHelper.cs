using System.IO;
using FileFlow.Core.Plugins;

namespace FileFlow.App.Services;

/// <summary>
/// Provee centralización para el registro de ensamblados de plugins predeterminados y escaneo de directorios externos.
/// </summary>
public static class PluginRegistryHelper
{
    /// <summary>
    /// Crea y configura una instancia de <see cref="PluginLoader"/> con todos los plugins incorporados y directorio /Plugins.
    /// </summary>
    public static PluginLoader CreateConfiguredLoader()
    {
        var loader = new PluginLoader();

        // 1. Registrar ensamblados de plugins incorporados (ya en memoria, Default ALC)
        // Esto también marca sus nombres en _registeredAssemblyNames para que LoadPluginsDirectory los salte.
        RegisterBuiltInAssemblies(loader);

        // 2. Cargar plugins externos del directorio /Plugins/ (saltará los que ya están registrados)
        LoadPluginsDirectory(loader);

        // 3. Escaneo único del AppDomain al final para capturar cualquier ensamblado cargado dinámicamente
        // que no estuviera en memoria cuando se llamó a RegisterBuiltInAssemblies.
        // Se hace UNA SOLA VEZ aquí, no dentro de LoadPluginDirectory, para evitar re-registros duplicados.
        loader.ScanCurrentAppDomain();

        return loader;
    }

    /// <summary>
    /// Registra los ensamblados de plugins oficiales incorporados en la solución.
    /// </summary>
    public static void RegisterBuiltInAssemblies(PluginLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.FileSystem.FolderSourceNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Archives.SmartUnpackNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Images.ImageOptimizerNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Logic.SwitchCaseNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Hashing.HashCalculatorNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Integrations.CliExecutionNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Scripting.CustomScriptNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.AI.PromptObjectDetectorNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Data.ExcelReaderNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Documents.PdfMergeNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Network.NetworkDownloadNode).Assembly);
        loader.RegisterNodeTypesFromAssembly(typeof(FileFlow.Plugin.Subflows.SubflowNode).Assembly);
    }

    /// <summary>
    /// Carga dinámicamente cualquier ensamblado de plugin adicional ubicado en el directorio /Plugins de la aplicación
    /// o en el directorio de plugins de datos del usuario (%AppData%/FileFlow/plugins o data/plugins).
    /// </summary>
    public static void LoadPluginsDirectory(PluginLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);

        // 1. Plugins distribuidos junto a la aplicación (solo lectura, sin crear carpeta en Program Files)
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string pluginsDirectory = Path.Combine(baseDir, "Plugins");
        if (Directory.Exists(pluginsDirectory))
        {
            loader.LoadPluginDirectory(pluginsDirectory);
        }

        // 2. Plugins de usuario instalados dinámicamente
        string userPluginsDir = FileFlow.Sdk.Storage.AppPaths.PluginsDirectory;
        if (Directory.Exists(userPluginsDir))
        {
            loader.LoadPluginDirectory(userPluginsDir);
        }
    }
}
