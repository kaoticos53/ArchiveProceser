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
/// Nodo de super-resolución y restauración neural de imágenes y documentos antiguos con Real-ESRGAN Compact.
/// Escala y reconstruye detalles visuales a 2x o 4x previniendo pixelado y artefactos de compresión.
/// </summary>
[NodeDefinition("SuperResolutionUpscalerNode_Name", "ImageVision", "SuperResolutionUpscalerNode_Desc", PipelineRole.Transform,
    "super resolucion", "escalar", "aumentar", "upscale", "4x", "realesrgan", "calidad", "nitidez")]
public sealed class SuperResolutionUpscalerNode : AiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("SuperResolutionUpscalerNode_Name", "Super-Resolución IA");
    public override string Category => "ImageVision";
    public override string Description => LocalizationManager.Instance.GetString("SuperResolutionUpscalerNode_Desc", "Escala y restaura imágenes o documentos de baja resolución con modelos Real-ESRGAN.");
    public override AiTaskType TaskType => AiTaskType.SuperResolution;

    public SuperResolutionUpscalerNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Skipped", typeof(FileItemContext), PortDirection.Output, "Skipped"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Model"] = "Auto";
        Parameters["ScaleFactor"] = "4x";
        Parameters["MaxInputDimension"] = 2048;
        Parameters["OutputDirectory"] = "";
        Parameters["SkipIfExists"] = false;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "realesrgan-compact"],
            HelpText: "Modelo neural de super-resolución ('Auto' selecciona según hardware).", DisplayOrder: 1),
        new("ScaleFactor", ParameterEditorType.Dropdown, DefaultValue: "4x",
            Options: ["2x", "4x"],
            HelpText: "Factor de aumento de resolución.", DisplayOrder: 2),
        new("MaxInputDimension", ParameterEditorType.Number, DefaultValue: 2048, Min: 256, Max: 8192,
            HelpText: "Límite máximo de ancho/alto original para prevenir consumo excesivo de RAM.", DisplayOrder: 3),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "",
            HelpText: "Carpeta de destino donde se guardarán las imágenes escaladas. Si se deja vacía, usa el directorio temporal con subcarpeta aleatoria anti-colisiones.", DisplayOrder: 4),
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
            Log(context, $"[SuperResolution] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_supportedExtensions.Contains(ext))
        {
            Log(context, $"[SuperResolution] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            await EmitAsync(context, item, "Skipped").ConfigureAwait(false);
            return;
        }

        try
        {
            string scaleStr = GetParameter("ScaleFactor", "4x");
            int maxDim = GetParameter("MaxInputDimension", 2048);
            string outputDirRaw = GetParameter("OutputDirectory", "{GlobalOutputDir}");
            bool skipIfExists = GetParameter("SkipIfExists", false);

            string targetDir = ParameterHelper.ResolveIntermediateOutputDir(outputDirRaw, item, context);
            await storage.CreateDirectoryAsync(targetDir, cancellationToken).ConfigureAwait(false);

            string targetFileName = $"{Path.GetFileNameWithoutExtension(item.CurrentPath)}_upscaled{ext}";
            string targetPath = Path.Combine(targetDir, targetFileName);

            if (skipIfExists && await storage.FileExistsAsync(targetPath, cancellationToken).ConfigureAwait(false))
            {
                Log(context, $"[SuperResolution] ⏭️ El archivo de salida ya existe ('{targetFileName}'). Omitiendo inferencia.", LogLevel.Information, item);
                var existingItem = item.DeepClone();
                existingItem.CurrentPath = targetPath;
                existingItem.PhysicalPath = targetPath;
                existingItem.FileSizeBytes = await storage.GetFileSizeAsync(targetPath, cancellationToken).ConfigureAwait(false);
                existingItem.Metadata["AI:SuperResolution"] = true;
                await EmitAsync(context, existingItem).ConfigureAwait(false);
                return;
            }

            int requestedScale = scaleStr.Contains("2") ? 2 : 4;

            using var inStream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            using var image = await Image.LoadAsync<Rgb24>(inStream, cancellationToken).ConfigureAwait(false);
            int origW = image.Width;
            int origH = image.Height;

            // Verificar si excede el tamaño máximo
            if (origW > maxDim || origH > maxDim)
            {
                Log(context, $"[SuperResolution] ⏭️ Imagen omitida ({origW}x{origH} > límite {maxDim}px) para evitar saturación de memoria.", LogLevel.Information, item);
                await EmitAsync(context, item, "Skipped").ConfigureAwait(false);
                return;
            }

            string? modelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                Log(context, "[SuperResolution] ⚠️ Modelo de super-resolución no disponible. Se emite sin procesar.", LogLevel.Warning, item);
                await EmitAsync(context, item, "Skipped").ConfigureAwait(false);
                return;
            }

            Log(context, $"[SuperResolution] 🔍 Escalando {scaleStr} ({origW}x{origH}) para '{item.FileName}'...", LogLevel.Information, item);

            using var upscaledImage = await Task.Run(
                () => OnnxInferenceEngine.UpscaleImage(modelPath, image, requestedScale),
                cancellationToken).ConfigureAwait(false);

            int newW = upscaledImage.Width;
            int newH = upscaledImage.Height;

            await using (var outStream = await storage.OpenWriteAsync(targetPath, cancellationToken).ConfigureAwait(false))
            {
                var encoder = GetEncoder(ext);
                await upscaledImage.SaveAsync(outStream, encoder, cancellationToken).ConfigureAwait(false);
            }

            var newItem = item.DeepClone();
            newItem.CurrentPath = targetPath;
            newItem.PhysicalPath = targetPath;
            newItem.RegisterVersion("SuperResolution", targetPath);
            long origSizeBytes = item.FileSizeBytes > 0 ? item.FileSizeBytes : (await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false) ? await storage.GetFileSizeAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false) : 0);
            long newSizeBytes = await storage.GetFileSizeAsync(targetPath, cancellationToken).ConfigureAwait(false);
            newItem.FileSizeBytes = newSizeBytes;
            newItem.Metadata["OriginalFileSize"] = origSizeBytes;
            newItem.Metadata["OriginalFileSizeBytes"] = origSizeBytes;
            newItem.Metadata["OutputFileSize"] = newSizeBytes;
            newItem.Metadata["OutputFileSizeBytes"] = newSizeBytes;
            newItem.Metadata["SavedBytes"] = origSizeBytes - newSizeBytes;
            newItem.Metadata["SavedPercent"] = origSizeBytes > 0 ? Math.Round((1.0 - ((double)newSizeBytes / origSizeBytes)) * 100.0, 2) : 0.0;
            newItem.Metadata["CompressionRatio"] = origSizeBytes > 0 ? Math.Round((double)newSizeBytes / origSizeBytes, 4) : 1.0;
            newItem.Metadata["AI:Upscaled"] = true;
            newItem.Metadata["AI:OriginalResolution"] = $"{origW}x{origH}";
            newItem.Metadata["AI:NewResolution"] = $"{newW}x{newH}";
            newItem.Metadata["AI:ScaleFactor"] = scaleStr;
            newItem.Metadata["AI:UpscalerModel"] = Path.GetFileNameWithoutExtension(modelPath);

            Log(context, $"[SuperResolution] ✅ Escalado a {newW}x{newH} completado: '{targetFileName}'", LogLevel.Information, newItem);
            await EmitAsync(context, newItem).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[SuperResolution] ❌ Error escalando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }

    private static SixLabors.ImageSharp.Formats.IImageEncoder GetEncoder(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder(),
        ".webp" => new SixLabors.ImageSharp.Formats.Webp.WebpEncoder(),
        ".bmp" => new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder(),
        ".tiff" => new SixLabors.ImageSharp.Formats.Tiff.TiffEncoder(),
        _ => new SixLabors.ImageSharp.Formats.Png.PngEncoder()
    };
}
