using System.IO;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Platform;
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
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddTransient<IFolderWatcherService, FolderWatcherService>();
        services.AddSingleton<FileFlow.Sdk.Services.IExternalToolsService>(_ => FileFlow.Core.Services.ExternalToolsService.Instance);

        // 2. Cargador de Plugins con auto-descubrimiento
        services.AddSingleton(sp => PluginRegistryHelper.CreateConfiguredLoader());

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
        services.AddTransient<AiModelManagerViewModel>();
        services.AddTransient<WorkflowSettingsViewModel>();

        return services;
    }
}
