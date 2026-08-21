using System.Globalization;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using MetadataExtractor;
using SixLabors.ImageSharp;

namespace FileFlow.Plugin.Images;

[NodeDefinition("ExifMetadataNode_Name", "Images", "ExifMetadataNode_Desc")]
public class ExifMetadataNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ExifMetadataNode_Name", "EXIF Metadata");
    public string Category => "Images";
    public string Description => LocalizationManager.Instance.GetString("ExifMetadataNode_Desc", "Extracts EXIF metadata (Date Taken, Camera Model, Dimensions, Orientation) from images.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FallbackToCreationDate"] = true
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string filePath = item.CurrentPath;
        bool fallbackToCreation = Parameters.TryGetValue("FallbackToCreationDate", out var fVal) && ParameterHelper.GetBoolean(fVal, true);

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            context.Log($"ExifMetadataNode: File '{filePath}' not found.", LogLevel.Warning);
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
            context.Log($"Extracting EXIF metadata for: {filePath}", LogLevel.Information);
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            string? dateTaken = null;
            string? cameraModel = null;
            string? make = null;

            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    if (tag.Name.Equals("Date/Time Original", StringComparison.OrdinalIgnoreCase) ||
                        tag.Name.Equals("Date/Time", StringComparison.OrdinalIgnoreCase))
                    {
                        dateTaken ??= tag.Description;
                    }
                    else if (tag.Name.Equals("Model", StringComparison.OrdinalIgnoreCase))
                    {
                        cameraModel ??= tag.Description;
                    }
                    else if (tag.Name.Equals("Make", StringComparison.OrdinalIgnoreCase))
                    {
                        make ??= tag.Description;
                    }
                }
            }

            if (string.IsNullOrEmpty(dateTaken) && fallbackToCreation)
            {
                dateTaken = File.GetCreationTime(filePath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }

            item.Metadata["DateTaken"] = dateTaken ?? "Unknown";
            item.Metadata["CameraModel"] = cameraModel ?? "Unknown";
            item.Metadata["CameraMake"] = make ?? "Unknown";

            // Extract Image Dimensions and Orientation
            try
            {
                var info = Image.Identify(filePath);
                if (info != null)
                {
                    int width = info.Width;
                    int height = info.Height;
                    item.Metadata["ImageWidth"] = width;
                    item.Metadata["ImageHeight"] = height;
                    string orientation = width > height ? "Landscape" : (height > width ? "Portrait" : "Square");
                    item.Metadata["Orientation"] = orientation;
                    item.Metadata["AspectRatio"] = CalculateAspectRatio(width, height);
                    item.Metadata["Megapixels"] = ((width * (double)height) / 1_000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "MP";
                }
            }
            catch (Exception ex)
            {
                context.Log($"ExifMetadataNode: Could not read image dimensions: {ex.Message}", LogLevel.Warning);
            }

            context.Log($"EXIF Extracted - DateTaken: {dateTaken}, Model: {cameraModel}", LogLevel.Information);
            item.AddLog($"ExifMetadataNode extracted DateTaken={dateTaken}, Model={cameraModel}");
        }
        catch (Exception ex)
        {
            context.Log($"ExifMetadataNode warning reading '{filePath}': {ex.Message}", LogLevel.Warning);
            if (fallbackToCreation)
            {
                item.Metadata["DateTaken"] = File.GetCreationTime(filePath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
        }

        await context.EmitAsync("Out", item);
    }

    private static string CalculateAspectRatio(int width, int height)
    {
        int gcd = GCD(width, height);
        return gcd > 0 ? $"{width / gcd}:{height / gcd}" : $"{width}:{height}";
    }

    private static int GCD(int a, int b) => b == 0 ? a : GCD(b, a % b);
}
