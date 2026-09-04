using System.IO;
using FileFlow.Plugin.AI.Inference;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

[NodeDefinition("ObjectDetectorNode_Name", "ImageVision", "ObjectDetectorNode_Desc", PipelineRole.Analyze,
    "objetos", "yolo", "detectar", "vision", "ia", "personas", "coches", "bounding box")]
public class ObjectDetectorNode : IFlowNode, IModelLifecycleNode
{
    public event Action? ModelStatusChanged;

    public ObjectDetectorNode()
    {
        OnnxSessionManager.SessionStateChanged += () => ModelStatusChanged?.Invoke();
    }

    public bool IsModelLoaded
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.ObjectDetection);
            return modelPath != null && OnnxSessionManager.IsSessionLoaded(modelPath);
        }
    }

    public bool IsGpuAccelerated
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.ObjectDetection);
            return modelPath != null && OnnxSessionManager.ShouldUseDirectMl(modelPath);
        }
    }

    public string? ModelIdentifier
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            return AiModelManager.GetModelDisplayName(modelChoice, AiTaskType.ObjectDetection);
        }
    }

    public async Task PreloadModelAsync(CancellationToken cancellationToken = default)
    {
        string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
        string? modelPath = await AiModelManager.ResolveModelPathAsync(modelChoice, AiTaskType.ObjectDetection, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            OnnxSessionManager.GetOrCreateSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public void UnloadModel()
    {
        string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
        string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.ObjectDetection);
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            OnnxSessionManager.UnloadSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ObjectDetectorNode_Name", "Detector de Objetos (SSD)");
    public string Category => "ImageVision";
    public string Description => LocalizationManager.Instance.GetString("ObjectDetectorNode_Desc", "Detecta e identifica objetos (personas, vehículos, animales, objetos cotidianos) presentes en imágenes usando SSD MobileNet ONNX.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Model"] = "Auto",
        ["MinimumConfidence"] = 0.4,
        ["FilterLabel"] = "",
        ["MaxDetections"] = 10
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "yolov8n", "yolov8s", "yolov8m"],
            HelpText: "Modelo para detección de objetos ('Auto' selecciona según el hardware del equipo).", DisplayOrder: 1),
        new("MinimumConfidence", ParameterEditorType.Slider, DefaultValue: 0.4, Min: 0.1, Max: 1.0, Step: 0.05, DisplayOrder: 2),
        new("FilterLabel", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 3),
        new("MaxDetections", ParameterEditorType.Number, DefaultValue: 10, Min: 1, Max: 100, DisplayOrder: 4)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[ObjectDetector] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp"))
        {
            context.Log($"[ObjectDetector] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            await context.EmitAsync("Out", item).ConfigureAwait(false);
            return;
        }

        try
        {
            context.Log($"[ObjectDetector] Detectando objetos en {item.FileName}...", LogLevel.Information, item);

            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";

            string? modelPath = await AiModelManager.ResolveModelPathAsync(
                modelChoice,
                AiTaskType.ObjectDetection,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                context.Log($"[ObjectDetector] ⚠️ Modelo de detección de objetos no disponible. El nodo pasa el archivo sin detección.", LogLevel.Warning, item);
                await context.EmitAsync("Out", item).ConfigureAwait(false);
                return;
            }

            double threshold = Parameters.TryGetValue("MinimumConfidence", out var ct) ? ParameterHelper.GetDouble(ct, 0.4) : 0.4;
            string filter = Parameters.TryGetValue("FilterLabel", out var fl) ? fl?.ToString() ?? string.Empty : string.Empty;
            int maxDets = Parameters.TryGetValue("MaxDetections", out var md) ? ParameterHelper.GetInt32(md, 10) : 10;

            using var image = await Image.LoadAsync<Rgb24>(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            int origW = image.Width;
            int origH = image.Height;

            var detected = await Task.Run(
                () => OnnxInferenceEngine.DetectObjects(modelPath, image, threshold, origW, origH),
                cancellationToken).ConfigureAwait(false);

            // Aplicar filtro opcional
            if (!string.IsNullOrWhiteSpace(filter))
            {
                detected = detected.Where(d => d.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            detected = detected.Take(maxDets).ToList();

            item.Metadata["AI:DetectedObjects"] = string.Join(", ", detected.Select(d => d.Label));
            item.Metadata["AI:TopObject"] = detected.FirstOrDefault().Label ?? string.Empty;
            item.Metadata["AI:ObjectCount"] = detected.Count;
            item.Metadata["AI:ObjectScores"] = string.Join(", ", detected.Select(d => $"{d.Label}:{d.Confidence:F2}"));
            item.Metadata["AI:Model"] = Path.GetFileNameWithoutExtension(modelPath);
            if (IsGpuAccelerated)
            {
                item.Metadata["AI:DirectMlAccelerated"] = true;
                item.Metadata["AI:Device"] = "GPU (DirectML)";
            }

            if (detected.Count > 0)
            {
                var boxes = detected.Select(d => d.Box).ToList();
                item.Metadata["AI:DetectedBoxes"] = System.Text.Json.JsonSerializer.Serialize(boxes);
                item.Metadata["AI:FaceBoxes"] = null!; // Avoid collision
                item.Metadata.Remove("AI:FaceBoxes");
            }
            else
            {
                item.Metadata.Remove("AI:DetectedBoxes");
            }

            if (detected.Count > 0)
            {
                context.Log($"[ObjectDetector] ✅ {detected.Count} objeto(s) detectado(s): {item.Metadata["AI:DetectedObjects"]}", LogLevel.Information, item);
            }
            else
            {
                context.Log($"[ObjectDetector] ℹ️ 0 objetos detectados en {item.FileName} con umbral de confianza {threshold * 100:F0}%.", LogLevel.Information, item);
            }

            await context.EmitAsync("Out", item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[ObjectDetector] Error en detección de objetos para {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
