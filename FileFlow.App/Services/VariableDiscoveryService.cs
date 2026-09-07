using System.IO;
using FileFlow.App.Models;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio responsable de construir el catálogo de variables disponibles para un nodo específico en el grafo,
/// categorizándolas, enriqueciéndolas con valores de muestra y detectando variables upstream.
/// </summary>
public class VariableDiscoveryService : IVariableDiscoveryService
{
    public static readonly VariableDiscoveryService Instance = new();
    public List<VariableGroupItem> GetAvailableVariables(NodeViewModel targetNode, IEnumerable<ConnectionViewModel> connections)
    {
        ArgumentNullException.ThrowIfNull(targetNode);
        ArgumentNullException.ThrowIfNull(connections);

        var result = new List<VariableGroupItem>();
        var previewItem = CreatePreviewItem(targetNode);

        // 1. Upstream Traversal (Variables que realmente existen según los nodos anteriores en el grafo DAG)
        var visitedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(targetNode.Id))
        {
            visitedNodeIds.Add(targetNode.Id);
        }

        var queue = new Queue<NodeViewModel>();
        queue.Enqueue(targetNode);

        var connectionsList = connections.ToList();
        var upstreamGroups = new List<VariableGroupItem>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var incomingConns = connectionsList.Where(c => 
                c.Target.NodeOwner == current || 
                (c.Target.NodeOwner != null && !string.IsNullOrEmpty(current.Id) && string.Equals(c.Target.NodeOwner.Id, current.Id, StringComparison.OrdinalIgnoreCase))).ToList();

            foreach (var conn in incomingConns)
            {
                var upstreamNode = conn.Source.NodeOwner;
                if (upstreamNode != null && visitedNodeIds.Add(upstreamNode.Id))
                {
                    queue.Enqueue(upstreamNode);

                    string typeName = upstreamNode.NodeTypeName;
                    var upstreamGroup = new VariableGroupItem($"🔗 {upstreamNode.Title}", isUpstream: true);

                    void AddUpstreamVar(string name, string token, string desc)
                    {
                        string sample = VariableTemplateResolver.Resolve(token, previewItem);
                        upstreamGroup.Variables.Add(new VariableItem(
                            name,
                            token,
                            desc,
                            Category: "Nodos Anteriores",
                            SampleValue: sample,
                            IsUpstream: true,
                            SourceNodeTitle: upstreamNode.Title));
                    }

                    if (typeName.Contains("ExifMetadataNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("DateTaken", "{DateTaken}", "Fecha/Hora original de captura EXIF");
                        AddUpstreamVar("Year", "{Year(DateTaken)}", "Año de 4 dígitos de la foto");
                        AddUpstreamVar("Month", "{Month(DateTaken)}", "Mes de 2 dígitos (01-12)");
                        AddUpstreamVar("Day", "{Day(DateTaken)}", "Día de 2 dígitos (01-31)");
                        AddUpstreamVar("CameraModel", "{CameraModel}", "Modelo de cámara EXIF");
                        AddUpstreamVar("CameraMake", "{CameraMake}", "Fabricante de la cámara");
                        AddUpstreamVar("ImageWidth", "{ImageWidth}", "Ancho original en píxeles");
                        AddUpstreamVar("ImageHeight", "{ImageHeight}", "Alto original en píxeles");
                        AddUpstreamVar("Orientation", "{Orientation}", "Orientación (Horizontal, Vertical o Cuadrada)");
                        AddUpstreamVar("AspectRatio", "{AspectRatio}", "Relación de aspecto calculada (ej. 16:9)");
                        AddUpstreamVar("Megapixels", "{Megapixels}", "Resolución en Megapíxeles");
                    }
                    else if (typeName.Contains("ImageOptimizerNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("File:Optimized", "{File:Optimized}", "Ruta absoluta al archivo optimizado");
                        AddUpstreamVar("FileSize:Optimized", "{FileSize:Optimized}", "Tamaño del archivo optimizado en bytes");
                        AddUpstreamVar("FileSizeKB:Optimized", "{FileSizeKB:Optimized}", "Tamaño optimizado en Kilobytes");
                        AddUpstreamVar("FileSizeMB:Optimized", "{FileSizeMB:Optimized}", "Tamaño optimizado en Megabytes");
                        AddUpstreamVar("OutputFileSize", "{OutputFileSize}", "Tamaño del archivo optimizado en bytes");
                        AddUpstreamVar("OutputFileSizeKB", "{OutputFileSizeKB}", "Tamaño optimizado en Kilobytes");
                        AddUpstreamVar("OutputFileSizeMB", "{OutputFileSizeMB}", "Tamaño optimizado en Megabytes");
                        AddUpstreamVar("OriginalFileSize", "{OriginalFileSize}", "Tamaño original del archivo en bytes");
                        AddUpstreamVar("SavedBytes", "{SavedBytes}", "Bytes reducidos/ahorrados");
                        AddUpstreamVar("SavedPercent", "{SavedPercent}", "Porcentaje de reducción de tamaño");
                        AddUpstreamVar("CompressionRatio", "{CompressionRatio}", "Ratio de compresión (salida / original)");
                        AddUpstreamVar("OptimizedFormat", "{OptimizedFormat}", "Formato de imagen resultante (WebP/JPEG/PNG)");
                        AddUpstreamVar("OptimizedWidth", "{OptimizedWidth}", "Ancho optimizado en píxeles");
                        AddUpstreamVar("OptimizedHeight", "{OptimizedHeight}", "Alto optimizado en píxeles");
                    }
                    else if (typeName.Contains("BackgroundRemoverNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("File:NoBackground", "{File:NoBackground}", "Ruta absoluta al archivo sin fondo");
                        AddUpstreamVar("FileSize:NoBackground", "{FileSize:NoBackground}", "Tamaño de la imagen sin fondo en bytes");
                        AddUpstreamVar("OutputFileSize", "{OutputFileSize}", "Tamaño de imagen sin fondo");
                        AddUpstreamVar("OriginalFileSize", "{OriginalFileSize}", "Tamaño original previo a eliminar fondo");
                        AddUpstreamVar("SavedBytes", "{SavedBytes}", "Diferencia de tamaño en bytes");
                        AddUpstreamVar("AI:BackgroundRemoved", "{AI:BackgroundRemoved}", "Verdadero si el fondo fue segmentado");
                        AddUpstreamVar("AI:BackgroundModel", "{AI:BackgroundModel}", "Nombre del modelo neural RMBG utilizado");
                        AddUpstreamVar("AI:AlphaMaskGenerated", "{AI:AlphaMaskGenerated}", "Verdadero si se generó la máscara alfa");
                    }
                    else if (typeName.Contains("SuperResolutionUpscalerNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("File:SuperResolution", "{File:SuperResolution}", "Ruta absoluta a la imagen reescalada");
                        AddUpstreamVar("FileSize:SuperResolution", "{FileSize:SuperResolution}", "Tamaño de la imagen reescalada en bytes");
                        AddUpstreamVar("OutputFileSize", "{OutputFileSize}", "Tamaño de imagen escalada");
                        AddUpstreamVar("OriginalFileSize", "{OriginalFileSize}", "Tamaño antes de escalar");
                        AddUpstreamVar("AI:Upscaled", "{AI:Upscaled}", "Verdadero si la super-resolución fue aplicada");
                        AddUpstreamVar("AI:ScaleFactor", "{AI:ScaleFactor}", "Factor de escala neural (2x, 4x)");
                        AddUpstreamVar("AI:OriginalResolution", "{AI:OriginalResolution}", "Resolución previa al escalado");
                        AddUpstreamVar("AI:NewResolution", "{AI:NewResolution}", "Nueva resolución tras escalado");
                        AddUpstreamVar("AI:UpscalerModel", "{AI:UpscalerModel}", "Modelo de red convolucional utilizado");
                    }
                    else if (typeName.Contains("SmartImageClassifierNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("AI:TopLabel", "{AI:TopLabel}", "Etiqueta visual de mayor confianza");
                        AddUpstreamVar("AI:Confidence", "{AI:Confidence}", "Porcentaje de certeza de la clasificación");
                        AddUpstreamVar("AI:ClassificationScore", "{AI:ClassificationScore}", "Puntuación numérica de inferencia");
                    }
                    else if (typeName.Contains("ObjectDetectorNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("AI:DetectedObjects", "{AI:DetectedObjects}", "Lista de objetos detectados (ej. person, car)");
                        AddUpstreamVar("AI:ObjectsCount", "{AI:ObjectsCount}", "Cantidad total de objetos detectados");
                        AddUpstreamVar("AI:HasTarget", "{AI:HasTarget}", "True si detectó el objeto buscado");
                    }
                    else if (typeName.Contains("FaceDetectorNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("AI:FacesCount", "{AI:FacesCount}", "Número de rostros humanos detectados");
                        AddUpstreamVar("AI:HasFaces", "{AI:HasFaces}", "True si contiene uno o más rostros");
                    }
                    else if (typeName.Contains("LocalWhisperTranscriberNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("AI:Transcription", "{AI:Transcription}", "Transcripción textual de audio/voz");
                        AddUpstreamVar("AI:Language", "{AI:Language}", "Idioma detectado del audio");
                        AddUpstreamVar("AI:AudioDuration", "{AI:AudioDuration}", "Duración total del audio en segundos");
                    }
                    else if (typeName.Contains("PiiAnonymizerNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("AI:PiiRedacted", "{AI:PiiRedacted}", "Texto con datos sensibles anonimizados");
                        AddUpstreamVar("AI:PiiEntitiesCount", "{AI:PiiEntitiesCount}", "Total de entidades personales redactadas");
                    }
                    else if (typeName.Contains("VariableInjectorNode", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var param in upstreamNode.Parameters)
                        {
                            string keyName = param.Key;
                            if (!string.IsNullOrWhiteSpace(keyName))
                            {
                                AddUpstreamVar(keyName, $"{{{keyName}}}", $"Inyectado por {upstreamNode.Title}");
                            }
                        }
                    }
                    else if (typeName.Contains("SmartUnpackNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("UnpackedFrom", "{UnpackedFrom}", "Ruta del archivo comprimido original");
                        AddUpstreamVar("ArchiveFormat", "{ArchiveFormat}", "Formato del contenedor (ZIP/7Z/RAR)");
                        AddUpstreamVar("UnpackedFileCount", "{UnpackedFileCount}", "Número de ficheros extraídos");
                    }
                    else if (typeName.Contains("HashCalculatorNode", StringComparison.OrdinalIgnoreCase))
                    {
                        string storeKey = upstreamNode.Parameters.FirstOrDefault(p => p.Key.Equals("StoreInMetadataKey", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? "Hash:SHA256";
                        AddUpstreamVar(storeKey, $"{{{storeKey}}}", $"Hash criptográfico calculado por {upstreamNode.Title}");
                        AddUpstreamVar("Hash", "{Hash}", $"Hash genérico de {upstreamNode.Title}");
                    }
                    else if (typeName.Contains("DeduplicationFilterNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("DuplicateOf", "{DuplicateOf}", "Ruta del archivo original si es duplicado");
                    }
                    else if (typeName.Contains("CliExecutionNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("Cli:StdOut", "{Cli:StdOut}", "Salida estándar (stdout) del comando");
                        AddUpstreamVar("Cli:StdErr", "{Cli:StdErr}", "Salida de errores (stderr) del comando");
                        AddUpstreamVar("Cli:ExitCode", "{Cli:ExitCode}", "Código de salida del proceso");
                    }
                    else if (typeName.Contains("DirectoryInspectorNode", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUpstreamVar("Directory:FilesCount", "{Directory:FilesCount}", "Archivos en la carpeta inspeccionada");
                        AddUpstreamVar("Directory:SubdirsCount", "{Directory:SubdirsCount}", "Subdirectorios presentes");
                        AddUpstreamVar("Directory:ArchivesCount", "{Directory:ArchivesCount}", "Paquetes comprimidos detectados");
                    }

                    if (upstreamGroup.Variables.Count > 0)
                    {
                        upstreamGroups.Add(upstreamGroup);
                    }
                }
            }
        }

        // Añadir grupos upstream al inicio si existen
        if (upstreamGroups.Count > 0)
        {
            result.AddRange(upstreamGroups);
        }

        // 2. Variables de Sistema y Entorno
        var systemGroup = new VariableGroupItem("🌐 Sistema & Rutas (System)");
        void AddSysVar(string name, string token, string desc, string category = "Sistema")
        {
            string sample = VariableTemplateResolver.Resolve(token, previewItem);
            systemGroup.Variables.Add(new VariableItem(name, token, desc, Category: category, SampleValue: sample));
        }

        AddSysVar("FileName", "{FileName}", "Nombre completo con extensión (ej. photo.jpg)");
        AddSysVar("FileNameNoExt", "{FileNameNoExt}", "Nombre base sin extensión (ej. photo)");
        AddSysVar("Extension", "{Extension}", "Extensión del fichero sin punto (ej. jpg)");
        AddSysVar("CurrentPath", "{CurrentPath}", "Ruta absoluta actual del elemento");
        AddSysVar("OriginalPath", "{OriginalPath}", "Ruta de origen inicial inmutable");
        AddSysVar("File:Original", "{File:Original}", "Ruta absoluta al archivo original inmutable");
        AddSysVar("File:Current", "{File:Current}", "Ruta absoluta al archivo activo actual");
        AddSysVar("RelativePath", "{RelativePath}", "Ruta relativa desde el directorio raíz");
        AddSysVar("DateNow", "{DateNow}", "Fecha y hora actual ISO");
        AddSysVar("TempDir", "{TempDir}", "Directorio de trabajo temporal configurado en Ajustes");
        AddSysVar("GlobalOutputDir", "{GlobalOutputDir}", "Ruta de salida global configurada en Ajustes");
        AddSysVar("RandomId", "{RandomId}", "Identificador aleatorio único de 8 caracteres");
        AddSysVar("Guid", "{Guid}", "Identificador GUID estándar completo");
        AddSysVar("Counter", "{Counter}", "Contador secuencial en el lote actual (ej. 1, 2, 3...)");
        AddSysVar("FileCount", "{FileCount}", "Número total de archivos procesados");
        AddSysVar("UserName", "{UserName}", "Nombre del usuario de Windows");
        AddSysVar("MachineName", "{MachineName}", "Nombre del equipo host");
        result.Add(systemGroup);

        // 3. Fechas y Horas
        var dateGroup = new VariableGroupItem("📅 Fechas & Tiempos");
        void AddDateVar(string name, string token, string desc)
        {
            string sample = VariableTemplateResolver.Resolve(token, previewItem);
            dateGroup.Variables.Add(new VariableItem(name, token, desc, Category: "Fechas", SampleValue: sample));
        }

        AddDateVar("DateNow", "{DateNow}", "Fecha actual de ejecución (yyyy-MM-dd)");
        AddDateVar("TimeNow", "{TimeNow}", "Hora actual de ejecución (HH-mm-ss)");
        AddDateVar("DateTimeNow", "{DateTimeNow}", "Marca temporal combinada (yyyy-MM-dd_HH-mm-ss)");
        AddDateVar("Year", "{Year}", "Año actual de 4 dígitos (yyyy)");
        AddDateVar("Month", "{Month}", "Mes actual de 2 dígitos (MM)");
        AddDateVar("Day", "{Day}", "Día actual de 2 dígitos (dd)");
        AddDateVar("Hour", "{Hour}", "Hora actual (HH)");
        AddDateVar("Minute", "{Minute}", "Minuto actual (mm)");
        AddDateVar("Second", "{Second}", "Segundo actual (ss)");
        result.Add(dateGroup);

        // 4. Dimensiones y Tamaños
        var sizeGroup = new VariableGroupItem("📐 Tamaño & Métricas");
        void AddSizeVar(string name, string token, string desc)
        {
            string sample = VariableTemplateResolver.Resolve(token, previewItem);
            sizeGroup.Variables.Add(new VariableItem(name, token, desc, Category: "Tamaños", SampleValue: sample));
        }

        AddSizeVar("SizeMB", "{SizeMB}", "Tamaño del archivo en Megabytes (ej. 3.00 MB)");
        AddSizeVar("SizeKB", "{SizeKB}", "Tamaño del archivo en Kilobytes");
        AddSizeVar("SizeBytes", "{SizeBytes}", "Tamaño exacto del archivo en bytes");
        AddSizeVar("FileSize:Original", "{FileSize:Original}", "Tamaño del archivo original en bytes");
        AddSizeVar("FileSizeKB:Original", "{FileSizeKB:Original}", "Tamaño del archivo original en KB");
        AddSizeVar("FileSizeMB:Original", "{FileSizeMB:Original}", "Tamaño del archivo original en MB");
        AddSizeVar("FileSize:Current", "{FileSize:Current}", "Tamaño del archivo activo actual en bytes");
        AddSizeVar("OriginalFileSize", "{OriginalFileSize}", "Tamaño original del archivo en bytes");
        AddSizeVar("OutputFileSize", "{OutputFileSize}", "Tamaño del archivo resultante en bytes");
        AddSizeVar("SavedBytes", "{SavedBytes}", "Bytes ahorrados/reducidos");
        AddSizeVar("SavedPercent", "{SavedPercent}", "Porcentaje de reducción de tamaño");
        AddSizeVar("CompressionRatio", "{CompressionRatio}", "Ratio numérico de compresión");
        result.Add(sizeGroup);

        // 5. Funciones de Expresión
        var fnGroup = new VariableGroupItem("🔤 Funciones de Transformación");
        void AddFnVar(string name, string token, string desc)
        {
            string sample = VariableTemplateResolver.Resolve(token, previewItem);
            fnGroup.Variables.Add(new VariableItem(name, token, desc, Category: "Funciones", SampleValue: sample));
        }

        AddFnVar("Upper", "{Upper(FileNameNoExt)}", "Convierte el texto a MAYÚSCULAS");
        AddFnVar("Lower", "{Lower(Extension)}", "Convierte el texto a minúsculas");
        AddFnVar("Sanitize", "{Sanitize(FileName)}", "Limpia caracteres prohibidos de Windows");
        AddFnVar("PadLeft", "{PadLeft(Counter, 4, \"0\")}", "Rellena números con ceros a la izquierda (ej. 0001)");
        AddFnVar("FormatDate", "{FormatDate(DateNow, \"yyyy-MM\")}", "Formato personalizado de fecha");
        AddFnVar("Substring", "{Substring(FileNameNoExt, 0, 8)}", "Extrae los primeros caracteres del texto");
        AddFnVar("RegexMatch", "{RegexMatch(FileNameNoExt, \"[0-9]+\")}", "Extrae una coincidencia de expresión regular");
        AddFnVar("RegexReplace", "{RegexReplace(FileNameNoExt, \"[^a-zA-Z0-9]\", \"_\")}", "Reemplaza patrones con Regex");
        AddFnVar("Coalesce", "{Coalesce(DateTaken, DateNow)}", "Primer valor no vacío de la lista");
        AddFnVar("Default", "{Default(Extension, \"dat\")}", "Valor de respaldo si el campo está vacío");
        result.Add(fnGroup);

        return result;
    }

    public FileItemContext CreatePreviewItem(NodeViewModel? targetNode)
    {
        var item = new FileItemContext(@"C:\Photos\2026\Vacations\IMG_4096.jpg")
        {
            OriginalPath = @"C:\Photos\2026\Vacations\IMG_4096.jpg",
            FileSizeBytes = 3_145_728 // 3.0 MB
        };

        item.Metadata["TemporaryDirectory"] = AppPaths.DefaultTempDirectory;
        item.Metadata["GlobalOutputDir"] = AppPaths.DefaultGlobalOutputDir;
        item.Metadata["Counter"] = 1;
        item.Metadata["TotalFileCount"] = 42;

        // EXIF & Metadatos de imagen
        string sampleDate = DateTime.Now.AddDays(-15).ToString("yyyy-MM-dd HH:mm:ss");
        item.Metadata["DateTaken"] = sampleDate;
        item.Metadata["Exif:DateTaken"] = sampleDate;
        item.Metadata["CameraModel"] = "Sony A7 IV";
        item.Metadata["CameraMake"] = "Sony";
        item.Metadata["ImageWidth"] = 3840;
        item.Metadata["ImageHeight"] = 2160;
        item.Metadata["Exif:ImageWidth"] = 3840;
        item.Metadata["Exif:ImageHeight"] = 2160;
        item.Metadata["Orientation"] = "Landscape";
        item.Metadata["AspectRatio"] = "16:9";
        item.Metadata["Megapixels"] = "8.3 MP";

        // Tamaños y compresión
        item.Metadata["OriginalFileSize"] = 3_145_728L;
        item.Metadata["OriginalFileSizeBytes"] = 3_145_728L;
        item.Metadata["OutputFileSize"] = 1_048_576L; // 1 MB
        item.Metadata["OutputFileSizeBytes"] = 1_048_576L;
        item.Metadata["SavedBytes"] = 2_097_152L; // 2 MB ahorrados
        item.Metadata["SavedPercent"] = 66.7;
        item.Metadata["CompressionRatio"] = 0.3333;
        item.Metadata["OptimizedFormat"] = "WebP";
        item.Metadata["OptimizedWidth"] = 1920;
        item.Metadata["OptimizedHeight"] = 1080;
        item.RegisterVersion("Optimized", @"C:\FileFlow\Samples\sample_photo_optimized.webp");
        item.Metadata["FileSize:Optimized"] = 1_048_576L;
        item.Metadata["FileSizeKB:Optimized"] = 1024L;
        item.Metadata["FileSizeMB:Optimized"] = 1.0;

        // Metadatos de IA
        item.Metadata["AI:TopLabel"] = "Landscape";
        item.Metadata["AI:Confidence"] = "96.4%";
        item.Metadata["AI:ClassificationScore"] = "0.96";
        item.Metadata["AI:ObjectsCount"] = 3;
        item.Metadata["AI:DetectedObjects"] = "person, dog, tree";
        item.Metadata["AI:HasTarget"] = true;
        item.Metadata["AI:FacesCount"] = 1;
        item.Metadata["AI:HasFaces"] = true;
        item.Metadata["AI:BackgroundRemoved"] = true;
        item.Metadata["AI:BackgroundModel"] = "rmbg-1.4";
        item.Metadata["AI:AlphaMaskGenerated"] = true;
        item.Metadata["AI:Upscaled"] = true;
        item.Metadata["AI:ScaleFactor"] = "4x";
        item.Metadata["AI:UpscalerModel"] = "realesrgan";
        item.Metadata["AI:OriginalResolution"] = "1920x1080";
        item.Metadata["AI:NewResolution"] = "7680x4320";
        item.Metadata["AI:Transcription"] = "FileFlow Studio transcript sample";
        item.Metadata["AI:Language"] = "es";
        item.Metadata["AI:AudioDuration"] = "12.5";
        item.Metadata["AI:PiiRedacted"] = "Cliente [REDACTADO]";
        item.Metadata["AI:PiiEntitiesCount"] = 1;

        // Hash
        item.Metadata["Hash:SHA256"] = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        item.Metadata["Hash"] = "e3b0c442";

        // Archivos y CLI
        item.Metadata["UnpackedFrom"] = @"C:\Archives\Bundle.zip";
        item.Metadata["ArchiveFormat"] = "ZIP";
        item.Metadata["UnpackedFileCount"] = 15;
        item.Metadata["Cli:StdOut"] = "SUCCESS";
        item.Metadata["Cli:StdErr"] = "";
        item.Metadata["Cli:ExitCode"] = 0;

        return item;
    }

    public List<FileVersionOption> GetAvailableFileVersions(NodeViewModel targetNode, IEnumerable<ConnectionViewModel> connections)
    {
        var versions = new List<FileVersionOption>
        {
            new("Original", "{OriginalPath}", FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Version_Original", "Original"), "📄", FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Version_Original_Desc", "Archivo original inmutable")),
            new("Current", "{CurrentPath}", FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Version_Current", "Actual"), "⚡", FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Version_Current_Desc", "Versión activa procesada hasta este nodo"))
        };

        if (targetNode == null || connections == null)
        {
            return versions;
        }

        var visitedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(targetNode.Id))
        {
            visitedNodeIds.Add(targetNode.Id);
        }

        var queue = new Queue<NodeViewModel>();
        queue.Enqueue(targetNode);
        var connectionsList = connections.ToList();
        var addedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Original", "Current" };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var incomingConns = connectionsList.Where(c => 
                c.Target.NodeOwner == current || 
                (c.Target.NodeOwner != null && !string.IsNullOrEmpty(current.Id) && string.Equals(c.Target.NodeOwner.Id, current.Id, StringComparison.OrdinalIgnoreCase))).ToList();

            foreach (var conn in incomingConns)
            {
                var upstreamNode = conn.Source.NodeOwner;
                if (upstreamNode != null && visitedNodeIds.Add(upstreamNode.Id))
                {
                    queue.Enqueue(upstreamNode);
                    string typeName = upstreamNode.NodeTypeName ?? string.Empty;
                    string instName = upstreamNode.NodeInstance?.GetType().Name ?? string.Empty;
                    string instFullName = upstreamNode.NodeInstance?.GetType().FullName ?? string.Empty;

                    bool isOptimizer = typeName.Contains("ImageOptimizerNode", StringComparison.OrdinalIgnoreCase)
                                       || instName.Contains("ImageOptimizerNode", StringComparison.OrdinalIgnoreCase)
                                       || instFullName.Contains("ImageOptimizerNode", StringComparison.OrdinalIgnoreCase);

                    bool isBgRemover = typeName.Contains("BackgroundRemoverNode", StringComparison.OrdinalIgnoreCase)
                                       || instName.Contains("BackgroundRemoverNode", StringComparison.OrdinalIgnoreCase)
                                       || instFullName.Contains("BackgroundRemoverNode", StringComparison.OrdinalIgnoreCase);

                    bool isSuperRes = typeName.Contains("SuperResolutionUpscalerNode", StringComparison.OrdinalIgnoreCase)
                                      || instName.Contains("SuperResolutionUpscalerNode", StringComparison.OrdinalIgnoreCase)
                                      || instFullName.Contains("SuperResolutionUpscalerNode", StringComparison.OrdinalIgnoreCase);

                    if (isOptimizer && addedTags.Add("Optimized"))
                    {
                        versions.Add(new FileVersionOption(
                            "Optimized",
                            "{File:Optimized}",
                            FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Version_Optimized", "Optimizada"),
                            "🖼️",
                            FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Version_Optimized_Desc", "Versión optimizada por ImageOptimizerNode"),
                            IsUpstream: true,
                            SourceNodeTitle: upstreamNode.Title));
                    }
                    else if (isBgRemover && addedTags.Add("NoBackground"))
                    {
                        versions.Add(new FileVersionOption(
                            "NoBackground",
                            "{File:NoBackground}",
                            FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Version_NoBackground", "Sin Fondo"),
                            "✂️",
                            FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Version_NoBackground_Desc", "Versión sin fondo segmentada por IA"),
                            IsUpstream: true,
                            SourceNodeTitle: upstreamNode.Title));
                    }
                    else if (isSuperRes && addedTags.Add("SuperResolution"))
                    {
                        versions.Add(new FileVersionOption(
                            "SuperResolution",
                            "{File:SuperResolution}",
                            FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Version_SuperResolution", "Super-Resolución"),
                            "🔍",
                            FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString("Version_SuperResolution_Desc", "Imagen escalada con red neural"),
                            IsUpstream: true,
                            SourceNodeTitle: upstreamNode.Title));
                    }
                }
            }
        }

        return versions;
    }
}
