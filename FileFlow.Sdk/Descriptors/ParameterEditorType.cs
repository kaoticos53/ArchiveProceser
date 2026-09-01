namespace FileFlow.Sdk;

/// <summary>
/// Especifica el tipo de editor de interfaz gráfica que debe renderizarse para un parámetro de nodo.
/// </summary>
public enum ParameterEditorType
{
    /// <summary>
    /// Entrada de texto estándar de una sola línea.
    /// </summary>
    Text,

    /// <summary>
    /// Entrada numérica con validación y posibles límites mínimo/máximo.
    /// </summary>
    Number,

    /// <summary>
    /// Deslizador numérico continuo o por pasos (ej. Calidad 1-100).
    /// </summary>
    Slider,

    /// <summary>
    /// Lista desplegable de selección única entre opciones predefinidas.
    /// </summary>
    Dropdown,

    /// <summary>
    /// Interruptor o casilla de verificación booleana (true/false).
    /// </summary>
    Toggle,

    /// <summary>
    /// Selector de ruta de directorio con botón para abrir el explorador de carpetas.
    /// </summary>
    FolderPath,

    /// <summary>
    /// Selector de ruta de archivo con botón para abrir el explorador de archivos.
    /// </summary>
    FilePath,

    /// <summary>
    /// Área de texto enriquecida multilínea (para scripts, JSON, plantillas de argumentos o payloads).
    /// </summary>
    MultiLineText,

    /// <summary>
    /// Selector de lista de contraseñas de archivo.
    /// </summary>
    PasswordList,

    /// <summary>
    /// Selector de preset multimedia con acceso al gestor de presets.
    /// </summary>
    MediaPreset
}
