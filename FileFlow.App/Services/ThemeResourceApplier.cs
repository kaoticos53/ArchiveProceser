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
        AddSolidBrush("TextMutedBrush", theme.TextMuted, "#7C8698");
        AddSolidBrush("TextOnAccentBrush", theme.TextOnAccent, "#FFFFFF");
        AddSolidBrush("BorderDarkBrush", theme.BorderDark, "#30363D");
        AddSolidBrush("BorderSubtleBrush", theme.BorderSubtle, "#21262D");
        AddSolidBrush("GridLineBrush", theme.GridLine, "#1A202C");

        // Translucent overlay surface (canvas HUD, floating zoom bar, floating cards over the DAG canvas).
        // Derived from the surface color so every theme - including user-created ones - gets a coherent overlay.
        // El alfa se integra en el propio Color (no en SolidColorBrush.Opacity) para que el token sea
        // un espejo exacto del declarado en el diccionario de arranque y comparable byte a byte.
        dict["OverlaySurfaceBrush"] = new SolidColorBrush(BuildOverlayColor(theme.BgSurface));

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
            dict["AppFontFamily"] = new FontFamily(string.IsNullOrWhiteSpace(theme.FontFamily) ? "Segoe UI Variable Text, Segoe UI, sans-serif" : theme.FontFamily);
            dict["CodeFontFamily"] = new FontFamily(string.IsNullOrWhiteSpace(theme.CodeFontFamily) ? "Cascadia Code, Consolas" : theme.CodeFontFamily);
            dict["AppFontSize"] = theme.BaseFontSize > 0 ? theme.BaseFontSize : 12.0;
            dict["AppCornerRadius"] = new CornerRadius(Math.Max(0, theme.CornerRadius));
        }
        catch
        {
        }

        // Escala tipográfica y de radios derivada del tema.
        // Hace efectivos los ajustes CornerRadius y BaseFontSize del Theme Studio en toda la interfaz:
        // las vistas y los estilos de componente consumen estas claves en lugar de literales.
        AddScaleTokens(dict, theme);

        return dict;
    }

    /// <summary>
    /// Publica las escalas derivadas del tema: radios (a partir de <see cref="ThemeDefinition.CornerRadius"/>),
    /// tipografía (a partir de <see cref="ThemeDefinition.BaseFontSize"/>), espaciado (a partir de
    /// <see cref="ThemeDefinition.SpacingUnit"/>) y elevación (a partir de <see cref="ThemeDefinition.NodeShadowBlur"/> y
    /// <see cref="ThemeDefinition.NodeShadowOpacity"/>).
    /// Los valores se redondean a 0.5 px para evitar medidas fraccionarias arbitrarias en la UI.
    /// </summary>
    private static void AddScaleTokens(ResourceDictionary dict, ThemeDefinition theme)
    {
        const double ReferenceFontSize = 12.0;

        try
        {
            double baseRadius = theme.CornerRadius > 0 ? theme.CornerRadius : 6.0;
            dict["RadiusXs"] = new CornerRadius(Math.Max(2, baseRadius - 2));
            dict["RadiusSm"] = new CornerRadius(baseRadius);
            dict["RadiusMd"] = new CornerRadius(baseRadius + 2);
            dict["RadiusLg"] = new CornerRadius(baseRadius + 4);
            dict["RadiusXl"] = new CornerRadius(baseRadius + 6);
            dict["RadiusXxl"] = new CornerRadius(baseRadius + 8);
            dict["RadiusPill"] = new CornerRadius(999);

            double baseFont = theme.BaseFontSize > 0 ? theme.BaseFontSize : ReferenceFontSize;
            double factor = baseFont / ReferenceFontSize;

            dict["FontSizeMicro"] = Scale(10.0, factor);
            dict["FontSizeCaption"] = Scale(11.0, factor);
            dict["FontSizeBodySm"] = Scale(12.0, factor);
            dict["FontSizeBody"] = Scale(13.0, factor);
            dict["FontSizeSubtitle"] = Scale(15.0, factor);
            dict["FontSizeTitle"] = Scale(17.0, factor);
            dict["FontSizeDisplay"] = Scale(20.0, factor);

            AddSpacingTokens(dict, theme.SpacingUnit > 0 ? theme.SpacingUnit : 4.0);
            AddElevationTokens(dict, theme);
        }
        catch
        {
            // Un tema corrupto no debe romper el arranque: los consumidores caen al valor por defecto del control.
        }
    }

    /// <summary>
    /// Escala de espaciado: los multiplicadores van de ½ a 8 unidades, de modo que la densidad del tema
    /// (compacta o cómoda) se propaga a rellenos, márgenes y separaciones de toda la interfaz.
    /// </summary>
    private static void AddSpacingTokens(ResourceDictionary dict, double unit)
    {
        double[] multipliers = [0.5, 1, 1.5, 2, 3, 4, 5, 6, 8];

        for (int i = 0; i < multipliers.Length; i++)
        {
            double value = Scale(unit, multipliers[i]);
            string index = (i + 1).ToString();

            dict[$"Space{index}"] = value;
            dict[$"Pad{index}"] = new Thickness(value);
        }
    }

    /// <summary>
    /// Escala de elevación: cuatro niveles de profundidad (tarjeta → panel flotante → superposición → modal)
    /// más los resplandores de acento que usan los elementos activos (selección de nodo, ejecución en curso).
    /// </summary>
    private static void AddElevationTokens(ResourceDictionary dict, ThemeDefinition theme)
    {
        double blur = theme.NodeShadowBlur > 0 ? theme.NodeShadowBlur : 24.0;
        double opacity = theme.NodeShadowOpacity > 0 ? Math.Clamp(theme.NodeShadowOpacity, 0.05, 1.0) : 0.55;

        // (desplazamiento vertical, factor de desenfoque, factor de opacidad)
        (double OffsetY, double BlurFactor, double OpacityFactor)[] levels =
        [
            (1, 0.35, 0.45),
            (2, 0.50, 0.70),
            (4, 0.75, 1.00),
            (8, 1.00, 1.00)
        ];

        for (int i = 0; i < levels.Length; i++)
        {
            var (offsetY, blurFactor, opacityFactor) = levels[i];
            dict[$"Elev{i + 1}"] = BuildShadow(0, offsetY, blur * blurFactor, 0, opacity * opacityFactor);
        }

        // Sombra lateral del panel de navegación: a sangre completa, el panel proyecta hacia el borde
        // opuesto (horizontal) en lugar de hacia abajo, así que la escala vertical no sirve aquí.
        dict["ElevPanelLeft"] = BuildShadow(blur * 0.35, 0, blur * 1.35, 0, opacity * 0.8);

        dict["ElevGlowAccent"] = BuildShadow(0, 0, blur * 0.42, 0, 0.60, Color.Parse(theme.AccentPrimary));
        dict["ElevGlowSuccess"] = BuildShadow(0, 0, blur * 0.62, 0, 0.88, Color.Parse(theme.AccentSuccess));
        dict["ElevGlowError"] = BuildShadow(0, 0, blur * 0.58, 0, 0.85, Color.Parse(theme.AccentError));

        // Velo de las capas modales: oscurece el contenido que queda detrás de un panel o superposición.
        // No depende de la paleta porque su función es separar planos, no decorar.
        dict["ScrimBrush"] = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0));
        dict["ScrimStrongBrush"] = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0));

        AddTintTokens(dict, theme.TextSecondary);
    }

    /// <summary>
    /// Tintes translúcidos de las microsuperficies (chips, contadores, raíles de seguimiento).
    /// Se derivan del color de texto secundario en lugar de usar un gris fijo: así se leen igual de bien sobre
    /// una tarjeta oscura y sobre una superficie clara, que es justo lo que un gris literal no consigue.
    /// </summary>
    private static void AddTintTokens(ResourceDictionary dict, string secondaryTextHex)
    {
        Color baseColor;

        try
        {
            baseColor = Color.Parse(string.IsNullOrWhiteSpace(secondaryTextHex) ? "#8B949E" : secondaryTextHex);
        }
        catch
        {
            baseColor = Color.Parse("#8B949E");
        }

        dict["ChipBrush"] = new SolidColorBrush(Color.FromArgb(0x18, baseColor.R, baseColor.G, baseColor.B));
        dict["ChipStrongBrush"] = new SolidColorBrush(Color.FromArgb(0x20, baseColor.R, baseColor.G, baseColor.B));
        dict["TintFaintBrush"] = new SolidColorBrush(Color.FromArgb(0x08, baseColor.R, baseColor.G, baseColor.B));
    }

    /// <summary>
    /// Construye una sombra (o un resplandor, con desplazamiento cero) para la escala de elevación.
    /// El color por defecto es negro: es el que produce profundidad legible tanto en temas oscuros como claros,
    /// y su alfa sale de la opacidad de sombra declarada por el tema.
    /// </summary>
    private static BoxShadows BuildShadow(
        double offsetX,
        double offsetY,
        double blur,
        double spread,
        double opacity,
        Color? tint = null)
    {
        var baseColor = tint ?? Colors.Black;
        byte alpha = (byte)Math.Clamp(Math.Round(opacity * 255), 0, 255);

        return new BoxShadows(new BoxShadow
        {
            OffsetX = offsetX,
            OffsetY = offsetY,
            Blur = blur,
            Spread = spread,
            Color = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B)
        });
    }

    private static double Scale(double value, double factor = 1.0) => Math.Round(value * factor * 2, MidpointRounding.AwayFromZero) / 2;

    private const byte OverlayAlpha = 0xE6;

    private static Color BuildOverlayColor(string surfaceHex)
    {
        try
        {
            var surface = Color.Parse(string.IsNullOrWhiteSpace(surfaceHex) ? "#131720" : surfaceHex);
            return Color.FromArgb(OverlayAlpha, surface.R, surface.G, surface.B);
        }
        catch
        {
            return Color.FromArgb(OverlayAlpha, 0x13, 0x17, 0x20);
        }
    }
}
