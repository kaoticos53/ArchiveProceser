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
        RegisterBuiltInAssemblies(loader);
        LoadPluginsDirectory(loader);
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
    }

    /// <summary>
    /// Carga dinámicamente cualquier ensamblado de plugin adicional ubicado en el directorio /Plugins de la aplicación.
    /// </summary>
    public static void LoadPluginsDirectory(PluginLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string pluginsDirectory = Path.Combine(baseDir, "Plugins");
        if (!Directory.Exists(pluginsDirectory))
        {
            Directory.CreateDirectory(pluginsDirectory);
        }
        loader.LoadPluginDirectory(pluginsDirectory);
    }
}
