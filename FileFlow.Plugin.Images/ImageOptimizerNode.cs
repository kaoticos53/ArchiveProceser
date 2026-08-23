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
        ["MaxWidth"] = 1920,
        ["MaxHeight"] = 1080,
        ["TargetFormat"] = "WebP",
        ["Quality"] = 80,
        ["OutputDirectory"] = @"{RelativeDir}\OptimizedImages"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string filePath = item.CurrentPath;
        int maxWidth = Parameters.TryGetValue("MaxWidth", out var wVal) ? ParameterHelper.GetInt32(wVal, 1920) : 1920;
        int maxHeight = Parameters.TryGetValue("MaxHeight", out var hVal) ? ParameterHelper.GetInt32(hVal, 1080) : 1080;
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

                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxWidth, maxHeight)
                }));

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
            outputItem.AddLog($"ImageOptimizerNode output saved to {outputPath}");

            string detailsJson = $"{{\"format\": \"{formatStr}\", \"quality\": {quality}, \"originalDimensions\": \"{origWidth}x{origHeight}\", \"optimizedDimensions\": \"{newWidth}x{newHeight}\", \"originalSizeBytes\": {item.FileSizeBytes}, \"optimizedSizeBytes\": {newSizeBytes}, \"savedPct\": {savedPct.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}}}";
            context.Log($"[Optimizador Imágenes] Optimizado ({formatStr} Q:{quality}): '{Path.GetFileName(outputPath)}' (Ahorro: {savedPct:F1}%)", LogLevel.Information, outputItem, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

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
