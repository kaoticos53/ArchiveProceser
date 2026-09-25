using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Styling;
using FileFlow.App.Core;
using FileFlow.App.Services;
using FileFlow.App.Themes;

namespace FileFlow.App.Services;

/// <summary>
/// Instalación de los bordes de tema del host Avalonia hacia el núcleo portable: publicar la
/// variante clara/oscurecida en el runtime de Avalonia (la Application y cada ventana abierta) y
/// generar los tokens del tema como diccionario portable. Es la pieza que el ThemeManager del
/// núcleo invoca vía <see cref="ThemeHostBridge"/>; aquí vive todo lo que toca tipos Window.
/// </summary>
public static class AvaloniaThemeHost
{
    /// <summary>
    /// Instala el puente de temas. Debe llamarse antes de aplicar cualquier tema guardado.
    /// </summary>
    public static void Install()
    {
        ThemeHostBridge.PublishThemeVariant = PublishThemeVariant;
        ThemeHostBridge.BuildResources = BuildResources;
    }

    private static void PublishThemeVariant(bool isDark)
    {
        var app = Application.Current;
        if (app == null) return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => PublishThemeVariant(isDark));
            return;
        }

        // Publica Application.RequestedThemeVariant: los controles internos de Fluent siguen la variante.
        app.RequestedThemeVariant = WindowThemeHelper.ResolveThemeVariant(isDark);

        // Republica los tokens del tema activo en Application.Resources: es lo que hace que un
        // DynamicResource ya evaluado (p. ej. el Foreground de la splash) vea el pincel nuevo.
        var active = ThemeManager.Instance.ActiveThemeDefinition;
        if (active is not null)
        {
            var generated = ThemeResourceApplier.BuildResourceDictionary(active);
            foreach (var key in generated.Keys)
            {
                if (key is string s)
                {
                    app.Resources[s] = generated[s];
                }
            }
        }

        WindowThemeHelper.ApplyThemeToOpenWindows();
    }

    private static IReadOnlyDictionary<string, object?> BuildResources(object themeDefinition)
    {
        var dict = ThemeResourceApplier.BuildResourceDictionary((ThemeDefinition)themeDefinition);
        var tokens = new Dictionary<string, object?>(dict.Count, StringComparer.Ordinal);
        foreach (var key in dict.Keys)
        {
            if (key is string s)
            {
                tokens[s] = dict[s];
            }
        }
        return tokens;
    }
}
