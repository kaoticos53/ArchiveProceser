using System;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FileFlow.App.Models;
using FileFlow.App.Services;
using FileFlow.App.Views;
using FileFlow.Sdk.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.App;

public partial class App : Application
{
    /// <summary>Código de salida cuando el arranque falla: el proceso termina diciendo que no arrancó.</summary>
    public const int StartupFailureExitCode = 1;

    private static int s_fatalSurfaced;

    public static IServiceProvider Services { get; private set; } = null!;
    public static Window? MainWindow => (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    /// <summary>
    /// Registro de incidentes acotado (deduplicación + rotación): un emisor que falle en bucle ya no puede
    /// escribir cientos de entradas ni hacer crecer el fichero sin límite.
    /// </summary>
    private static readonly CrashLogWriter s_crashLog = new();

    /// <summary>Convierte un fallo de arranque en algo visible (log + ventana de error).</summary>
    private static readonly StartupFailureReporter s_startupFailures = new(s_crashLog);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogCrashToFile(args.ExceptionObject);
            SurfaceFatalException(args.ExceptionObject);
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            // Una tarea no observada no mata el proceso: se registra (con deduplicación y rotación) y se da por
            // observada. No se muestra diálogo: la aplicación sigue funcionando.
            LogCrashToFile(args.Exception);
            System.Diagnostics.Debug.WriteLine($"Unobserved Task Exception: {args.Exception.Message}");
            args.SetObserved();
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startup = new StartupOrchestrator(s_startupFailures);

            // Cada etapa va aislada: si una falla, el informe dice CUÁL, y el arranque se detiene de forma
            // controlada en lugar de morir sin abrir ninguna ventana.
            startup.TryExecute(StartupPhase.Resources, RegisterHostResources);
            startup.TryExecute(StartupPhase.Services, BuildServices);

            IUserPreferencesService? preferences = null;
            startup.TryExecute(StartupPhase.Preferences, () => preferences = LoadPreferences());
            startup.TryExecute(StartupPhase.Theme, ApplySavedTheme);
            startup.TryExecute(StartupPhase.Plugins, LoadPlugins);
            startup.TryExecute(StartupPhase.Shell, () => CreateAndShowMainWindow(desktop), out Window? _);

            if (startup.IsAborted)
            {
                AbortStartup(desktop);
            }
            else if (preferences is not null)
            {
                StartBackgroundWork(preferences);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Etapas del arranque
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Registra los recursos de texto del host antes que nada: sin ellos la interfaz sale sin textos.</summary>
    private static void RegisterHostResources()
    {
        var resourceManager = new ResourceManager("FileFlow.App.Resources.Strings", typeof(App).Assembly);
        LocalizationManager.Instance.RegisterResourceManager(resourceManager);
    }

    /// <summary>Construye el contenedor de servicios. Un grafo mal registrado se ve aquí, no al usar la interfaz.</summary>
    private static void BuildServices()
    {
        var serviceCollection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        serviceCollection.AddFileFlowServices();
        Services = serviceCollection.BuildServiceProvider();
    }

    /// <summary>Carga las preferencias del usuario, normaliza el idioma guardado y fija la cultura de arranque.</summary>
    private static IUserPreferencesService LoadPreferences()
    {
        var prefsService = Services.GetRequiredService<IUserPreferencesService>();
        prefsService.Load();

        string savedLang = prefsService.Preferences.Language;

        // El idioma guardado se normaliza a uno de los que ofrece la aplicación. Un valor ausente o heredado
        // deja el selector de idioma sin nada que mostrar (el desplegable sólo pinta un valor que exista en su
        // lista) y, si encima el control devuelve 'null' al view model, la preferencia se guarda vacía: es
        // justo el estado del que no se vuelve solo. Se reescribe una vez y el idioma queda consistente.
        string language = LanguageCatalog.Resolve(savedLang)?.Code ?? LanguageCatalog.All[0].Code;
        LocalizationManager.Instance.SetCulture(language);

        if (!string.Equals(language, savedLang, StringComparison.Ordinal))
        {
            prefsService.Preferences.Language = language;
            prefsService.Save();
        }

        // Fuerza la inicialización de las herramientas externas en el arranque (rutas y disponibilidad).
        _ = Services.GetRequiredService<FileFlow.Sdk.Services.IExternalToolsService>().Config;

        return prefsService;
    }

    /// <summary>Aplica el tema guardado, normalizando el identificador almacenado.</summary>
    private static void ApplySavedTheme()
    {
        var prefsService = Services.GetRequiredService<IUserPreferencesService>();
        string savedTheme = prefsService.Preferences.ActiveTheme;

        // Las preferencias antiguas guardaban el nombre del enumerado ('Dark', 'Light') y el catálogo usa sus
        // propios identificadores ('dark_fluent', 'light_studio'): sin traducirlo, el selector de tema del menú
        // no encontraba su valor en la lista y aparecía en blanco aunque el tema se aplicara.
        string themeId = ThemeManager.ResolveThemeId(savedTheme) ?? ThemeManager.DefaultThemeId;
        Services.GetRequiredService<IThemeService>().SetThemeById(themeId);

        if (!string.Equals(themeId, savedTheme, StringComparison.Ordinal))
        {
            // Se reescribe una sola vez: la preferencia deja de arrastrar el identificador heredado.
            prefsService.Preferences.ActiveTheme = themeId;
            prefsService.Save();
        }
    }

    /// <summary>Resuelve el cargador de plugins: aquí se descubre el catálogo de nodos.</summary>
    private static void LoadPlugins()
    {
        _ = Services.GetRequiredService<FileFlow.Core.Plugins.PluginLoader>();
    }

    /// <summary>
    /// Construye y muestra la ventana principal. Es la etapa que cubre el XAML: una plantilla o un estilo
    /// inválido se reporta como «interfaz principal (XAML)» y no como un cierre sin explicación.
    /// </summary>
    private static Window CreateAndShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var mainVm = Services.GetRequiredService<ViewModels.MainViewModel>();
        var mainWindow = new MainWindow
        {
            DataContext = mainVm
        };

        desktop.MainWindow = mainWindow;
        mainWindow.Show();

        return mainWindow;
    }

    /// <summary>
    /// Detiene el arranque de forma visible: la ventana de error ya está en pantalla (la mostró el reporter) y
    /// la aplicación se cierra con código de error en cuanto el usuario la cierre. Sin ella, al menos no se
    /// queda un proceso colgado sin ventanas.
    /// </summary>
    private static void AbortStartup(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var errorWindow = StartupErrorWindow.Current;
        if (errorWindow is null)
        {
            desktop.Shutdown(StartupFailureExitCode);
            return;
        }

        desktop.MainWindow = errorWindow;
        errorWindow.Closed += (_, _) => desktop.Shutdown(StartupFailureExitCode);
    }

    /// <summary>
    /// Tareas de fondo posteriores a un arranque correcto: limpieza de temporales y comprobación de
    /// actualizaciones. Ninguna puede tumbar la aplicación.
    /// </summary>
    private static void StartBackgroundWork(IUserPreferencesService prefsService)
    {
        if (prefsService.Preferences.CleanStaleTempOnStartup)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    FileFlow.Sdk.Storage.AppPaths.CleanupStaleTempDirectories(TimeSpan.FromHours(2));
                }
                catch
                {
                    // Best effort background housekeeping
                }
            });
        }

        if (!prefsService.Preferences.AutoCheckForUpdates)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3000); // Esperar a que la UI esté completamente renderizada
                var channel = string.Equals(prefsService.Preferences.UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase)
                    ? FileFlow.Sdk.Services.UpdateChannel.Beta
                    : FileFlow.Sdk.Services.UpdateChannel.Stable;

                var checkResult = await AppUpdateService.Instance.CheckForUpdatesAsync(channel, force: false, CancellationToken.None);
                if (!checkResult.UpdateAvailable || checkResult.UpdateInfo is null)
                {
                    return;
                }

                if (string.Equals(prefsService.Preferences.IgnoredUpdateVersion, checkResult.UpdateInfo.VersionTag, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    (MainWindow?.DataContext as ViewModels.MainViewModel)?.ControlBar.SetPendingUpdate(checkResult.UpdateInfo);
                });
            }
            catch
            {
                // Comprobación en segundo plano tolerante a fallos
            }
        });
    }

    /// <summary>
    /// Hace visible un fallo no controlado una vez arrancada la aplicación: el proceso va a terminar, así que
    /// el usuario debe saber por qué. Sólo se muestra un diálogo (el primero); el resto se registra.
    /// </summary>
    private static void SurfaceFatalException(object? exceptionObject)
    {
        if (exceptionObject is not Exception ex)
        {
            return;
        }

        if (Interlocked.Exchange(ref s_fatalSurfaced, 1) != 0)
        {
            return;
        }

        s_startupFailures.Report(StartupPhase.Runtime, ex);
    }

    /// <summary>
    /// Vuelca un incidente al log acotado. La deduplicación, la rotación y el fallback a
    /// <c>%TEMP%/fileflow_crash.log</c> viven en <see cref="Services.CrashLogWriter"/>.
    /// </summary>
    private static void LogCrashToFile(object exception)
    {
        try
        {
            FileFlow.Sdk.Storage.AppPaths.EnsureDirectories();
        }
        catch { }

        s_crashLog.Write(exception);
    }
}
