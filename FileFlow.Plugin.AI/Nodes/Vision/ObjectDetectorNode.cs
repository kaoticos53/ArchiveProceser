using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

[NodeDefinition("ObjectDetectorNode_Name", "ImageVision", "ObjectDetectorNode_Desc", PipelineRole.Analyze,
    "objetos", "yolo", "detectar", "vision", "ia", "personas", "coches", "bounding box")]
public sealed class ObjectDetectorNode : AiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("ObjectDetectorNode_Name", "Detector de Objetos (SSD)");
    public override string Category => "ImageVision";
    public override string Description => LocalizationManager.Instance.GetString("ObjectDetectorNode_Desc", "Detecta e identifica objetos (personas, vehículos, animales, objetos cotidianos) presentes en imágenes usando SSD MobileNet ONNX.");
    public override AiTaskType TaskType => AiTaskType.ObjectDetection;

    public ObjectDetectorNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Model"] = "Auto";
        Parameters["MinimumConfidence"] = 0.4;
        Parameters["FilterLabel"] = "";
        Parameters["MaxDetections"] = 10;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "yolov8n", "yolov8s", "yolov8m"],
            HelpText: "Modelo para detección de objetos ('Auto' selecciona según el hardware del equipo).", DisplayOrder: 1),
        new("MinimumConfidence", ParameterEditorType.Slider, DefaultValue: 0.4, Min: 0.1, Max: 1.0, Step: 0.05, DisplayOrder: 2),
        new("FilterLabel", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 3),
        new("MaxDetections", ParameterEditorType.Number, DefaultValue: 10, Min: 1, Max: 100, DisplayOrder: 4)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
        {
            Log(context, $"[ObjectDetector] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp"))
        {
            Log(context, $"[ObjectDetector] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            await EmitAsync(context, item).ConfigureAwait(false);
            return;
        }

        try
        {
            Log(context, $"[ObjectDetector] Detectando objetos en {item.FileName}...", LogLevel.Information, item);

            string? modelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                Log(context, "[ObjectDetector] ⚠️ Modelo de detección de objetos no disponible. El nodo pasa el archivo sin detección.", LogLevel.Warning, item);
                await EmitAsync(context, item).ConfigureAwait(false);
                return;
            }

            double threshold = GetParameter("MinimumConfidence", 0.4);
            string filter = GetParameter("FilterLabel", string.Empty);
            int maxDets = GetParameter("MaxDetections", 10);

            await using var stream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            using var image = await Image.LoadAsync<Rgb24>(stream, cancellationToken).ConfigureAwait(false);
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
                Log(context, $"[ObjectDetector] ✅ {detected.Count} objeto(s) detectado(s): {item.Metadata["AI:DetectedObjects"]}", LogLevel.Information, item);
            }
            else
            {
                Log(context, $"[ObjectDetector] ℹ️ 0 objetos detectados en {item.FileName} con umbral de confianza {threshold * 100:F0}%.", LogLevel.Information, item);
            }

            await EmitAsync(context, item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[ObjectDetector] Error en detección de objetos para {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }
}
