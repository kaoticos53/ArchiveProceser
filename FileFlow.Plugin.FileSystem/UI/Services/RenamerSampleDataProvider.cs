using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.FileSystem.UI.Services;

/// <summary>
/// Modelo de transferencia para deserializar muestras sintéticas desde archivos JSON externos.
/// </summary>
public sealed class SyntheticSampleItemDto
{
    public string Directory { get; set; } = @"C:\Muestras";
    public string FileName { get; set; } = "archivo.dat";
    public long FileSizeBytes { get; set; } = 1024;
    public bool IsDirectory { get; set; } = false;
    public Dictionary<string, object?> Metadata { get; set; } = [];
}

/// <summary>
/// Proveedor de muestras de datos sintéticas y reales para previsualización en vivo dentro del plugin de renombrado.
/// Carga las muestras en cascada desde %AppData%, directorio Config/ o fallback en memoria.
/// </summary>
public static class RenamerSampleDataProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static List<FileItemContext> GetSampleItems(out string sourceDescription)
    {
        // 1. Intentar cargar desde el fichero de usuario en %AppData%/FileFlow/samples/renamer_samples.json
        AppPaths.EnsureDirectories();
        string appDataFile = AppPaths.RenamerSamplesFile;
        if (File.Exists(appDataFile))
        {
            var userSamples = TryLoadFromFile(appDataFile);
            if (userSamples != null && userSamples.Count > 0)
            {
                sourceDescription = $"({userSamples.Count} Muestras sintéticas cargadas desde {appDataFile})";
                return userSamples;
            }
        }

        // 2. Intentar cargar desde el directorio Config/ local de la aplicación o plugin
        string[] candidatePaths =
        [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "renamer_samples.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Config", "renamer_samples.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Config", "renamer_samples.json"),
            Path.Combine(AppContext.BaseDirectory, "Config", "renamer_samples.json")
        ];

        foreach (var path in candidatePaths.Distinct())
        {
            if (File.Exists(path))
            {
                var factorySamples = TryLoadFromFile(path);
                if (factorySamples != null && factorySamples.Count > 0)
                {
                    sourceDescription = $"({factorySamples.Count} Muestras sintéticas cargadas desde Config/renamer_samples.json)";
                    return factorySamples;
                }
            }
        }

        // 3. Fallback en memoria garantizado ante ausencia de archivos en entornos de prueba
        sourceDescription = "(18 Muestras sintéticas predefinidas en memoria)";
        return GetFallbackItems();
    }

    public static List<FileItemContext>? TryLoadFromFile(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            var dtos = JsonSerializer.Deserialize<List<SyntheticSampleItemDto>>(json, JsonOptions);
            if (dtos == null || dtos.Count == 0) return null;

            var items = new List<FileItemContext>(dtos.Count);
            foreach (var dto in dtos)
            {
                string virtualPath = Path.Combine(string.IsNullOrWhiteSpace(dto.Directory) ? @"C:\Muestras" : dto.Directory, dto.FileName);
                var item = new FileItemContext(virtualPath, dto.IsDirectory)
                {
                    FileSizeBytes = dto.FileSizeBytes
                };

                if (dto.Metadata != null)
                {
                    foreach (var (k, v) in dto.Metadata)
                    {
                        if (v is JsonElement elem)
                        {
                            item.Metadata[k] = elem.ValueKind switch
                            {
                                JsonValueKind.String => elem.GetString(),
                                JsonValueKind.Number when elem.TryGetInt64(out var l) => l,
                                JsonValueKind.Number => elem.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Null => null,
                                _ => elem.ToString()
                            };
                        }
                        else
                        {
                            item.Metadata[k] = v;
                        }
                    }
                }

                items.Add(item);
            }

            return items;
        }
        catch
        {
            return null;
        }
    }

    private static List<FileItemContext> GetFallbackItems()
    {
        return
        [
            CreateSyntheticItem(@"C:\Muestras\Fotografia", "DSC_0042.JPG", 4_194_304, false, new Dictionary<string, object?>
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
            CreateSyntheticItem(@"C:\Muestras\Fotografia", "IMG_20260901_120000.png", 1_048_576, false, new Dictionary<string, object?>
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
            CreateSyntheticItem(@"C:\Muestras\Fotografia", "_MG_9843.CR3", 35_651_584, false, new Dictionary<string, object?>
            {
                ["Exif:CameraModel"] = "Canon EOS R5",
                ["Exif:CameraMake"] = "Canon",
                ["Exif:DateTaken"] = "2026:07:20 18:45:10",
                ["Img:Width"] = 8192,
                ["Img:Height"] = 5464,
                ["Orientation"] = "Landscape",
                ["Megapixels"] = "44.8"
            }),
            CreateSyntheticItem(@"C:\Muestras\Video", "GOPR0125.MP4", 450_887_680, false, new Dictionary<string, object?>
            {
                ["Exif:CameraModel"] = "GoPro HERO12",
                ["Exif:DateTaken"] = "2026:08:10 10:15:00",
                ["Video:Width"] = 5312,
                ["Video:Height"] = 2988,
                ["Video:Duration"] = "00:04:15",
                ["AspectRatio"] = "16:9"
            }),
            CreateSyntheticItem(@"C:\Muestras\Series", "Breaking.Bad.S01E03.1080p.BluRay.x264-FLIX.mkv", 1_572_864_000, false, new Dictionary<string, object?>
            {
                ["Video:Width"] = 1920,
                ["Video:Height"] = 1080,
                ["Video:Duration"] = "00:48:12",
                ["AspectRatio"] = "16:9"
            }),
            CreateSyntheticItem(@"C:\Muestras\Series", "Stranger.Things.2x04.720p.HDTV.mp4", 629_145_600, false, new Dictionary<string, object?>
            {
                ["Video:Width"] = 1280,
                ["Video:Height"] = 720,
                ["Video:Duration"] = "00:52:40",
                ["AspectRatio"] = "16:9"
            }),
            CreateSyntheticItem(@"C:\Muestras\Video", "video_tutorial_parte_1_4k.mp4", 104_857_600, false, new Dictionary<string, object?>
            {
                ["Video:Width"] = 3840,
                ["Video:Height"] = 2160,
                ["Video:Duration"] = "00:15:30",
                ["AspectRatio"] = "16:9"
            }),
            CreateSyntheticItem(@"C:\Muestras\Musica", "01 - Bohemian Rhapsody.mp3", 8_388_608, false, new Dictionary<string, object?>
            {
                ["Audio:Artist"] = "Queen",
                ["Audio:Title"] = "Bohemian Rhapsody",
                ["Audio:Album"] = "A Night at the Opera",
                ["Audio:Year"] = 1975,
                ["Audio:Track"] = 1,
                ["Audio:Genre"] = "Rock"
            }),
            CreateSyntheticItem(@"C:\Muestras\Musica", "pink_floyd_-_06_-_money_(remastered).flac", 41_943_040, false, new Dictionary<string, object?>
            {
                ["Audio:Artist"] = "Pink Floyd",
                ["Audio:Title"] = "Money",
                ["Audio:Album"] = "The Dark Side of the Moon",
                ["Audio:Year"] = 1973,
                ["Audio:Track"] = 6,
                ["Audio:Genre"] = "Progressive Rock"
            }),
            CreateSyntheticItem(@"C:\Muestras\Podcasts", "Podcast_Ep12_Inteligencia_Artificial.m4a", 52_428_800, false, new Dictionary<string, object?>
            {
                ["Audio:Artist"] = "TechTalk Podcast",
                ["Audio:Title"] = "El Futuro de la IA Generativa",
                ["Audio:Album"] = "Temporada 2026",
                ["Audio:Year"] = 2026,
                ["Audio:Track"] = 12
            }),
            CreateSyntheticItem(@"C:\Muestras\Facturas", "FAC-2026-08-00124_ClienteACME_v1.2.pdf", 524_288, false, new Dictionary<string, object?>
            {
                ["CustomCategory"] = "Facturas",
                ["Hash:SHA256"] = "8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4",
                ["Hash:MD5"] = "c4ca4238a0b923820dcc509a6f75849b"
            }),
            CreateSyntheticItem(@"C:\Muestras\Finanzas", "informe trimestral Q2 2026 borrador.docx", 786_432, false, new Dictionary<string, object?>
            {
                ["CustomCategory"] = "Finanzas"
            }),
            CreateSyntheticItem(@"C:\Muestras\Contabilidad", "reporte_mensual_2026_08.xlsx", 262_144, false, new Dictionary<string, object?>
            {
                ["CustomCategory"] = "Reportes"
            }),
            CreateSyntheticItem(@"C:\Muestras\Corporativo", "Presentacion_Estrategia_Corporativa_2026.pptx", 5_242_880, false, new Dictionary<string, object?>
            {
                ["CustomCategory"] = "Presentaciones"
            }),
            CreateSyntheticItem(@"C:\Muestras\Descargas", "  mi.archivo.de.prueba...v1.0--FINAL(copia)  .pdf", 314_572, false, new Dictionary<string, object?>
            {
                ["CustomCategory"] = "Documentos"
            }),
            CreateSyntheticItem(@"C:\Muestras\Descargas", "DOCUMENTO CON ESPACIOS   MULTIPLES Y CARACTERES #%&.txt", 12_288, false, new Dictionary<string, object?>()),
            CreateSyntheticItem(@"C:\Muestras\Capitulos", "capitulo_1_introduccion.mp4", 83_886_080, false, new Dictionary<string, object?>
            {
                ["Video:Duration"] = "00:10:00"
            }),
            CreateSyntheticItem(@"C:\Muestras\Backups", "backup_database_production_20260830_full.tar.gz", 209_715_200, false, new Dictionary<string, object?>
            {
                ["Hash:SHA256"] = "9f83c68a0a8635fc950c441b439534f59e924a35cf9119159d33cb41b8a536c4"
            })
        ];
    }

    private static FileItemContext CreateSyntheticItem(string directory, string fileName, long sizeBytes, bool isDirectory, Dictionary<string, object?> metadata)
    {
        string virtualPath = Path.Combine(directory, fileName);
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
