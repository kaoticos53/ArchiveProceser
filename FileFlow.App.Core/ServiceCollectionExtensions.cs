using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Platform;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.App.Core;

/// <summary>
/// Registro de servicios portables del núcleo de aplicación (FileFlow.App.Core): motor, plugins,
/// servicios de infraestructura y ViewModels. Es la capa que comparten los hosts (Avalonia, Uno);
/// cada host añade después sus adaptadores de plataforma (diálogos, portapapeles, despachado,
/// ventanas) ANTES de resolver ViewModels, porque varios ViewModels toman los adaptadores por
/// parámetro opcional y caen a sus implementaciones nulas si nadie los registró.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Devuelve el constructor listo para que el host registre sus adaptadores.</summary>
    public static IServiceCollection AddFileFlowCoreServices(this IServiceCollection services)
    {
        // 1. Servicios Base y Puertos de Dominio / Core (idéntico en ambos hosts)
        services.AddSingleton<ILocalizationService>(_ => LocalizationManager.Instance);
        services.AddSingleton<ILogStore>(_ => SqliteLogStore.Instance);
        services.AddSingleton<IFileRecycler>(_ => WindowsShellFileRecycler.Instance);
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddTransient<IFolderWatcherService, FolderWatcherService>();
        services.AddSingleton<FileFlow.Sdk.Services.IExternalToolsService>(_ => FileFlow.Core.Services.ExternalToolsService.Instance);

        // 2. Cargador de Plugins con auto-descubrimiento (única implementación, compartida)
        services.AddSingleton(sp => PluginRegistryHelper.CreateConfiguredLoader());

        // 3. Servicios y Adaptadores de Infraestructura (portables)
        services.AddSingleton<IWorkflowStorageService, WorkflowStorageService>();
        services.AddSingleton<IVariableDiscoveryService, VariableDiscoveryService>();
        services.AddSingleton<INodeClipboardService, NodeClipboardService>();
        services.AddSingleton<ISystemPerformanceMonitor, SystemPerformanceMonitor>();
        services.AddSingleton<IThemeService>(_ => ThemeManager.Instance);
        services.AddSingleton<IUserPreferencesService>(_ => UserPreferencesService.Instance);
        services.AddSingleton<IFileDialogService, NullFileDialogService>();
        services.AddSingleton<IColorPickerService, NullColorPickerService>();

        // El registro de latidos de la aplicación: uno solo, de modo que los cuatro latidos —vigilante de
        // subflujos, consola, rendimiento y fotograma visual— queden declarados en el mismo sitio y una guardia
        // pueda leerlos. Antes cada componente programaba el suyo y no había forma de enumerarlos.
        services.AddSingleton<IHeartbeatService>(sp => new HeartbeatService(uiDispatcher: sp.GetRequiredService<IUiDispatcher>()));
        services.AddSingleton<IProcessLauncherService, ProcessLauncherService>();
        services.AddSingleton<FileFlow.App.Services.UndoRedo.IUndoRedoService, FileFlow.App.Services.UndoRedo.UndoRedoService>();

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
