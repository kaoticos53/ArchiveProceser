using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo clasificador de moderación de contenido para detección de material sensible o explícito (OpenNSFW2).
/// Bifurca el flujo de ejecución entre 'Safe' y 'Sensitive' según el umbral de probabilidad configurado.
/// </summary>
[NodeDefinition("ContentModerationFilterNode_Name", "Security", "ContentModerationFilterNode_Desc", PipelineRole.Filter,
    "moderacion", "nsfw", "sensible", "inapropiado", "seguridad", "filtro", "opennsfw")]
public sealed class ContentModerationFilterNode : AiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("ContentModerationFilterNode_Name", "Filtro de Moderación IA");
    public override string Category => "Security";
    public override string Description => LocalizationManager.Instance.GetString("ContentModerationFilterNode_Desc", "Evalúa contenido sensible con OpenNSFW2 y bifurca el flujo en puertos Seguro y Sensible.");
    public override AiTaskType TaskType => AiTaskType.ContentModeration;

    public ContentModerationFilterNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Safe", typeof(FileItemContext), PortDirection.Output, "Safe"),
            new NodePort("Sensitive", typeof(FileItemContext), PortDirection.Output, "Sensitive"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Model"] = "Auto";
        Parameters["SensitivityThreshold"] = 0.6;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
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

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
        {
            Log(context, $"[ContentModeration] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_supportedExtensions.Contains(ext))
        {
            Log(context, $"[ContentModeration] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            item.Metadata["AI:IsSensitiveContent"] = false;
            item.Metadata["AI:NsfwScore"] = 0.0;
            await EmitAsync(context, item, "Safe").ConfigureAwait(false);
            return;
        }

        try
        {
            double threshold = GetParameter("SensitivityThreshold", 0.6);

            string? modelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                Log(context, "[ContentModeration] ⚠️ Modelo de moderación no disponible. Se asume seguro por defecto.", LogLevel.Warning, item);
                item.Metadata["AI:IsSensitiveContent"] = false;
                item.Metadata["AI:NsfwScore"] = 0.0;
                await EmitAsync(context, item, "Safe").ConfigureAwait(false);
                return;
            }

            Log(context, $"[ContentModeration] 🛡️ Analizando contenido de '{item.FileName}'...", LogLevel.Information, item);

            await using var stream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            using var image = await Image.LoadAsync<Rgb24>(stream, cancellationToken).ConfigureAwait(false);

            double nsfwScore = await Task.Run(
                () => OnnxInferenceEngine.DetectNsfwScore(modelPath, image),
                cancellationToken).ConfigureAwait(false);

            bool isSensitive = nsfwScore >= threshold;

            item.Metadata["AI:NsfwScore"] = nsfwScore;
            item.Metadata["AI:IsSensitiveContent"] = isSensitive;
            item.Metadata["AI:ModerationModel"] = Path.GetFileNameWithoutExtension(modelPath);
            if (IsGpuAccelerated)
            {
                item.Metadata["AI:DirectMlAccelerated"] = true;
                item.Metadata["AI:Device"] = "GPU (DirectML)";
            }

            if (isSensitive)
            {
                Log(context, $"[ContentModeration] ⚠️ Contenido sensible detectado en {item.FileName} (probabilidad: {nsfwScore * 100:F1}% >= umbral {threshold * 100:F1}%).", LogLevel.Warning, item);
                await EmitAsync(context, item, "Sensitive").ConfigureAwait(false);
            }
            else
            {
                Log(context, $"[ContentModeration] ✅ Contenido seguro: {item.FileName} (probabilidad: {nsfwScore * 100:F1}% < umbral {threshold * 100:F1}%).", LogLevel.Information, item);
                await EmitAsync(context, item, "Safe").ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[ContentModeration] ❌ Error analizando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }
}
