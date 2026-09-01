using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Sdk.Renaming;

/// <summary>
/// Modelo de ajuste predefinido (Preset) para el motor de renombrado avanzado.
/// </summary>
public sealed record RenamerPreset
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = "General";
    public List<RenameMethodStep> Steps { get; init; } = [];
}

/// <summary>
/// Servicio de gestión de ajustes predefinidos (Presets) para AdvancedRenamer.
/// Soporta carga en cascada desde %AppData%, directorio Config/ y fallback en memoria.
/// </summary>
public static class RenamerPresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static IReadOnlyList<RenamerPreset> GetBuiltinPresets()
    {
        // 1. Intentar cargar desde el fichero de usuario en %AppData%/FileFlow/presets/renamer_presets.json
        AppPaths.EnsureDirectories();
        string appDataFile = AppPaths.RenamerPresetsFile;
        if (File.Exists(appDataFile))
        {
            var userPresets = TryLoadPresetsFromFile(appDataFile);
            if (userPresets != null && userPresets.Count > 0)
            {
                return userPresets;
            }
        }

        // 2. Intentar cargar desde el directorio Config/ de la aplicación o plugin
        string[] candidatePaths =
        [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "renamer_presets.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Config", "renamer_presets.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Config", "renamer_presets.json"),
            Path.Combine(AppContext.BaseDirectory, "Config", "renamer_presets.json")
        ];

        foreach (var path in candidatePaths.Distinct())
        {
            if (File.Exists(path))
            {
                var factoryPresets = TryLoadPresetsFromFile(path);
                if (factoryPresets != null && factoryPresets.Count > 0)
                {
                    return factoryPresets;
                }
            }
        }

        // 3. Fallback determinista en memoria
        return GetFallbackPresets();
    }

    public static List<RenamerPreset>? TryLoadPresetsFromFile(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<RenamerPreset>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static string SerializePreset(RenamerPreset preset)
    {
        return JsonSerializer.Serialize(preset, JsonOptions);
    }

    public static RenamerPreset? DeserializePreset(string json)
    {
        return JsonSerializer.Deserialize<RenamerPreset>(json, JsonOptions);
    }

    public static string SerializeSteps(IReadOnlyList<RenameMethodStep> steps)
    {
        return JsonSerializer.Serialize(steps, JsonOptions);
    }

    public static List<RenameMethodStep> DeserializeSteps(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<RenameMethodStep>>(json, JsonOptions) ?? [];
    }

    private static List<RenamerPreset> GetFallbackPresets()
    {
        return
        [
            new RenamerPreset
            {
                Name = "📷 Fotografía Digital (Fecha EXIF + Modelo + Contador)",
                Category = "Fotografía",
                Description = "Organiza fotos con año-mes-día de captura, modelo de cámara y contador incremental de 3 dígitos con extensión en minúsculas.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.NewName,
                        ApplyTo = ApplyToTarget.NameOnly,
                        Pattern = "<Date Taken:yyyyMMdd>_<Exif:CameraModel>_<Inc Nr:001>",
                        Name = "Plantilla EXIF"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.CaseConversion,
                        ApplyTo = ApplyToTarget.ExtensionOnly,
                        CaseType = CaseTransformType.Lowercase,
                        Name = "Extensión en Minúsculas"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        SanitizeInvalidChars = true,
                        Name = "Limpieza de Caracteres"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🖼️ Fotografía (Fecha + Resolución [Ancho x Alto])",
                Category = "Fotografía",
                Description = "Agrega fecha de captura y dimensiones en píxeles al nombre original de la imagen.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.NewName,
                        ApplyTo = ApplyToTarget.NameOnly,
                        Pattern = "<Date Taken:yyyyMMdd>_[<Img Width>x<Img Height>]_<FileNameNoExt>",
                        Name = "Plantilla Fecha y Resolución"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.CaseConversion,
                        ApplyTo = ApplyToTarget.ExtensionOnly,
                        CaseType = CaseTransformType.Lowercase,
                        Name = "Extensión en Minúsculas"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        SanitizeInvalidChars = true,
                        Name = "Sanitizar Nombre"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🎬 Series de TV y Vídeo (Estandarizar S01E02 / NxN)",
                Category = "Vídeo",
                Description = "Normaliza temporadas y episodios rellenando ceros a 2 dígitos (ej. 1x2 -> S01E02 o 01x02) y limpia nombres de release.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.NormalizeNumbers,
                        ApplyTo = ApplyToTarget.NameOnly,
                        NumberTarget = NumberPaddingTarget.EpisodeFormat,
                        NumberPaddingDigits = 2,
                        PadSeasonAndEpisode = true,
                        Name = "Normalizar Temporada y Episodio"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = false,
                        SearchText = ".",
                        ReplaceText = " ",
                        ReplaceAll = true,
                        Name = "Puntos a Espacios"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.CaseConversion,
                        ApplyTo = ApplyToTarget.NameOnly,
                        CaseType = CaseTransformType.TitleCase,
                        Name = "Mayúsculas de Título"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        TrimWhitespace = true,
                        SanitizeInvalidChars = true,
                        Name = "Limpieza Final"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🎵 Música y Audio (Pista - Artista - Título)",
                Category = "Audio",
                Description = "Estandariza canciones con número de pista a 2 dígitos, artista y título de canción mediante etiquetas ID3.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.NewName,
                        ApplyTo = ApplyToTarget.NameOnly,
                        Pattern = "<Audio:Track> - <Audio:Artist> - <Audio:Title>",
                        Name = "Plantilla ID3 Estándar"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        SanitizeInvalidChars = true,
                        Name = "Sanitizar Nombre"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "💿 Música (Artista - [Año] Álbum - Pista. Título)",
                Category = "Audio",
                Description = "Organización discográfica completa incluyendo artista, año del álbum, nombre del álbum y número de pista.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.NewName,
                        ApplyTo = ApplyToTarget.NameOnly,
                        Pattern = "<Audio:Artist> - [<Audio:Year>] <Audio:Album> - <Audio:Track>. <Audio:Title>",
                        Name = "Plantilla Discográfica"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.CaseConversion,
                        ApplyTo = ApplyToTarget.ExtensionOnly,
                        CaseType = CaseTransformType.Lowercase,
                        Name = "Extensión en Minúsculas"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        SanitizeInvalidChars = true,
                        Name = "Limpieza de Caracteres"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🌐 Web & SEO Cleaner (Slug Limpio en Minúsculas / Kebab-case)",
                Category = "Web / SEO",
                Description = "Convierte espacios y caracteres especiales en guiones, pasa todo a minúsculas y normaliza caracteres Unicode.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.CaseConversion,
                        ApplyTo = ApplyToTarget.FullName,
                        CaseType = CaseTransformType.Lowercase,
                        Name = "Minúsculas Completas"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"[^\w\-]+",
                        ReplaceText = "-",
                        ReplaceAll = true,
                        Name = "Espacios y Símbolos a Guiones"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        TrimWhitespace = true,
                        SanitizeInvalidChars = true,
                        NormalizationMode = UnicodeNormalizationMode.FormC,
                        Name = "Normalización Unicode"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🔠 Normalización de Título (TitleCase con Espacios Limpios)",
                Category = "General",
                Description = "Convierte guiones bajos y puntos en espacios, y aplica mayúscula a la letra inicial de cada palabra.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"[_\.]+",
                        ReplaceText = " ",
                        ReplaceAll = true,
                        Name = "Guiones Bajos y Puntos a Espacios"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.CaseConversion,
                        ApplyTo = ApplyToTarget.NameOnly,
                        CaseType = CaseTransformType.TitleCase,
                        Name = "Capitalizar Cada Palabra"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        TrimWhitespace = true,
                        Name = "Colapsar Espacios y Recortar"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "💼 Documentos y Facturas (Fecha ISO_Carpeta_Nombre_Hash)",
                Category = "Empresarial",
                Description = "Formato documental con fecha de creación ISO, departamento/carpeta contenedora y verificación SHA256 corta.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.NewName,
                        ApplyTo = ApplyToTarget.NameOnly,
                        Pattern = "<Date Created:yyyyMMdd>_<DirName>_<FileNameNoExt>_[<Hash:SHA256:8>]",
                        Name = "Prefijo Empresarial con Hash"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        SanitizeInvalidChars = true,
                        CollapseSpaces = true,
                        Name = "Limpieza y Sanitización"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🧹 Limpieza Extrema (Sanitizar SO + Colapsar Espacios + Trim)",
                Category = "General",
                Description = "Elimina caracteres ilegales del sistema operativo, recorta espacios al inicio y final y colapsa espacios dobles o triples.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        TrimWhitespace = true,
                        CollapseSpaces = true,
                        SanitizeInvalidChars = true,
                        NormalizationMode = UnicodeNormalizationMode.FormC,
                        Name = "Limpieza Profunda"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🔢 Numeración Incremental (001, 002...) por Carpeta",
                Category = "Secuencias",
                Description = "Añade un contador con relleno de ceros (001_...) que se reinicia automáticamente al cambiar de directorio.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.Numbering,
                        ApplyTo = ApplyToTarget.NameOnly,
                        StartNumber = 1,
                        Increment = 1,
                        PaddingZeroes = 3,
                        ResetOn = NumberingResetOn.DirectoryChange,
                        Name = "Contador con 3 Dígitos"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.Insert,
                        ApplyTo = ApplyToTarget.NameOnly,
                        Pattern = " - ",
                        Position = CharacterPosition.FromStart,
                        PositionIndex = 3,
                        Name = "Separador Guion"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "0️⃣1️⃣ Rellenar Números (1, 2... 10 -> 01, 02... 10)",
                Category = "Secuencias",
                Description = "Rellena los números individuales existentes en el nombre con ceros a la izquierda para garantizar ordenación alfanumérica perfecta en el explorador.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.NormalizeNumbers,
                        ApplyTo = ApplyToTarget.NameOnly,
                        NumberTarget = NumberPaddingTarget.AllNumbers,
                        NumberPaddingDigits = 2,
                        Name = "Rellenar Números a 2 Dígitos"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        Name = "Colapsar Espacios"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "✂️ Limpiador de Tags / Publicidad (Regex Cleaner)",
                Category = "Limpieza",
                Description = "Elimina automáticamente tags publicitarios de descargas (ej. [www.sitio.com], (v1.0), 1080p, Bluray, copia).",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)(\[[^\]]*www\.[^\]]+\]|\((copia|final|v\d+(\.\d+)?)\)|\b(1080p|720p|hdtv|x264|bluray)\b)",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Eliminar Tags Publicitarios"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        TrimWhitespace = true,
                        CollapseSpaces = true,
                        SanitizeInvalidChars = true,
                        Name = "Limpieza de Espacios Restantes"
                    }
                ]
            }
        ];
    }
}
