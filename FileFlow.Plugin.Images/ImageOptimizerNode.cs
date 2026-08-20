using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace FileFlow.Plugin.Images;

[NodeDefinition("ImageOptimizerNode_Name", "Images", "ImageOptimizerNode_Desc")]
public class ImageOptimizerNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ImageOptimizerNode_Name", "Image Optimizer");
    public string Category => "Images";
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
        ["OutputDirectory"] = @"C:\FileFlowOptimized"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string filePath = item.CurrentPath;
        int maxWidth = Parameters.TryGetValue("MaxWidth", out var wVal) ? Convert.ToInt32(wVal) : 1920;
        int maxHeight = Parameters.TryGetValue("MaxHeight", out var hVal) ? Convert.ToInt32(hVal) : 1080;
        string formatStr = Parameters.TryGetValue("TargetFormat", out var fVal) ? fVal?.ToString() ?? "WebP" : "WebP";
        int quality = Parameters.TryGetValue("Quality", out var qVal) ? Convert.ToInt32(qVal) : 80;
        string outputDir = Parameters.TryGetValue("OutputDirectory", out var oVal) ? oVal?.ToString() ?? @"C:\FileFlowOptimized" : @"C:\FileFlowOptimized";
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && Convert.ToBoolean(dryVal);

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            context.Log($"ImageOptimizerNode: File '{filePath}' not found.", LogLevel.Warning);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            context.Log($"Optimizing image '{filePath}' -> MaxSize: {maxWidth}x{maxHeight}, Format: {formatStr}, Quality: {quality}", LogLevel.Information);

            string ext = formatStr.ToLowerInvariant() switch
            {
                "webp" => ".webp",
                "png" => ".png",
                _ => ".jpg"
            };

            string filenameNoExt = Path.GetFileNameWithoutExtension(filePath);
            string outputPath = Path.Combine(outputDir, $"{filenameNoExt}_optimized{ext}");

            if (!isDryRun)
            {
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                using Image image = await Image.LoadAsync(filePath, cancellationToken);

                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxWidth, maxHeight)
                }));

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

            var outputItem = new FileItemContext(outputPath, isDirectory: false);
            foreach (var kvp in item.Metadata)
            {
                outputItem.Metadata[kvp.Key] = kvp.Value;
            }
            outputItem.Metadata["OptimizedFormat"] = formatStr;
            outputItem.AddLog($"ImageOptimizerNode output saved to {outputPath}");

            await context.EmitAsync("Out", outputItem);
        }
        catch (Exception ex)
        {
            context.Log($"ImageOptimizerNode Error processing '{filePath}': {ex.Message}", LogLevel.Error);
            item.AddLog($"ImageOptimizerNode failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }
}
