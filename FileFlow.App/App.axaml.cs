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

    public override void OnFrameworkInitializationCompleted()
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
            try
            {
                var resourceManager = new ResourceManager("FileFlow.App.Resources.Strings", typeof(App).Assembly);
                LocalizationManager.Instance.RegisterResourceManager(resourceManager);

                var serviceCollection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
                serviceCollection.AddFileFlowServices();
                Services = serviceCollection.BuildServiceProvider();

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

                var pluginLoader = Services.GetRequiredService<FileFlow.Core.Plugins.PluginLoader>();

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

                var mainVm = Services.GetRequiredService<ViewModels.MainViewModel>();
                var mainWindow = new MainWindow
                {
                    DataContext = mainVm
                };

                desktop.MainWindow = mainWindow;
                mainWindow.Show();

                // Comprobación de actualizaciones en segundo plano no bloqueante
                if (prefsService.Preferences.AutoCheckForUpdates)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(3000); // Esperar a que la UI esté completamente renderizada
                            var channel = string.Equals(prefsService.Preferences.UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase)
                                ? FileFlow.Sdk.Services.UpdateChannel.Beta
                                : FileFlow.Sdk.Services.UpdateChannel.Stable;

                            var checkResult = await AppUpdateService.Instance.CheckForUpdatesAsync(channel, force: false, CancellationToken.None);
                            if (checkResult.UpdateAvailable && checkResult.UpdateInfo != null)
                            {
                                if (!string.Equals(prefsService.Preferences.IgnoredUpdateVersion, checkResult.UpdateInfo.VersionTag, StringComparison.OrdinalIgnoreCase))
                                {
                                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                    {
                                        mainVm.ControlBar.SetPendingUpdate(checkResult.UpdateInfo);
                                    });
                                }
                            }
                        }
                        catch
                        {
                            // Comprobación en segundo plano tolerante a fallos
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogCrashToFile(ex);
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void LogCrashToFile(object exception)
    {
        string logText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Unhandled Exception:\n{exception}\n\n";

        try
        {
            FileFlow.Sdk.Storage.AppPaths.EnsureDirectories();
            string crashFile = FileFlow.Sdk.Storage.AppPaths.CrashLogFile;
            string? dir = Path.GetDirectoryName(crashFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            System.IO.File.AppendAllText(crashFile, logText);
            return;
        }
        catch { }

        // Fallback a %TEMP%/fileflow_crash.log si el directorio estándar no fuese accesible
        try
        {
            string tempCrash = Path.Combine(Path.GetTempPath(), "fileflow_crash.log");
            System.IO.File.AppendAllText(tempCrash, logText);
        }
        catch { }
    }
}
