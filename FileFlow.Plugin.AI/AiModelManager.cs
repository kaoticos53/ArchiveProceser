using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using FileFlow.Sdk;

namespace FileFlow.Plugin.AI;

/// <summary>
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
    string Category = ""
);

/// <summary>
/// Gestor centralizado para la localización, descarga automática y gestión de modelos de IA.
/// Soporta descarga con barra de progreso integrada en el contexto de ejecución del nodo o en interfaces de usuario.
/// </summary>
public static class AiModelManager
{
    private static readonly Lock _fileLock = new();
    private static readonly ConcurrentDictionary<string, bool> _downloadInProgress = new();

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(15)
    };

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
            Category: "Visión"
        ),
        ["ultraface"] = new(
            Id: "ultraface",
            FileName: "version-RFB-320.onnx",
            DownloadUrl: "https://github.com/onnx/models/raw/main/validated/vision/body_analysis/ultraface/models/version-RFB-320.onnx",
            MinSizeBytes: 1_000_000,
            Description: "UltraFace RFB 320 - Detector de rostros (1.2 MB)",
            FriendlyName: "UltraFace RFB 320",
            Category: "Visión"
        ),
        ["tiny-yolov3"] = new(
            Id: "tiny-yolov3",
            FileName: "tiny-yolov3-11.onnx",
            DownloadUrl: "https://github.com/onnx/models/raw/main/validated/vision/object_detection_segmentation/tiny-yolov3/model/tiny-yolov3-11.onnx",
            MinSizeBytes: 30_000_000,
            Description: "Tiny YOLOv3 COCO - Detección de 80 objetos comunes (34 MB)",
            FriendlyName: "Tiny YOLOv3 COCO",
            Category: "Visión"
        ),
        ["whisper-tiny"] = new(
            Id: "whisper-tiny",
            FileName: "ggml-tiny.bin",
            DownloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
            MinSizeBytes: 38_000_000,
            Description: "Whisper Tiny - Transcripción ultra-rápida de voz (39 MB)",
            FriendlyName: "Whisper Tiny",
            Category: "Audio"
        ),
        ["whisper-base"] = new(
            Id: "whisper-base",
            FileName: "ggml-base.bin",
            DownloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
            MinSizeBytes: 72_000_000,
            Description: "Whisper Base - Transcripción equilibrada de voz (148 MB)",
            FriendlyName: "Whisper Base",
            Category: "Audio"
        ),
        ["whisper-small"] = new(
            Id: "whisper-small",
            FileName: "ggml-small.bin",
            DownloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
            MinSizeBytes: 240_000_000,
            Description: "Whisper Small - Transcripción de alta fidelidad (488 MB)",
            FriendlyName: "Whisper Small",
            Category: "Audio"
        ),
        ["tessdata-spa"] = new(
            Id: "tessdata-spa",
            FileName: "tessdata/spa.traineddata",
            DownloadUrl: "https://github.com/tesseract-ocr/tessdata_fast/raw/main/spa.traineddata",
            MinSizeBytes: 1_500_000,
            Description: "Tesseract OCR - Datos de entrenamiento Español (2.3 MB)",
            FriendlyName: "Tesseract OCR (Español)",
            Category: "OCR"
        ),
        ["tessdata-eng"] = new(
            Id: "tessdata-eng",
            FileName: "tessdata/eng.traineddata",
            DownloadUrl: "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata",
            MinSizeBytes: 2_500_000,
            Description: "Tesseract OCR - Datos de entrenamiento Inglés (4.1 MB)",
            FriendlyName: "Tesseract OCR (Inglés)",
            Category: "OCR"
        ),
        ["grounding-dino"] = new(
            Id: "grounding-dino",
            FileName: "yolov8s-worldv2.onnx",
            DownloadUrl: "https://github.com/ultralytics/assets/releases/download/v8.2.0/yolov8s-worldv2.onnx",
            MinSizeBytes: 35_000_000,
            Description: "Grounding DINO / YOLO-World - Detección de objetos por prompt en texto libre (45 MB)",
            FriendlyName: "Grounding DINO / Open-Vocab",
            Category: "Visión"
        ),
        ["marian-es-en"] = new(
            Id: "marian-es-en",
            FileName: "opus-mt-es-en.onnx",
            DownloadUrl: "https://huggingface.co/onnx-community/opus-mt-es-en/resolve/main/onnx/model.onnx",
            MinSizeBytes: 40_000_000,
            Description: "Helsinki-NLP MarianMT - Traductor Español a Inglés para prompts (60 MB)",
            FriendlyName: "MarianMT ES-EN (Helsinki-NLP)",
            Category: "NLP / Texto"
        ),
    };

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
                    progress?.Report(100.0);
                    return targetPath;
                }
                if (!_downloadInProgress.ContainsKey(modelId)) break;
            }
            return IsModelAvailable(modelId) ? targetPath : null;
        }

        try
        {
            statusLogger?.Invoke($"⬇️ Conectando para descargar: {info.Description}...");
            progress?.Report(0.0);

            string? parentDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(parentDir))
                Directory.CreateDirectory(parentDir);

            string tempPath = targetPath + ".downloading";

            // Descargar y escribir en bloque cerrado para que fileStream se libere antes de File.Move
            using (var response = await _httpClient.GetAsync(
                info.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

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
                                statusLogger?.Invoke($"Descargando {Path.GetFileName(info.FileName)}: {intPercent}% ({totalRead / 1_048_576.0:F1} / {totalBytes.Value / 1_048_576.0:F1} MB)");
                            }
                        }
                    }

                    await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                } // <-- fileStream y contentStream se cierran y liberan determinísticamente aquí
            } // <-- response se libera aquí

            // Una vez cerrado el fileStream en Windows, verificar tamaño y mover
            var fi = new FileInfo(tempPath);
            if (!fi.Exists || fi.Length < info.MinSizeBytes)
            {
                CleanupTemp(tempPath);
                statusLogger?.Invoke($"❌ Descarga incompleta de {info.FileName} ({(fi.Exists ? fi.Length : 0):N0} bytes < {info.MinSizeBytes:N0} bytes mínimos).");
                return null;
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

            progress?.Report(100.0);
            statusLogger?.Invoke($"✅ Modelo descargado: {info.FileName} ({fi.Length / 1_048_576.0:F1} MB)");
            return targetPath;
        }
        catch (OperationCanceledException)
        {
            CleanupTemp(GetModelPath(info.FileName) + ".downloading");
            throw;
        }
        catch (Exception ex)
        {
            CleanupTemp(GetModelPath(info.FileName) + ".downloading");
            statusLogger?.Invoke($"❌ Error descargando '{modelId}': {ex.Message}");
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
