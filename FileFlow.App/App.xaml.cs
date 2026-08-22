using System.Resources;
using System.Windows;
using FileFlow.App.Services;
using FileFlow.Sdk.Localization;

namespace FileFlow.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"Error no controlado en la aplicación:\n{args.ExceptionObject}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Error de interfaz (XAML/UI):\n{args.Exception.Message}\n\n{args.Exception.InnerException?.Message}", "Error UI", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);

        var resourceManager = new ResourceManager("FileFlow.App.Resources.Strings", typeof(App).Assembly);
        LocalizationManager.Instance.RegisterResourceManager(resourceManager);
        LocalizationManager.Instance.SetCulture("es-ES");

        // Cargar configuración persistente e inicializar Tema y Herramientas
        UserPreferencesService.Instance.Load();
        _ = ExternalToolsService.Instance.Config;

        string savedTheme = UserPreferencesService.Instance.Preferences.ActiveTheme;
        if (Enum.TryParse<AppTheme>(savedTheme, out var themeEnum))
        {
            ThemeManager.Instance.SetTheme(themeEnum);
        }
    }
}
