using System.Windows;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Services;

/// <summary>
/// Proveedor de catálogo de tokens y variables para el asistente de renombrado avanzado.
/// </summary>
public static class RenamerTagCatalogService
{
    public static List<TagPickerItem> GetAvailableTags(NodeViewModel nodeViewModel)
    {
        var tags = new List<TagPickerItem>();

        // 1. Sistema y Archivo
        tags.Add(new TagPickerItem("Sistema y Archivo", "<FileName>", "Nombre de archivo completo con extensión (ej. foto.jpg)"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "<FileNameNoExt>", "Nombre de archivo sin extensión (ej. foto)"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "<Ext>", "Extensión del archivo (ej. jpg)"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "<DirName>", "Nombre del directorio contenedor directo"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "<ParentDir>", "Nombre de la carpeta superior"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "<FileSize>", "Tamaño del archivo formateado (ej. 4.25 MB)"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "<FileSizeBytes>", "Tamaño exacto en bytes"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "<CurrentPath>", "Ruta absoluta actual"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "<OriginalPath>", "Ruta de origen original del archivo"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "<RelativePath>", "Ruta relativa respecto al directorio raíz"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "{UserName}", "Nombre del usuario del sistema"));
        tags.Add(new TagPickerItem("Sistema y Archivo", "{MachineName}", "Nombre del equipo / host"));

        // 2. Secuencias y Contadores
        tags.Add(new TagPickerItem("Secuencias", "<Inc Nr:001>", "Contador incremental con 3 dígitos (001, 002...)"));
        tags.Add(new TagPickerItem("Secuencias", "<Inc Nr:1>", "Contador incremental simple (1, 2, 3...)"));
        tags.Add(new TagPickerItem("Secuencias", "<File Count>", "Cantidad total de archivos en el lote"));
        tags.Add(new TagPickerItem("Secuencias", "{Counter}", "Índice de elemento en el flujo de ejecución"));

        // 3. Fechas y Horas
        tags.Add(new TagPickerItem("Fechas y Horas", "<Year>", "Año actual a 4 dígitos (ej. 2026)"));
        tags.Add(new TagPickerItem("Fechas y Horas", "<Month>", "Mes a 2 dígitos (01-12)"));
        tags.Add(new TagPickerItem("Fechas y Horas", "<Day>", "Día a 2 dígitos (01-31)"));
        tags.Add(new TagPickerItem("Fechas y Horas", "<Hour>", "Hora actual (00-23)"));
        tags.Add(new TagPickerItem("Fechas y Horas", "<Min>", "Minutos actuales (00-59)"));
        tags.Add(new TagPickerItem("Fechas y Horas", "<Sec>", "Segundos actuales (00-59)"));
        tags.Add(new TagPickerItem("Fechas y Horas", "<Date Created:yyyyMMdd>", "Fecha de creación del archivo (formato yyyyMMdd)"));
        tags.Add(new TagPickerItem("Fechas y Horas", "<Date Modified:yyyyMMdd_HHmmss>", "Fecha de modificación del archivo"));
        tags.Add(new TagPickerItem("Fechas y Horas", "{DateNow}", "Fecha actual del sistema (yyyy-MM-dd)"));
        tags.Add(new TagPickerItem("Fechas y Horas", "{TimeNow}", "Hora actual del sistema (HH-mm-ss)"));
        tags.Add(new TagPickerItem("Fechas y Horas", "{DateTimeNow}", "Timestamp completo del sistema"));

        // 4. Fotografía y EXIF
        tags.Add(new TagPickerItem("Fotografía EXIF", "<Date Taken:yyyyMMdd>", "Fecha de captura original EXIF"));
        tags.Add(new TagPickerItem("Fotografía EXIF", "<Exif:CameraModel>", "Modelo de la cámara fotográfica"));
        tags.Add(new TagPickerItem("Fotografía EXIF", "<Exif:CameraMake>", "Fabricante de la cámara"));
        tags.Add(new TagPickerItem("Fotografía EXIF", "<Img Width>", "Ancho de la imagen en píxeles"));
        tags.Add(new TagPickerItem("Fotografía EXIF", "<Img Height>", "Alto de la imagen en píxeles"));
        tags.Add(new TagPickerItem("Fotografía EXIF", "{Orientation}", "Orientación (Landscape / Portrait / Square)"));
        tags.Add(new TagPickerItem("Fotografía EXIF", "{AspectRatio}", "Relación de aspecto (ej. 16:9)"));
        tags.Add(new TagPickerItem("Fotografía EXIF", "{Megapixels}", "Resolución en megapíxeles"));

        // 5. Audio y Video (ID3 / Tags)
        tags.Add(new TagPickerItem("Audio y Video", "<Audio:Artist>", "Nombre del artista o banda"));
        tags.Add(new TagPickerItem("Audio y Video", "<Audio:Title>", "Título de la canción / pista"));
        tags.Add(new TagPickerItem("Audio y Video", "<Audio:Album>", "Nombre del álbum"));
        tags.Add(new TagPickerItem("Audio y Video", "<Audio:TrackNumber>", "Número de pista en el álbum"));
        tags.Add(new TagPickerItem("Audio y Video", "<Audio:Year>", "Año de publicación del audio"));
        tags.Add(new TagPickerItem("Audio y Video", "<Audio:Genre>", "Género musical"));
        tags.Add(new TagPickerItem("Audio y Video", "<Video:Width>", "Ancho del fotograma de video"));
        tags.Add(new TagPickerItem("Audio y Video", "<Video:Height>", "Alto del fotograma de video"));

        // 6. Hashes y Documentos
        tags.Add(new TagPickerItem("Metadatos y Hashes", "{Hash:SHA256}", "Hash criptográfico SHA-256"));
        tags.Add(new TagPickerItem("Metadatos y Hashes", "{Hash:MD5}", "Hash MD5 del archivo"));
        tags.Add(new TagPickerItem("Metadatos y Hashes", "{Doc:WordCount}", "Número de palabras del documento"));
        tags.Add(new TagPickerItem("Metadatos y Hashes", "{Doc:PageCount}", "Número de páginas del documento"));
        tags.Add(new TagPickerItem("Metadatos y Hashes", "{Cli:StdOut}", "Salida estándar de ejecución CLI"));
        tags.Add(new TagPickerItem("Metadatos y Hashes", "{Cli:ExitCode}", "Código de salida de proceso CLI"));

        // 7. Funciones de Expresión
        tags.Add(new TagPickerItem("Funciones de Expresión", "{Upper(FileNameNoExt)}", "Convertir a MAYÚSCULAS"));
        tags.Add(new TagPickerItem("Funciones de Expresión", "{Lower(Ext)}", "Convertir a minúsculas"));
        tags.Add(new TagPickerItem("Funciones de Expresión", "{Substring(FileNameNoExt, 0, 8)}", "Extraer subcadena por índice y longitud"));
        tags.Add(new TagPickerItem("Funciones de Expresión", "{Sanitize(CameraModel)}", "Sanitizar caracteres ilegales"));
        tags.Add(new TagPickerItem("Funciones de Expresión", "{PadLeft(Counter, 4, \"0\")}", "Rellenar con ceros a la izquierda"));
        tags.Add(new TagPickerItem("Funciones de Expresión", "{RegexMatch(FileNameNoExt, \"[0-9]+\")}", "Extraer primera coincidencia Regex"));
        tags.Add(new TagPickerItem("Funciones de Expresión", "{RegexReplace(FileNameNoExt, \"[^a-zA-Z0-9]\", \"_\")}", "Reemplazar patrón con Regex"));
        tags.Add(new TagPickerItem("Funciones de Expresión", "{Coalesce(DateTaken, DateCreated, DateNow)}", "Primer valor no vacío de la lista"));
        tags.Add(new TagPickerItem("Funciones de Expresión", "{FormatDate(DateCreated, \"yyyy-MM\")}", "Formatear fecha con patrón"));
        tags.Add(new TagPickerItem("Funciones de Expresión", "{Default(DateTaken, \"2026-01-01\")}", "Valor de respaldo si está vacío"));
        tags.Add(new TagPickerItem("Funciones de Expresión", "{FileAgeDays(DateCreated)}", "Días transcurridos desde fecha"));

        // 8. Cargar variables de nodos aguas arriba
        var connections = nodeViewModel.ParentEditor?.Connections?.ToList()
            ?? (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm ? mainVm.Editor.Connections.ToList() : new List<ConnectionViewModel>());

        var upstreamGroups = new VariableDiscoveryService().GetAvailableVariables(nodeViewModel, connections);
        foreach (var group in upstreamGroups)
        {
            if (group.GroupName.Contains("System", StringComparison.OrdinalIgnoreCase) ||
                group.GroupName.Contains("Expression", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var v in group.Variables)
            {
                if (!tags.Any(t => t.Tag.Equals(v.Token, StringComparison.OrdinalIgnoreCase)))
                {
                    tags.Add(new TagPickerItem(group.GroupName, v.Token, v.Description));
                }
            }
        }

        // 9. Cargar variables inyectadas de cualquier VariableInjectorNode presente en el flujo
        var editorNodes = nodeViewModel.ParentEditor?.Nodes?.ToList()
            ?? (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm2 ? mainVm2.Editor.Nodes.ToList() : new List<NodeViewModel>());

        foreach (var injectorNode in editorNodes.Where(n => n.IsVariableInjectorNode))
        {
            string groupName = $"🔗 {injectorNode.Title}";
            foreach (var param in injectorNode.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(param.Key))
                {
                    string token = $"{{{param.Key}}}";
                    if (!tags.Any(t => t.Tag.Equals(token, StringComparison.OrdinalIgnoreCase)))
                    {
                        tags.Add(new TagPickerItem(groupName, token, $"Variable personalizada: '{param.Key}' = '{param.Value}'"));
                    }
                }
            }
        }

        return tags;
    }
}
