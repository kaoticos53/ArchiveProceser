using System.IO;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.App.Services;

/// <summary>
/// Métodos de extensión para configurar y registrar todos los servicios y ViewModels de la aplicación en el contenedor IoC.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra todas las dependencias del motor, servicios de infraestructura, adaptadores de UI y ViewModels.
    /// </summary>
    public static IServiceCollection AddFileFlowServices(this IServiceCollection services)
    {
        // 1. Servicios Base y Puertos de Dominio / Core
        services.AddSingleton<ILocalizationService>(_ => LocalizationManager.Instance);
        services.AddSingleton<ILogStore>(_ => SqliteLogStore.Instance);
        services.AddSingleton<IFileRecycler>(_ => WindowsShellFileRecycler.Instance);
        services.AddTransient<IFolderWatcherService, FolderWatcherService>();

        // 2. Cargador de Plugins con auto-descubrimiento
        services.AddSingleton(sp =>
        {
            var loader = new PluginLoader();
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

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string pluginsDirectory = Path.Combine(baseDir, "Plugins");
            if (!Directory.Exists(pluginsDirectory))
            {
                Directory.CreateDirectory(pluginsDirectory);
            }
            loader.LoadPluginDirectory(pluginsDirectory);
            return loader;
        });

        // 3. Servicios y Adaptadores de Infraestructura de la UI
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IWorkflowStorageService, WorkflowStorageService>();
        services.AddSingleton<IVariableDiscoveryService, VariableDiscoveryService>();
        services.AddSingleton<INodeClipboardService, NodeClipboardService>();
        services.AddSingleton<ISystemPerformanceMonitor, SystemPerformanceMonitor>();
        services.AddSingleton<IThemeService>(_ => ThemeManager.Instance);
        services.AddSingleton<IUserPreferencesService>(_ => UserPreferencesService.Instance);
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IProcessLauncherService, ProcessLauncherService>();

        // 4. ViewModels (Ciclo de vida Singleton en el ámbito de aplicación de escritorio)
        services.AddSingleton<LogViewModel>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<ToolboxViewModel>();
        services.AddSingleton<NodeInspectorViewModel>();
        services.AddSingleton<ControlBarViewModel>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<MainViewModel>();

        return services;
    }
}
