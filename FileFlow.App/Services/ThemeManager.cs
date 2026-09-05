using System.Windows;
using FileFlow.Sdk.Themes;
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

public class ThemeManager : IThemeService
{
    private static readonly Lazy<ThemeManager> _instance = new(() => new ThemeManager());
    public static ThemeManager Instance => _instance.Value;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
    public string CurrentThemeId { get; private set; } = "dark_fluent";
    public ThemeDefinition? ActiveThemeDefinition { get; private set; }

    public bool IsCurrentThemeDark => ActiveThemeDefinition?.IsDark ?? (CurrentTheme switch
    {
        AppTheme.Light => false,
        AppTheme.Pastel => false,
        AppTheme.System => !IsWindowsInLightMode(),
        _ => true
    });

    public event Action<AppTheme>? ThemeChanged;
    public event Action<ThemeDefinition>? CustomThemeChanged;

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
            CurrentThemeId = "system";
        }
        else
        {
            string themeId = theme switch
            {
                AppTheme.Light => "light_studio",
                AppTheme.Pastel => "pastel_spring",
                AppTheme.Cyber => "cyber_neon",
                _ => "dark_fluent"
            };

            var themeDef = CustomThemeService.Instance.GetThemeById(themeId);
            if (themeDef != null)
            {
                SetTheme(themeDef);
                return;
            }

            string themeUri = theme switch
            {
                AppTheme.Light => "Themes/LightTheme.xaml",
                AppTheme.Pastel => "Themes/PastelTheme.xaml",
                AppTheme.Cyber => "Themes/CyberTheme.xaml",
                _ => "Themes/DarkTheme.xaml"
            };
            ApplyThemeResource(themeUri);
            CurrentThemeId = themeId;
        }
        ThemeChanged?.Invoke(CurrentTheme);
    }

    public void SetThemeById(string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return;

        if (themeId.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            SetTheme(AppTheme.System);
            return;
        }

        var themeDef = CustomThemeService.Instance.GetThemeById(themeId);
        if (themeDef != null)
        {
            SetTheme(themeDef);
            return;
        }

        if (Enum.TryParse<AppTheme>(themeId, true, out var appTheme))
        {
            SetTheme(appTheme);
        }
    }

    public void SetTheme(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        ActiveThemeDefinition = theme.Clone();
        CurrentThemeId = theme.Id;

        // Mapear al enum clásico aproximado si coincide
        CurrentTheme = theme.Id.ToLowerInvariant() switch
        {
            "light_studio" or "light" => AppTheme.Light,
            "pastel_spring" or "pastel" => AppTheme.Pastel,
            "cyber_neon" or "cyber" => AppTheme.Cyber,
            _ => theme.IsDark ? AppTheme.Dark : AppTheme.Light
        };

        var dict = CustomThemeService.BuildResourceDictionary(theme);
        ApplyResourceDictionary(dict);

        ThemeChanged?.Invoke(CurrentTheme);
        CustomThemeChanged?.Invoke(ActiveThemeDefinition);
    }

    private void ApplySystemTheme()
    {
        bool isLight = IsWindowsInLightMode();
        string themeId = isLight ? "light_studio" : "dark_fluent";
        var themeDef = CustomThemeService.Instance.GetThemeById(themeId);
        if (themeDef != null)
        {
            var dict = CustomThemeService.BuildResourceDictionary(themeDef);
            ApplyResourceDictionary(dict);
            ActiveThemeDefinition = themeDef;
        }
        else
        {
            ApplyThemeResource(isLight ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml");
        }
    }

    public static void ApplyResourceDictionary(ResourceDictionary newThemeDict)
    {
        var app = Application.Current;
        if (app == null) return;

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

    private static void ApplyThemeResource(string themeRelativeUri)
    {
        var app = Application.Current;
        if (app == null) return;

        var newThemeDict = new ResourceDictionary
        {
            Source = new Uri(themeRelativeUri, UriKind.RelativeOrAbsolute)
        };

        ApplyResourceDictionary(newThemeDict);
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
