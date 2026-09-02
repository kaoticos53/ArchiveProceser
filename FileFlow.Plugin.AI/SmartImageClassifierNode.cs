using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI;

[NodeDefinition("SmartImageClassifierNode_Name", "AI & Computer Vision", "SmartImageClassifierNode_Desc")]
public class SmartImageClassifierNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SmartImageClassifierNode_Name", "Clasificador Visual de Fotos (IA)");
    public string Category => "AI & Computer Vision";
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
        ["MinimumConfidence"] = 0.5,
        ["FallbackCategory"] = "Fotografía General"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("MinimumConfidence", ParameterEditorType.Slider, DefaultValue: 0.5, Min: 0.1, Max: 1.0, Step: 0.05, DisplayOrder: 1),
        new("FallbackCategory", ParameterEditorType.Text, DefaultValue: "Fotografía General", DisplayOrder: 2)
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

            // Asegurar que el modelo MobileNetV2 está descargado
            string? modelPath = await AiModelManager.EnsureModelAsync("mobilenetv2", context, item, cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                context.Log($"[ImageClassifier] ⚠️ Modelo MobileNetV2 no disponible. El nodo pasa el archivo sin clasificar.", LogLevel.Warning, item);
                await context.EmitAsync("Out", item).ConfigureAwait(false);
                return;
            }

            using var image = await Image.LoadAsync<Rgb24>(item.CurrentPath, cancellationToken).ConfigureAwait(false);

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
