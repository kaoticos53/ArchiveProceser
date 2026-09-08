using System.IO;

namespace FileFlow.Sdk.SyntheticData;

/// <summary>
/// Representa la definición de un archivo o carpeta sintética dentro de un dataset personalizable,
/// con soporte para rutas jerárquicas multinivel y simulación de contenidos comprimidos.
/// </summary>
public sealed class SyntheticFileDefinition
{
    private string _relativePath = string.Empty;

    /// <summary>
    /// Ruta relativa jerárquica dentro del dataset (ej: 'Temporada 01/Capitulo 01.mkv' o 'Facturas/2024/Q1/FAC001.pdf').
    /// </summary>
    public string RelativePath
    {
        get => _relativePath;
        set
        {
            _relativePath = value?.Replace('\\', '/').TrimStart('/') ?? string.Empty;
        }
    }

    /// <summary>
    /// Nombre del archivo o carpeta extraído de la ruta relativa.
    /// </summary>
    public string FileName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_relativePath)) return "item.dat";
            var parts = _relativePath.Split('/');
            return parts.Length > 0 ? parts[^1] : _relativePath;
        }
    }

    /// <summary>
    /// Directorio relativo contenedor (ej: 'Temporada 01' o 'Facturas/2024/Q1').
    /// </summary>
    public string Directory
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_relativePath)) return string.Empty;
            int lastSlash = _relativePath.LastIndexOf('/');
            return lastSlash >= 0 ? _relativePath[..lastSlash] : string.Empty;
        }
    }

    /// <summary>
    /// Tamaño simulado en bytes.
    /// </summary>
    public long FileSizeBytes { get; set; } = 1024;

    /// <summary>
    /// Indica si el elemento es un directorio contenedor.
    /// </summary>
    public bool IsDirectory { get; set; } = false;

    /// <summary>
    /// Metadatos clave/valor asociados a este archivo sintético (EXIF, ID3, Video, Hashes, Documental).
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Colección opcional de entradas internas simuladas si el archivo representa un paquete comprimido (ZIP, RAR, 7Z).
    /// </summary>
    public List<SyntheticArchiveEntryDefinition> SimulatedArchiveEntries { get; set; } = [];

    /// <summary>
    /// Indica si este archivo es un paquete comprimido con entradas simuladas o extensión típica de archivo.
    /// </summary>
    public bool IsArchive
    {
        get
        {
            if (SimulatedArchiveEntries.Count > 0) return true;
            string ext = Path.GetExtension(FileName).ToLowerInvariant();
            return ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz";
        }
    }

    public SyntheticFileDefinition() { }

    public SyntheticFileDefinition(
        string relativePath,
        long fileSizeBytes = 1024,
        bool isDirectory = false,
        Dictionary<string, object?>? metadata = null)
    {
        RelativePath = relativePath;
        FileSizeBytes = fileSizeBytes;
        IsDirectory = isDirectory;
        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                Metadata[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Clona profundamente la definición de archivo.
    /// </summary>
    public SyntheticFileDefinition Clone()
    {
        var clone = new SyntheticFileDefinition
        {
            RelativePath = RelativePath,
            FileSizeBytes = FileSizeBytes,
            IsDirectory = IsDirectory,
            Metadata = new Dictionary<string, object?>(Metadata, StringComparer.OrdinalIgnoreCase)
        };

        foreach (var entry in SimulatedArchiveEntries)
        {
            clone.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition
            {
                InnerPath = entry.InnerPath,
                FileSizeBytes = entry.FileSizeBytes,
                IsDirectory = entry.IsDirectory,
                Metadata = new Dictionary<string, object?>(entry.Metadata, StringComparer.OrdinalIgnoreCase)
            });
        }

        return clone;
    }
}
