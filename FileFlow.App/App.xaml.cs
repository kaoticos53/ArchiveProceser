using System.Resources;
using System.Windows;
using FileFlow.App.Services;
using FileFlow.App.Views;
using FileFlow.Sdk.Localization;

namespace FileFlow.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogCrashToFile(args.ExceptionObject);
            try
            {
                MessageBox.Show($"Error no controlado en la aplicación:\n{args.ExceptionObject}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            LogCrashToFile(args.Exception);
            try
            {
                MessageBox.Show($"Error de interfaz (XAML/UI):\n{args.Exception.Message}\n\n{args.Exception.InnerException?.Message}", "Error UI", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogCrashToFile(args.Exception);
            System.Diagnostics.Debug.WriteLine($"Unobserved Task Exception: {args.Exception.Message}");
            args.SetObserved();
        };

        base.OnStartup(e);

        // Mostrar pantalla de carga atractiva
        var splash = new SplashScreenWindow();
        splash.Show();

        try
        {
            splash.UpdateStatus("Iniciando servicios de localización y recursos...", 15);
            await Task.Delay(40);

            var resourceManager = new ResourceManager("FileFlow.App.Resources.Strings", typeof(App).Assembly);
            LocalizationManager.Instance.RegisterResourceManager(resourceManager);
            LocalizationManager.Instance.SetCulture("es-ES");

            splash.UpdateStatus("Cargando preferencias de usuario y temas...", 35);
            await Task.Delay(40);

            UserPreferencesService.Instance.Load();
            _ = ExternalToolsService.Instance.Config;

            string savedTheme = UserPreferencesService.Instance.Preferences.ActiveTheme;
            if (Enum.TryParse<AppTheme>(savedTheme, out var themeEnum))
            {
                ThemeManager.Instance.SetTheme(themeEnum);
            }

            splash.UpdateStatus("Cargando plugins y motor de nodos...", 60);
            await Task.Delay(40);

            splash.UpdateStatus("Construyendo espacio de trabajo y lienzo Nodify...", 85);
            await Task.Delay(40);

            var mainWindow = new MainWindow();

            splash.UpdateStatus("¡Listo! Iniciando FileFlow Studio...", 100);
            await Task.Delay(180);

            mainWindow.Show();
            MainWindow = mainWindow;

            await splash.CloseWithFadeAsync();
        }
        catch (Exception ex)
        {
            LogCrashToFile(ex);
            splash.Close();
            MessageBox.Show($"Error al iniciar la aplicación:\n{ex.Message}", "Error de Inicialización", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            FileFlow.Core.Telemetry.SqliteLogStore.Instance.Dispose();
        }
        catch
        {
            // Limpieza defensiva en apagado
        }

        base.OnExit(e);
    }

    private static void LogCrashToFile(object exception)
    {
        try
        {
            string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileFlowStudio");
            System.IO.Directory.CreateDirectory(appData);
            string crashFile = System.IO.Path.Combine(appData, "crash.log");
            string logText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Unhandled Exception:\n{exception}\n\n";
            System.IO.File.AppendAllText(crashFile, logText);
        }
        catch { }
    }
}
