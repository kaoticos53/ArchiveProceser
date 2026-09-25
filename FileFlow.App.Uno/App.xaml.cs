using System;
using FileFlow.App.Core;
using FileFlow.App.Uno.Platform;
using FileFlow.Core.Telemetry;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Platform;
using FileFlow.Sdk.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FileFlow.App.Uno;

/// <summary>
/// Composition root del host Uno Platform. Registra el núcleo portable completo (motor, plugins,
/// servicios de infraestructura y ViewModels de <c>FileFlow.App.Core</c>) y añade los tres
/// adaptadores de plataforma Uno/WinUI (diálogo, portapapeles, despachado). Sin ninguna referencia
/// a Avalonia: el núcleo y los ViewModels ya viven en FileFlow.App.Core (rebanada 2).
/// </summary>
public partial class App : Application
{
    /// <summary>Código de salida cuando el arranque falla: el proceso termina diciendo que no arrancó.</summary>
    public const int StartupFailureExitCode = 1;

    private static Window? s_mainWindow;
    private static IServiceProvider? s_services;

    /// <summary>Ventana principal; los adaptadores la usan para anclar diálogos.</summary>
    public static Window? MainWindow => s_mainWindow;

    public static IServiceProvider Services => s_services ?? throw new InvalidOperationException("La aplicación aún no ha arrancado.");

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        s_services = services.BuildServiceProvider();

        // Bordes del host hacia el núcleo portable: sin estas instalaciones el núcleo cae a sus
        // no-ops seguros (portapapeles descartado, vista previa sin ventana, temas sin publicar).
        HostUi.Install(
            dispatcher: s_services.GetRequiredService<IUiDispatcher>(),
            clipboard: s_services.GetRequiredService<IClipboardService>(),
            mainWindowOwner: null); // la ventana aún no existe; el dialog service la resuelve él mismo

        s_mainWindow = new MainWindow();
        s_mainWindow.Activate();
    }

    /// <summary>
    /// Registro de servicios idéntico en intención al del host Avalonia: la base portable
    /// (<c>AddFileFlowCoreServices</c>) más los adaptadores Uno/WinUI. Los ViewModels llegan del
    /// núcleo; aquí sólo se declara lo acoplado a la UI de este host.
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // 1. Núcleo portable: motor, plugins, servicios de infraestructura y ViewModels
        services.AddFileFlowCoreServices();

        // 2. Adaptadores de plataforma implementados con Uno (los únicos puntos acoplados a la UI)
        services.AddSingleton<IDialogService, UnoDialogService>();
        services.AddSingleton<IClipboardService, UnoClipboardService>();
        services.AddSingleton<IUiDispatcher, UnoUiDispatcher>();
    }
}
