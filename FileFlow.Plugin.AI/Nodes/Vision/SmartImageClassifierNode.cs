using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

[NodeDefinition("SmartImageClassifierNode_Name", "ImageVision", "SmartImageClassifierNode_Desc", PipelineRole.Analyze,
    "clasificar", "imagen", "foto", "vision", "ia", "mobilenet", "etiquetas", "classifier")]
public sealed class SmartImageClassifierNode : AiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("SmartImageClassifierNode_Name", "Clasificador Visual de Fotos (IA)");
    public override string Category => "ImageVision";
    public override string Description => LocalizationManager.Instance.GetString("SmartImageClassifierNode_Desc", "Analiza el contenido visual de fotografías e imágenes asignando una categoría temática (Paisajes, Documentos, Vehículos, Comida, etc.) en los metadatos.");
    public override AiTaskType TaskType => AiTaskType.ImageClassification;

    public SmartImageClassifierNode()
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
        Parameters["MinimumConfidence"] = 0.5;
        Parameters["FallbackCategory"] = "Fotografía General";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "mobilenetv2"],
            HelpText: "Modelo para clasificación visual ('Auto' selecciona según el hardware del equipo).", DisplayOrder: 1),
        new("MinimumConfidence", ParameterEditorType.Slider, DefaultValue: 0.5, Min: 0.1, Max: 1.0, Step: 0.05, DisplayOrder: 2),
        new("FallbackCategory", ParameterEditorType.Text, DefaultValue: "Fotografía General", DisplayOrder: 3)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
        {
            Log(context, $"[ImageClassifier] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp"))
        {
            Log(context, $"[ImageClassifier] Formato no compatible ({ext}): {item.FileName}", LogLevel.Debug, item);
            await EmitAsync(context, item).ConfigureAwait(false);
            return;
        }

        try
        {
            Log(context, $"[ImageClassifier] Clasificando: {item.FileName}...", LogLevel.Information, item);

            string? modelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                Log(context, "[ImageClassifier] ⚠️ Modelo de clasificación visual no disponible. El nodo pasa el archivo sin clasificar.", LogLevel.Warning, item);
                await EmitAsync(context, item).ConfigureAwait(false);
                return;
            }

            await using var stream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            using var image = await Image.LoadAsync<Rgb24>(stream, cancellationToken).ConfigureAwait(false);
            image.Mutate(x => x.Resize(224, 224));

            var (category, label, confidence) = await Task.Run(
                () => OnnxInferenceEngine.ClassifyImage(modelPath, image),
                cancellationToken).ConfigureAwait(false);

            double minConfidence = GetParameter("MinimumConfidence", 0.5);
            string fallback = GetParameter("FallbackCategory", "Fotografía General");

            if (confidence < minConfidence)
            {
                Log(context, $"[ImageClassifier] Confianza {confidence * 100:F0}% < umbral {minConfidence * 100:F0}%. Usando categoría de respaldo: '{fallback}'.", LogLevel.Debug, item);
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

            Log(context, $"[ImageClassifier] ✅ Clasificación: '{category}' ({label}) — confianza: {confidence * 100:F1}%", LogLevel.Information, item);

            await EmitAsync(context, item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[ImageClassifier] Error clasificando imagen {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }
}
