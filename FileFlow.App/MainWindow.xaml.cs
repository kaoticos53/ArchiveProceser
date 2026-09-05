using System.Windows;

namespace FileFlow.App;

public partial class MainWindow : Window
{
    public MainWindow() : this(App.Services?.GetService(typeof(ViewModels.MainViewModel)) as ViewModels.MainViewModel)
    {
    }

    public MainWindow(ViewModels.MainViewModel? mainViewModel)
    {
        InitializeComponent();
        if (mainViewModel != null)
        {
            DataContext = mainViewModel;
        }
        Services.WindowThemeHelper.ApplyThemeToWindow(this);

        Services.ThemeManager.Instance.ThemeChanged += (theme) =>
        {
            Services.WindowThemeHelper.ApplyThemeToWindow(this);
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current?.Shutdown();
    }
}
