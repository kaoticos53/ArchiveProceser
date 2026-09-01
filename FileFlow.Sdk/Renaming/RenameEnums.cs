namespace FileFlow.Sdk.Renaming;

/// <summary>
/// Identificador del tipo de método de transformación en el pipeline de renombrado.
/// </summary>
public enum RenameMethodType
{
    NewName,             // Sustitución total basada en plantilla y tags
    SearchReplace,       // Búsqueda y reemplazo (texto plano o Regex)
    Insert,              // Inserción de texto en posición absoluta/relativa
    Remove,              // Eliminación por rango, patrón o recuento
    CaseConversion,      // Minúsculas, MAYÚSCULAS, Title Case, Sentence Case, etc.
    Numbering,           // Secuencia numérica incremental con padding y reinicio
    ReplaceList,         // Tabla de sustituciones por pares clave-valor o borrado
    TrimClean,           // Recorte de espacios, caracteres ilegales y normalización Unicode
    NormalizeNumbers     // Normalización y relleno de ceros (padding) en números detectados
}

/// <summary>
/// Objetivo de detección de números a normalizar con ceros a la izquierda.
/// </summary>
public enum NumberPaddingTarget
{
    AllNumbers,          // Todos los números encontrados en el nombre (ej. 1x2 -> 01x02)
    FirstNumber,         // Solo el primer número encontrado (ej. 1 - foto -> 01 - foto)
    LastNumber,          // Solo el último número encontrado (ej. serie 1x2 -> serie 1x02, track 5 -> track 05)
    EpisodeFormat,       // Formato de episodios/temporadas (ej. 1x1 -> 1x01, S1E1 -> S01E01, Cap. 2 -> Cap. 02)
    CustomRegex          // Coincidencia mediante expresión regular personalizada
}

/// <summary>
/// Ámbito al que se aplica la transformación del nombre.
/// </summary>
public enum ApplyToTarget
{
    NameOnly,            // Solo el nombre base sin extensión
    ExtensionOnly,       // Solo la extensión (sin el punto)
    FullName             // Nombre completo con extensión
}

/// <summary>
/// Tipo de transformación de mayúsculas/minúsculas.
/// </summary>
public enum CaseTransformType
{
    Lowercase,           // todo en minúsculas
    Uppercase,           // TODO EN MAYÚSCULAS
    TitleCase,           // Tipo Título (Primera Letra De Cada Palabra)
    SentenceCase,        // Tipo Oración (primera letra de cada frase tras punto)
    CapitalizeFirst      // Solo la primera letra del nombre
}

/// <summary>
/// Posición de inserción o eliminación de caracteres.
/// </summary>
public enum CharacterPosition
{
    FromStart,           // Índice desde el inicio
    FromEnd,             // Índice desde el final
    AbsoluteIndex        // Offset absoluto
}

/// <summary>
/// Criterio de reinicio para la numeración incremental.
/// </summary>
public enum NumberingResetOn
{
    Never,               // Contador global continuo para todo el lote
    DirectoryChange,     // Reinicia el contador al cambiar de carpeta contenedora
    MetadataChange       // Reinicia el contador al cambiar un valor de metadatos (ej. Exif:CameraModel)
}

/// <summary>
/// Modo de normalización Unicode.
/// </summary>
public enum UnicodeNormalizationMode
{
    None,
    FormC,               // NFC: Composición canónica
    FormD,               // NFD: Descomposición canónica
    FormKC,              // NFKC: Composición por compatibilidad
    FormKD               // NFKD: Descomposición por compatibilidad
}
