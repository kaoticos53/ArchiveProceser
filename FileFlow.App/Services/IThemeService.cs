using FileFlow.Sdk.Themes;

namespace FileFlow.App.Services;

/// <summary>
/// Contrato de puerto para la gestión y aplicación reactiva de temas visuales en la aplicación.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Tema activo basado en la enumeración estándar.
    /// </summary>
    AppTheme CurrentTheme { get; }

    /// <summary>
    /// Identificador único del tema actualmente cargado.
    /// </summary>
    string CurrentThemeId { get; }

    /// <summary>
    /// Definición de tema dinámico enriquecido activo, si aplica.
    /// </summary>
    ThemeDefinition? ActiveThemeDefinition { get; }

    /// <summary>
    /// Indica si el tema actual posee una tonalidad oscura.
    /// </summary>
    bool IsCurrentThemeDark { get; }

    /// <summary>
    /// Evento emitido cuando cambia el tema estándar.
    /// </summary>
    event Action<AppTheme>? ThemeChanged;

    /// <summary>
    /// Evento emitido cuando se aplica un tema personalizado/dinámico.
    /// </summary>
    event Action<ThemeDefinition>? CustomThemeChanged;

    /// <summary>
    /// Aplica un tema visual por enumeración.
    /// </summary>
    void SetTheme(AppTheme theme);

    /// <summary>
    /// Aplica un tema visual por identificador de cadena o archivo.
    /// </summary>
    void SetThemeById(string themeId);

    /// <summary>
    /// Aplica una definición de tema dinámico.
    /// </summary>
    void SetTheme(ThemeDefinition theme);
}
