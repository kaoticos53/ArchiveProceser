namespace FileFlow.Sdk.SyntheticData;

/// <summary>
/// Representa la definición de un archivo o entrada simulada dentro de un paquete comprimido sintético (ZIP, RAR, 7Z, TAR).
/// </summary>
public sealed class SyntheticArchiveEntryDefinition
{
    /// <summary>
    /// Ruta interna relativa dentro del archivo comprimido (ej: 'docs/informe.pdf').
    /// </summary>
    public string InnerPath { get; set; } = string.Empty;

    /// <summary>
    /// Tamaño simulado en bytes del archivo contenido.
    /// </summary>
    public long FileSizeBytes { get; set; } = 1024;

    /// <summary>
    /// Indica si la entrada representa un directorio interno.
    /// </summary>
    public bool IsDirectory { get; set; } = false;

    /// <summary>
    /// Metadatos opcionales específicos de esta entrada interna.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public SyntheticArchiveEntryDefinition() { }

    public SyntheticArchiveEntryDefinition(string innerPath, long fileSizeBytes = 1024, bool isDirectory = false)
    {
        InnerPath = innerPath;
        FileSizeBytes = fileSizeBytes;
        IsDirectory = isDirectory;
    }
}
