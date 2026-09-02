using FileFlow.Plugin.FileSystem.UI.Models;
using FileFlow.Sdk;

namespace FileFlow.Plugin.FileSystem.UI.Services;

/// <summary>
/// Proveedor de catálogo de tokens y variables para el asistente de renombrado avanzado.
/// </summary>
public static class RenamerTagCatalogService
{
    public static List<TagPickerItem> GetAvailableTags()
    {
        var tags = new List<TagPickerItem>
        {
            // 1. Sistema y Archivo
            new("Sistema y Archivo", "<FileName>", "Nombre de archivo completo con extensión (ej. foto.jpg)"),
            new("Sistema y Archivo", "<FileNameNoExt>", "Nombre de archivo sin extensión (ej. foto)"),
            new("Sistema y Archivo", "<Ext>", "Extensión del archivo (ej. jpg)"),
            new("Sistema y Archivo", "<DirName>", "Nombre del directorio contenedor directo"),
            new("Sistema y Archivo", "<ParentDir>", "Nombre de la carpeta superior"),
            new("Sistema y Archivo", "<FileSize>", "Tamaño del archivo formateado (ej. 4.25 MB)"),
            new("Sistema y Archivo", "<FileSizeBytes>", "Tamaño exacto en bytes"),
            new("Sistema y Archivo", "<CurrentPath>", "Ruta absoluta actual"),
            new("Sistema y Archivo", "<OriginalPath>", "Ruta de origen original del archivo"),
            new("Sistema y Archivo", "<RelativePath>", "Ruta relativa respecto al directorio raíz"),
            new("Sistema y Archivo", "{GlobalOutputDir}", "Ruta del directorio global de salida por defecto"),
            new("Sistema y Archivo", "{DefaultOutputDir}", "Ruta del directorio global de salida por defecto"),
            new("Sistema y Archivo", "{UserName}", "Nombre del usuario del sistema"),
            new("Sistema y Archivo", "{MachineName}", "Nombre del equipo / host"),

            // 2. Secuencias y Contadores
            new("Secuencias", "<Inc Nr:001>", "Contador incremental con 3 dígitos (001, 002...)"),
            new("Secuencias", "<Inc Nr:1>", "Contador incremental simple (1, 2, 3...)"),
            new("Secuencias", "<File Count>", "Cantidad total de archivos en el lote"),
            new("Secuencias", "{Counter}", "Índice de elemento en el flujo de ejecución"),

            // 3. Fechas y Horas
            new("Fechas y Horas", "<Year>", "Año actual a 4 dígitos (ej. 2026)"),
            new("Fechas y Horas", "<Month>", "Mes a 2 dígitos (01-12)"),
            new("Fechas y Horas", "<Day>", "Día a 2 dígitos (01-31)"),
            new("Fechas y Horas", "<Hour>", "Hora actual (00-23)"),
            new("Fechas y Horas", "<Min>", "Minutos actuales (00-59)"),
            new("Fechas y Horas", "<Sec>", "Segundos actuales (00-59)"),
            new("Fechas y Horas", "<Date Created:yyyyMMdd>", "Fecha de creación del archivo (formato yyyyMMdd)"),
            new("Fechas y Horas", "<Date Modified:yyyyMMdd_HHmmss>", "Fecha de modificación del archivo"),
            new("Fechas y Horas", "{DateNow}", "Fecha actual del sistema (yyyy-MM-dd)"),
            new("Fechas y Horas", "{TimeNow}", "Hora actual del sistema (HH-mm-ss)"),
            new("Fechas y Horas", "{DateTimeNow}", "Timestamp completo del sistema"),

            // 4. Fotografía y EXIF
            new("Fotografía EXIF", "<Date Taken:yyyyMMdd>", "Fecha de captura original EXIF"),
            new("Fotografía EXIF", "<Exif:CameraModel>", "Modelo de la cámara fotográfica"),
            new("Fotografía EXIF", "<Exif:CameraMake>", "Fabricante de la cámara"),
            new("Fotografía EXIF", "<Img Width>", "Ancho de la imagen en píxeles"),
            new("Fotografía EXIF", "<Img Height>", "Alto de la imagen en píxeles"),
            new("Fotografía EXIF", "{Orientation}", "Orientación (Landscape / Portrait / Square)"),
            new("Fotografía EXIF", "{AspectRatio}", "Relación de aspecto (ej. 16:9)"),
            new("Fotografía EXIF", "{Megapixels}", "Resolución en megapíxeles"),

            // 5. Audio y Video (ID3 / Tags)
            new("Audio y Video", "<Audio:Artist>", "Nombre del artista o banda"),
            new("Audio y Video", "<Audio:Title>", "Título de la canción / pista"),
            new("Audio y Video", "<Audio:Album>", "Título del álbum discográfico"),
            new("Audio y Video", "<Audio:Year>", "Año de lanzamiento del audio"),
            new("Audio y Video", "<Audio:Track>", "Número de pista / canción"),
            new("Audio y Video", "<Video:Width>", "Ancho del vídeo en píxeles"),
            new("Audio y Video", "<Video:Height>", "Alto del vídeo en píxeles"),
            new("Audio y Video", "<Video:Duration>", "Duración formateada del vídeo (HH:mm:ss)"),

            // 6. Hashes y Checksums
            new("Hashes", "<Hash:SHA256:8>", "Primeros 8 caracteres del hash SHA-256"),
            new("Hashes", "<Hash:SHA256>", "Hash criptográfico SHA-256 completo (64 caracteres)"),
            new("Hashes", "<Hash:MD5>", "Hash MD5 completo (32 caracteres)"),
            new("Hashes", "<Hash:SHA512>", "Hash criptográfico SHA-512"),
            new("Hashes", "{Guid}", "Identificador global único (GUID aleatorio)"),
            new("Hashes", "{ShortGuid}", "GUID abreviado a 8 caracteres")
        };

        return tags;
    }
}
