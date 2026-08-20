using System.Resources;
using System.Windows;
using FileFlow.Sdk.Localization;

namespace FileFlow.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var resourceManager = new ResourceManager("FileFlow.App.Resources.Strings", typeof(App).Assembly);
        LocalizationManager.Instance.RegisterResourceManager(resourceManager);
        LocalizationManager.Instance.SetCulture("es-ES");
    }
}
