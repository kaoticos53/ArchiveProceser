using FileFlow.Sdk;

namespace FileFlow.Plugin.FileSystem.UI.Services;

/// <summary>
/// Proveedor de muestras de datos sintéticas y reales para previsualización en vivo dentro del plugin de renombrado.
/// </summary>
public static class RenamerSampleDataProvider
{
    public static List<FileItemContext> GetSampleItems(out string sourceDescription)
    {
        sourceDescription = "(Muestras sintéticas predefinidas)";
        return
        [
            CreateSyntheticItem("DSC_0042.JPG", 4_194_304, false, new Dictionary<string, object?>
            {
                ["Exif:CameraModel"] = "Nikon D850",
                ["Exif:CameraMake"] = "Nikon",
                ["Exif:DateTaken"] = "2026:08:15 14:32:05",
                ["Img:Width"] = 8256,
                ["Img:Height"] = 5504,
                ["Orientation"] = "Landscape",
                ["AspectRatio"] = "3:2",
                ["Megapixels"] = "45.4",
                ["Hash:SHA256"] = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
            }),
            CreateSyntheticItem("IMG_20260901_120000.png", 1_048_576, false, new Dictionary<string, object?>
            {
                ["Exif:CameraModel"] = "iPhone 15 Pro",
                ["Exif:CameraMake"] = "Apple",
                ["Exif:DateTaken"] = "2026:09:01 12:00:00",
                ["Img:Width"] = 4032,
                ["Img:Height"] = 3024,
                ["Orientation"] = "Landscape",
                ["AspectRatio"] = "4:3",
                ["Megapixels"] = "12.2"
            }),
            CreateSyntheticItem("documento confidencial v2 final.pdf", 524_288, false, new Dictionary<string, object?>
            {
                ["CustomCategory"] = "Finanzas",
                ["Hash:SHA256"] = "8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4"
            }),
            CreateSyntheticItem("01 - Bohemian Rhapsody.mp3", 8_388_608, false, new Dictionary<string, object?>
            {
                ["Audio:Artist"] = "Queen",
                ["Audio:Title"] = "Bohemian Rhapsody",
                ["Audio:Album"] = "A Night at the Opera",
                ["Audio:Year"] = 1975,
                ["Audio:Track"] = 1
            }),
            CreateSyntheticItem("video_tutorial_parte_1_4k.mp4", 104_857_600, false, new Dictionary<string, object?>
            {
                ["Video:Width"] = 3840,
                ["Video:Height"] = 2160,
                ["Video:Duration"] = "00:15:30",
                ["AspectRatio"] = "16:9"
            }),
            CreateSyntheticItem("reporte_mensual_2026_08.xlsx", 262_144, false, new Dictionary<string, object?>
            {
                ["CustomCategory"] = "Reportes"
            })
        ];
    }

    private static FileItemContext CreateSyntheticItem(string fileName, long sizeBytes, bool isDirectory, Dictionary<string, object?> metadata)
    {
        string virtualPath = Path.Combine(@"C:\Muestras\Fotos", fileName);
        var item = new FileItemContext(virtualPath, isDirectory)
        {
            FileSizeBytes = sizeBytes
        };

        foreach (var (k, v) in metadata)
        {
            item.Metadata[k] = v;
        }

        return item;
    }
}
