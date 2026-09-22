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
/// Nodo de pipeline para segmentación de sujeto y eliminación de fondos con IA (RMBG-1.4 y MODNet).
/// Permite generar imágenes PNG con canal alfa transparente, reemplazo de color de fondo o máscaras aisladas.
/// </summary>
[NodeDefinition("BackgroundRemoverNode_Name", "ImageVision", "BackgroundRemoverNode_Desc", PipelineRole.Transform,
    "fondo", "recortar", "transparente", "png", "mascara", "alpha", "quitar fondo", "cutout")]
public sealed class BackgroundRemoverNode : AiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("BackgroundRemoverNode_Name", "Eliminador de Fondo IA");
    public override string Category => "ImageVision";
    public override string Description => LocalizationManager.Instance.GetString("BackgroundRemoverNode_Desc", "Segmenta el sujeto y elimina el fondo de imágenes con redes neuronales RMBG y MODNet.");
    public override AiTaskType TaskType => AiTaskType.BackgroundRemoval;

    public BackgroundRemoverNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Bypass", typeof(FileItemContext), PortDirection.Output, "Bypass"),
            new NodePort("Mask", typeof(FileItemContext), PortDirection.Output, "Mask"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Model"] = "Auto";
        Parameters["OutputMode"] = "TransparentPng";
        Parameters["BackgroundColor"] = "#FFFFFF";
        Parameters["OutputDirectory"] = "";
        Parameters["SkipIfExists"] = false;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "rmbg-1.4", "modnet"],
            HelpText: "Modelo neural para segmentación de fondo ('Auto' selecciona según hardware).", DisplayOrder: 1),
        new("OutputMode", ParameterEditorType.Dropdown, DefaultValue: "TransparentPng",
            Options: ["TransparentPng", "ColorBackground", "MaskOnly"],
            HelpText: "Formato de salida (PNG con canal alfa transparente, color sólido o solo máscara).", DisplayOrder: 2),
        new("BackgroundColor", ParameterEditorType.Text, DefaultValue: "#FFFFFF",
            HelpText: "Color de fondo hexadecimal (ej. #FFFFFF) si seleccionó 'ColorBackground'.", DisplayOrder: 3),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "",
            HelpText: "Carpeta de destino donde se guardarán las imágenes procesadas. Si se deja vacía, usa el directorio temporal con subcarpeta aleatoria anti-colisiones.", DisplayOrder: 4),
        new("SkipIfExists", ParameterEditorType.Toggle, DefaultValue: false,
            HelpText: "Si el archivo resultante ya existe en destino, omite la inferencia neural y reutiliza el archivo.", DisplayOrder: 5)
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
            Log(context, $"[BackgroundRemover] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_supportedExtensions.Contains(ext))
        {
            Log(context, $"[BackgroundRemover] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            await EmitAsync(context, item, "Bypass").ConfigureAwait(false);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
            return;
        }

        try
        {
            string outputMode = GetParameter("OutputMode", "TransparentPng");
            string bgColorHex = GetParameter("BackgroundColor", "#FFFFFF");
            string outputDirRaw = GetParameter("OutputDirectory", "{GlobalOutputDir}");
            bool skipIfExists = GetParameter("SkipIfExists", false);

            string targetDir = ParameterHelper.ResolveIntermediateOutputDir(outputDirRaw, item, context);
            await storage.CreateDirectoryAsync(targetDir, cancellationToken).ConfigureAwait(false);

            bool maskOnly = string.Equals(outputMode, "MaskOnly", StringComparison.OrdinalIgnoreCase);
            string targetFileName = maskOnly
                ? Path.GetFileNameWithoutExtension(item.CurrentPath) + "_mask.png"
                : Path.GetFileNameWithoutExtension(item.CurrentPath) + "_nobg.png";
            string targetPath = Path.Combine(targetDir, targetFileName);

            if (skipIfExists && await storage.FileExistsAsync(targetPath, cancellationToken).ConfigureAwait(false))
            {
                Log(context, $"[BackgroundRemover] ⏭️ El archivo de salida ya existe ('{targetFileName}'). Omitiendo inferencia.", LogLevel.Information, item);

                if (maskOnly)
                {
                    var maskItem = item.DeepClone();
                    maskItem.CurrentPath = targetPath;
                    maskItem.PhysicalPath = targetPath;
                    maskItem.FileSizeBytes = await storage.GetFileSizeAsync(targetPath, cancellationToken).ConfigureAwait(false);
                    maskItem.Metadata["AI:AlphaMaskGenerated"] = true;
                    await EmitAsync(context, maskItem, "Mask").ConfigureAwait(false);
                }
                else
                {
                    var outItem = item.DeepClone();
                    outItem.CurrentPath = targetPath;
                    outItem.PhysicalPath = targetPath;
                    outItem.FileSizeBytes = await storage.GetFileSizeAsync(targetPath, cancellationToken).ConfigureAwait(false);
                    outItem.Metadata["AI:BackgroundRemoved"] = true;
                    await EmitAsync(context, outItem).ConfigureAwait(false);

                    string maskFileName = Path.GetFileNameWithoutExtension(item.CurrentPath) + "_mask.png";
                    string maskPath = Path.Combine(targetDir, maskFileName);
                    if (await storage.FileExistsAsync(maskPath, cancellationToken).ConfigureAwait(false))
                    {
                        var maskItem = item.DeepClone();
                        maskItem.CurrentPath = maskPath;
                        maskItem.PhysicalPath = maskPath;
                        maskItem.FileSizeBytes = await storage.GetFileSizeAsync(maskPath, cancellationToken).ConfigureAwait(false);
                        maskItem.Metadata["AI:AlphaMaskGenerated"] = true;
                        await EmitAsync(context, maskItem, "Mask").ConfigureAwait(false);
                    }
                }

                await EmitAsync(context, item, "Bypass").ConfigureAwait(false);
                return;
            }

            string? modelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                Log(context, "[BackgroundRemover] ⚠️ Modelo de eliminación de fondo no disponible. El archivo se emite por Bypass.", LogLevel.Warning, item);
                await EmitAsync(context, item, "Bypass").ConfigureAwait(false);
                await EmitAsync(context, item, "Error").ConfigureAwait(false);
                return;
            }

            bool isDml = IsGpuAccelerated;
            if (isDml)
            {
                item.Metadata["AI:DirectMlAccelerated"] = true;
                item.Metadata["AI:Device"] = "GPU (DirectML)";
            }

            Log(context, $"[BackgroundRemover] ✂️ Eliminando fondo de '{item.FileName}'...", LogLevel.Information, item);

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

            using var inStream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            using var originalImage = await Image.LoadAsync<Rgba32>(inStream, cancellationToken).ConfigureAwait(false);

            using var processedImage = await Task.Run(
                () => OnnxInferenceEngine.RemoveBackground(modelPath, originalImage, bgColor, maskOnly),
                cancellationToken).ConfigureAwait(false);

            if (isDml)
            {
                item.Metadata["AI:DirectMlAccelerated"] = true;
                item.Metadata["AI:Device"] = "GPU (DirectML)";
            }

            if (maskOnly)
            {
                // Modo solo máscara (targetPath ya es el maskPath)
                await using (var outStream = await storage.OpenWriteAsync(targetPath, cancellationToken).ConfigureAwait(false))
                {
                    await processedImage.SaveAsPngAsync(outStream, cancellationToken).ConfigureAwait(false);
                }

                var maskItem = item.DeepClone();
                maskItem.CurrentPath = targetPath;
                maskItem.PhysicalPath = targetPath;
                maskItem.FileSizeBytes = await storage.GetFileSizeAsync(targetPath, cancellationToken).ConfigureAwait(false);
                maskItem.Metadata["AI:AlphaMaskGenerated"] = true;
                maskItem.Metadata["AI:BackgroundModel"] = Path.GetFileNameWithoutExtension(modelPath);

                Log(context, $"[BackgroundRemover] ✅ Máscara generada con éxito: '{targetFileName}'", LogLevel.Information, maskItem);
                await EmitAsync(context, maskItem, "Mask").ConfigureAwait(false);
            }
            else
            {
                // Modo imagen procesada (transparente o color)
                await using (var outStream = await storage.OpenWriteAsync(targetPath, cancellationToken).ConfigureAwait(false))
                {
                    await processedImage.SaveAsPngAsync(outStream, cancellationToken).ConfigureAwait(false);
                }

                var outItem = item.DeepClone();
                outItem.CurrentPath = targetPath;
                outItem.PhysicalPath = targetPath;
                outItem.RegisterVersion("NoBackground", targetPath);
                long origSizeBytes = item.FileSizeBytes > 0 ? item.FileSizeBytes : (await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false) ? await storage.GetFileSizeAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false) : 0);
                long newSizeBytes = await storage.GetFileSizeAsync(targetPath, cancellationToken).ConfigureAwait(false);
                outItem.FileSizeBytes = newSizeBytes;
                outItem.Metadata["OriginalFileSize"] = origSizeBytes;
                outItem.Metadata["OriginalFileSizeBytes"] = origSizeBytes;
                outItem.Metadata["OutputFileSize"] = newSizeBytes;
                outItem.Metadata["OutputFileSizeBytes"] = newSizeBytes;
                outItem.Metadata["SavedBytes"] = origSizeBytes - newSizeBytes;
                outItem.Metadata["SavedPercent"] = origSizeBytes > 0 ? Math.Round((1.0 - ((double)newSizeBytes / origSizeBytes)) * 100.0, 2) : 0.0;
                outItem.Metadata["CompressionRatio"] = origSizeBytes > 0 ? Math.Round((double)newSizeBytes / origSizeBytes, 4) : 1.0;
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

                await using (var maskStream = await storage.OpenWriteAsync(maskPath, cancellationToken).ConfigureAwait(false))
                {
                    await maskImage.SaveAsPngAsync(maskStream, cancellationToken).ConfigureAwait(false);
                }

                var maskItem = item.DeepClone();
                maskItem.CurrentPath = maskPath;
                maskItem.PhysicalPath = maskPath;
                maskItem.FileSizeBytes = await storage.GetFileSizeAsync(maskPath, cancellationToken).ConfigureAwait(false);
                maskItem.Metadata["AI:AlphaMaskGenerated"] = true;
                maskItem.Metadata["AI:BackgroundModel"] = Path.GetFileNameWithoutExtension(modelPath);

                Log(context, $"[BackgroundRemover] ✅ Fondo procesado con éxito: '{targetFileName}' y máscara '{maskFileName}'", LogLevel.Information, outItem);

                await EmitAsync(context, outItem).ConfigureAwait(false);
                await EmitAsync(context, maskItem, "Mask").ConfigureAwait(false);
            }

            // Emitir siempre el archivo original tal cual por el puerto Bypass
            await EmitAsync(context, item, "Bypass").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[BackgroundRemover] ❌ Error procesando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }
}
