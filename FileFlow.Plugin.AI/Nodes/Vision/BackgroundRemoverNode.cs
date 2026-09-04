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
/// Nodo de pipeline para segmentación de sujeto y eliminación de fondos con IA (RMBG-1.4 y MODNet).
/// Permite generar imágenes PNG con canal alfa transparente, reemplazo de color de fondo o máscaras aisladas.
/// </summary>
[NodeDefinition("BackgroundRemoverNode_Name", "ImageVision", "BackgroundRemoverNode_Desc", PipelineRole.Transform,
    "fondo", "recortar", "transparente", "png", "mascara", "alpha", "quitar fondo", "cutout")]
public class BackgroundRemoverNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("BackgroundRemoverNode_Name", "Eliminador de Fondo IA");
    public string Description => LocalizationManager.Instance.GetString("BackgroundRemoverNode_Desc", "Segmenta el sujeto y elimina el fondo de imágenes con redes neuronales RMBG y MODNet.");
    public string Category => "ImageVision";

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Mask", typeof(FileItemContext), PortDirection.Output, "Mask"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Model"] = "Auto",
        ["OutputMode"] = "TransparentPng",
        ["BackgroundColor"] = "#FFFFFF",
        ["OutputDirectory"] = "{GlobalOutputDir}"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "rmbg-1.4", "modnet"],
            HelpText: "Modelo neural para segmentación de fondo ('Auto' selecciona según hardware).", DisplayOrder: 1),
        new("OutputMode", ParameterEditorType.Dropdown, DefaultValue: "TransparentPng",
            Options: ["TransparentPng", "ColorBackground", "MaskOnly"],
            HelpText: "Formato de salida (PNG con canal alfa transparente, color sólido o solo máscara).", DisplayOrder: 2),
        new("BackgroundColor", ParameterEditorType.Text, DefaultValue: "#FFFFFF",
            HelpText: "Color de fondo hexadecimal (ej. #FFFFFF) si seleccionó 'ColorBackground'.", DisplayOrder: 3),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}",
            HelpText: "Carpeta de destino donde se guardarán las imágenes procesadas.", DisplayOrder: 4)
    ];

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tiff"
    };

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[BackgroundRemover] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_supportedExtensions.Contains(ext))
        {
            context.Log($"[BackgroundRemover] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            await context.EmitAsync("Out", item).ConfigureAwait(false);
            return;
        }

        try
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string outputMode = Parameters.TryGetValue("OutputMode", out var omVal) ? omVal?.ToString() ?? "TransparentPng" : "TransparentPng";
            string bgColorHex = Parameters.TryGetValue("BackgroundColor", out var bgVal) ? bgVal?.ToString() ?? "#FFFFFF" : "#FFFFFF";
            string outputDirRaw = Parameters.TryGetValue("OutputDirectory", out var odVal) ? odVal?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";

            string? modelPath = await AiModelManager.ResolveModelPathAsync(
                modelChoice,
                AiTaskType.BackgroundRemoval,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                context.Log($"[BackgroundRemover] ⚠️ Modelo de eliminación de fondo no disponible. El archivo se emite sin modificar.", LogLevel.Warning, item);
                await context.EmitAsync("Out", item).ConfigureAwait(false);
                return;
            }

            context.Log($"[BackgroundRemover] ✂️ Eliminando fondo de '{item.FileName}'...", LogLevel.Information, item);

            Rgba32? bgColor = null;
            if (string.Equals(outputMode, "ColorBackground", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    bgColor = Rgba32.ParseHex(bgColorHex.TrimStart('#'));
                }
                catch
                {
                    bgColor = new Rgba32(255, 255, 255, 255);
                }
            }

            bool maskOnly = string.Equals(outputMode, "MaskOnly", StringComparison.OrdinalIgnoreCase);

            using var originalImage = await Image.LoadAsync<Rgba32>(item.CurrentPath, cancellationToken).ConfigureAwait(false);

            using var processedImage = await Task.Run(
                () => OnnxInferenceEngine.RemoveBackground(modelPath, originalImage, bgColor, maskOnly),
                cancellationToken).ConfigureAwait(false);

            // Determinar directorio de salida no destructivo
            string targetDir = string.IsNullOrWhiteSpace(outputDirRaw) || outputDirRaw.Contains("{GlobalOutputDir}")
                ? Path.Combine(Path.GetDirectoryName(item.CurrentPath) ?? Directory.GetCurrentDirectory(), "Processed")
                : Path.GetFullPath(outputDirRaw);

            Directory.CreateDirectory(targetDir);

            string fileSuffix = maskOnly ? "_mask.png" : "_nobg.png";
            string targetFileName = Path.GetFileNameWithoutExtension(item.CurrentPath) + fileSuffix;
            string targetPath = Path.Combine(targetDir, targetFileName);

            await processedImage.SaveAsPngAsync(targetPath, cancellationToken).ConfigureAwait(false);

            var newItem = item.DeepClone();
            newItem.CurrentPath = targetPath;
            newItem.FileSizeBytes = new FileInfo(targetPath).Length;
            newItem.Metadata["AI:BackgroundRemoved"] = !maskOnly;
            newItem.Metadata["AI:AlphaMaskGenerated"] = maskOnly;
            newItem.Metadata["AI:BackgroundModel"] = Path.GetFileNameWithoutExtension(modelPath);

            context.Log($"[BackgroundRemover] ✅ Fondo procesado con éxito: '{targetFileName}'", LogLevel.Information, newItem);

            if (maskOnly)
            {
                await context.EmitAsync("Mask", newItem).ConfigureAwait(false);
            }
            else
            {
                await context.EmitAsync("Out", newItem).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            context.Log($"[BackgroundRemover] ❌ Error procesando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
