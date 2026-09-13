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

    public static void ApplyThemeToWindow(Window window)
    {
        if (window == null) return;

        bool isDarkTheme = ThemeManager.Instance.IsCurrentThemeDark;
        window.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;

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
