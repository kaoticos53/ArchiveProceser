namespace FileFlow.Sdk;

/// <summary>
/// Define el rol o etapa funcional de un nodo dentro de la arquitectura de pipeline DAG (ETL).
/// </summary>
public enum PipelineRole
{
    /// <summary>
    /// Ingesta, disparadores o fuentes de datos de entrada (FolderSource, RemoteDownload, ExcelReader, etc.).
    /// </summary>
    Source,

    /// <summary>
    /// Filtrado, decisiones condicionales y bifurcación de flujos (ExpressionFilter, SwitchCase, Deduplication, etc.).
    /// </summary>
    Filter,

    /// <summary>
    /// Transformación, conversión de formatos, modificación o sanitización de contenido (Renamer, ImageOptimizer, PiiAnonymizer, etc.).
    /// </summary>
    Transform,

    /// <summary>
    /// Análisis profundo, inferencia de inteligencia artificial o extracción de metadatos (OCR, Whisper, ObjectDetector, Exif, etc.).
    /// </summary>
    Analyze,

    /// <summary>
    /// Destinos finales, almacenamiento, exportación o entrega (DestinationSink, SftpUpload, Sqlite, CsvExport, etc.).
    /// </summary>
    Sink,

    /// <summary>
    /// Control de flujo, sincronización, temporización o utilidades de diagnóstico (Batch, Delay, ForkJoin, Scripts, Logs, etc.).
    /// </summary>
    Control
}
