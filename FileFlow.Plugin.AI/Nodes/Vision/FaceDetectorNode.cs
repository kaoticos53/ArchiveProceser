using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.AI;

[NodeDefinition("FaceDetectorNode_Name", "ImageVision", "FaceDetectorNode_Desc", PipelineRole.Filter,
    "rostros", "caras", "personas", "faces", "ultraface", "detector", "vision", "ia")]
public class FaceDetectorNode : AiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("FaceDetectorNode_Name", "Detector de Rostros (Facial)");
    public override string Category => "ImageVision";
    public override string Description => LocalizationManager.Instance.GetString("FaceDetectorNode_Desc", "Analiza imágenes para detectar la presencia y el número de rostros humanos, bifurcando el flujo hacia fotos familiares o paisajes.");
    public override AiTaskType TaskType => AiTaskType.FaceDetection;

    public FaceDetectorNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("FacesFound", typeof(FileItemContext), PortDirection.Output, "FacesFound"),
            new NodePort("NoFaces", typeof(FileItemContext), PortDirection.Output, "NoFaces")
        ];

        Parameters["Model"] = "Auto";
        Parameters["ConfidenceThreshold"] = 0.7;
        Parameters["MinimumFaces"] = 1;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "ultraface"],
            HelpText: "Modelo para detección facial ('Auto' selecciona según el hardware del equipo).", DisplayOrder: 1),
        new("ConfidenceThreshold", ParameterEditorType.Slider, DefaultValue: 0.7, Min: 0.1, Max: 1.0, Step: 0.05, DisplayOrder: 2),
        new("MinimumFaces", ParameterEditorType.Number, DefaultValue: 1, Min: 1, Max: 50, DisplayOrder: 3)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            Log(context, $"Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            item.Metadata["AI:HasFaces"] = false;
            item.Metadata["AI:FaceCount"] = 0;
            await EmitAsync(context, item, "NoFaces").ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".gif" or ".tiff"))
        {
            Log(context, $"Formato '{ext}' omitido (no es imagen compatible con detector facial).", LogLevel.Debug, item);
            item.Metadata["AI:HasFaces"] = false;
            item.Metadata["AI:FaceCount"] = 0;
            await EmitAsync(context, item, "NoFaces").ConfigureAwait(false);
            return;
        }

        try
        {
            Log(context, $"Detectando rostros en {item.FileName}...", LogLevel.Debug, item);

            string? modelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                Log(context, "⚠️ Modelo de detección facial no disponible. El nodo pasa el archivo sin detección.", LogLevel.Warning, item);
                item.Metadata["AI:HasFaces"] = false;
                item.Metadata["AI:FaceCount"] = 0;
                await EmitAsync(context, item, "NoFaces").ConfigureAwait(false);
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
                Log(context, $"✅ {faceCount} rostro(s) detectado(s) en {item.FileName} (confianza máx: {maxConf * 100:F1}%).", LogLevel.Information, item);
                await EmitAsync(context, item, "FacesFound").ConfigureAwait(false);
            }
            else
            {
                Log(context, $"ℹ️ No se detectaron suficientes rostros ({faceCount} < {minFaces}) en {item.FileName}.", LogLevel.Information, item);
                await EmitAsync(context, item, "NoFaces").ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"Error al procesar {item.FileName}: {ex.Message}", LogLevel.Error, item);
            item.Metadata["AI:HasFaces"] = false;
            item.Metadata["AI:FaceCount"] = 0;
            await EmitAsync(context, item, "NoFaces").ConfigureAwait(false);
        }
    }
}
