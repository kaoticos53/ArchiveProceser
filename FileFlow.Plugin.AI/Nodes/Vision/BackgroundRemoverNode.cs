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
        new NodePort("Bypass", typeof(FileItemContext), PortDirection.Output, "Bypass"),
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
            await context.EmitAsync("Bypass", item).ConfigureAwait(false);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
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
                context.Log($"[BackgroundRemover] ⚠️ Modelo de eliminación de fondo no disponible. El archivo se emite por Bypass.", LogLevel.Warning, item);
                await context.EmitAsync("Bypass", item).ConfigureAwait(false);
                await context.EmitAsync("Error", item).ConfigureAwait(false);
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
            string targetDir = ParameterHelper.ResolveOutputPath(
                string.IsNullOrWhiteSpace(outputDirRaw) ? "{GlobalOutputDir}" : outputDirRaw,
                item);

            Directory.CreateDirectory(targetDir);

            if (maskOnly)
            {
                // Modo solo máscara
                string maskFileName = Path.GetFileNameWithoutExtension(item.CurrentPath) + "_mask.png";
                string maskPath = Path.Combine(targetDir, maskFileName);
                await processedImage.SaveAsPngAsync(maskPath, cancellationToken).ConfigureAwait(false);

                var maskItem = item.DeepClone();
                maskItem.CurrentPath = maskPath;
                maskItem.PhysicalPath = maskPath;
                maskItem.FileSizeBytes = new FileInfo(maskPath).Length;
                maskItem.Metadata["AI:AlphaMaskGenerated"] = true;
                maskItem.Metadata["AI:BackgroundModel"] = Path.GetFileNameWithoutExtension(modelPath);

                context.Log($"[BackgroundRemover] ✅ Máscara generada con éxito: '{maskFileName}'", LogLevel.Information, maskItem);
                await context.EmitAsync("Mask", maskItem).ConfigureAwait(false);
            }
            else
            {
                // Modo imagen procesada (transparente o color)
                string targetFileName = Path.GetFileNameWithoutExtension(item.CurrentPath) + "_nobg.png";
                string targetPath = Path.Combine(targetDir, targetFileName);
                await processedImage.SaveAsPngAsync(targetPath, cancellationToken).ConfigureAwait(false);

                var outItem = item.DeepClone();
                outItem.CurrentPath = targetPath;
                outItem.PhysicalPath = targetPath;
                outItem.FileSizeBytes = new FileInfo(targetPath).Length;
                outItem.Metadata["AI:BackgroundRemoved"] = true;
                outItem.Metadata["AI:BackgroundModel"] = Path.GetFileNameWithoutExtension(modelPath);

                // Generar también la máscara aislada para el puerto Mask
                string maskFileName = Path.GetFileNameWithoutExtension(item.CurrentPath) + "_mask.png";
                string maskPath = Path.Combine(targetDir, maskFileName);

                using var maskImage = new Image<L8>(processedImage.Width, processedImage.Height);
                processedImage.ProcessPixelRows(maskImage, (srcAccessor, dstAccessor) =>
                {
                    for (int y = 0; y < srcAccessor.Height; y++)
                    {
                        var srcRow = srcAccessor.GetRowSpan(y);
                        var dstRow = dstAccessor.GetRowSpan(y);
                        for (int x = 0; x < srcRow.Length; x++)
                        {
                            dstRow[x] = new L8(srcRow[x].A);
                        }
                    }
                });
                await maskImage.SaveAsPngAsync(maskPath, cancellationToken).ConfigureAwait(false);

                var maskItem = item.DeepClone();
                maskItem.CurrentPath = maskPath;
                maskItem.PhysicalPath = maskPath;
                maskItem.FileSizeBytes = new FileInfo(maskPath).Length;
                maskItem.Metadata["AI:AlphaMaskGenerated"] = true;
                maskItem.Metadata["AI:BackgroundModel"] = Path.GetFileNameWithoutExtension(modelPath);

                context.Log($"[BackgroundRemover] ✅ Fondo procesado con éxito: '{targetFileName}' y máscara '{maskFileName}'", LogLevel.Information, outItem);

                await context.EmitAsync("Out", outItem).ConfigureAwait(false);
                await context.EmitAsync("Mask", maskItem).ConfigureAwait(false);
            }

            // Emitir siempre el archivo original tal cual por el puerto Bypass
            await context.EmitAsync("Bypass", item).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Log($"[BackgroundRemover] ❌ Error procesando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
