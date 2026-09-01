using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.Images;

[NodeDefinition("ImageOptimizerNode_Name", "MediaDocs", "ImageOptimizerNode_Desc")]
public class ImageOptimizerNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ImageOptimizerNode_Name", "Image Optimizer");
    public string Category => "MediaDocs";
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
        ["SizeMode"] = "Pixels",
        ["Width"] = 1920,
        ["Height"] = 1080,
        ["ScalePercentage"] = 100.0,
        ["ScalePercentageY"] = 100.0,
        ["MaintainAspectRatio"] = true,
        ["OnlyDownscale"] = true,
        ["TargetFormat"] = "WebP",
        ["Quality"] = 80,
        ["OutputDirectory"] = @"{RelativeDir}\OptimizedImages"
    };

    public static (int TargetWidth, int TargetHeight, bool ResizeNeeded) CalculateTargetDimensions(
        int origWidth,
        int origHeight,
        string sizeMode,
        int width,
        int height,
        double scalePercentage,
        double scalePercentageY,
        bool maintainAspectRatio,
        bool onlyDownscale)
    {
        if (origWidth <= 0 || origHeight <= 0)
        {
            return (Math.Max(1, origWidth), Math.Max(1, origHeight), false);
        }

        int targetW = origWidth;
        int targetH = origHeight;

        if (string.Equals(sizeMode, "Percentage", StringComparison.OrdinalIgnoreCase))
        {
            double scaleX = Math.Max(0.01, scalePercentage) / 100.0;
            double scaleY = maintainAspectRatio ? scaleX : Math.Max(0.01, scalePercentageY) / 100.0;

            targetW = Math.Max(1, (int)Math.Round(origWidth * scaleX));
            targetH = Math.Max(1, (int)Math.Round(origHeight * scaleY));
        }
        else // "Pixels" (default)
        {
            if (width > 0 && height <= 0)
            {
                targetW = width;
                targetH = maintainAspectRatio
                    ? Math.Max(1, (int)Math.Round((double)origHeight * width / origWidth))
                    : origHeight;
            }
            else if (height > 0 && width <= 0)
            {
                targetH = height;
                targetW = maintainAspectRatio
                    ? Math.Max(1, (int)Math.Round((double)origWidth * height / origHeight))
                    : origWidth;
            }
            else if (width > 0 && height > 0)
            {
                if (maintainAspectRatio)
                {
                    double ratio = Math.Min((double)width / origWidth, (double)height / origHeight);
                    targetW = Math.Max(1, (int)Math.Round(origWidth * ratio));
                    targetH = Math.Max(1, (int)Math.Round(origHeight * ratio));
                }
                else
                {
                    targetW = width;
                    targetH = height;
                }
            }
            else
            {
                // Width <= 0 && Height <= 0 -> keep original dimensions
                targetW = origWidth;
                targetH = origHeight;
            }
        }

        // Apply OnlyDownscale restriction (No agrandar imágenes más pequeñas)
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

        string sizeMode = Parameters.TryGetValue("SizeMode", out var smVal) ? ParameterHelper.GetString(smVal, "Pixels") : "Pixels";
        
        int width = 0;
        if (Parameters.TryGetValue("Width", out var wVal))
            width = ParameterHelper.GetInt32(wVal, 0);
        else if (Parameters.TryGetValue("MaxWidth", out var mwVal))
            width = ParameterHelper.GetInt32(mwVal, 0);

        int height = 0;
        if (Parameters.TryGetValue("Height", out var hVal))
            height = ParameterHelper.GetInt32(hVal, 0);
        else if (Parameters.TryGetValue("MaxHeight", out var mhVal))
            height = ParameterHelper.GetInt32(mhVal, 0);

        double scalePercentage = 100.0;
        if (Parameters.TryGetValue("ScalePercentage", out var spVal) && spVal != null)
        {
            if (spVal is double d) scalePercentage = d;
            else if (spVal is float f) scalePercentage = f;
            else if (spVal is int i) scalePercentage = i;
            else double.TryParse(spVal.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out scalePercentage);
        }

        double scalePercentageY = scalePercentage;
        if (Parameters.TryGetValue("ScalePercentageY", out var spyVal) && spyVal != null)
        {
            if (spyVal is double d) scalePercentageY = d;
            else if (spyVal is float f) scalePercentageY = f;
            else if (spyVal is int i) scalePercentageY = i;
            else double.TryParse(spyVal.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out scalePercentageY);
        }

        bool maintainAspectRatio = !Parameters.TryGetValue("MaintainAspectRatio", out var arVal) || ParameterHelper.GetBoolean(arVal, true);
        bool onlyDownscale = !Parameters.TryGetValue("OnlyDownscale", out var odVal) || ParameterHelper.GetBoolean(odVal, true);

        string formatStr = Parameters.TryGetValue("TargetFormat", out var fVal) ? ParameterHelper.GetString(fVal, "WebP") : "WebP";
        int quality = Parameters.TryGetValue("Quality", out var qVal) ? ParameterHelper.GetInt32(qVal, 80) : 80;
        string outputPattern = Parameters.TryGetValue("OutputDirectory", out var oVal) ? ParameterHelper.GetString(oVal, @"{RelativeDir}\OptimizedImages") : @"{RelativeDir}\OptimizedImages";
        string outputDir = ParameterHelper.ResolveOutputPath(outputPattern, item);
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);

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

            if (!isDryRun)
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
                    sizeMode,
                    width,
                    height,
                    scalePercentage,
                    scalePercentageY,
                    maintainAspectRatio,
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
            long newSizeBytes = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
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

            string detailsJson = $"{{\"format\": \"{formatStr}\", \"quality\": {quality}, \"sizeMode\": \"{sizeMode}\", \"maintainAspectRatio\": {maintainAspectRatio.ToString().ToLowerInvariant()}, \"onlyDownscale\": {onlyDownscale.ToString().ToLowerInvariant()}, \"originalDimensions\": \"{origWidth}x{origHeight}\", \"optimizedDimensions\": \"{newWidth}x{newHeight}\", \"originalSizeBytes\": {item.FileSizeBytes}, \"optimizedSizeBytes\": {newSizeBytes}, \"savedPct\": {savedPct.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}}}";
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
