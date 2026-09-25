using Material.Icons;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio centralizado de iconografía de nodos y categorías del flujo.
///
/// Devuelve <see cref="MaterialIconKind"/> (icono vectorial de Material Design Icons) en lugar de
/// emojis: los emojis dependen de las fuentes del sistema y se renderizan distinto (o como
/// cuadraditos "tofu") en Windows, Linux y macOS. Los trazos vectoriales son idénticos en las tres
/// plataformas y heredan el color del tema vía <c>Foreground</c>.
/// </summary>
public static class NodeIconResolver
{
    /// <summary>Icono de reserva para nodos desconocidos.</summary>
    public const MaterialIconKind FallbackNodeIcon = MaterialIconKind.Puzzle;

    /// <summary>Icono de reserva para categorías desconocidas.</summary>
    public const MaterialIconKind FallbackCategoryIcon = MaterialIconKind.Puzzle;

    /// <summary>Icono de reserva para acciones personalizadas sin icono reconocible.</summary>
    public const MaterialIconKind FallbackActionIcon = MaterialIconKind.Cog;

    private static readonly Dictionary<string, MaterialIconKind> _exactNodeTypeIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        // Archivos y sistema
        ["FolderSourceNode"] = MaterialIconKind.Folder,
        ["DirectoryInspectorNode"] = MaterialIconKind.FileSearchOutline,
        ["DestinationSinkNode"] = MaterialIconKind.ContentSaveOutline,
        ["FileRelocatorNode"] = MaterialIconKind.Truck,
        ["EmptyDirectoryCleanerNode"] = MaterialIconKind.Broom,
        ["IntermediateCleanupNode"] = MaterialIconKind.Broom,
        ["SafeRecycleDeleteNode"] = MaterialIconKind.Recycle,
        ["OriginalFileActionNode"] = MaterialIconKind.ShieldLock,
        ["AdvancedRenamerNode"] = MaterialIconKind.RenameBox,

        // Archivos comprimidos
        ["SmartUnpackNode"] = MaterialIconKind.PackageVariantClosed,
        ["ArchiveCompressorNode"] = MaterialIconKind.ZipBox,
        ["ArchiveFilterNode"] = MaterialIconKind.ArchiveOutline,
        ["ArchiveFanOutNode"] = MaterialIconKind.FolderZip,

        // Imagen y visión IA
        ["ImageOptimizerNode"] = MaterialIconKind.Image,
        ["ExifMetadataNode"] = MaterialIconKind.Tag,
        ["MediaTranscoderNode"] = MaterialIconKind.Movie,
        ["LocalOcrNode"] = MaterialIconKind.TextRecognition,
        ["SmartImageClassifierNode"] = MaterialIconKind.Eye,
        ["FaceDetectorNode"] = MaterialIconKind.FaceRecognition,
        ["ObjectDetectorNode"] = MaterialIconKind.Target,
        ["PromptObjectDetectorNode"] = MaterialIconKind.Target,
        ["SuperResolutionUpscalerNode"] = MaterialIconKind.AutoFix,
        ["BackgroundRemoverNode"] = MaterialIconKind.ImageFilter,

        // Audio y voz IA
        ["WhisperTranscriptionNode"] = MaterialIconKind.Microphone,
        ["VoiceActivityDetectorNode"] = MaterialIconKind.Waveform,
        ["TextToSpeechNode"] = MaterialIconKind.VolumeHigh,

        // Documentos
        ["DocumentProcessorNode"] = MaterialIconKind.FileDocument,
        ["PdfMergeNode"] = MaterialIconKind.FilePdfBox,

        // Red y nube
        ["NetworkDownloadNode"] = MaterialIconKind.Download,
        ["NetworkUploadNode"] = MaterialIconKind.Upload,
        ["WebhookNotificationNode"] = MaterialIconKind.Webhook,

        // Datos y tablas
        ["ExcelDataSourceNode"] = MaterialIconKind.FileExcel,
        ["CsvDataSourceNode"] = MaterialIconKind.FileTable,
        ["DataLookupNode"] = MaterialIconKind.TableSearch,
        ["SqliteExportNode"] = MaterialIconKind.Database,
        ["DataFormatConverterNode"] = MaterialIconKind.SwapHorizontal,
        ["SyntheticDataSourceNode"] = MaterialIconKind.Database,
        ["SyntheticDataSetNode"] = MaterialIconKind.Database,

        // Lenguaje y LLM
        ["LocalLlmProcessorNode"] = MaterialIconKind.Brain,
        ["MultimodalVisionLlmNode"] = MaterialIconKind.Brain,
        ["LocalAiTranslatorNode"] = MaterialIconKind.Language,
        ["PromptTransformerNode"] = MaterialIconKind.AutoFix,

        // Seguridad y RGPD
        ["PiiAnonymizerNode"] = MaterialIconKind.ShieldLock,
        ["ContentModerationFilterNode"] = MaterialIconKind.ShieldCheck,
        ["HashCalculatorNode"] = MaterialIconKind.Fingerprint,

        // Lógica y control
        ["ExpressionFilterNode"] = MaterialIconKind.Flash,
        ["SwitchCaseNode"] = MaterialIconKind.CallSplit,
        ["VersionRouterNode"] = MaterialIconKind.CallSplit,
        ["ForkJoinBarrierNode"] = MaterialIconKind.Merge,
        ["BestVersionSelectorNode"] = MaterialIconKind.Certificate,
        ["SwitchActiveFileNode"] = MaterialIconKind.SwapHorizontal,
        ["FileForkNode"] = MaterialIconKind.SourceFork,
        ["DeduplicationFilterNode"] = MaterialIconKind.ContentCopy,
        ["ThrottleDelayNode"] = MaterialIconKind.TimerSandComplete,
        ["BatchBufferNode"] = MaterialIconKind.ChartBar,
        ["VariableInjectorNode"] = MaterialIconKind.Tag,

        // Metadatos, informes y registro
        ["OperationReportNode"] = MaterialIconKind.ClipboardText,
        ["LogOutputNode"] = MaterialIconKind.NoteText,

        // Integraciones y scripts
        ["CliExecutionNode"] = MaterialIconKind.ConsoleLine,
        ["CustomScriptNode"] = MaterialIconKind.ScriptText,
        ["ScriptStudioNode"] = MaterialIconKind.ScriptText
    };

    /// <summary>
    /// Reglas heurísticas por palabra clave, aplicadas cuando el tipo de nodo no está en la tabla exacta.
    /// El orden importa: la primera coincidencia gana.
    /// </summary>
    private static readonly (string[] Keywords, MaterialIconKind Kind)[] _keywordRules =
    [
        (["Whisper", "Voice", "Audio", "Speech", "Tts"], MaterialIconKind.Microphone),
        (["Ocr", "Recogni"], MaterialIconKind.TextRecognition),
        (["Face", "Person", "Pii"], MaterialIconKind.FaceRecognition),
        (["Object", "Detector", "Target"], MaterialIconKind.Target),
        (["Unpack", "Archive", "Zip", "Compress"], MaterialIconKind.ZipBox),
        (["Pdf"], MaterialIconKind.FilePdfBox),
        (["Document", "Word"], MaterialIconKind.FileDocument),
        (["Image", "Photo", "Picture"], MaterialIconKind.Image),
        (["Video", "Movie", "Transcod", "Media"], MaterialIconKind.Movie),
        (["Download", "Fetch"], MaterialIconKind.Download),
        (["Upload", "Push"], MaterialIconKind.Upload),
        (["Webhook", "Http", "Api", "Cloud", "Network", "Remote"], MaterialIconKind.Webhook),
        (["Excel", "Spreadsheet"], MaterialIconKind.FileExcel),
        (["Csv", "Table"], MaterialIconKind.FileTable),
        (["Data", "Database", "Sql", "Query"], MaterialIconKind.Database),
        (["Llm", "Language", "Translate", "Prompt"], MaterialIconKind.Brain),
        (["Moderat", "Content"], MaterialIconKind.ShieldCheck),
        (["Hash", "Security", "Crypt", "Key"], MaterialIconKind.Fingerprint),
        (["Switch", "Case", "Branch", "Router", "Split", "Fork"], MaterialIconKind.CallSplit),
        (["Merge", "Join", "Barrier"], MaterialIconKind.Merge),
        (["Filter", "Expression", "Logic", "Condition"], MaterialIconKind.Flash),
        (["Throttle", "Delay", "Wait", "Retry"], MaterialIconKind.TimerSandComplete),
        (["Cleanup", "Clean", "Purge", "Tidy"], MaterialIconKind.Broom),
        (["Recycle", "Delete", "Trash", "Remove"], MaterialIconKind.Recycle),
        (["Rename", "Rename", "Name"], MaterialIconKind.RenameBox),
        (["Report", "Metrics", "Stats"], MaterialIconKind.ClipboardText),
        (["Log", "Console", "Journal"], MaterialIconKind.NoteText),
        (["Script", "Code", "Program"], MaterialIconKind.ScriptText),
        (["Cli", "Shell", "Command", "Terminal", "Exec"], MaterialIconKind.ConsoleLine),
        (["Source", "Folder", "Directory", "Input"], MaterialIconKind.Folder),
        (["Sink", "Save", "Output", "Export"], MaterialIconKind.ContentSaveOutline),
        (["Transcode", "Convert", "Format"], MaterialIconKind.SwapHorizontal)
    ];

    private static readonly Dictionary<string, MaterialIconKind> _categoryIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["all"] = MaterialIconKind.Web,
        ["todas"] = MaterialIconKind.Web,
        ["favorites"] = MaterialIconKind.Star,
        ["favoritos"] = MaterialIconKind.Star,
        ["frequent"] = MaterialIconKind.Fire,
        ["frecuentes"] = MaterialIconKind.Fire,
        ["más usados"] = MaterialIconKind.Fire,
        ["files"] = MaterialIconKind.Folder,
        ["filesystem"] = MaterialIconKind.Folder,
        ["archivos"] = MaterialIconKind.Folder,
        ["archivos y sistema"] = MaterialIconKind.Folder,
        ["file system"] = MaterialIconKind.Folder,
        ["imagevision"] = MaterialIconKind.Image,
        ["images"] = MaterialIconKind.Image,
        ["imágenes"] = MaterialIconKind.Image,
        ["imagen y visión ia"] = MaterialIconKind.Image,
        ["fotos"] = MaterialIconKind.Image,
        ["audiovoice"] = MaterialIconKind.Microphone,
        ["audio"] = MaterialIconKind.Microphone,
        ["audio y voz ia"] = MaterialIconKind.Microphone,
        ["voz"] = MaterialIconKind.Microphone,
        ["documents"] = MaterialIconKind.FileDocument,
        ["documentos"] = MaterialIconKind.FileDocument,
        ["documentos y pdf"] = MaterialIconKind.FileDocument,
        ["pdf"] = MaterialIconKind.FilePdfBox,
        ["pdfs"] = MaterialIconKind.FilePdfBox,
        ["data"] = MaterialIconKind.ChartBar,
        ["datos"] = MaterialIconKind.ChartBar,
        ["datos y tablas"] = MaterialIconKind.ChartBar,
        ["data & tables"] = MaterialIconKind.ChartBar,
        ["data & databases"] = MaterialIconKind.ChartBar,
        ["databases"] = MaterialIconKind.Database,
        ["languageai"] = MaterialIconKind.Brain,
        ["lenguaje y llm"] = MaterialIconKind.Brain,
        ["language & llm"] = MaterialIconKind.Brain,
        ["llm"] = MaterialIconKind.Brain,
        ["security"] = MaterialIconKind.ShieldLock,
        ["seguridad y rgpd"] = MaterialIconKind.ShieldLock,
        ["security & privacy"] = MaterialIconKind.ShieldLock,
        ["hashing"] = MaterialIconKind.Fingerprint,
        ["logic"] = MaterialIconKind.CallSplit,
        ["lógica y control"] = MaterialIconKind.CallSplit,
        ["logic & flow"] = MaterialIconKind.CallSplit,
        ["flujo"] = MaterialIconKind.CallSplit,
        ["archives"] = MaterialIconKind.ZipBox,
        ["archivos comprimidos"] = MaterialIconKind.ZipBox,
        ["compresión"] = MaterialIconKind.ZipBox,
        ["compressed archives"] = MaterialIconKind.ZipBox,
        ["network"] = MaterialIconKind.Web,
        ["red"] = MaterialIconKind.Web,
        ["red y nube"] = MaterialIconKind.Web,
        ["network & cloud"] = MaterialIconKind.Web,
        ["network & remote"] = MaterialIconKind.Web,
        ["integrations"] = MaterialIconKind.Flash,
        ["integraciones y diagnóstico"] = MaterialIconKind.Flash,
        ["integrations & diagnostics"] = MaterialIconKind.Flash,
        ["webhooks"] = MaterialIconKind.Webhook,
        ["cli"] = MaterialIconKind.ConsoleLine,
        ["scripting"] = MaterialIconKind.ScriptText,
        ["scripts"] = MaterialIconKind.ScriptText,
        ["subflows"] = MaterialIconKind.VectorCombine,
        ["subflujos"] = MaterialIconKind.VectorCombine,
        ["subflow"] = MaterialIconKind.VectorCombine,
        ["subgrafo"] = MaterialIconKind.VectorCombine,
        ["c#"] = MaterialIconKind.LanguageCsharp,
        ["javascript"] = MaterialIconKind.LanguageJavascript,
        ["metadata"] = MaterialIconKind.Tag,
        ["metadatos"] = MaterialIconKind.Tag,
        ["media & docs"] = MaterialIconKind.Movie,
        ["mediadocs"] = MaterialIconKind.Movie,
        ["general"] = MaterialIconKind.ViewGridOutline,
        ["testing"] = MaterialIconKind.TestTube,
        ["test"] = MaterialIconKind.TestTube,
        ["muestra"] = MaterialIconKind.ShapeOutline,
        ["sample"] = MaterialIconKind.ShapeOutline,
        ["role_source"] = MaterialIconKind.TrayArrowDown,
        ["source"] = MaterialIconKind.TrayArrowDown,
        ["role_filter"] = MaterialIconKind.FilterOutline,
        ["filter"] = MaterialIconKind.FilterOutline,
        ["role_transform"] = MaterialIconKind.AutoFix,
        ["transform"] = MaterialIconKind.AutoFix,
        ["role_analyze"] = MaterialIconKind.EyeOutline,
        ["analyze"] = MaterialIconKind.EyeOutline,
        ["role_sink"] = MaterialIconKind.TrayArrowUp,
        ["sink"] = MaterialIconKind.TrayArrowUp,
        ["role_control"] = MaterialIconKind.Tune,
        ["control"] = MaterialIconKind.Tune
    };

    /// <summary>
    /// Tabla de equivalencia emoji → icono vectorial.
    /// Necesaria para valores que llegan por contrato del SDK (<c>NodeActionDescriptor.Icon</c>) o por
    /// preferencias/flujos guardados antes de la migración: el SDK debe permanecer puro (sin depender de
    /// la librería de iconos), así que la traducción vive aquí.
    /// </summary>
    private static readonly Dictionary<string, MaterialIconKind> _legacyEmojiIcons = new(StringComparer.Ordinal)
    {
        ["📁"] = MaterialIconKind.Folder,
        ["📂"] = MaterialIconKind.FolderOpen,
        ["🗂"] = MaterialIconKind.FolderOpen,
        ["📄"] = MaterialIconKind.FileDocument,
        ["📃"] = MaterialIconKind.FileDocument,
        ["📝"] = MaterialIconKind.NoteText,
        ["📋"] = MaterialIconKind.ClipboardText,
        ["📑"] = MaterialIconKind.FileTable,
        ["📑️"] = MaterialIconKind.FileTable,
        ["📊"] = MaterialIconKind.ChartBar,
        ["📈"] = MaterialIconKind.ChartLine,
        ["📉"] = MaterialIconKind.TrendingUp,
        ["🖼"] = MaterialIconKind.Image,
        ["🎨"] = MaterialIconKind.Palette,
        ["🎬"] = MaterialIconKind.Movie,
        ["🎵"] = MaterialIconKind.Music,
        ["🎙"] = MaterialIconKind.Microphone,
        ["🔊"] = MaterialIconKind.VolumeHigh,
        ["📦"] = MaterialIconKind.ZipBox,
        ["🗜"] = MaterialIconKind.ZipBox,
        ["🗄"] = MaterialIconKind.Database,
        ["💾"] = MaterialIconKind.ContentSaveOutline,
        ["🖥"] = MaterialIconKind.Monitor,
        ["💻"] = MaterialIconKind.Laptop,
        ["🎮"] = MaterialIconKind.ExpansionCard,
        ["🧠"] = MaterialIconKind.Brain,
        ["🤖"] = MaterialIconKind.Robot,
        ["👁"] = MaterialIconKind.Eye,
        ["🔍"] = MaterialIconKind.Magnify,
        ["🔎"] = MaterialIconKind.Magnify,
        ["👤"] = MaterialIconKind.FaceRecognition,
        ["🕵"] = MaterialIconKind.FileSearchOutline,
        ["🎯"] = MaterialIconKind.Target,
        ["✨"] = MaterialIconKind.AutoFix,
        ["🪄"] = MaterialIconKind.AutoFix,
        ["✂"] = MaterialIconKind.ImageFilter,
        ["✏"] = MaterialIconKind.RenameBox,
        ["✒"] = MaterialIconKind.RenameBox,
        ["🔑"] = MaterialIconKind.Fingerprint,
        ["🔒"] = MaterialIconKind.ShieldLock,
        ["🛡"] = MaterialIconKind.ShieldLock,
        ["⚖"] = MaterialIconKind.Certificate,
        ["🔀"] = MaterialIconKind.CallSplit,
        ["🔱"] = MaterialIconKind.SourceFork,
        ["♻"] = MaterialIconKind.Recycle,
        ["🧹"] = MaterialIconKind.Broom,
        ["🗑"] = MaterialIconKind.TrashCan,
        ["🚚"] = MaterialIconKind.Truck,
        ["⏳"] = MaterialIconKind.TimerSandComplete,
        ["⏱"] = MaterialIconKind.Timer,
        ["⏸"] = MaterialIconKind.Pause,
        ["▶"] = MaterialIconKind.Play,
        ["⏭"] = MaterialIconKind.DebugStepOver,
        ["⏹"] = MaterialIconKind.Stop,
        ["🐞"] = MaterialIconKind.Bug,
        ["↶"] = MaterialIconKind.Undo,
        ["🔄"] = MaterialIconKind.Refresh,
        ["⬇"] = MaterialIconKind.Download,
        ["📥"] = MaterialIconKind.Download,
        ["📤"] = MaterialIconKind.Upload,
        ["🌐"] = MaterialIconKind.Web,
        ["🔗"] = MaterialIconKind.Link,
        ["🛰"] = MaterialIconKind.Server,
        ["📡"] = MaterialIconKind.Server,
        ["🧩"] = MaterialIconKind.Puzzle,
        ["⭐"] = MaterialIconKind.Star,
        ["★"] = MaterialIconKind.Star,
        ["☆"] = MaterialIconKind.StarOutline,
        ["🔥"] = MaterialIconKind.Fire,
        ["🟢"] = MaterialIconKind.CheckboxMarkedCircle,
        ["🟠"] = MaterialIconKind.AlertCircle,
        ["🔵"] = MaterialIconKind.Information,
        ["🟣"] = MaterialIconKind.Bug,
        ["🔴"] = MaterialIconKind.AlertCircle,
        ["⚪"] = MaterialIconKind.CircleOutline,
        ["✅"] = MaterialIconKind.CheckCircle,
        ["❌"] = MaterialIconKind.CloseCircle,
        ["⚠"] = MaterialIconKind.Alert,
        ["⚡"] = MaterialIconKind.Flash,
        ["⚙"] = MaterialIconKind.Cog,
        ["☰"] = MaterialIconKind.Menu,
        ["✕"] = MaterialIconKind.Close,
        ["➕"] = MaterialIconKind.Plus,
        ["➔"] = MaterialIconKind.ArrowRight,
        ["→"] = MaterialIconKind.ArrowRight,
        ["▼"] = MaterialIconKind.ChevronDown,
        ["🔲"] = MaterialIconKind.SelectGroup,
        ["📐"] = MaterialIconKind.Ruler,
        ["🔤"] = MaterialIconKind.FormatLetterCase,
        ["🔠"] = MaterialIconKind.FormatLetterCaseUpper,
        ["📷"] = MaterialIconKind.Camera,
        ["🗺"] = MaterialIconKind.Sitemap,
        ["🧪"] = MaterialIconKind.TestTube,
        ["📜"] = MaterialIconKind.ScriptText,
        ["📌"] = MaterialIconKind.ChartBar,
        ["🔧"] = MaterialIconKind.Wrench,
        ["🕒"] = MaterialIconKind.Clock,
        ["⌛"] = MaterialIconKind.TimerSandComplete,
        ["🖱"] = MaterialIconKind.CursorPointer,
        ["⌨"] = MaterialIconKind.Keyboard,
        ["🔵️"] = MaterialIconKind.Information
    };

    /// <summary>
    /// Icono representativo de una categoría de nodos (toolbox, inspector y panel de métricas).
    /// </summary>
    public static MaterialIconKind GetIconForCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return FallbackCategoryIcon;
        }

        string key = category.Trim().ToLowerInvariant();
        return _categoryIcons.TryGetValue(key, out var kind) ? kind : FallbackCategoryIcon;
    }

    /// <summary>
    /// Icono asociado a un tipo de nodo, con tabla exacta primero y heurística por palabra clave después.
    /// </summary>
    public static MaterialIconKind GetIconForNodeType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return FallbackNodeIcon;
        }

        string cleanName = typeName.Split('.').Last().Trim();

        if (_exactNodeTypeIcons.TryGetValue(cleanName, out var exact))
        {
            return exact;
        }

        foreach (var (keywords, kind) in _keywordRules)
        {
            foreach (string keyword in keywords)
            {
                if (cleanName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return kind;
                }
            }
        }

        return FallbackNodeIcon;
    }

    /// <summary>
    /// Traduce a icono vectorial un valor que puede venir del SDK o de datos persistidos:
    /// acepta tanto el nombre de un <see cref="MaterialIconKind"/> como un emoji heredado.
    /// </summary>
    public static MaterialIconKind FromLegacyIcon(string? value, MaterialIconKind fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string raw = value.Trim();

        // 1. Ya es un nombre de icono vectorial (valores nuevos, persistidos tras la migración).
        if (Enum.TryParse<MaterialIconKind>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        // 2. Emoji heredado (contrato del SDK o flujo guardado antes de la migración).
        if (_legacyEmojiIcons.TryGetValue(raw, out var legacy))
        {
            return legacy;
        }

        // El texto puede llevar selectores de variación (U+FE0F) o ser un emoji compuesto.
        string stripped = raw.Replace("\uFE0F", string.Empty);
        if (stripped.Length > 0 && _legacyEmojiIcons.TryGetValue(stripped, out var strippedLegacy))
        {
            return strippedLegacy;
        }

        foreach (var (emoji, kind) in _legacyEmojiIcons)
        {
            if (raw.StartsWith(emoji, StringComparison.Ordinal) || stripped.StartsWith(emoji, StringComparison.Ordinal))
            {
                return kind;
            }
        }

        return fallback;
    }

    /// <summary>
    /// Icono de una acción personalizada declarada por un plugin.
    /// El contrato <c>NodeActionDescriptor</c> del SDK sigue transportando el valor como texto
    /// (el SDK debe permanecer puro), por lo que aquí se traduce a icono vectorial.
    /// </summary>
    public static MaterialIconKind GetIconForAction(string? descriptorIcon)
        => FromLegacyIcon(descriptorIcon, FallbackActionIcon);
}
