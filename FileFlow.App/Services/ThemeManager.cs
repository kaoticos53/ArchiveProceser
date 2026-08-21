using System.Windows;
using Microsoft.Win32;

namespace FileFlow.App.Services;

public enum AppTheme
{
    Dark,
    Light,
    Pastel,
    Cyber,
    System
}

public class ThemeManager
{
    private static readonly Lazy<ThemeManager> _instance = new(() => new ThemeManager());
    public static ThemeManager Instance => _instance.Value;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public event Action<AppTheme>? ThemeChanged;

    private ThemeManager()
    {
        SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            if (CurrentTheme == AppTheme.System)
            {
                ApplySystemTheme();
            }
        };
    }

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        if (theme == AppTheme.System)
        {
            ApplySystemTheme();
        }
        else
        {
            string themeUri = theme switch
            {
                AppTheme.Light => "Themes/LightTheme.xaml",
                AppTheme.Pastel => "Themes/PastelTheme.xaml",
                AppTheme.Cyber => "Themes/CyberTheme.xaml",
                _ => "Themes/DarkTheme.xaml"
            };
            ApplyThemeResource(themeUri);
        }
        ThemeChanged?.Invoke(CurrentTheme);
    }

    private void ApplySystemTheme()
    {
        bool isLight = IsWindowsInLightMode();
        ApplyThemeResource(isLight ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml");
    }

    private static void ApplyThemeResource(string themeRelativeUri)
    {
        var app = Application.Current;
        if (app == null) return;

        var newThemeDict = new ResourceDictionary
        {
            Source = new Uri(themeRelativeUri, UriKind.RelativeOrAbsolute)
        };

        // Reemplazar o insertar el diccionario de tema en la primera posición
        var merged = app.Resources.MergedDictionaries;
        if (merged.Count > 0)
        {
            merged[0] = newThemeDict;
        }
        else
        {
            merged.Add(newThemeDict);
        }
    }

    private static bool IsWindowsInLightMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int intVal)
            {
                return intVal != 0;
            }
        }
        catch
        {
            // Fallback to dark if registry cannot be read
        }
        return false;
    }
}
