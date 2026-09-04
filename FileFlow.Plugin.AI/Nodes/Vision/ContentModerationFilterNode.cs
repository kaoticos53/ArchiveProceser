using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo clasificador de moderación de contenido para detección de material sensible o explícito (OpenNSFW2).
/// Bifurca el flujo de ejecución entre 'Safe' y 'Sensitive' según el umbral de probabilidad configurado.
/// </summary>
[NodeDefinition("ContentModerationFilterNode_Name", "Security", "ContentModerationFilterNode_Desc", PipelineRole.Filter,
    "moderacion", "nsfw", "sensible", "inapropiado", "seguridad", "filtro", "opennsfw")]
public class ContentModerationFilterNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ContentModerationFilterNode_Name", "Filtro de Moderación IA");
    public string Description => LocalizationManager.Instance.GetString("ContentModerationFilterNode_Desc", "Evalúa contenido sensible con OpenNSFW2 y bifurca el flujo en puertos Seguro y Sensible.");
    public string Category => "Security";

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Safe", typeof(FileItemContext), PortDirection.Output, "Safe"),
        new NodePort("Sensitive", typeof(FileItemContext), PortDirection.Output, "Sensitive"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Model"] = "Auto",
        ["SensitivityThreshold"] = 0.6
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "opennsfw2"],
            HelpText: "Modelo neural de moderación de contenido ('Auto' selecciona según hardware).", DisplayOrder: 1),
        new("SensitivityThreshold", ParameterEditorType.Slider, DefaultValue: 0.6, Min: 0.1, Max: 0.95, Step: 0.05,
            HelpText: "Umbral de probabilidad a partir del cual se bifurca a 'Sensitive'.", DisplayOrder: 2)
    ];

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tiff"
    };

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[ContentModeration] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_supportedExtensions.Contains(ext))
        {
            context.Log($"[ContentModeration] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            item.Metadata["AI:IsSensitiveContent"] = false;
            item.Metadata["AI:NsfwScore"] = 0.0;
            await context.EmitAsync("Safe", item).ConfigureAwait(false);
            return;
        }

        try
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            double threshold = Parameters.TryGetValue("SensitivityThreshold", out var stVal) ? ParameterHelper.GetDouble(stVal, 0.6) : 0.6;

            string? modelPath = await AiModelManager.ResolveModelPathAsync(
                modelChoice,
                AiTaskType.ContentModeration,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                context.Log($"[ContentModeration] ⚠️ Modelo de moderación no disponible. Se asume seguro por defecto.", LogLevel.Warning, item);
                item.Metadata["AI:IsSensitiveContent"] = false;
                item.Metadata["AI:NsfwScore"] = 0.0;
                await context.EmitAsync("Safe", item).ConfigureAwait(false);
                return;
            }

            context.Log($"[ContentModeration] 🛡️ Analizando contenido de '{item.FileName}'...", LogLevel.Information, item);

            using var image = await Image.LoadAsync<Rgb24>(item.CurrentPath, cancellationToken).ConfigureAwait(false);

            double nsfwScore = await Task.Run(
                () => OnnxInferenceEngine.DetectNsfwScore(modelPath, image),
                cancellationToken).ConfigureAwait(false);

            bool isSensitive = nsfwScore >= threshold;

            item.Metadata["AI:NsfwScore"] = nsfwScore;
            item.Metadata["AI:IsSensitiveContent"] = isSensitive;
            item.Metadata["AI:ModerationModel"] = Path.GetFileNameWithoutExtension(modelPath);

            if (isSensitive)
            {
                context.Log($"[ContentModeration] ⚠️ Contenido sensible detectado en {item.FileName} (probabilidad: {nsfwScore * 100:F1}% >= umbral {threshold * 100:F1}%).", LogLevel.Warning, item);
                await context.EmitAsync("Sensitive", item).ConfigureAwait(false);
            }
            else
            {
                context.Log($"[ContentModeration] ✅ Contenido seguro: {item.FileName} (probabilidad: {nsfwScore * 100:F1}% < umbral {threshold * 100:F1}%).", LogLevel.Information, item);
                await context.EmitAsync("Safe", item).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            context.Log($"[ContentModeration] ❌ Error analizando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
