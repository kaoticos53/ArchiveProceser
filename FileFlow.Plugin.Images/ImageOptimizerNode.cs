using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.Images;

[NodeDefinition("ImageOptimizerNode_Name", "ImageVision", "ImageOptimizerNode_Desc", PipelineRole.Transform,
    "imagen", "foto", "redimensionar", "optimizar", "comprimir", "webp", "jpeg", "png", "resize", "convert")]
public class ImageOptimizerNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ImageOptimizerNode_Name", "Image Optimizer");
    public string Category => "ImageVision";
    public string Description => LocalizationManager.Instance.GetString("ImageOptimizerNode_Desc", "Resizes images keeping aspect ratio and converts to modern formats.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Width"] = "",
        ["Height"] = "100%",
        ["TargetFormat"] = "WebP",
        ["Quality"] = 80,
        ["OnlyDownscale"] = true,
        ["OutputDirectory"] = @"{RelativeDir}\OptimizedImages"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("Width", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 1, HelpText: "Pixels (e.g. 1920) or Empty for automatic proportional calculation."),
        new("Height", ParameterEditorType.Text, DefaultValue: "100%", DisplayOrder: 2, HelpText: "Pixels (e.g. 1080) or Percentage (e.g. 100%, 50%)."),
        new("TargetFormat", ParameterEditorType.Dropdown, DefaultValue: "WebP", DisplayOrder: 3, Options: ["WebP", "JPEG", "PNG", "GIF"]),
        new("Quality", ParameterEditorType.Slider, DefaultValue: 80, DisplayOrder: 4, Min: 1, Max: 100, Step: 1),
        new("OnlyDownscale", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 5),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: @"{RelativeDir}\OptimizedImages", DisplayOrder: 6)
    ];

    public static (int Pixels, double? Percentage) ParseDimensionSpec(object? value)
    {
        if (value == null) return (0, null);

        if (value is int intVal) return (Math.Max(0, intVal), null);
        if (value is double dblVal) return ((int)Math.Round(Math.Max(0, dblVal)), null);
        if (value is float fltVal) return ((int)Math.Round(Math.Max(0, fltVal)), null);

        string str = value.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(str) || str.Equals("auto", StringComparison.OrdinalIgnoreCase) || str.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return (0, null);
        }

        if (str.EndsWith('%'))
        {
            string numPart = str[..^1].Trim();
            if (double.TryParse(numPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double pct))
            {
                return (0, Math.Max(0.01, pct));
            }
        }

        if (str.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            str = str[..^2].Trim();
        }

        if (int.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out int px))
        {
            return (Math.Max(0, px), null);
        }

        return (0, null);
    }

    public static (int TargetWidth, int TargetHeight, bool ResizeNeeded) CalculateTargetDimensions(
        int origWidth,
        int origHeight,
        object? widthSpec,
        object? heightSpec,
        bool onlyDownscale = true)
    {
        if (origWidth <= 0 || origHeight <= 0)
        {
            return (Math.Max(1, origWidth), Math.Max(1, origHeight), false);
        }

        var (widthPx, widthPct) = ParseDimensionSpec(widthSpec);
        var (heightPx, heightPct) = ParseDimensionSpec(heightSpec);

        int targetW = origWidth;
        int targetH = origHeight;

        // Caso 1: Ambos son porcentajes
        if (widthPct.HasValue && heightPct.HasValue)
        {
            targetW = Math.Max(1, (int)Math.Round(origWidth * (widthPct.Value / 100.0)));
            targetH = Math.Max(1, (int)Math.Round(origHeight * (heightPct.Value / 100.0)));
        }
        // Caso 2: Solo Width es porcentaje (Height automático / proporcional)
        else if (widthPct.HasValue && !heightPct.HasValue && heightPx <= 0)
        {
            double scale = widthPct.Value / 100.0;
            targetW = Math.Max(1, (int)Math.Round(origWidth * scale));
            targetH = Math.Max(1, (int)Math.Round(origHeight * scale));
        }
        // Caso 3: Solo Height es porcentaje (Width automático / proporcional)
        else if (heightPct.HasValue && !widthPct.HasValue && widthPx <= 0)
        {
            double scale = heightPct.Value / 100.0;
            targetH = Math.Max(1, (int)Math.Round(origHeight * scale));
            targetW = Math.Max(1, (int)Math.Round(origWidth * scale));
        }
        // Caso 4: Porcentaje en Width y Píxeles en Height
        else if (widthPct.HasValue && heightPx > 0)
        {
            targetW = Math.Max(1, (int)Math.Round(origWidth * (widthPct.Value / 100.0)));
            targetH = heightPx;
        }
        // Caso 5: Píxeles en Width y Porcentaje en Height
        else if (widthPx > 0 && heightPct.HasValue)
        {
            targetW = widthPx;
            targetH = Math.Max(1, (int)Math.Round(origHeight * (heightPct.Value / 100.0)));
        }
        // Caso 6: Solo Width en píxeles (Height calculado automáticamente manteniendo relación de aspecto)
        else if (widthPx > 0 && heightPx <= 0)
        {
            targetW = widthPx;
            targetH = Math.Max(1, (int)Math.Round((double)origHeight * widthPx / origWidth));
        }
        // Caso 7: Solo Height en píxeles (Width calculado automáticamente manteniendo relación de aspecto)
        else if (heightPx > 0 && widthPx <= 0)
        {
            targetH = heightPx;
            targetW = Math.Max(1, (int)Math.Round((double)origWidth * heightPx / origHeight));
        }
        // Caso 8: Ambos en píxeles (Bounding Box Fit preservando aspect ratio sin deformar)
        else if (widthPx > 0 && heightPx > 0)
        {
            double ratio = Math.Min((double)widthPx / origWidth, (double)heightPx / origHeight);
            targetW = Math.Max(1, (int)Math.Round(origWidth * ratio));
            targetH = Math.Max(1, (int)Math.Round(origHeight * ratio));
        }
        else
        {
            targetW = origWidth;
            targetH = origHeight;
        }

        // Apply OnlyDownscale restriction (No agrandar imágenes si ya son más pequeñas)
        if (onlyDownscale && targetW >= origWidth && targetH >= origHeight)
        {
            targetW = origWidth;
            targetH = origHeight;
        }

        bool resizeNeeded = (targetW != origWidth || targetH != origHeight);
        return (targetW, targetH, resizeNeeded);
    }

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string filePath = item.CurrentPath;

        // Soporte unificado de Width y Height (con migración transparente de parámetros legados)
        object? widthSpec = "1920";
        if (Parameters.TryGetValue("Width", out var wVal) && wVal != null)
            widthSpec = wVal;
        else if (Parameters.TryGetValue("MaxWidth", out var mwVal) && mwVal != null)
            widthSpec = mwVal;

        object? heightSpec = "1080";
        if (Parameters.TryGetValue("Height", out var hVal) && hVal != null)
            heightSpec = hVal;
        else if (Parameters.TryGetValue("MaxHeight", out var mhVal) && mhVal != null)
            heightSpec = mhVal;

        // Migración retrocompatible si venía SizeMode == "Percentage"
        if (Parameters.TryGetValue("SizeMode", out var smVal) &&
            string.Equals(smVal?.ToString(), "Percentage", StringComparison.OrdinalIgnoreCase))
        {
            if (Parameters.TryGetValue("ScalePercentage", out var spVal) && spVal != null)
            {
                widthSpec = $"{spVal}%";
                heightSpec = Parameters.TryGetValue("ScalePercentageY", out var spyVal) && spyVal != null ? $"{spyVal}%" : $"{spVal}%";
            }
        }

        bool onlyDownscale = !Parameters.TryGetValue("OnlyDownscale", out var odVal) || ParameterHelper.GetBoolean(odVal, true);

        string formatStr = Parameters.TryGetValue("TargetFormat", out var fVal) ? ParameterHelper.GetString(fVal, "WebP") : "WebP";
        int quality = Parameters.TryGetValue("Quality", out var qVal) ? ParameterHelper.GetInt32(qVal, 80) : 80;
        string outputPattern = Parameters.TryGetValue("OutputDirectory", out var oVal) ? ParameterHelper.GetString(oVal, @"{RelativeDir}\OptimizedImages") : @"{RelativeDir}\OptimizedImages";
        string outputDir = ParameterHelper.ResolveOutputPath(outputPattern, item);
        bool isDryRun = context.IsDryRun || (item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            context.Log($"[Optimizador Imágenes] Archivo de imagen no encontrado: '{filePath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            string ext = formatStr.ToLowerInvariant() switch
            {
                "webp" => ".webp",
                "png" => ".png",
                _ => ".jpg"
            };

            string filenameNoExt = Path.GetFileNameWithoutExtension(filePath);
            string outputPath = Path.Combine(outputDir, $"{filenameNoExt}_optimized{ext}");

            int origWidth = 0, origHeight = 0;
            int newWidth = 0, newHeight = 0;

            if (isDryRun)
            {
                context.RegisterPlannedAction(new PlannedAction(
                    Guid.NewGuid(),
                    Id,
                    Name,
                    PlannedOperationType.TransformMedia,
                    filePath,
                    outputPath,
                    $"Optimize image to {formatStr} (Quality={quality})",
                    item.FileSizeBytes
                ));
            }
            else
            {
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                using Image image = await Image.LoadAsync(filePath, cancellationToken);
                origWidth = image.Width;
                origHeight = image.Height;

                var (targetWidth, targetHeight, resizeNeeded) = CalculateTargetDimensions(
                    origWidth,
                    origHeight,
                    widthSpec,
                    heightSpec,
                    onlyDownscale);

                if (resizeNeeded)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Stretch,
                        Size = new Size(targetWidth, targetHeight)
                    }));
                }

                newWidth = image.Width;
                newHeight = image.Height;

                switch (formatStr.ToUpperInvariant())
                {
                    case "WEBP":
                        await image.SaveAsWebpAsync(outputPath, new WebpEncoder { Quality = quality }, cancellationToken);
                        break;
                    case "PNG":
                        await image.SaveAsPngAsync(outputPath, new PngEncoder(), cancellationToken);
                        break;
                    case "JPEG":
                    case "JPG":
                    default:
                        await image.SaveAsJpegAsync(outputPath, new JpegEncoder { Quality = quality }, cancellationToken);
                        break;
                }
            }

            sw.Stop();
            long newSizeBytes = (!isDryRun && File.Exists(outputPath)) ? new FileInfo(outputPath).Length : item.FileSizeBytes;
            double savedPct = item.FileSizeBytes > 0 && newSizeBytes > 0 ? (1.0 - ((double)newSizeBytes / item.FileSizeBytes)) * 100.0 : 0.0;

            var outputItem = new FileItemContext(outputPath, isDirectory: false);
            foreach (var kvp in item.Metadata)
            {
                outputItem.Metadata[kvp.Key] = kvp.Value;
            }
            outputItem.FileSizeBytes = newSizeBytes;
            outputItem.Metadata["OptimizedFormat"] = formatStr;
            outputItem.Metadata["OptimizedWidth"] = newWidth;
            outputItem.Metadata["OptimizedHeight"] = newHeight;
            outputItem.AddLog($"ImageOptimizerNode output saved to {outputPath}");

            string detailsJson = $"{{\"format\": \"{formatStr}\", \"quality\": {quality}, \"width\": \"{widthSpec}\", \"height\": \"{heightSpec}\", \"onlyDownscale\": {onlyDownscale.ToString().ToLowerInvariant()}, \"originalDimensions\": \"{origWidth}x{origHeight}\", \"optimizedDimensions\": \"{newWidth}x{newHeight}\", \"originalSizeBytes\": {item.FileSizeBytes}, \"optimizedSizeBytes\": {newSizeBytes}, \"savedPct\": {savedPct.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}}}";
            context.Log($"[Optimizador Imágenes] Optimizado ({formatStr} Q:{quality} {newWidth}x{newHeight}): '{Path.GetFileName(outputPath)}' (Ahorro: {savedPct:F1}%)", LogLevel.Information, outputItem, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            await context.EmitAsync("Out", outputItem);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"file\": \"{filePath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Optimizador Imágenes] Error al optimizar imagen: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"ImageOptimizerNode failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }
}
