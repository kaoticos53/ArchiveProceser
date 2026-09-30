using System;
using System.Collections.Generic;
using FileFlow.App.Core;
using FileFlow.App.Services;
using FileFlow.App.Themes;
using FileFlow.App.Uno.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FileFlow.App.Uno.Platform;

/// <summary>
/// Instalación de los bordes de tema del host Uno hacia el núcleo portable — la mitad Uno del
/// <see cref="ThemeHostBridge"/> (fase 3.5 del plan Uno), espejo de <c>el host de temas original</c>.
///
/// <para><b>Por qué muta los pinceles en vez de republicar claves</b>: WinUI captura la INSTANCIA
/// del pincel en <c>StaticResource</c> (evaluación única en la carga del XAML) y no re-evalúa
/// recursos al reescribir <c>Application.Resources</c> — un <c>ThemeResource</c> de aplicación
/// conservó su instancia antigua en todas las corridas de esta fase. La cura robusta es declarar
/// UNA instancia por pincel en App.xaml y cambiar su COLOR: la mutación in-place notifica a todos
/// los consumidores vivos (XAML capturado, pinceles leídos por código) sin reconstruir nada.</para>
/// </summary>
public static class UnoThemeHost
{
    /// <summary>La ventana viva del host (WinUI 3 no expone la lista de ventanas en Application).</summary>
    private static Func<Window?>? _windowProvider;

    /// <summary>
    /// Instala el puente de temas. Debe llamarse antes de aplicar cualquier tema guardado; el
    /// proveedor de ventana puede entregarse antes de crearla (se lee al notificar, no al instalar).
    /// </summary>
    public static void Install(Func<Window?>? windowProvider)
    {
        _windowProvider = windowProvider;
        ThemeHostBridge.PublishThemeVariant = PublishThemeVariant;
        ThemeHostBridge.BuildResources = BuildResources;
    }

    private static void PublishThemeVariant(bool isDark)
    {
        var window = _windowProvider?.Invoke();
        var queue = window?.DispatcherQueue;
        if (queue is null || !queue.HasThreadAccess)
        {
            queue?.TryEnqueue(() => PublishThemeVariant(isDark));
            return;
        }

        // 1. La variante clara/oscurecida sobre la raíz del contenido: los controles internos de la
        //    app (ComboBox, ScrollBar, TextBox, Popup...) siguen a la variante aplicada.
        if (window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        }

        // 2. Los tokens del tema activo, mutando los pinceles singleton de App.xaml.
        RepublishTokens();
    }

    /// <summary>
    /// Escribe el COLOR de cada pincel Canvas* con el valor del tema activo. La mutación in-place
    /// repinta a todos los consumidores del pincel (la notificación la hace el SolidColorBrush).
    /// </summary>
    public static void RepublishTokens()
    {
        var app = Application.Current;
        var theme = ThemeManager.Instance.ActiveThemeDefinition;
        if (app is null || theme is null)
        {
            return;
        }

        var resources = app.Resources;
        SetBrush(resources, "CanvasBackgroundBrush", theme.BgEditor, "#10131B");
        SetBrush(resources, "CanvasBgDarkBrush", theme.BgDark, "#0D1117");
        SetBrush(resources, "CanvasGridBrush", theme.GridLine, "#1A202C");
        SetBrush(resources, "CanvasSurfaceBrush", theme.BgSurface, "#131720");
        SetBrush(resources, "CanvasCardBrush", theme.BgCard, "#161B22");
        SetBrush(resources, "CanvasBorderBrush", theme.BorderDark, "#30363D");
        SetBrush(resources, "CanvasTextBrush", theme.TextPrimary, "#F0F6FC");
        SetBrush(resources, "CanvasSecondaryBrush", theme.TextSecondary, "#8B949E");
        SetBrush(resources, "CanvasMutedBrush", theme.TextMuted, "#7C8698");
        SetBrush(resources, "CanvasOnAccentBrush", theme.TextOnAccent, "#FFFFFF");
        SetBrush(resources, "CanvasAccentPrimaryBrush", theme.AccentPrimary, "#6366F1");
        SetBrush(resources, "CanvasAccentGlowBrush", theme.AccentGlow, "#818CF8");
        SetBrush(resources, "CanvasSuccessBrush", theme.AccentSuccess, "#10B981");
        SetBrush(resources, "CanvasWarningBrush", theme.AccentWarning, "#F59E0B");
        SetBrush(resources, "CanvasPurpleBrush", theme.AccentPurple, "#A855F7");
        SetBrush(resources, "CanvasWireBrush", theme.WireColorStart, "#818CF8");
    }

    /// <summary>
    /// Obtiene el pincel singleton de App.xaml (o lo crea la primera vez) y cambia su color: la
    /// MISMA instancia vive en todos los consumidores, así que la mutación los repinta a todos.
    /// </summary>
    private static void SetBrush(ResourceDictionary resources, string key, string hex, string fallback)
    {
        if (resources[key] is not SolidColorBrush brush)
        {
            brush = new SolidColorBrush(NodeCardViewModel.ParseHex(fallback));
            resources[key] = brush;
        }

        brush.Color = NodeCardViewModel.ParseHex(hex);
    }

    /// <summary>
    /// Generador portable de tokens que el núcleo consume como pares clave → valor (el Theme Studio
    /// y cualquier previsualización viva usan la misma vía que el host original: ThemeHostBridge.BuildResources).
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildResources(object themeDefinition)
    {
        if (themeDefinition is not ThemeDefinition theme)
        {
            return new Dictionary<string, object?>();
        }

        var tokens = new Dictionary<string, object?>
        {
            ["CanvasBackgroundBrush"] = NodeCardViewModel.ParseHex(theme.BgEditor),
            ["CanvasBgDarkBrush"] = NodeCardViewModel.ParseHex(theme.BgDark),
            ["CanvasGridBrush"] = NodeCardViewModel.ParseHex(theme.GridLine),
            ["CanvasSurfaceBrush"] = NodeCardViewModel.ParseHex(theme.BgSurface),
            ["CanvasCardBrush"] = NodeCardViewModel.ParseHex(theme.BgCard),
            ["CanvasBorderBrush"] = NodeCardViewModel.ParseHex(theme.BorderDark),
            ["CanvasTextBrush"] = NodeCardViewModel.ParseHex(theme.TextPrimary),
            ["CanvasSecondaryBrush"] = NodeCardViewModel.ParseHex(theme.TextSecondary),
            ["CanvasMutedBrush"] = NodeCardViewModel.ParseHex(theme.TextMuted),
            ["CanvasOnAccentBrush"] = NodeCardViewModel.ParseHex(theme.TextOnAccent),
            ["CanvasAccentPrimaryBrush"] = NodeCardViewModel.ParseHex(theme.AccentPrimary),
            ["CanvasAccentGlowBrush"] = NodeCardViewModel.ParseHex(theme.AccentGlow),
            ["CanvasSuccessBrush"] = NodeCardViewModel.ParseHex(theme.AccentSuccess),
            ["CanvasWarningBrush"] = NodeCardViewModel.ParseHex(theme.AccentWarning),
            ["CanvasPurpleBrush"] = NodeCardViewModel.ParseHex(theme.AccentPurple),
            ["CanvasWireBrush"] = NodeCardViewModel.ParseHex(theme.WireColorStart),
        };

        return tokens;
    }
}
