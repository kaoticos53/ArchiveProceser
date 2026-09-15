using System;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FileFlow.App.Services;
using FileFlow.App.Views;
using FileFlow.Sdk.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static Window? MainWindow => (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogCrashToFile(args.ExceptionObject);
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogCrashToFile(args.Exception);
            System.Diagnostics.Debug.WriteLine($"Unobserved Task Exception: {args.Exception.Message}");
            args.SetObserved();
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SplashScreenWindow? splash = null;
            try
            {
                splash = new SplashScreenWindow();
                splash.Show();
                splash.UpdateStatus("Iniciando localización y servicios...", 15);

                var resourceManager = new ResourceManager("FileFlow.App.Resources.Strings", typeof(App).Assembly);
                LocalizationManager.Instance.RegisterResourceManager(resourceManager);

                var serviceCollection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
                serviceCollection.AddFileFlowServices();
                Services = serviceCollection.BuildServiceProvider();

                splash.UpdateStatus("Cargando preferencias y tema...", 35);
                var prefsService = Services.GetRequiredService<IUserPreferencesService>();
                prefsService.Load();
                string savedLang = prefsService.Preferences.Language;
                LocalizationManager.Instance.SetCulture(!string.IsNullOrWhiteSpace(savedLang) ? savedLang : "es-ES");
                _ = Services.GetRequiredService<FileFlow.Sdk.Services.IExternalToolsService>().Config;

                var themeService = Services.GetRequiredService<IThemeService>();
                string savedTheme = prefsService.Preferences.ActiveTheme;
                if (Enum.TryParse<AppTheme>(savedTheme, out var themeEnum))
                {
                    themeService.SetTheme(themeEnum);
                }

                splash.UpdateStatus("Descubriendo módulos y plugins...", 60);
                var pluginLoader = Services.GetRequiredService<FileFlow.Core.Plugins.PluginLoader>();
                splash.SetNodeCount(pluginLoader.DiscoveredNodeTypes.Count);

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

                splash.UpdateStatus("Inicializando lienzo DAG...", 85);
                var mainVm = Services.GetRequiredService<ViewModels.MainViewModel>();
                var mainWindow = new MainWindow
                {
                    DataContext = mainVm
                };

                splash.UpdateStatus("¡Listo!", 100);
                desktop.MainWindow = mainWindow;
                mainWindow.Show();

                _ = splash.CloseWithFadeAsync();
            }
            catch (Exception ex)
            {
                splash?.Close();
                LogCrashToFile(ex);
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void LogCrashToFile(object exception)
    {
        try
        {
            FileFlow.Sdk.Storage.AppPaths.EnsureDirectories();
            string crashFile = FileFlow.Sdk.Storage.AppPaths.CrashLogFile;
            string logText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Unhandled Exception:\n{exception}\n\n";
            System.IO.File.AppendAllText(crashFile, logText);
        }
        catch { }
    }
}
