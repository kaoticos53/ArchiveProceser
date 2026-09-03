using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

[NodeDefinition("FaceDetectorNode_Name", "AI & Computer Vision", "FaceDetectorNode_Desc")]
public class FaceDetectorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("FaceDetectorNode_Name", "Detector de Rostros (Facial)");
    public string Category => "AI & Computer Vision";
    public string Description => LocalizationManager.Instance.GetString("FaceDetectorNode_Desc", "Analiza imágenes para detectar la presencia y el número de rostros humanos, bifurcando el flujo hacia fotos familiares o paisajes.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("FacesFound", typeof(FileItemContext), PortDirection.Output, "FacesFound"),
        new NodePort("NoFaces", typeof(FileItemContext), PortDirection.Output, "NoFaces")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ConfidenceThreshold"] = 0.7,
        ["MinimumFaces"] = 1
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("ConfidenceThreshold", ParameterEditorType.Slider, DefaultValue: 0.7, Min: 0.1, Max: 1.0, Step: 0.05, DisplayOrder: 1),
        new("MinimumFaces", ParameterEditorType.Number, DefaultValue: 1, Min: 1, Max: 50, DisplayOrder: 2)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[FaceDetector] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            item.Metadata["AI:HasFaces"] = false;
            item.Metadata["AI:FaceCount"] = 0;
            await context.EmitAsync("NoFaces", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp"))
        {
            context.Log($"[FaceDetector] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            item.Metadata["AI:HasFaces"] = false;
            item.Metadata["AI:FaceCount"] = 0;
            await context.EmitAsync("NoFaces", item).ConfigureAwait(false);
            return;
        }

        try
        {
            context.Log($"[FaceDetector] Detectando rostros en {item.FileName}...", LogLevel.Information, item);

            // Asegurar modelo UltraFace descargado automáticamente
            string? modelPath = await AiModelManager.EnsureModelAsync("ultraface", context, item, cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                context.Log($"[FaceDetector] ⚠️ Modelo UltraFace no disponible. El nodo pasa el archivo sin detección.", LogLevel.Warning, item);
                item.Metadata["AI:HasFaces"] = false;
                item.Metadata["AI:FaceCount"] = 0;
                await context.EmitAsync("NoFaces", item).ConfigureAwait(false);
                return;
            }

            double threshold = Parameters.TryGetValue("ConfidenceThreshold", out var ct) ? ParameterHelper.GetDouble(ct, 0.7) : 0.7;
            int minFaces = Parameters.TryGetValue("MinimumFaces", out var mf) ? ParameterHelper.GetInt32(mf, 1) : 1;

            using var image = await Image.LoadAsync<Rgb24>(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            image.Mutate(x => x.Resize(320, 240));

            var (faceCount, maxConf, faces) = await Task.Run(
                () => OnnxInferenceEngine.DetectFaces(modelPath, image, threshold),
                cancellationToken).ConfigureAwait(false);

            item.Metadata["AI:FaceCount"] = faceCount;
            item.Metadata["AI:HasFaces"] = faceCount >= minFaces;
            item.Metadata["AI:FaceMaxConfidence"] = Math.Round(maxConf, 4);
            item.Metadata["AI:Model"] = "ultraface-slim-320";

            if (faces.Count > 0)
            {
                item.Metadata["AI:FaceBoxes"] = System.Text.Json.JsonSerializer.Serialize(faces);
            }
            else
            {
                item.Metadata.Remove("AI:FaceBoxes");
            }

            if (faceCount >= minFaces)
            {
                context.Log($"[FaceDetector] ✅ {faceCount} rostro(s) detectado(s) en {item.FileName} (confianza máx: {maxConf * 100:F1}%).", LogLevel.Information, item);
                await context.EmitAsync("FacesFound", item).ConfigureAwait(false);
            }
            else
            {
                context.Log($"[FaceDetector] ℹ️ No se detectaron suficientes rostros ({faceCount} < {minFaces}) en {item.FileName}.", LogLevel.Information, item);
                await context.EmitAsync("NoFaces", item).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[FaceDetector] Error al procesar {item.FileName}: {ex.Message}", LogLevel.Error, item);
            item.Metadata["AI:HasFaces"] = false;
            item.Metadata["AI:FaceCount"] = 0;
            await context.EmitAsync("NoFaces", item).ConfigureAwait(false);
        }
    }
}
