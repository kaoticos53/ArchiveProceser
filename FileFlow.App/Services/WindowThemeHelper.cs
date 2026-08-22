using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FileFlow.App.Services;

public static class WindowThemeHelper
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static void ApplyThemeToWindow(Window window)
    {
        if (window == null) return;

        bool isDarkTheme = ThemeManager.Instance.CurrentTheme switch
        {
            AppTheme.Light => false,
            AppTheme.Pastel => false,
            AppTheme.Cyber => true,
            AppTheme.Dark => true,
            AppTheme.System => !IsWindowsInLightMode(),
            _ => true
        };

        if (window.IsLoaded)
        {
            SetWindowDarkMode(window, isDarkTheme);
        }
        else
        {
            window.Loaded += (s, e) => SetWindowDarkMode(window, isDarkTheme);
        }
    }

    private static void SetWindowDarkMode(Window window, bool isDarkTheme)
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int useDarkMode = isDarkTheme ? 1 : 0;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
            }
        }
        catch
        {
            // Ignore if OS does not support DWM dark mode attribute
        }
    }

    private static bool IsWindowsInLightMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int intVal)
            {
                return intVal != 0;
            }
        }
        catch
        {
        }
        return false;
    }
}
