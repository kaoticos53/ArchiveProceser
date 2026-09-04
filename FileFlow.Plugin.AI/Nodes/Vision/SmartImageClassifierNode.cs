using System.IO;
using FileFlow.Plugin.AI.Inference;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

[NodeDefinition("SmartImageClassifierNode_Name", "ImageVision", "SmartImageClassifierNode_Desc", PipelineRole.Analyze,
    "clasificar", "imagen", "foto", "vision", "ia", "mobilenet", "etiquetas", "classifier")]
public class SmartImageClassifierNode : IFlowNode, IModelLifecycleNode
{
    public event Action? ModelStatusChanged;

    public SmartImageClassifierNode()
    {
        OnnxSessionManager.SessionStateChanged += () => ModelStatusChanged?.Invoke();
    }

    public bool IsModelLoaded
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.ImageClassification);
            return modelPath != null && OnnxSessionManager.IsSessionLoaded(modelPath);
        }
    }

    public bool IsGpuAccelerated
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.ImageClassification);
            return modelPath != null && OnnxSessionManager.ShouldUseDirectMl(modelPath);
        }
    }

    public string? ModelIdentifier
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            return AiModelManager.GetModelDisplayName(modelChoice, AiTaskType.ImageClassification);
        }
    }

    public async Task PreloadModelAsync(CancellationToken cancellationToken = default)
    {
        string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
        string? modelPath = await AiModelManager.ResolveModelPathAsync(modelChoice, AiTaskType.ImageClassification, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            OnnxSessionManager.GetOrCreateSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public void UnloadModel()
    {
        string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
        string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.ImageClassification);
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            OnnxSessionManager.UnloadSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SmartImageClassifierNode_Name", "Clasificador Visual de Fotos (IA)");
    public string Category => "ImageVision";
    public string Description => LocalizationManager.Instance.GetString("SmartImageClassifierNode_Desc", "Analiza el contenido visual de fotografías e imágenes asignando una categoría temática (Paisajes, Documentos, Vehículos, Comida, etc.) en los metadatos.");

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
        ["MinimumConfidence"] = 0.5,
        ["FallbackCategory"] = "Fotografía General"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "mobilenetv2"],
            HelpText: "Modelo para clasificación visual ('Auto' selecciona según el hardware del equipo).", DisplayOrder: 1),
        new("MinimumConfidence", ParameterEditorType.Slider, DefaultValue: 0.5, Min: 0.1, Max: 1.0, Step: 0.05, DisplayOrder: 2),
        new("FallbackCategory", ParameterEditorType.Text, DefaultValue: "Fotografía General", DisplayOrder: 3)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[ImageClassifier] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp"))
        {
            context.Log($"[ImageClassifier] Formato no compatible ({ext}): {item.FileName}", LogLevel.Debug, item);
            await context.EmitAsync("Out", item).ConfigureAwait(false);
            return;
        }

        try
        {
            context.Log($"[ImageClassifier] Clasificando: {item.FileName}...", LogLevel.Information, item);

            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";

            string? modelPath = await AiModelManager.ResolveModelPathAsync(
                modelChoice,
                AiTaskType.ImageClassification,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                context.Log($"[ImageClassifier] ⚠️ Modelo de clasificación visual no disponible. El nodo pasa el archivo sin clasificar.", LogLevel.Warning, item);
                await context.EmitAsync("Out", item).ConfigureAwait(false);
                return;
            }

            using var image = await Image.LoadAsync<Rgb24>(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            image.Mutate(x => x.Resize(224, 224));

            var (category, label, confidence) = await Task.Run(
                () => OnnxInferenceEngine.ClassifyImage(modelPath, image),
                cancellationToken).ConfigureAwait(false);

            double minConfidence = Parameters.TryGetValue("MinimumConfidence", out var mc) ? ParameterHelper.GetDouble(mc, 0.5) : 0.5;
            string fallback = Parameters.TryGetValue("FallbackCategory", out var fb) ? fb?.ToString() ?? "Fotografía General" : "Fotografía General";

            if (confidence < minConfidence)
            {
                context.Log($"[ImageClassifier] Confianza {confidence * 100:F0}% < umbral {minConfidence * 100:F0}%. Usando categoría de respaldo: '{fallback}'.", LogLevel.Debug, item);
                category = fallback;
            }

            item.Metadata["AI:Category"] = category;
            item.Metadata["AI:TopLabel"] = label;
            item.Metadata["AI:Confidence"] = Math.Round(confidence, 4);
            item.Metadata["AI:Model"] = "mobilenetv2-7";
            if (IsGpuAccelerated)
            {
                item.Metadata["AI:DirectMlAccelerated"] = true;
                item.Metadata["AI:Device"] = "GPU (DirectML)";
            }

            context.Log($"[ImageClassifier] ✅ Clasificación: '{category}' ({label}) — confianza: {confidence * 100:F1}%", LogLevel.Information, item);

            await context.EmitAsync("Out", item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[ImageClassifier] Error clasificando imagen {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
