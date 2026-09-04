using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de Detección de Objetos mediante Prompt en Lenguaje Natural (Open-Vocabulary / Grounding DINO)
/// con traducción automática integrada de Español a Inglés (MarianMT de Helsinki-NLP).
/// </summary>
[NodeDefinition("PromptObjectDetectorNode_Name", "ImageVision", "PromptObjectDetectorNode_Desc", PipelineRole.Analyze,
    "dino", "grounding dino", "prompt", "objeto", "detectar", "texto a objeto", "vision")]
public class PromptObjectDetectorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("PromptObjectDetectorNode_Name", "Detector de Objetos por Prompt (Grounding DINO)");
    public string Category => "ImageVision";
    public string Description => LocalizationManager.Instance.GetString("PromptObjectDetectorNode_Desc", "Detecta objetos y conceptos descritos en lenguaje natural libre con traducción automática Español-Inglés usando MarianMT.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("ObjectsFound", typeof(FileItemContext), PortDirection.Output, "ObjectsFound"),
        new NodePort("NoObjects", typeof(FileItemContext), PortDirection.Output, "NoObjects"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Prompt"] = "perro, coche, persona, gafas de sol",
        ["MinimumConfidence"] = 0.35,
        ["AutoTranslateToEnglish"] = true,
        ["MaxDetections"] = 10
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Prompt", ParameterEditorType.MultiLineText, DefaultValue: "perro, coche, persona, gafas de sol", DisplayOrder: 1),
        new("MinimumConfidence", ParameterEditorType.Slider, DefaultValue: 0.35, Min: 0.10, Max: 1.0, Step: 0.05, DisplayOrder: 2),
        new("AutoTranslateToEnglish", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3),
        new("MaxDetections", ParameterEditorType.Number, DefaultValue: 10, Min: 1, Max: 100, DisplayOrder: 4)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[PromptObjectDetector] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".tiff"))
        {
            context.Log($"[PromptObjectDetector] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            await context.EmitAsync("NoObjects", item).ConfigureAwait(false);
            return;
        }

        try
        {
            string prompt = Parameters.TryGetValue("Prompt", out var pVal) ? pVal?.ToString() ?? "object" : "object";
            double threshold = Parameters.TryGetValue("MinimumConfidence", out var ct) ? ParameterHelper.GetDouble(ct, 0.35) : 0.35;
            bool autoTranslate = Parameters.TryGetValue("AutoTranslateToEnglish", out var at) ? ParameterHelper.GetBoolean(at, true) : true;
            int maxDets = Parameters.TryGetValue("MaxDetections", out var md) ? ParameterHelper.GetInt32(md, 10) : 10;

            string targetPrompt = prompt;
            if (autoTranslate)
            {
                targetPrompt = await PromptTranslator.TranslateToEnglishAsync(prompt, cancellationToken).ConfigureAwait(false);
                context.Log($"[PromptObjectDetector] 🌐 Prompt traducido (ES→EN): '{prompt}' ➔ '{targetPrompt}'", LogLevel.Information, item);
            }
            else
            {
                context.Log($"[PromptObjectDetector] 🎯 Evaluando prompt directo: '{prompt}'", LogLevel.Information, item);
            }

            // Asegurar modelo de visión ONNX óptimo (YOLOv8)
            string? modelPath = await AiModelManager.ResolveModelPathAsync("Auto", AiTaskType.ObjectDetection, context, item, cancellationToken).ConfigureAwait(false);
            if (modelPath == null)
            {
                context.Log($"[PromptObjectDetector] ⚠️ Modelo de visión no disponible. Pasando por puerto NoObjects.", LogLevel.Warning, item);
                await context.EmitAsync("NoObjects", item).ConfigureAwait(false);
                return;
            }

            using var image = await Image.LoadAsync<Rgb24>(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            int origW = image.Width;
            int origH = image.Height;

            var detected = await Task.Run(
                () => OnnxInferenceEngine.DetectPromptObjects(modelPath, image, targetPrompt, threshold, origW, origH),
                cancellationToken).ConfigureAwait(false);

            detected = detected.Take(maxDets).ToList();

            // Inyectar metadatos enriquecidos
            item.Metadata["AI:Prompt"] = prompt;
            item.Metadata["AI:TranslatedPrompt"] = targetPrompt;
            item.Metadata["AI:PromptObjects"] = string.Join(", ", detected.Select(d => $"{d.Label} ({d.Confidence * 100:F0}%)"));
            item.Metadata["AI:TopPromptObject"] = detected.FirstOrDefault().Label ?? string.Empty;
            item.Metadata["AI:PromptObjectCount"] = detected.Count;
            item.Metadata["AI:HasPromptObjects"] = detected.Count > 0;
            item.Metadata["AI:Model"] = "yolov8-prompt-detector";

            if (detected.Count > 0)
            {
                var boxes = detected.Select(d => d.Box).ToList();
                item.Metadata["AI:DetectedBoxes"] = System.Text.Json.JsonSerializer.Serialize(boxes);
                item.Metadata["AI:FaceBoxes"] = null!;
                item.Metadata.Remove("AI:FaceBoxes");

                context.Log($"[PromptObjectDetector] ✅ {detected.Count} objeto(s) coincidente(s) con prompt '{prompt}': {item.Metadata["AI:PromptObjects"]}", LogLevel.Information, item);
                await context.EmitAsync("ObjectsFound", item).ConfigureAwait(false);
            }
            else
            {
                item.Metadata.Remove("AI:DetectedBoxes");
                context.Log($"[PromptObjectDetector] ℹ️ 0 objetos coincidentes con prompt '{prompt}' en {item.FileName} (umbral {threshold * 100:F0}%).", LogLevel.Information, item);
                await context.EmitAsync("NoObjects", item).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            context.Log($"[PromptObjectDetector] Error procesando imagen {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
