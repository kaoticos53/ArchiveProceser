namespace FileFlow.App.Services;

/// <summary>
/// Servicio centralizado para resolución de iconos y emojis visuales asociados a tipos de nodo y categorías del flujo.
/// </summary>
public static class NodeIconResolver
{
    private static readonly Dictionary<string, string> _exactNodeTypeIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FolderSourceNode"] = "📁",
        ["DirectoryInspectorNode"] = "🕵️",
        ["SmartUnpackNode"] = "📦",
        ["ArchiveCompressorNode"] = "🗜️",
        ["ArchiveFilterNode"] = "🗄️",
        ["ImageOptimizerNode"] = "🖼️",
        ["ExifMetadataNode"] = "🏷️",
        ["MediaTranscoderNode"] = "🎬",
        ["LocalOcrNode"] = "🔍",
        ["SmartImageClassifierNode"] = "👁️",
        ["FaceDetectorNode"] = "👤",
        ["ObjectDetectorNode"] = "🎯",
        ["PromptObjectDetectorNode"] = "🎯",
        ["SuperResolutionUpscalerNode"] = "✨",
        ["BackgroundRemoverNode"] = "✂️",
        ["WhisperTranscriptionNode"] = "🎙️",
        ["DocumentProcessorNode"] = "📄",
        ["PdfMergeNode"] = "📑",
        ["NetworkDownloadNode"] = "📥",
        ["NetworkUploadNode"] = "📤",
        ["ExcelDataSourceNode"] = "📊",
        ["CsvDataSourceNode"] = "📑",
        ["DataLookupNode"] = "🔍",
        ["SqliteExportNode"] = "🗄️",
        ["DataFormatConverterNode"] = "🔄",
        ["VariableInjectorNode"] = "🏷️",
        ["DeduplicationFilterNode"] = "👯",
        ["HashCalculatorNode"] = "🔑",
        ["ExpressionFilterNode"] = "⚡",
        ["SwitchCaseNode"] = "🔀",
        ["ForkJoinBarrierNode"] = "🔀",
        ["ThrottleDelayNode"] = "⏳",
        ["BatchBufferNode"] = "📊",
        ["DestinationSinkNode"] = "💾",
        ["FileRelocatorNode"] = "🚚",
        ["EmptyDirectoryCleanerNode"] = "🧹",
        ["SafeRecycleDeleteNode"] = "♻️",
        ["OriginalFileActionNode"] = "🛡️",
        ["AdvancedRenamerNode"] = "✏️",
        ["OperationReportNode"] = "📋",
        ["LogOutputNode"] = "📝",
        ["WebhookNotificationNode"] = "🌐",
        ["CliExecutionNode"] = "💻",
        ["CustomScriptNode"] = "📜"
    };

    /// <summary>
    /// Devuelve el icono representativo para una categoría de nodos.
    /// </summary>
    public static string GetIconForCategory(string category)
    {
        return (category?.Trim().ToLowerInvariant()) switch
        {
            "all" or "todas" => "🌐",
            "favorites" or "favoritos" => "⭐",
            "frequent" or "frecuentes" or "más usados" => "🔥",
            "files" or "filesystem" or "archivos y sistema" or "archivos" or "file system" => "📁",
            "imagevision" or "images" or "imágenes" or "imagen y visión ia" or "fotos" => "🖼️",
            "audiovoice" or "audio" or "audio y voz ia" or "voz" => "🎙️",
            "documents" or "documentos" or "documentos y pdf" or "pdf" or "pdfs" => "📄",
            "data" or "data & tables" or "data & databases" or "datos y tablas" or "datos" or "databases" => "📊",
            "languageai" or "lenguaje y llm" or "language & llm" or "llm" => "🧠",
            "security" or "seguridad y rgpd" or "security & privacy" or "hashing" => "🔒",
            "logic" or "lógica y control" or "logic & flow" or "flujo" => "🔀",
            "archives" or "archivos comprimidos" or "compresión" or "compressed archives" => "📦",
            "network" or "red y nube" or "network & cloud" or "network & remote" or "red" => "🌐",
            "integrations" or "integraciones y diagnóstico" or "integrations & diagnostics" or "webhooks" or "cli" => "⚡",
            "scripting" or "scripts" or "c#" or "javascript" => "📜",
            "metadata" or "metadatos" => "🏷️",
            "media & docs" or "mediadocs" => "🎬",
            _ => "🧩"
        };
    }

    /// <summary>
    /// Devuelve el icono asociado al nombre del tipo de nodo.
    /// </summary>
    public static string GetIconForNodeType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return "🧩";

        string cleanName = typeName.Split('.').Last();

        if (_exactNodeTypeIcons.TryGetValue(cleanName, out var icon))
        {
            return icon;
        }

        // Fallback heurístico por palabras clave
        if (cleanName.Contains("Source", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Folder", StringComparison.OrdinalIgnoreCase)) return "📁";
        if (cleanName.Contains("Inspector", StringComparison.OrdinalIgnoreCase)) return "🕵️";
        if (cleanName.Contains("Archive", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Unpack", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Compress", StringComparison.OrdinalIgnoreCase)) return "📦";
        if (cleanName.Contains("Image", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Photo", StringComparison.OrdinalIgnoreCase)) return "🖼️";
        if (cleanName.Contains("Ocr", StringComparison.OrdinalIgnoreCase)) return "🔍";
        if (cleanName.Contains("Face", StringComparison.OrdinalIgnoreCase)) return "👤";
        if (cleanName.Contains("Object", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Detector", StringComparison.OrdinalIgnoreCase)) return "🎯";
        if (cleanName.Contains("Audio", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Voice", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Whisper", StringComparison.OrdinalIgnoreCase)) return "🎙️";
        if (cleanName.Contains("Pdf", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Document", StringComparison.OrdinalIgnoreCase)) return "📄";
        if (cleanName.Contains("Download", StringComparison.OrdinalIgnoreCase)) return "📥";
        if (cleanName.Contains("Upload", StringComparison.OrdinalIgnoreCase)) return "📤";
        if (cleanName.Contains("Network", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Cloud", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Http", StringComparison.OrdinalIgnoreCase)) return "🌐";
        if (cleanName.Contains("Data", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Table", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Excel", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Csv", StringComparison.OrdinalIgnoreCase)) return "📊";
        if (cleanName.Contains("Hash", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Security", StringComparison.OrdinalIgnoreCase)) return "🔑";
        if (cleanName.Contains("Filter", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Switch", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Logic", StringComparison.OrdinalIgnoreCase)) return "🔀";
        if (cleanName.Contains("Sink", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Save", StringComparison.OrdinalIgnoreCase)) return "💾";
        if (cleanName.Contains("Rename", StringComparison.OrdinalIgnoreCase)) return "✏️";
        if (cleanName.Contains("Report", StringComparison.OrdinalIgnoreCase) || cleanName.Contains("Log", StringComparison.OrdinalIgnoreCase)) return "📋";
        if (cleanName.Contains("Script", StringComparison.OrdinalIgnoreCase)) return "📜";

        return "🧩";
    }
}
