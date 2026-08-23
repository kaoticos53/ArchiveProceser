using System.Globalization;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using MetadataExtractor;
using SixLabors.ImageSharp;

namespace FileFlow.Plugin.Images;

[NodeDefinition("ExifMetadataNode_Name", "Metadata", "ExifMetadataNode_Desc")]
public class ExifMetadataNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("ExifMetadataNode_Name", "EXIF Metadata");
    public string Category => "Metadata";
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

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            context.Log($"[Metadatos EXIF] Archivo de imagen no encontrado: '{filePath}'", LogLevel.Warning, item);
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
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

            int imgWidth = 0, imgHeight = 0;
            string orientation = "Unknown";
            string megapixels = string.Empty;

            // Extract Image Dimensions and Orientation
            try
            {
                var info = Image.Identify(filePath);
                if (info != null)
                {
                    imgWidth = info.Width;
                    imgHeight = info.Height;
                    item.Metadata["ImageWidth"] = imgWidth;
                    item.Metadata["ImageHeight"] = imgHeight;
                    orientation = imgWidth > imgHeight ? "Landscape" : (imgHeight > imgWidth ? "Portrait" : "Square");
                    item.Metadata["Orientation"] = orientation;
                    item.Metadata["AspectRatio"] = CalculateAspectRatio(imgWidth, imgHeight);
                    megapixels = ((imgWidth * (double)imgHeight) / 1_000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "MP";
                    item.Metadata["Megapixels"] = megapixels;
                }
            }
            catch (Exception ex)
            {
                context.Log($"[Metadatos EXIF] Dimensiones gráficas no legibles: {ex.Message}", LogLevel.Debug, item);
            }

            sw.Stop();
            string detailsJson = $"{{\"dateTaken\": \"{dateTaken ?? "N/A"}\", \"cameraModel\": \"{cameraModel ?? "N/A"}\", \"make\": \"{make ?? "N/A"}\", \"resolution\": \"{imgWidth}x{imgHeight}\", \"orientation\": \"{orientation}\", \"megapixels\": \"{megapixels}\"}}";
            context.Log($"[Metadatos EXIF] Extraído ({cameraModel ?? "Cámara Desconocida"}): {dateTaken ?? "Sin fecha"} • {imgWidth}x{imgHeight} ({orientation})", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);
            item.AddLog($"ExifMetadataNode extracted DateTaken={dateTaken}, Model={cameraModel}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\"}}";
            context.Log($"[Metadatos EXIF] Advertencia al leer EXIF: {ex.Message}", LogLevel.Warning, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
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
