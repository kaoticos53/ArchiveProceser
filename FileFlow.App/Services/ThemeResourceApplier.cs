using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FileFlow.App.Themes;

namespace FileFlow.App.Services;

/// <summary>
/// Generador de diccionarios de recursos Avalonia (Brushes, Dropshadows, Tipografías) a partir de definiciones de temas.
/// </summary>
public static class ThemeResourceApplier
{
    public static ResourceDictionary BuildResourceDictionary(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return Avalonia.Threading.Dispatcher.UIThread.Invoke(() => BuildResourceDictionary(theme));
        }

        var dict = new ResourceDictionary();

        void AddSolidBrush(string key, string hex, string fallbackHex = "#FFFFFF")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) hex = fallbackHex;
                var color = Color.Parse(hex);
                var brush = new SolidColorBrush(color);
                dict[key] = brush;
            }
            catch
            {
                var fallbackColor = Color.Parse(fallbackHex);
                var fallbackBrush = new SolidColorBrush(fallbackColor);
                dict[key] = fallbackBrush;
            }
        }

        AddSolidBrush("AppBackgroundBrush", theme.AppBackground, "#0D1117");
        AddSolidBrush("BgDarkBrush", theme.BgDark, "#0D1117");
        AddSolidBrush("BgEditorBrush", theme.BgEditor, "#10131B");
        AddSolidBrush("BgCardBrush", theme.BgCard, "#161B22");
        AddSolidBrush("BgSurfaceBrush", theme.BgSurface, "#131720");
        AddSolidBrush("BgHeaderBrush", theme.BgHeader, "#1A1F29");
        AddSolidBrush("BgHoverBrush", theme.BgHover, "#21262D");

        AddSolidBrush("AccentPrimaryBrush", theme.AccentPrimary, "#6366F1");
        AddSolidBrush("AccentHoverBrush", theme.AccentHover, "#4F46E5");
        AddSolidBrush("AccentGlowBrush", theme.AccentGlow, "#818CF8");
        AddSolidBrush("AccentSuccessBrush", theme.AccentSuccess, "#10B981");
        AddSolidBrush("AccentWarningBrush", theme.AccentWarning, "#F59E0B");
        AddSolidBrush("AccentErrorBrush", theme.AccentError, "#EF4444");
        AddSolidBrush("AccentCyanBrush", theme.AccentCyan, "#06B6D4");
        AddSolidBrush("AccentPurpleBrush", theme.AccentPurple, "#A855F7");

        AddSolidBrush("TextPrimaryBrush", theme.TextPrimary, "#F0F6FC");
        AddSolidBrush("TextSecondaryBrush", theme.TextSecondary, "#8B949E");
        AddSolidBrush("BorderDarkBrush", theme.BorderDark, "#30363D");
        AddSolidBrush("BorderSubtleBrush", theme.BorderSubtle, "#21262D");
        AddSolidBrush("GridLineBrush", theme.GridLine, "#1A202C");

        AddSolidBrush("ScrollbarThumbBrush", theme.ScrollbarThumb, "#384152");
        AddSolidBrush("ScrollbarThumbHoverBrush", theme.ScrollbarThumbHover, "#4F5B73");

        // Gradient connection wire brush
        try
        {
            var colStart = Color.Parse(string.IsNullOrWhiteSpace(theme.WireColorStart) ? "#818CF8" : theme.WireColorStart);
            var colMid = Color.Parse(string.IsNullOrWhiteSpace(theme.WireColorMid) ? "#6366F1" : theme.WireColorMid);
            var colEnd = Color.Parse(string.IsNullOrWhiteSpace(theme.WireColorEnd) ? "#C084FC" : theme.WireColorEnd);

            var gradBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(colStart, 0.0),
                    new GradientStop(colMid, 0.5),
                    new GradientStop(colEnd, 1.0)
                }
            };
            dict["ConnectionWireBrush"] = gradBrush;
        }
        catch
        {
        }

        // Typography & Scale Tokens
        try
        {
            dict["AppFontFamily"] = new FontFamily(string.IsNullOrWhiteSpace(theme.FontFamily) ? "Segoe UI" : theme.FontFamily);
            dict["CodeFontFamily"] = new FontFamily(string.IsNullOrWhiteSpace(theme.CodeFontFamily) ? "Cascadia Code, Consolas" : theme.CodeFontFamily);
            dict["AppFontSize"] = theme.BaseFontSize > 0 ? theme.BaseFontSize : 12.0;
            dict["AppCornerRadius"] = new CornerRadius(Math.Max(0, theme.CornerRadius));
        }
        catch
        {
        }

        return dict;
    }
}
