using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Styling;

namespace FileFlow.App.Services;

public static class WindowThemeHelper
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>
    /// Aplica el tema activo a todas las ventanas abiertas de la aplicación (principal y diálogos).
    /// Las ventanas creadas con posterioridad heredan la variante desde <see cref="Avalonia.Application.RequestedThemeVariant"/>.
    /// </summary>
    public static void ApplyThemeToOpenWindows()
    {
        var app = Avalonia.Application.Current;
        if (app == null) return;

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(ApplyThemeToOpenWindows);
            return;
        }

        if (app.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows.ToArray())
            {
                ApplyThemeToWindow(window);
            }
        }
    }

    /// <summary>
    /// Traduce el carácter claro/oscuro del tema activo a la variante de FluentTheme.
    /// Punto único de decisión usado tanto para la Application como para cada ventana.
    /// </summary>
    public static ThemeVariant ResolveThemeVariant(bool isDark) => isDark ? ThemeVariant.Dark : ThemeVariant.Light;

    public static void ApplyThemeToWindow(Window window)
    {
        if (window == null) return;

        bool isDarkTheme = ThemeManager.Instance.IsCurrentThemeDark;
        window.RequestedThemeVariant = ResolveThemeVariant(isDarkTheme);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (handle != IntPtr.Zero)
                {
                    int useDarkMode = isDarkTheme ? 1 : 0;
                    if (DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int)) != 0)
                    {
                        DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
                    }
                }
            }
            catch
            {
                // Ignored
            }
        }
    }
}
