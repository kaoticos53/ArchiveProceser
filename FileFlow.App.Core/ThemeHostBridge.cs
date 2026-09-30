namespace FileFlow.App.Core;

/// <summary>
/// Puente de temas del núcleo portable al host. El host original instala en el arranque sus dos
/// bordes —publicar la variante clara/oscurecida en su runtime y generar los tokens del tema— y
/// el <see cref="FileFlow.App.Services.ThemeManager"/> los invoca sin conocer ventanas, pinceles
/// ni diccionarios de recursos. Sin host (pruebas), ambos bordes son no-ops: el estado del gestor
/// de temas (tema activo, oscuro/claro, eventos) sigue funcionando igual.
/// </summary>
public static class ThemeHostBridge
{
    /// <summary>
    /// El host instala aquí cómo se publica una variante de tema en su runtime: los controles del
    /// tema de sistema (ComboBox, ScrollBar, DataGrid, Popup…) siguen a la variante aplicada.
    /// Recibe si el tema es oscuro.
    /// </summary>
    public static Action<bool>? PublishThemeVariant { get; set; }

    /// <summary>
    /// El host instala aquí cómo se generan los tokens visuales de una definición de tema, en un
    /// diccionario portable clave → valor (el editor de temas lo consume para su previsualización
    /// en vivo y el host para pintar toda la interfaz).
    /// </summary>
    public static Func<object, IReadOnlyDictionary<string, object?>>? BuildResources { get; set; }
}
