using System.Windows;

namespace FileFlow.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Services.WindowThemeHelper.ApplyThemeToWindow(this);

        Services.ThemeManager.Instance.ThemeChanged += (theme) =>
        {
            Services.WindowThemeHelper.ApplyThemeToWindow(this);
        };
    }
}
