using System;
using FileFlow.App.Core;
using FileFlow.App.Themes;

namespace FileFlow.App.Services;

public enum AppTheme
{
    Dark,
    Light,
    Pastel,
    Cyber,
    System
}

/// <summary>
/// Gestor de temas portable: mantiene el estado (tema activo, variante, definición enriquecida),
/// resuelve identificadores heredados y publica los cambios. La aplicación al runtime de cada host
/// (variante de FluentTheme en el host original, recursos en WinUI…) vive en el puente
/// <see cref="ThemeHostBridge.PublishThemeVariant"/>, que el host instala en el arranque; sin host
/// (pruebas, headless) el estado sigue funcionando y publicar es un no-op.
/// </summary>
public class ThemeManager : IThemeService
{
    /// <summary>Identificador del tema oscuro por defecto (el que se aplica cuando no hay nada guardado).</summary>
    public const string DefaultThemeId = "dark_fluent";

    /// <summary>Identificador reservado del tema que sigue al sistema operativo.</summary>
    public const string SystemThemeId = "system";

    private static readonly Lazy<ThemeManager> _instance = new(() => new ThemeManager());
    public static ThemeManager Instance => _instance.Value;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
    public string CurrentThemeId { get; private set; } = "dark_fluent";
    public ThemeDefinition? ActiveThemeDefinition { get; private set; }

    public bool IsCurrentThemeDark => ActiveThemeDefinition?.IsDark ?? (CurrentTheme switch
    {
        AppTheme.Light => false,
        AppTheme.Pastel => false,
        AppTheme.System => !IsOperatingSystemInLightMode(),
        _ => true
    });

    public event Action<AppTheme>? ThemeChanged;
    public event Action<ThemeDefinition>? CustomThemeChanged;

    private ThemeManager()
    {
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
            string themeId = ThemeIdFor(theme);

            var themeDef = CustomThemeService.Instance.GetThemeById(themeId);
            if (themeDef != null)
            {
                SetTheme(themeDef);
                return;
            }

            CurrentThemeId = themeId;
            PublishThemeChange(IsDarkFor(theme));
        }
        ThemeChanged?.Invoke(CurrentTheme);
    }

    /// <summary>
    /// Determina si un tema integrado debe resolverse en variante oscura cuando no existe definición explícita.
    /// </summary>
    private static bool IsDarkFor(AppTheme theme) => theme switch
    {
        AppTheme.Light or AppTheme.Pastel => false,
        AppTheme.System => !IsOperatingSystemInLightMode(),
        _ => true
    };

    public void SetThemeById(string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return;

        // Un identificador heredado ('Dark', 'Light'…) se resuelve a su tema real; si no existe, no hay nada
        // que aplicar (antes se aceptaba cualquier nombre que casara con el enumerado y el selector quedaba
        // mostrando un valor que no está en su lista: campo en blanco).
        string? resolved = ResolveThemeId(themeId);
        if (resolved is null)
        {
            return;
        }

        if (resolved.Equals(SystemThemeId, StringComparison.OrdinalIgnoreCase))
        {
            SetTheme(AppTheme.System);
            return;
        }

        var themeDef = CustomThemeService.Instance.GetThemeById(resolved);
        if (themeDef != null)
        {
            SetTheme(themeDef);
            return;
        }

        if (Enum.TryParse<AppTheme>(resolved, true, out var appTheme))
        {
            SetTheme(appTheme);
        }
    }

    /// <summary>
    /// Resuelve un identificador guardado (o heredado) al identificador real del tema que existe hoy en el
    /// catálogo, o <c>null</c> si no corresponde a ningún tema.
    ///
    /// Las preferencias guardan el identificador tal como se escribió en su momento: las versiones antiguas
    /// almacenaban el nombre del enumerado (<c>"Dark"</c>) y el catálogo usa identificadores propios
    /// (<c>"dark_fluent"</c>). Sin esta traducción, un desplegable de temas atado a los identificadores del
    /// catálogo no encuentra el valor guardado y aparece en blanco aunque el tema sí se haya aplicado.
    /// </summary>
    public static string? ResolveThemeId(string? storedId)
    {
        if (string.IsNullOrWhiteSpace(storedId))
        {
            return null;
        }

        string candidate = storedId.Trim();

        if (candidate.Equals(SystemThemeId, StringComparison.OrdinalIgnoreCase))
        {
            return SystemThemeId;
        }

        // El catálogo manda: devuelve el identificador con su grafía real (una preferencia escrita con otra
        // capitalización se normaliza en lugar de crear una entrada duplicada).
        var definition = CustomThemeService.Instance.GetThemeById(candidate);
        if (definition != null)
        {
            return definition.Id;
        }

        return Enum.TryParse<AppTheme>(candidate, true, out var legacy) ? ThemeIdFor(legacy) : null;
    }

    private static string ThemeIdFor(AppTheme theme) => theme switch
    {
        AppTheme.Light => "light_studio",
        AppTheme.Pastel => "pastel_spring",
        AppTheme.Cyber => "cyber_neon",
        _ => DefaultThemeId
    };

    public void SetTheme(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        ActiveThemeDefinition = theme.Clone();
        CurrentThemeId = theme.Id;

        CurrentTheme = theme.Id.ToLowerInvariant() switch
        {
            "light_studio" or "light" => AppTheme.Light,
            "pastel_spring" or "pastel" => AppTheme.Pastel,
            "cyber_neon" or "cyber" => AppTheme.Cyber,
            _ => theme.IsDark ? AppTheme.Dark : AppTheme.Light
        };

        PublishThemeChange(theme.IsDark);

        ThemeChanged?.Invoke(CurrentTheme);
        CustomThemeChanged?.Invoke(ActiveThemeDefinition);
    }

    /// <summary>
    /// Publica el cambio de tema a través del puente del host: la variante clara/oscurecida y los
    /// tokens del tema activo. Sin host (pruebas) no hay runtime que notificar: el estado ya quedó
    /// actualizado y los eventos ya se emitieron.
    /// </summary>
    private static void PublishThemeChange(bool isDark)
    {
        ThemeHostBridge.PublishThemeVariant?.Invoke(isDark);
    }

    private void ApplySystemTheme()
    {
        bool isLight = IsOperatingSystemInLightMode();
        string themeId = isLight ? "light_studio" : DefaultThemeId;
        var themeDef = CustomThemeService.Instance.GetThemeById(themeId);
        if (themeDef != null)
        {
            ActiveThemeDefinition = themeDef;
            PublishThemeChange(!isLight);
        }
    }

    private static bool IsOperatingSystemInLightMode()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var val = key?.GetValue("AppsUseLightTheme");
                if (val is int intVal)
                {
                    return intVal != 0;
                }
            }
        }
        catch
        {
            // Fallback to dark
        }
        return false;
    }
}
