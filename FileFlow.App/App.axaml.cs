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

            // Los recursos del host van primero: la splash se construye inmediatamente después y así sus
            // textos XAML resuelven ya traducidos (sin ellos saldrían con la clave cruda en el primer
            // fotograma, porque el indexador devuelve la clave cuando el diccionario no está registrado).
            startup.TryExecute(StartupPhase.Resources, RegisterHostResources);

            // La splash es la primera superficie visible: se muestra antes de las etapas pesadas para que
            // el usuario vea progreso en lugar de una pantalla vacía. Todo corre síncrono en el hilo de UI
            // (las continuaciones async del arranque creaban pinceles fuera del hilo de UI y tumbaban el
            // render, hito 153); los descansos entre etapas son despachos que dejan pintar el fotograma real.
            SplashScreenWindow? splash = null;
            startup.TryExecute(StartupPhase.Splash, () =>
            {
                splash = new SplashScreenWindow();
                splash.Show();

                // Barrido de acento de la barra: sólo en la aplicación real. En headless no arranca, para
                // que las capturas de la splash sean deterministas (ver SplashScreenWindow.StartShimmer).
                splash.StartShimmer();

                PumpFrame();
            });

            // Cada etapa va aislada: si una falla, el informe dice CUÁL, y el arranque se detiene de forma
            // controlada en lugar de morir sin abrir ninguna ventana.
            startup.TryExecute(StartupPhase.Services, () =>
            {
                splash?.UpdateStatus(LocalizationString("Splash_StatusServices", "Construyendo el contenedor de servicios..."), 20);
                PumpFrame();
                BuildServices();
            });

            IUserPreferencesService? preferences = null;
            startup.TryExecute(StartupPhase.Preferences, () =>
            {
                splash?.UpdateStatus(LocalizationString("Splash_StatusPreferences", "Cargando preferencias..."), 40);
                PumpFrame();
                preferences = LoadPreferences();
            });
            startup.TryExecute(StartupPhase.Theme, () =>
            {
                splash?.UpdateStatus(LocalizationString("Splash_StatusTheme", "Aplicando el tema guardado..."), 55);
                PumpFrame();
                ApplySavedTheme();
            });

            FileFlow.Core.Plugins.PluginLoader? pluginLoader = null;
            startup.TryExecute(StartupPhase.Plugins, () =>
            {
                splash?.UpdateStatus(LocalizationString("Splash_StatusPlugins", "Descubriendo módulos y plugins..."), 70);
                PumpFrame();
                pluginLoader = LoadPlugins();
            });
            startup.TryExecute(StartupPhase.Shell, () =>
            {
                if (pluginLoader is not null && splash is not null)
                {
                    // El catálogo real de nodos, no una cuenta genérica.
                    splash.SetNodeCount(pluginLoader.DiscoveredNodesCount);
                }

                splash?.UpdateStatus(LocalizationString("Splash_StatusInterface", "Inicializando el lienzo DAG..."), 90);
                PumpFrame();
                return CreateAndShowMainWindow(desktop);
            }, out Window? _);

            if (startup.IsAborted)
            {
                // Antes de la ventana de error: es Topmost y taparía el informe del fallo.
                splash?.Close();
                AbortStartup(desktop);
            }
            else
            {
                if (preferences is not null)
                {
                    StartBackgroundWork(preferences);
                }

                // La ventana principal ya está en pantalla: el splash se retira desvaneciéndose sobre ella.
                // Fire-and-forget deliberado; la retirada no puede abortar un arranque ya completado.
                splash?.UpdateStatus(LocalizationString("Splash_StatusReady", "¡Listo!"), 100);
                _ = splash?.CloseWithFadeAsync();
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
    private static FileFlow.Core.Plugins.PluginLoader LoadPlugins() =>
        Services.GetRequiredService<FileFlow.Core.Plugins.PluginLoader>();

    /// <summary>
    /// Deja que el hilo de UI pinte lo pendiente antes de una etapa de arranque bloqueante: sin esto el
    /// splash se muestra con el último fotograma (o con ninguno) hasta el final, y el progreso nunca llega
    /// a verse. No puede lanzar: el arranque sigue aunque el despachador no pueda bombear (pruebas headless).
    /// </summary>
    private static void PumpFrame()
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        catch
        {
            // Sin bucle de mensajes disponible: no hay nada que pintar.
        }
    }

    /// <summary>Texto localizado con respaldo literal: el splash no puede quedarse en blanco.</summary>
    private static string LocalizationString(string key, string fallback) =>
        LocalizationManager.Instance.GetString(key, fallback);

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
