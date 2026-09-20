using System;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.Services;

/// <summary>
/// Etapas del arranque de la aplicación. Cada una se ejecuta por separado para que un fallo se pueda
/// <b>atribuir</b>: saber que «falló el arranque» no sirve de nada; saber que falló la etapa de servicios o la
/// de la interfaz principal (XAML) es lo que permite arreglarlo.
/// </summary>
public enum StartupPhase
{
    /// <summary>Registro de los recursos de texto del host (i18n) y cultura base.</summary>
    Resources,

    /// <summary>Construcción del contenedor de inyección de dependencias y resolución del grafo.</summary>
    Services,

    /// <summary>Carga de las preferencias del usuario (tema, idioma, ajustes de arranque).</summary>
    Preferences,

    /// <summary>Aplicación del tema y del idioma guardados.</summary>
    Theme,

    /// <summary>Descubrimiento y carga de plugins.</summary>
    Plugins,

    /// <summary>Construcción y presentación de la ventana principal (carga del XAML).</summary>
    Shell,

    /// <summary>
    /// Fallo no controlado una vez la aplicación ya estaba en marcha (cualquier hilo). Se usa para que una
    /// excepción huérfana también se haga visible en lugar de morir en silencio.
    /// </summary>
    Runtime
}

/// <summary>
/// Nombres legibles de las etapas, localizados con <b>respaldo literal</b>: este texto se usa precisamente
/// cuando el arranque ha fallado, y la etapa que más probablemente falle es la de recursos. Si la
/// localización no responde, la ventana de error debe seguir siendo legible.
/// </summary>
public static class StartupPhaseDescriptions
{
    /// <summary>Clave de localización de la etapa.</summary>
    public static string Key(StartupPhase phase) => $"Startup_Phase_{phase}";

    /// <summary>Texto de respaldo si la localización no está disponible.</summary>
    public static string Fallback(StartupPhase phase) => phase switch
    {
        StartupPhase.Resources => "recursos e idioma base",
        StartupPhase.Services => "contenedor de servicios",
        StartupPhase.Preferences => "preferencias del usuario",
        StartupPhase.Theme => "tema e idioma",
        StartupPhase.Plugins => "carga de plugins",
        StartupPhase.Shell => "interfaz principal (XAML)",
        StartupPhase.Runtime => "ejecución",
        _ => phase.ToString()
    };

    /// <summary>Nombre localizado de la etapa, sin poder lanzar jamás.</summary>
    public static string Describe(StartupPhase phase)
    {
        try
        {
            string localized = LocalizationManager.Instance.GetString(Key(phase), Fallback(phase));
            return string.IsNullOrWhiteSpace(localized) ? Fallback(phase) : localized;
        }
        catch
        {
            return Fallback(phase);
        }
    }
}
