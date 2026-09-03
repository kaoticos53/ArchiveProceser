using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Catálogo de modelos IA con URLs de descarga, tamaños esperados y descriptores.
/// </summary>
public record AiModelInfo(
    string Id,
    string FileName,
    string DownloadUrl,
    long MinSizeBytes,
    string Description,
    string FriendlyName = "",
    string Category = "",
    IReadOnlyList<string>? DefaultUrls = null,
    AiTaskType TaskType = AiTaskType.ObjectDetection,
    long MinRamBytes = 2_000_000_000,
    bool GpuRecommended = false,
    string HardwareTier = "Lightweight"
)
{
    /// <summary>
    /// Lista de URLs de descarga configuradas (devuelve las personalizadas si existen; de lo contrario, las predeterminadas).
    /// </summary>
    public IReadOnlyList<string> DownloadUrls => AiModelManager.GetConfiguredUrls(Id);
}

/// <summary>
/// Gestor centralizado para la localización, descarga automática y gestión de modelos de IA.
/// Soporta descarga con barra de progreso integrada en el contexto de ejecución del nodo o en interfaces de usuario.
/// </summary>
public static class AiModelManager
{
    private static readonly Lock _fileLock = new();
    private static readonly Lock _configLock = new();
    private static readonly ConcurrentDictionary<string, bool> _downloadInProgress = new();
    private static readonly ConcurrentDictionary<string, List<string>> _customUrls = new(StringComparer.OrdinalIgnoreCase);

    private static string ConfigFilePath => Path.Combine(AppPaths.ConfigDirectory, "ai_models_config.json");

    static AiModelManager()
    {
        LoadConfig();
    }

    /// <summary>
    /// Carga la configuración de URLs personalizadas desde disco.
    /// </summary>
    public static void LoadConfig()
    {
        lock (_configLock)
        {
            try
            {
                string path = ConfigFilePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    if (dict != null)
                    {
                        _customUrls.Clear();
                        foreach (var kvp in dict)
                        {
                            var list = kvp.Value?
                                .Where(u => !string.IsNullOrWhiteSpace(u))
                                .Select(u => u.Trim())
                                .ToList() ?? [];
                            if (list.Count > 0)
                            {
                                _customUrls[kvp.Key] = list;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Resiliencia ante lectura de fichero corrupto
            }
        }
    }

    /// <summary>
    /// Guarda la configuración de URLs personalizadas en disco.
    /// </summary>
    public static void SaveConfig()
    {
        lock (_configLock)
        {
            try
            {
                string path = ConfigFilePath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var dict = new Dictionary<string, List<string>>(_customUrls, StringComparer.OrdinalIgnoreCase);
                string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Ignorar fallos de escritura concurrente
            }
        }
    }

    /// <summary>
    /// Devuelve la lista oficial de URLs predeterminadas de fábrica para un modelo.
    /// </summary>
    public static IReadOnlyList<string> GetDefaultUrls(string modelId)
    {
        if (Catalog.TryGetValue(modelId, out var info))
        {
            if (info.DefaultUrls != null && info.DefaultUrls.Count > 0)
            {
                return info.DefaultUrls;
            }
            return string.IsNullOrEmpty(info.DownloadUrl) ? [] : [info.DownloadUrl];
        }
        return [];
    }

    /// <summary>
    /// Devuelve la lista de URLs configuradas para un modelo (personalizadas si existen; en caso contrario, predeterminadas).
    /// </summary>
    public static IReadOnlyList<string> GetConfiguredUrls(string modelId)
    {
        if (_customUrls.TryGetValue(modelId, out var list) && list.Count > 0)
        {
            return list;
        }
        return GetDefaultUrls(modelId);
    }

    /// <summary>
    /// Indica si un modelo tiene URLs personalizadas definidas por el usuario.
    /// </summary>
    public static bool HasCustomUrls(string modelId)
    {
        return _customUrls.TryGetValue(modelId, out var list) && list.Count > 0;
    }

    /// <summary>
    /// Configura URLs personalizadas para un modelo determinado y las persiste en disco.
    /// </summary>
    public static void SetCustomUrls(string modelId, IEnumerable<string> urls)
    {
        var cleaned = urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleaned.Count == 0)
        {
            ResetCustomUrls(modelId);
            return;
        }

        var defaultUrls = GetDefaultUrls(modelId);
        if (cleaned.SequenceEqual(defaultUrls, StringComparer.OrdinalIgnoreCase))
        {
            ResetCustomUrls(modelId);
            return;
        }

        _customUrls[modelId] = cleaned;
        SaveConfig();
    }

    /// <summary>
    /// Restablece las URLs de un modelo a sus valores oficiales por defecto.
    /// </summary>
    public static void ResetCustomUrls(string modelId)
    {
        _customUrls.TryRemove(modelId, out _);
        SaveConfig();
    }

    /// <summary>
    /// Restablece todas las URLs de todos los modelos a sus valores oficiales por defecto.
    /// </summary>
    public static void ResetAllCustomUrls()
    {
        _customUrls.Clear();
        SaveConfig();
    }

    /// <summary>
    /// Último error detallado producido durante la descarga o verificación de un modelo de IA.
    /// </summary>
    public static string? LastError { get; private set; }

    private static readonly HttpClient _httpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 FileFlowStudio/1.0");
        client.DefaultRequestHeaders.Add("Accept", "*/*");
        return client;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Catálogo de modelos con URLs públicas y tamaños verificados
    // ──────────────────────────────────────────────────────────────────────────
    public static readonly IReadOnlyDictionary<string, AiModelInfo> Catalog = new Dictionary<string, AiModelInfo>
    {
        ["mobilenetv2"] = new(
            Id: "mobilenetv2",
            FileName: "mobilenetv2-7.onnx",
            DownloadUrl: "https://github.com/onnx/models/raw/main/validated/vision/classification/mobilenet/model/mobilenetv2-7.onnx",
            MinSizeBytes: 12_000_000,
            Description: "MobileNetV2 ImageNet - Clasificador visual de imágenes (14 MB)",
            FriendlyName: "MobileNetV2 ImageNet",
            Category: "Visión",
            TaskType: AiTaskType.ImageClassification,
            MinRamBytes: 1_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["ultraface"] = new(
            Id: "ultraface",
            FileName: "version-RFB-320.onnx",
            DownloadUrl: "https://github.com/onnx/models/raw/main/validated/vision/body_analysis/ultraface/models/version-RFB-320.onnx",
            MinSizeBytes: 1_000_000,
            Description: "UltraFace RFB 320 - Detector de rostros (1.2 MB)",
            FriendlyName: "UltraFace RFB 320",
            Category: "Visión",
            TaskType: AiTaskType.FaceDetection,
            MinRamBytes: 500_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["tiny-yolov3"] = new(
            Id: "tiny-yolov3",
            FileName: "tiny-yolov3-11.onnx",
            DownloadUrl: "https://github.com/onnx/models/raw/main/validated/vision/object_detection_segmentation/tiny-yolov3/model/tiny-yolov3-11.onnx",
            MinSizeBytes: 30_000_000,
            Description: "Tiny YOLOv3 COCO - Detección de 80 objetos comunes (34 MB)",
            FriendlyName: "Tiny YOLOv3 COCO",
            Category: "Visión",
            TaskType: AiTaskType.ObjectDetection,
            MinRamBytes: 1_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["whisper-tiny"] = new(
            Id: "whisper-tiny",
            FileName: "ggml-tiny.bin",
            DownloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
            MinSizeBytes: 38_000_000,
            Description: "Whisper Tiny - Transcripción ultra-rápida de voz (39 MB)",
            FriendlyName: "Whisper Tiny",
            Category: "Audio",
            TaskType: AiTaskType.SpeechToText,
            MinRamBytes: 1_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["whisper-base"] = new(
            Id: "whisper-base",
            FileName: "ggml-base.bin",
            DownloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
            MinSizeBytes: 72_000_000,
            Description: "Whisper Base - Transcripción equilibrada de voz (148 MB)",
            FriendlyName: "Whisper Base",
            Category: "Audio",
            TaskType: AiTaskType.SpeechToText,
            MinRamBytes: 2_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Balanced"
        ),
        ["whisper-small"] = new(
            Id: "whisper-small",
            FileName: "ggml-small.bin",
            DownloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
            MinSizeBytes: 240_000_000,
            Description: "Whisper Small - Transcripción de alta fidelidad (488 MB)",
            FriendlyName: "Whisper Small",
            Category: "Audio",
            TaskType: AiTaskType.SpeechToText,
            MinRamBytes: 4_000_000_000,
            GpuRecommended: true,
            HardwareTier: "Performance"
        ),
        ["tessdata-spa"] = new(
            Id: "tessdata-spa",
            FileName: "tessdata/spa.traineddata",
            DownloadUrl: "https://github.com/tesseract-ocr/tessdata_fast/raw/main/spa.traineddata",
            MinSizeBytes: 1_500_000,
            Description: "Tesseract OCR - Datos de entrenamiento Español (2.3 MB)",
            FriendlyName: "Tesseract OCR (Español)",
            Category: "OCR",
            TaskType: AiTaskType.Ocr,
            MinRamBytes: 500_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["tessdata-eng"] = new(
            Id: "tessdata-eng",
            FileName: "tessdata/eng.traineddata",
            DownloadUrl: "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata",
            MinSizeBytes: 2_500_000,
            Description: "Tesseract OCR - Datos de entrenamiento Inglés (4.1 MB)",
            FriendlyName: "Tesseract OCR (Inglés)",
            Category: "OCR",
            TaskType: AiTaskType.Ocr,
            MinRamBytes: 500_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["grounding-dino"] = new(
            Id: "grounding-dino",
            FileName: "yolov8s-worldv2.onnx",
            DownloadUrl: "https://huggingface.co/Instemic/yolo-world-onnx/resolve/main/yolov8s-worldv2.onnx",
            MinSizeBytes: 35_000_000,
            Description: "Grounding DINO / YOLO-World - Detección de objetos por prompt en texto libre (51 MB)",
            FriendlyName: "Grounding DINO / Open-Vocab",
            Category: "Visión",
            TaskType: AiTaskType.ObjectDetection,
            MinRamBytes: 2_000_000_000,
            GpuRecommended: true,
            HardwareTier: "Balanced"
        ),
        ["marian-es-en"] = new(
            Id: "marian-es-en",
            FileName: "opus-mt-es-en.onnx",
            DownloadUrl: "https://huggingface.co/onnx-community/opus-mt-es-en/resolve/main/onnx/decoder_model_merged_quantized.onnx",
            MinSizeBytes: 40_000_000,
            Description: "Helsinki-NLP MarianMT - Traductor Español a Inglés para prompts (193 MB)",
            FriendlyName: "MarianMT ES-EN (Helsinki-NLP)",
            Category: "NLP / Texto",
            TaskType: AiTaskType.TextTranslation,
            MinRamBytes: 2_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Balanced"
        ),
        ["marian-en-es"] = new(
            Id: "marian-en-es",
            FileName: "opus-mt-en-es.onnx",
            DownloadUrl: "https://huggingface.co/onnx-community/opus-mt-en-es/resolve/main/onnx/decoder_model_merged_quantized.onnx",
            MinSizeBytes: 40_000_000,
            Description: "Helsinki-NLP MarianMT - Traductor Inglés a Español de alta velocidad (193 MB)",
            FriendlyName: "MarianMT EN-ES (Helsinki-NLP)",
            Category: "NLP / Texto",
            TaskType: AiTaskType.TextTranslation,
            MinRamBytes: 2_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Balanced"
        ),
        ["nllb-200-600m"] = new(
            Id: "nllb-200-600m",
            FileName: "nllb-200-distilled-600M.onnx",
            DownloadUrl: "https://huggingface.co/Xenova/nllb-200-distilled-600M/resolve/main/onnx/decoder_model_merged_quantized.onnx",
            MinSizeBytes: 200_000_000,
            Description: "Meta NLLB-200 (600M) - Traductor neuronal universal en 200 idiomas (475 MB)",
            FriendlyName: "NLLB-200 (Universal 200 Idiomas)",
            Category: "NLP / Texto",
            TaskType: AiTaskType.TextTranslation,
            MinRamBytes: 4_000_000_000,
            GpuRecommended: true,
            HardwareTier: "Performance"
        ),
        ["qwen2.5-1.5b-instruct"] = new(
            Id: "qwen2.5-1.5b-instruct",
            FileName: "qwen2.5-1.5b-instruct-q4_k_m.gguf",
            DownloadUrl: "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf",
            MinSizeBytes: 500_000_000,
            Description: "Qwen 2.5 1.5B Instruct - Modelo LLM local para resúmenes y extracción (1.1 GB)",
            FriendlyName: "Qwen 2.5 1.5B Instruct",
            Category: "LLM / Texto",
            TaskType: AiTaskType.TextGenerationLlm,
            MinRamBytes: 4_000_000_000,
            GpuRecommended: true,
            HardwareTier: "Balanced"
        ),
        ["rmbg-1.4"] = new(
            Id: "rmbg-1.4",
            FileName: "rmbg-1.4.onnx",
            DownloadUrl: "https://huggingface.co/briaai/RMBG-1.4/resolve/main/onnx/model.onnx",
            MinSizeBytes: 150_000_000,
            Description: "Bria AI RMBG-1.4 - Segmentación y eliminación de fondo de alta fidelidad (176 MB)",
            FriendlyName: "RMBG 1.4 Background Remover",
            Category: "Visión",
            TaskType: AiTaskType.BackgroundRemoval,
            MinRamBytes: 2_000_000_000,
            GpuRecommended: true,
            HardwareTier: "Balanced"
        ),
        ["modnet"] = new(
            Id: "modnet",
            FileName: "modnet_photographic_portrait_matting.onnx",
            DownloadUrl: "https://github.com/ZHKKKe/MODNet/raw/master/pretrained/modnet_photographic_portrait_matting.onnx",
            MinSizeBytes: 20_000_000,
            Description: "MODNet Portrait Matting - Recorte ultraligero y rápido en CPU (25 MB)",
            FriendlyName: "MODNet Fast Matting",
            Category: "Visión",
            TaskType: AiTaskType.BackgroundRemoval,
            MinRamBytes: 1_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["realesrgan-compact"] = new(
            Id: "realesrgan-compact",
            FileName: "realesr-general-x4v3.onnx",
            DownloadUrl: "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesr-general-x4v3.onnx",
            MinSizeBytes: 15_000_000,
            Description: "Real-ESRGAN Compact x4 - Super-resolución y restauración de fotos y documentos (16 MB)",
            FriendlyName: "Real-ESRGAN Compact x4",
            Category: "Visión",
            TaskType: AiTaskType.SuperResolution,
            MinRamBytes: 1_500_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["opennsfw2"] = new(
            Id: "opennsfw2",
            FileName: "open_nsfw.onnx",
            DownloadUrl: "https://huggingface.co/Falconsai/nsfw_image_detection/resolve/main/onnx/model.onnx",
            MinSizeBytes: 10_000_000,
            Description: "OpenNSFW2 - Clasificador neural de contenido sensible y moderación (16 MB)",
            FriendlyName: "OpenNSFW2 Moderation",
            Category: "Visión",
            TaskType: AiTaskType.ContentModeration,
            MinRamBytes: 1_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["silero-vad"] = new(
            Id: "silero-vad",
            FileName: "silero_vad.onnx",
            DownloadUrl: "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx",
            MinSizeBytes: 1_500_000,
            Description: "Silero VAD v5 - Detección de actividad vocal y eliminación de silencios (2 MB)",
            FriendlyName: "Silero VAD v5",
            Category: "Audio & Voz",
            TaskType: AiTaskType.VoiceActivityDetection,
            MinRamBytes: 500_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["piper-es-davefx"] = new(
            Id: "piper-es-davefx",
            FileName: "es_ES-davefx-medium.onnx",
            DownloadUrl: "https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_ES/davefx/medium/es_ES-davefx-medium.onnx",
            MinSizeBytes: 50_000_000,
            Description: "Piper TTS Español - Síntesis neural de voz natural en español davefx (63 MB)",
            FriendlyName: "Piper TTS Español (Davefx)",
            Category: "Audio & Voz",
            TaskType: AiTaskType.TextToSpeech,
            MinRamBytes: 1_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["piper-en-lessac"] = new(
            Id: "piper-en-lessac",
            FileName: "en_US-lessac-medium.onnx",
            DownloadUrl: "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx",
            MinSizeBytes: 50_000_000,
            Description: "Piper TTS Inglés - Síntesis neural de voz natural en inglés lessac (63 MB)",
            FriendlyName: "Piper TTS Inglés (Lessac)",
            Category: "Audio & Voz",
            TaskType: AiTaskType.TextToSpeech,
            MinRamBytes: 1_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["pii-ner-multilingual"] = new(
            Id: "pii-ner-multilingual",
            FileName: "pii-ner-multilingual.onnx",
            DownloadUrl: "https://huggingface.co/Babelscape/wikineural-multilingual-ner/resolve/main/onnx/model.onnx",
            MinSizeBytes: 30_000_000,
            Description: "WikiNeural Multilingual NER - Detección de entidades nombradas y nombres propios PII (35 MB)",
            FriendlyName: "WikiNeural Multilingual NER",
            Category: "Seguridad & RGPD",
            TaskType: AiTaskType.PiiAnonymization,
            MinRamBytes: 1_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
        ["clip-vit-b32"] = new(
            Id: "clip-vit-b32",
            FileName: "clip-vit-base-patch32.onnx",
            DownloadUrl: "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/model.onnx",
            MinSizeBytes: 60_000_000,
            Description: "OpenAI CLIP ViT-B/32 - Embeddings multimodales texto e imagen para búsqueda semántica (65 MB)",
            FriendlyName: "CLIP ViT-B/32 Multimodal",
            Category: "Búsqueda Semántica",
            TaskType: AiTaskType.SemanticEmbeddings,
            MinRamBytes: 1_500_000_000,
            GpuRecommended: true,
            HardwareTier: "Balanced"
        ),
        ["bge-small-multilingual"] = new(
            Id: "bge-small-multilingual",
            FileName: "bge-small-en-v1.5.onnx",
            DownloadUrl: "https://huggingface.co/BAAI/bge-small-en-v1.5/resolve/main/onnx/model.onnx",
            MinSizeBytes: 40_000_000,
            Description: "BAAI BGE Small - Embeddings semánticos de texto de 384 dimensiones para documentos (45 MB)",
            FriendlyName: "BGE Small Text Embeddings",
            Category: "Búsqueda Semántica",
            TaskType: AiTaskType.SemanticEmbeddings,
            MinRamBytes: 1_000_000_000,
            GpuRecommended: false,
            HardwareTier: "Lightweight"
        ),
    };

    /// <summary>
    /// Devuelve todos los modelos del catálogo registrados para una tarea de IA determinada.
    /// </summary>
    public static IReadOnlyList<AiModelInfo> GetModelsForTask(AiTaskType taskType)
    {
        return Catalog.Values
            .Where(m => m.TaskType == taskType)
            .ToList();
    }

    /// <summary>
    /// Resuelve la ruta del modelo de IA a ejecutar según la elección del usuario (Auto, Catálogo Oficial o Archivo Local Personalizado).
    /// </summary>
    public static async Task<string?> ResolveModelPathAsync(
        string? modelSelection,
        string? customModelPath,
        AiTaskType taskType,
        IFlowExecutionContext context,
        FileItemContext? item = null,
        CancellationToken cancellationToken = default)
    {
        // Caso 1: Archivo local personalizado ("Custom")
        if (string.Equals(modelSelection, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(customModelPath))
            {
                context.Log($"[AiModelManager] ⚠️ Se ha seleccionado modelo personalizado ('Custom') pero la ruta de archivo está vacía.", LogLevel.Error, item);
                return null;
            }

            string fullPath = Path.GetFullPath(customModelPath);
            if (!File.Exists(fullPath))
            {
                context.Log($"[AiModelManager] ❌ Archivo de modelo personalizado no encontrado: '{fullPath}'", LogLevel.Error, item);
                return null;
            }

            context.Log($"[AiModelManager] 🔧 Usando modelo personalizado: '{Path.GetFileName(fullPath)}' ({new FileInfo(fullPath).Length / (1024.0 * 1024.0):F1} MB)", LogLevel.Information, item);
            return fullPath;
        }

        // Caso 2: Selección automática inteligente ("Auto" o no especificado)
        string targetModelId;
        if (string.IsNullOrWhiteSpace(modelSelection) || string.Equals(modelSelection, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            var optimalModel = HardwareCapabilityDetector.GetOptimalModelForTask(taskType);
            targetModelId = optimalModel.Id;
            context.Log($"[AiModelManager] ⚡ Modo Automático: seleccionado '{optimalModel.FriendlyName}' basado en el hardware del equipo ({HardwareCapabilityDetector.Specs.HardwareTier}, RAM: {HardwareCapabilityDetector.Specs.TotalRamGb:F1} GB, GPU DirectML: {HardwareCapabilityDetector.Specs.HasDirectMlGpu}).", LogLevel.Information, item);
        }
        else
        {
            targetModelId = modelSelection.Trim();
        }

        // Caso 3: Modelo del catálogo oficial (se asegura su descarga y existencia)
        return await EnsureModelAsync(targetModelId, context, item, cancellationToken).ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Directorio de modelos
    // ──────────────────────────────────────────────────────────────────────────
    public static string ModelsDirectory
    {
        get
        {
            string appBaseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Modo portable: data/models relativo al ejecutable
            if (File.Exists(Path.Combine(appBaseDir, "portable.dat")) ||
                Directory.Exists(Path.Combine(appBaseDir, "data")))
            {
                string portableDir = Path.Combine(appBaseDir, "data", "models");
                Directory.CreateDirectory(portableDir);
                return portableDir;
            }

            // Modo estándar: %AppData%/FileFlow/Models
            string standardDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FileFlow", "Models");
            Directory.CreateDirectory(standardDir);
            return standardDir;
        }
    }

    public static string GetModelPath(string modelFileName)
        => Path.Combine(ModelsDirectory, modelFileName);

    public static bool IsModelAvailable(string modelId)
    {
        if (!Catalog.TryGetValue(modelId, out var info)) return false;
        string path = GetModelPath(info.FileName);
        if (!File.Exists(path)) return false;
        return new FileInfo(path).Length >= info.MinSizeBytes;
    }

    public static long? GetModelDiskSizeBytes(string modelId)
    {
        if (!Catalog.TryGetValue(modelId, out var info)) return null;
        string path = GetModelPath(info.FileName);
        if (!File.Exists(path)) return null;
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return null;
        }
    }

    public static bool DeleteModel(string modelId)
    {
        if (!Catalog.TryGetValue(modelId, out var info)) return false;
        string path = GetModelPath(info.FileName);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Descarga con reporte de progreso desacoplado (para UI o Flujo)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Descarga un modelo con soporte para reporte de progreso numérico (0.0 a 100.0) y mensajes de estado.
    /// </summary>
    public static async Task<string?> DownloadModelWithProgressAsync(
        string modelId,
        IProgress<double>? progress = null,
        Action<string>? statusLogger = null,
        CancellationToken cancellationToken = default)
    {
        if (!Catalog.TryGetValue(modelId, out var info))
        {
            statusLogger?.Invoke($"Modelo desconocido: '{modelId}'");
            return null;
        }

        string targetPath = GetModelPath(info.FileName);

        if (IsModelAvailable(modelId))
        {
            LastError = null;
            progress?.Report(100.0);
            return targetPath;
        }

        // Evitar descargas concurrentes del mismo modelo
        if (!_downloadInProgress.TryAdd(modelId, true))
        {
            statusLogger?.Invoke($"Descarga de '{info.Description}' ya en curso...");
            for (int i = 0; i < 600; i++)
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                if (IsModelAvailable(modelId))
                {
                    LastError = null;
                    progress?.Report(100.0);
                    return targetPath;
                }
                if (!_downloadInProgress.ContainsKey(modelId)) break;
            }
            return IsModelAvailable(modelId) ? targetPath : null;
        }

        try
        {
            LastError = null;
            var urls = GetConfiguredUrls(modelId);
            if (urls.Count == 0)
            {
                LastError = $"No hay URLs configuradas para el modelo '{modelId}'.";
                statusLogger?.Invoke($"❌ {LastError}");
                return null;
            }

            string? parentDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(parentDir))
                Directory.CreateDirectory(parentDir);

            string tempPath = targetPath + ".downloading";
            var errors = new List<string>();

            for (int mirrorIndex = 0; mirrorIndex < urls.Count; mirrorIndex++)
            {
                string currentUrl = urls[mirrorIndex];
                string mirrorLabel = urls.Count > 1 ? $" (espejo {mirrorIndex + 1}/{urls.Count})" : string.Empty;

                statusLogger?.Invoke($"⬇️ Conectando{mirrorLabel} para descargar: {info.FriendlyName}...");
                progress?.Report(0.0);

                try
                {
                    using (var response = await _httpClient.GetAsync(
                        currentUrl,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string statusDetails = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
                            string failMsg = $"Fallo en enlace {currentUrl}: {statusDetails}";
                            errors.Add(failMsg);
                            if (mirrorIndex < urls.Count - 1)
                            {
                                statusLogger?.Invoke($"⚠️ {failMsg}. Conmutando al siguiente espejo...");
                            }
                            continue;
                        }

                        long? totalBytes = response.Content.Headers.ContentLength;
                        long totalRead = 0;
                        int lastReportedPercent = -1;

                        await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                        {
                            var buffer = new byte[81920];
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                                totalRead += bytesRead;

                                if (totalBytes.HasValue && totalBytes.Value > 0)
                                {
                                    double percent = (double)totalRead * 100.0 / totalBytes.Value;
                                    progress?.Report(percent);

                                    int intPercent = (int)percent;
                                    if (intPercent != lastReportedPercent && intPercent % 10 == 0)
                                    {
                                        lastReportedPercent = intPercent;
                                        statusLogger?.Invoke($"Descargando {Path.GetFileName(info.FileName)}{mirrorLabel}: {intPercent}% ({totalRead / 1_048_576.0:F1} / {totalBytes.Value / 1_048_576.0:F1} MB)");
                                    }
                                }
                            }

                            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }

                    // Una vez cerrado el fileStream en Windows, verificar tamaño y mover
                    var fi = new FileInfo(tempPath);
                    if (!fi.Exists || fi.Length < info.MinSizeBytes)
                    {
                        CleanupTemp(tempPath);
                        string failMsg = $"Descarga incompleta de {info.FileName} desde {currentUrl}: recibidos {(fi.Exists ? fi.Length : 0):N0} bytes, esperados >= {info.MinSizeBytes:N0} bytes.";
                        errors.Add(failMsg);
                        if (mirrorIndex < urls.Count - 1)
                        {
                            statusLogger?.Invoke($"⚠️ {failMsg}. Conmutando al siguiente espejo...");
                        }
                        continue;
                    }

                    lock (_fileLock)
                    {
                        if (File.Exists(targetPath))
                        {
                            try { File.Delete(targetPath); } catch { }
                        }

                        string? destDir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrWhiteSpace(destDir))
                            Directory.CreateDirectory(destDir);

                        File.Move(tempPath, targetPath, overwrite: true);
                    }

                    LastError = null;
                    progress?.Report(100.0);
                    statusLogger?.Invoke($"✅ Modelo descargado correctamente: {info.FileName} ({fi.Length / 1_048_576.0:F1} MB)");
                    return targetPath;
                }
                catch (OperationCanceledException)
                {
                    CleanupTemp(tempPath);
                    throw;
                }
                catch (Exception ex)
                {
                    CleanupTemp(tempPath);
                    string failMsg = $"Excepción al conectar con {currentUrl}: {ex.Message}";
                    errors.Add(failMsg);
                    if (mirrorIndex < urls.Count - 1)
                    {
                        statusLogger?.Invoke($"⚠️ {failMsg}. Conmutando al siguiente espejo...");
                    }
                }
            }

            // Si se agotaron todas las URLs sin éxito
            LastError = $"Fallaron todos los espejos configurados ({urls.Count}) para '{info.FriendlyName}':\n" +
                        string.Join("\n", errors.Select(e => " • " + e));
            statusLogger?.Invoke($"❌ {LastError}");
            return null;
        }
        catch (OperationCanceledException)
        {
            CleanupTemp(GetModelPath(info.FileName) + ".downloading");
            LastError = "Descarga cancelada por el usuario.";
            throw;
        }
        catch (Exception ex)
        {
            CleanupTemp(GetModelPath(info.FileName) + ".downloading");
            LastError = $"Error descargando '{modelId}': {ex.Message}";
            statusLogger?.Invoke($"❌ {LastError}");
            return null;
        }
        finally
        {
            _downloadInProgress.TryRemove(modelId, out _);
        }
    }

    /// <summary>
    /// Asegura que el modelo esté descargado y devuelve su ruta en disco.
    /// Registra el progreso en el contexto de ejecución del nodo.
    /// </summary>
    public static async Task<string?> EnsureModelAsync(
        string modelId,
        IFlowExecutionContext? context,
        FileItemContext? item,
        CancellationToken cancellationToken)
    {
        return await DownloadModelWithProgressAsync(
            modelId,
            progress: null,
            statusLogger: msg => context?.Log($"[AiModelManager] {msg}", LogLevel.Information, item),
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    private static void CleanupTemp(string tempPath)
    {
        try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
    }
}
