using FileFlow.App.Core;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Platform;
using FileFlow.Sdk.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registro de servicios portables del núcleo de aplicación (FileFlow.App.Core): motor, plugins,
/// servicios de infraestructura y ViewModels. Es la capa que comparten los hosts;
/// cada host añade después sus adaptadores de plataforma (diálogos, portapapeles, despachado,
/// ventanas) ANTES de resolver ViewModels.
/// </summary>
public static class FileFlowServiceCollectionExtensions
{
    /// <summary>Alias compatible con la API canónica del producto.</summary>
    public static IServiceCollection AddFileFlowServices(this IServiceCollection services) =>
        services.AddFileFlowCoreServices();

    /// <summary>Devuelve el constructor listo para que el host registre sus adaptadores.</summary>
    public static IServiceCollection AddFileFlowCoreServices(this IServiceCollection services)
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

        // 3. Servicios y Adaptadores de Infraestructura (portables con fallback neutro)
        services.AddSingleton<IWorkflowStorageService, WorkflowStorageService>();
        services.AddSingleton<IVariableDiscoveryService, VariableDiscoveryService>();
        services.AddSingleton<INodeClipboardService, NodeClipboardService>();
        services.AddSingleton<ISystemPerformanceMonitor, SystemPerformanceMonitor>();
        services.AddSingleton<IThemeService>(_ => ThemeManager.Instance);
        services.AddSingleton<IUserPreferencesService>(_ => UserPreferencesService.Instance);
        services.AddSingleton<IFileDialogService, NullFileDialogService>();
        services.AddSingleton<IColorPickerService, NullColorPickerService>();
        services.AddSingleton<IUiDispatcher>(_ => NullUiDispatcher.Instance);
        services.AddSingleton<IDialogService>(_ => NullDialogService.Instance);
        services.AddSingleton<IClipboardService>(_ => NullClipboardService.Instance);
        services.AddSingleton<IWindowService>(_ => NullWindowService.Instance);
        services.AddSingleton<IPopupMenuService>(_ => NullPopupMenuService.Instance);

        // El registro de latidos de la aplicación
        services.AddSingleton<IHeartbeatService>(sp => new HeartbeatService(uiDispatcher: sp.GetService<IUiDispatcher>() ?? NullUiDispatcher.Instance));
        services.AddSingleton<IProcessLauncherService, ProcessLauncherService>();
        services.AddSingleton<FileFlow.App.Services.UndoRedo.IUndoRedoService, FileFlow.App.Services.UndoRedo.UndoRedoService>();

        // 4. ViewModels (Ciclo de vida Singleton en el ámbito de la aplicación)
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
