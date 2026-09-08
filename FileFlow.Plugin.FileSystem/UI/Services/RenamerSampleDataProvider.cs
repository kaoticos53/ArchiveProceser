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
    public string Category { get; set; } = "General";
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

    private static readonly List<string> _inMemoryCustomSamples = [];
    private static readonly Lock _customSamplesLock = new();

    public static IReadOnlyList<string> AvailableCategories
    {
        get
        {
            var list = new List<string>
            {
                "Todas",
                "Películas",
                "Series",
                "Cómics y Manga",
                "Música",
                "Fotos",
                "Documentos",
                "Personalizada"
            };

            try
            {
                var userSets = FileFlow.Plugin.FileSystem.Services.SyntheticDataSetStorageService.Instance.GetAllDataSets().Where(d => !d.IsBuiltIn);
                foreach (var ds in userSets)
                {
                    if (!list.Contains(ds.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(ds.Name);
                    }
                }
            }
            catch
            {
                // Fallback a lista básica
            }

            return list;
        }
    }

    public static void AddCustomSample(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        lock (_customSamplesLock)
        {
            if (!_inMemoryCustomSamples.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                _inMemoryCustomSamples.Insert(0, fileName.Trim());
            }
        }
    }

    public static void ClearCustomSamples()
    {
        lock (_customSamplesLock)
        {
            _inMemoryCustomSamples.Clear();
        }
    }

    public static IReadOnlyList<string> GetCustomSamples()
    {
        lock (_customSamplesLock)
        {
            return _inMemoryCustomSamples.ToList();
        }
    }

    public static List<FileItemContext> GetSampleItems(out string sourceDescription)
    {
        return GetSampleItemsByCategory("Todas", out sourceDescription);
    }

    public static List<FileItemContext> GetSampleItemsByCategory(string? category, out string sourceDescription)
    {
        var allItems = LoadAllBaseItems(out var baseDesc);

        // Si se solicita expresamente 'Personalizada' o 'Manuales'
        if (string.Equals(category, "Personalizada", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "Manuales", StringComparison.OrdinalIgnoreCase))
        {
            var customList = GetCustomSamples();
            var customItems = new List<FileItemContext>(customList.Count);
            foreach (var fn in customList)
            {
                var it = CreateSyntheticItem(@"C:\Muestras\Personalizadas", fn, 1024, false, new Dictionary<string, object?>
                {
                    ["Category"] = "Personalizada",
                    ["VirtualSample"] = true,
                    ["IsManualSample"] = true
                });
                customItems.Add(it);
            }

            sourceDescription = $"({customItems.Count} Muestras manuales personalizadas)";
            return customItems;
        }

        // Si se solicita una categoría específica
        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "Todas", StringComparison.OrdinalIgnoreCase))
        {
            var filtered = allItems.Where(i =>
            {
                if (i.Metadata.TryGetValue("Category", out var cVal) && cVal is string catStr)
                {
                    return string.Equals(catStr, category, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }).ToList();

            if (filtered.Count == 0)
            {
                var customDs = FileFlow.Plugin.FileSystem.Services.SyntheticDataSetStorageService.Instance.GetDataSetByName(category)
                               ?? FileFlow.Plugin.FileSystem.Services.SyntheticDataSetStorageService.Instance.GetDataSetById(category);
                if (customDs != null)
                {
                    var dsItems = new List<FileItemContext>(customDs.Items.Count);
                    foreach (var it in customDs.Items)
                    {
                        var fic = CreateSyntheticItem(@"C:\Muestras\" + customDs.Category, it.FileName, it.FileSizeBytes, it.IsDirectory, it.Metadata);
                        fic.Metadata["RelativePath"] = it.RelativePath;
                        fic.Metadata["RelativeDir"] = it.Directory;
                        dsItems.Add(fic);
                    }
                    sourceDescription = $"({dsItems.Count} Muestras del dataset '{customDs.Name}')";
                    return dsItems;
                }
            }

            sourceDescription = $"({filtered.Count} Muestras de la categoría '{category}')";
            return filtered;
        }

        // Si se solicitan 'Todas', incluir también las muestras manuales al principio
        var combined = new List<FileItemContext>();
        var manuals = GetCustomSamples();
        foreach (var m in manuals)
        {
            combined.Add(CreateSyntheticItem(@"C:\Muestras\Personalizadas", m, 1024, false, new Dictionary<string, object?>
            {
                ["Category"] = "Personalizada",
                ["VirtualSample"] = true,
                ["IsManualSample"] = true
            }));
        }
        combined.AddRange(allItems);

        sourceDescription = manuals.Count > 0
            ? $"({combined.Count} Muestras: {allItems.Count} de catálogo + {manuals.Count} manuales)"
            : baseDesc;

        return combined;
    }

    private static List<FileItemContext> LoadAllBaseItems(out string sourceDescription)
    {
        // 1. Intentar cargar desde el fichero de usuario en %AppData%/FileFlow/samples/renamer_samples.json
        AppPaths.EnsureDirectories();
        string appDataFile = AppPaths.RenamerSamplesFile;
        if (File.Exists(appDataFile))
        {
            var userSamples = TryLoadFromFile(appDataFile);
            if (userSamples != null && userSamples.Count > 0)
            {
                sourceDescription = $"({userSamples.Count} Muestras cargadas desde {appDataFile})";
                return userSamples;
            }
        }

        // 2. Intentar cargar desde el directorio Config/ local de la aplicación o plugin
        string[] candidatePaths =
        [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "renamer_samples.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Config", "renamer_samples.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Config", "renamer_samples.json"),
            Path.Combine(AppContext.BaseDirectory, "Config", "renamer_samples.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Config", "renamer_samples.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "FileFlow.Plugin.FileSystem", "Config", "renamer_samples.json")
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
        sourceDescription = "(Muestras sintéticas categorizadas predefinidas en memoria)";
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

                if (!string.IsNullOrWhiteSpace(dto.Category))
                {
                    item.Metadata["Category"] = dto.Category;
                }

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
            // Películas
            CreateSyntheticItem(@"C:\Muestras\Peliculas", "[ Torrent9.sh ] Gladiator.II.2024.1080p.WEBRip.x264.Dual.Latino-Castellano-YIFY.mp4", 2_147_483_648, false, new Dictionary<string, object?>
            {
                ["Category"] = "Películas",
                ["MediaType"] = "Movie",
                ["VirtualSample"] = true
            }),
            CreateSyntheticItem(@"C:\Muestras\Peliculas", "Dune.Part.Two.2024.2160p.UHD.HDR.DV.TrueHD.7.1.Atmos-SWTYBLZ_[rarbg.to].mkv", 4_294_967_296, false, new Dictionary<string, object?>
            {
                ["Category"] = "Películas",
                ["MediaType"] = "Movie",
                ["VirtualSample"] = true
            }),
            CreateSyntheticItem(@"C:\Muestras\Peliculas", "Oppenheimer (2023) [720p] [BluRay] [YTS.MX] [English].mp4", 1_073_741_824, false, new Dictionary<string, object?>
            {
                ["Category"] = "Películas",
                ["MediaType"] = "Movie",
                ["VirtualSample"] = true
            }),

            // Series
            CreateSyntheticItem(@"C:\Muestras\Series", "Breaking.Bad.S05E16.Felina.1080p.BluRay.x264-ROVERS[rarbg.to].mkv", 1_572_864_000, false, new Dictionary<string, object?>
            {
                ["Category"] = "Series",
                ["MediaType"] = "Series",
                ["VirtualSample"] = true
            }),
            CreateSyntheticItem(@"C:\Muestras\Series", "Stranger.Things.S04E09.Chapter.Nine.1080p.NF.WEB-DL.DDP5.1.Atmos.x264-FLUX.mkv", 1_872_864_000, false, new Dictionary<string, object?>
            {
                ["Category"] = "Series",
                ["MediaType"] = "Series",
                ["VirtualSample"] = true
            }),
            CreateSyntheticItem(@"C:\Muestras\Series", "The.Bear.S02E06.Fishes.720p.HULU.WEBRip.DDP5.1.Atmos.x264-PHOENiX.mkv", 800_000_000, false, new Dictionary<string, object?>
            {
                ["Category"] = "Series",
                ["MediaType"] = "Series",
                ["VirtualSample"] = true
            }),

            // Cómics y Manga
            CreateSyntheticItem(@"C:\Muestras\Comics", "Batman - The Killing Joke (1988) (Digital) (Zone-Empire) [GetComics.INFO].cbr", 52_428_800, false, new Dictionary<string, object?>
            {
                ["Category"] = "Cómics y Manga",
                ["MediaType"] = "Comic",
                ["VirtualSample"] = true
            }),
            CreateSyntheticItem(@"C:\Muestras\Comics", "Berserk v41 (2022) (Digital) (danke-Empire) [Manga-Download.org].cbz", 73_400_320, false, new Dictionary<string, object?>
            {
                ["Category"] = "Cómics y Manga",
                ["MediaType"] = "Comic",
                ["VirtualSample"] = true
            }),

            // Música
            CreateSyntheticItem(@"C:\Muestras\Musica", "01. Daft Punk - Get Lucky (feat. Pharrell Williams).mp3", 9_437_184, false, new Dictionary<string, object?>
            {
                ["Category"] = "Música",
                ["MediaType"] = "Music",
                ["VirtualSample"] = true,
                ["Audio:Artist"] = "Daft Punk",
                ["Audio:Album"] = "Random Access Memories",
                ["Audio:Title"] = "Get Lucky",
                ["Audio:Track"] = 1,
                ["Audio:Year"] = 2013,
                ["Audio:Genre"] = "Disco / Funk",
                ["Audio:Bitrate"] = "320 kbps",
                ["Audio:Duration"] = "00:04:08"
            }),
            CreateSyntheticItem(@"C:\Muestras\Musica", "Queen - Bohemian Rhapsody (2011 Remaster).flac", 42_548_224, false, new Dictionary<string, object?>
            {
                ["Category"] = "Música",
                ["MediaType"] = "Music",
                ["VirtualSample"] = true,
                ["Audio:Artist"] = "Queen",
                ["Audio:Album"] = "A Night at the Opera",
                ["Audio:Title"] = "Bohemian Rhapsody",
                ["Audio:Track"] = 11,
                ["Audio:Year"] = 1975,
                ["Audio:Genre"] = "Classic Rock",
                ["Audio:Bitrate"] = "942 kbps",
                ["Audio:Duration"] = "00:05:55"
            }),

            // Fotos
            CreateSyntheticItem(@"C:\Muestras\Fotografia", "DSC_0042.JPG", 14_194_304, false, new Dictionary<string, object?>
            {
                ["Category"] = "Fotos",
                ["MediaType"] = "Photo",
                ["VirtualSample"] = true,
                ["Exif:CameraMake"] = "Sony",
                ["Exif:CameraModel"] = "ILCE-7RM4",
                ["Exif:LensModel"] = "FE 24-70mm F2.8 GM",
                ["Exif:DateTaken"] = "2026-05-18 14:32:10",
                ["Date Taken"] = "2026-05-18 14:32:10",
                ["Exif:ISO"] = 100,
                ["Exif:FNumber"] = 2.8,
                ["Exif:ExposureTime"] = "1/500s",
                ["Exif:FocalLength"] = "50mm",
                ["Img:Width"] = 9504,
                ["Img:Height"] = 6336,
                ["Orientation"] = "Landscape",
                ["AspectRatio"] = "3:2",
                ["Megapixels"] = 61.0,
                ["Exif:GPSCity"] = "Barcelona",
                ["Exif:GPSCountry"] = "Spain"
            }),

            // Documentos
            CreateSyntheticItem(@"C:\Muestras\Facturas", "FAC-2026-08-00124_ClienteACME.pdf", 524_288, false, new Dictionary<string, object?>
            {
                ["Category"] = "Documentos",
                ["MediaType"] = "Document",
                ["VirtualSample"] = true,
                ["CustomCategory"] = "Facturas",
                ["Doc:Author"] = "Departamento Contabilidad",
                ["Doc:Title"] = "Factura F2026-00124",
                ["Doc:PageCount"] = 3,
                ["Doc:CreationDate"] = "2026-08-15",
                ["FiscalYear"] = 2026,
                ["Doc:Currency"] = "EUR",
                ["Doc:TotalAmount"] = 1850.50
            }),
            CreateSyntheticItem(@"C:\Muestras\Descargas", "Informe_Auditoria_Q2_2026.pdf", 2_314_572, false, new Dictionary<string, object?>
            {
                ["Category"] = "Documentos",
                ["MediaType"] = "Document",
                ["VirtualSample"] = true,
                ["CustomCategory"] = "Informes",
                ["Doc:Author"] = "Audit Team",
                ["Doc:Title"] = "Informe Trimestral Q2",
                ["Doc:PageCount"] = 48,
                ["Doc:CreationDate"] = "2026-07-01"
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
