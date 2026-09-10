using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de clasificación de tipo de imagen determinista y neuronal.
/// Discrimina con alta precisión entre Documentos, Recibos/Tickets, Retratos, Fotos Grupales,
/// Fotografías, Capturas de Pantalla, Ilustraciones y Documentos de Identidad (DNI/Tarjetas).
/// </summary>
[NodeDefinition("ImageTypeClassifierNode_Name", "ImageVision", "ImageTypeClassifierNode_Desc", PipelineRole.Analyze,
    "tipo imagen", "documento", "recibo", "factura", "foto", "retrato", "captura", "screenshot", "clasificador", "vision", "ia")]
public sealed class ImageTypeClassifierNode : AiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("ImageTypeClassifierNode_Name", "Clasificador de Tipo de Imagen");
    public override string Category => "ImageVision";
    public override string Description => LocalizationManager.Instance.GetString("ImageTypeClassifierNode_Desc", "Clasifica imágenes de forma determinista y precisa en Documentos, Recibos, Retratos, Fotos, Capturas, Ilustraciones o Tarjetas.");
    public override AiTaskType TaskType => AiTaskType.FaceDetection;

    public ImageTypeClassifierNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Document", typeof(FileItemContext), PortDirection.Output, "Document"),
            new NodePort("Receipt", typeof(FileItemContext), PortDirection.Output, "Receipt"),
            new NodePort("Portrait", typeof(FileItemContext), PortDirection.Output, "Portrait"),
            new NodePort("GroupPhoto", typeof(FileItemContext), PortDirection.Output, "GroupPhoto"),
            new NodePort("Photo", typeof(FileItemContext), PortDirection.Output, "Photo"),
            new NodePort("Screenshot", typeof(FileItemContext), PortDirection.Output, "Screenshot"),
            new NodePort("Illustration", typeof(FileItemContext), PortDirection.Output, "Illustration"),
            new NodePort("IDCard", typeof(FileItemContext), PortDirection.Output, "IDCard"),
            new NodePort("Other", typeof(FileItemContext), PortDirection.Output, "Other"),
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["ConfidenceThreshold"] = 0.50;
        Parameters["EnableFaceDetection"] = true;
        Parameters["CheckExifMetadata"] = true;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("ConfidenceThreshold", ParameterEditorType.Slider, DefaultValue: 0.50, Min: 0.10, Max: 0.95, Step: 0.05,
            HelpText: "Umbral mínimo de confianza para clasificar en una categoría específica.", DisplayOrder: 1),
        new("EnableFaceDetection", ParameterEditorType.Toggle, DefaultValue: true,
            HelpText: "Activa la verificación facial (UltraFace) para distinguir con certeza retratos y fotos grupales.", DisplayOrder: 2),
        new("CheckExifMetadata", ParameterEditorType.Toggle, DefaultValue: true,
            HelpText: "Inspecciona los metadatos EXIF de cámara para discriminar fotografías del mundo real de capturas o dibujos.", DisplayOrder: 3)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
        {
            Log(context, $"Archivo de imagen no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".gif" or ".tiff"))
        {
            Log(context, $"Formato '{ext}' no es una imagen rasterizada compatible.", LogLevel.Warning, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        try
        {
            double threshold = Parameters.TryGetValue("ConfidenceThreshold", out var ct) ? ParameterHelper.GetDouble(ct, 0.50) : 0.50;
            bool enableFace = Parameters.TryGetValue("EnableFaceDetection", out var ef) ? ParameterHelper.GetBoolean(ef, true) : true;
            bool checkExif = Parameters.TryGetValue("CheckExifMetadata", out var ce) ? ParameterHelper.GetBoolean(ce, true) : true;

            string? faceModelPath = null;
            if (enableFace)
            {
                faceModelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);
            }

            Log(context, $"🔍 Analizando estructura visual de '{item.FileName}'...", LogLevel.Debug, item);

            using var image = await LoadInputRgb24ImageAsync(item, storage, cancellationToken).ConfigureAwait(false);
            if (image == null)
            {
                Log(context, $"No se pudo decodificar la imagen '{item.CurrentPath}'.", LogLevel.Error, item);
                await context.EmitAsync("Error", item).ConfigureAwait(false);
                return;
            }

            var result = await Task.Run(
                () => ImageTypeAnalyzerEngine.AnalyzeImage(image, faceModelPath, enableFace, checkExif, threshold),
                cancellationToken).ConfigureAwait(false);

            // Inyectar metadatos enriquecidos en el contexto del archivo
            item.Metadata["AI:ImageType"] = result.TopCategory;
            item.Metadata["AI:ImageTypeConfidence"] = Math.Round(result.TopScore, 4);
            item.Metadata["AI:ImageTypeScoresJson"] = JsonSerializer.Serialize(result.CategoryScores);
            item.Metadata["AI:HasFaces"] = result.HasFaces;
            item.Metadata["AI:FaceCount"] = result.FaceCount;
            item.Metadata["AI:HasCameraExif"] = result.HasCameraExif;
            item.Metadata["AI:AspectRatio"] = result.AspectRatio;

            Log(context, $"🖼️ Tipo de imagen detectado: '{result.TopCategory}' (confianza: {result.TopScore:P0}). HasFaces: {result.HasFaces}, EXIF: {result.HasCameraExif}.",
                LogLevel.Information, item);

            // Enrutamiento al puerto específico de la categoría
            string targetPort = result.TopCategory switch
            {
                ImageTypeAnalyzerEngine.CategoryDocument => "Document",
                ImageTypeAnalyzerEngine.CategoryReceipt => "Receipt",
                ImageTypeAnalyzerEngine.CategoryPortrait => "Portrait",
                ImageTypeAnalyzerEngine.CategoryGroupPhoto => "GroupPhoto",
                ImageTypeAnalyzerEngine.CategoryPhoto => "Photo",
                ImageTypeAnalyzerEngine.CategoryScreenshot => "Screenshot",
                ImageTypeAnalyzerEngine.CategoryIllustration => "Illustration",
                ImageTypeAnalyzerEngine.CategoryIdCard => "IDCard",
                _ => "Other"
            };

            await context.EmitAsync(targetPort, item).ConfigureAwait(false);
            await context.EmitAsync("Out", item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"Error analizando tipo de imagen para {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
