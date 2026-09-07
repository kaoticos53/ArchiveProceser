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
        var dict = new Dictionary<string, RenamerPreset>(StringComparer.OrdinalIgnoreCase);

        // 1. Fallback determinista en memoria (garantiza que todos los presets oficiales siempre existan)
        foreach (var p in GetFallbackPresets())
        {
            dict[p.Name] = p;
        }

        // 2. Intentar cargar/actualizar desde el directorio Config/ de la aplicación o plugin
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
                if (factoryPresets != null)
                {
                    foreach (var fp in factoryPresets)
                    {
                        dict[fp.Name] = fp;
                    }
                }
            }
        }

        // 3. Cargar presets de usuario en %AppData%/FileFlow/presets/renamer_presets.json
        AppPaths.EnsureDirectories();
        string appDataFile = AppPaths.RenamerPresetsFile;
        if (File.Exists(appDataFile))
        {
            var userPresets = TryLoadPresetsFromFile(appDataFile);
            if (userPresets != null)
            {
                foreach (var up in userPresets)
                {
                    dict[up.Name] = up;
                }
            }
        }

        return dict.Values.ToList();
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
            },
            new RenamerPreset
            {
                Name = "🧹 Limpiar Nombre",
                Category = "Limpieza",
                Description = "Pipeline completo con todos los métodos de las fases 1 a 6: URLs, calidades/códecs, plataformas, idiomas, grupos scene/trackers/corchetes y normalización de separadores y espacios.",
                Steps =
                [
                    // Fase 1: Sitios web, URLs y dominios publicitarios
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)(?:https?:\/\/)?(?:www\.)?[\w-]+\.(?:com|org|net|is|lat|lol|me|cc|biz|vip|re|nu|top|club|pro|info|to|in|fm)\b(?:\.[\w-]+)?(?:\s*-\s*)?",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 1: Sitios Web y URLs Publicitarias"
                    },
                    // Fase 2: Etiquetas de calidad, códecs, formatos de audio y vídeo
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)\b(?:2160p|1080p|720p|480p|4K|UHD|HDR\d*|DV|BluRay|BDRip|BRRip|WEBRip|WEB-DL|WEB|HDTV|CAMRip|CAM|HDCAM|TS|DVDRip|DivX(?:-[\d.]+)?|XviD|Remux|Remaster(?:ed)?|Criterion(?:\.Collection)?|PROPER|REPACK|EXTENDED|Unrated|IMAX|x264|x265|HEVC|H\.?264|H\.?265|10bit|DTS(?:-HD(?:\.MA)?)?|TrueHD|Atmos|DDPlus\d*\.?\d*|DDP\d*\.?\d*|DD\d*\.?\d*|AC3(?:\.5\.1)?|AAC(?:\d*\.?\d*)?|6CH|FLAC|Lossless|MP3|320kbps|24bit(?:-\d+kHz)?|SACD-DSD\d*)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 2: Etiquetas de Calidad y Códecs"
                    },
                    // Fase 3: Fuentes, plataformas y servicios
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)\b(?:NF|AMZN|DSNP|ATVP|HULU|HBO(?:-MAX)?|BBC\.iPlayer|CR|Tidal|Qobuz|iTunes|Apple\.Music|Weekly\.Shonen|MangaPlus|ComiXology)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 3: Fuentes y Plataformas de Streaming"
                    },
                    // Fase 4: Idiomas, doblajes y subtítulos genéricos
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)\b(?:Dual(?:\.Audio)?|Castellano|Latino|SPANiSH|Catalan|Ingles|English|German|JAP(?:ANESE)?|Hindi-Eng|Multi(?:-Audio)?|KORSUB|Subtitulos|sub-espanol)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 4: Idiomas, Doblajes y Subtítulos"
                    },
                    // Fase 5A: Grupos de ripeo y trackers
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)-(?:ROVERS|FLUX|NTb|SYNCOPY|CiNEFiLE|PSA|FGT|YIFY|EVO|Galaxy\w+|CMRG|TERMiNAL|ShortbreaD|TrollHD|SuccessfulCrab|MeGusta|CAKES|AVS|BiN|TOMMY|PHOENiX|danke-Empire|Zone-Empire|Empire|Minutemen-\w+)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 5A: Grupos de Ripeo y Trackers"
                    },
                    // Fase 5B: Corchetes residuales [...]
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"\[[^\]]*\]",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 5B: Corchetes Residuales [...]"
                    },
                    // Fase 6A: Separadores a espacio
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"[._]+",
                        ReplaceText = " ",
                        ReplaceAll = true,
                        Name = "Fase 6A: Puntos y Guiones Bajos a Espacios"
                    },
                    // Fase 6B: Normalizar guiones duplicados
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"\s+-\s+(?:\s+-)*",
                        ReplaceText = " - ",
                        ReplaceAll = true,
                        Name = "Fase 6B: Normalizar Guiones Duplicados"
                    },
                    // Fase 6C: Recorte de guiones y espacios en extremos
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"^\s*-\s*|\s*-\s*$|^\s+|\s+$",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 6C: Limpiar Guiones y Espacios en Extremos"
                    },
                    // Fase 6D: Limpieza de espacios y sanitización final
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        TrimWhitespace = true,
                        SanitizeInvalidChars = true,
                        Name = "Fase 6D: Colapsar Espacios y Limpieza Final"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🎬 Pipeline Limpieza Multimedia (Scene, Rips, Códecs y URLs)",
                Category = "Multimedia",
                Description = "Pipeline secuencial completo de 6 fases: elimina URLs/dominios publicitarios, tags de códecs/calidad, plataformas de streaming, idiomas/subtítulos, grupos scene/trackers/corchetes y normaliza separadores/guiones/espacios.",
                Steps =
                [
                    // Fase 1: Sitios web, URLs y dominios publicitarios
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)(?:https?:\/\/)?(?:www\.)?[\w-]+\.(?:com|org|net|is|lat|lol|me|cc|biz|vip|re|nu|top|club|pro|info|to|in|fm)\b(?:\.[\w-]+)?(?:\s*-\s*)?",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 1: Sitios Web y URLs Publicitarias"
                    },
                    // Fase 2: Etiquetas de calidad, códecs, formatos de audio y vídeo
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)\b(?:2160p|1080p|720p|480p|4K|UHD|HDR\d*|DV|BluRay|BDRip|BRRip|WEBRip|WEB-DL|WEB|HDTV|CAMRip|CAM|HDCAM|TS|DVDRip|DivX(?:-[\d.]+)?|XviD|Remux|Remaster(?:ed)?|Criterion(?:\.Collection)?|PROPER|REPACK|EXTENDED|Unrated|IMAX|x264|x265|HEVC|H\.?264|H\.?265|10bit|DTS(?:-HD(?:\.MA)?)?|TrueHD|Atmos|DDPlus\d*\.?\d*|DDP\d*\.?\d*|DD\d*\.?\d*|AC3(?:\.5\.1)?|AAC(?:\d*\.?\d*)?|6CH|FLAC|Lossless|MP3|320kbps|24bit(?:-\d+kHz)?|SACD-DSD\d*)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 2: Etiquetas de Calidad y Códecs"
                    },
                    // Fase 3: Fuentes, plataformas y servicios
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)\b(?:NF|AMZN|DSNP|ATVP|HULU|HBO(?:-MAX)?|BBC\.iPlayer|CR|Tidal|Qobuz|iTunes|Apple\.Music|Weekly\.Shonen|MangaPlus|ComiXology)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 3: Fuentes y Plataformas de Streaming"
                    },
                    // Fase 4: Idiomas, doblajes y subtítulos genéricos
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)\b(?:Dual(?:\.Audio)?|Castellano|Latino|SPANiSH|Catalan|Ingles|English|German|JAP(?:ANESE)?|Hindi-Eng|Multi(?:-Audio)?|KORSUB|Subtitulos|sub-espanol)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 4: Idiomas, Doblajes y Subtítulos"
                    },
                    // Fase 5A: Grupos de ripeo y trackers
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)-(?:ROVERS|FLUX|NTb|SYNCOPY|CiNEFiLE|PSA|FGT|YIFY|EVO|Galaxy\w+|CMRG|TERMiNAL|ShortbreaD|TrollHD|SuccessfulCrab|MeGusta|CAKES|AVS|BiN|TOMMY|PHOENiX|danke-Empire|Zone-Empire|Empire|Minutemen-\w+)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 5A: Grupos de Ripeo y Trackers"
                    },
                    // Fase 5B: Corchetes residuales [...]
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"\[[^\]]*\]",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 5B: Corchetes Residuales [...]"
                    },
                    // Fase 6A: Separadores a espacio
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"[._]+",
                        ReplaceText = " ",
                        ReplaceAll = true,
                        Name = "Fase 6A: Puntos y Guiones Bajos a Espacios"
                    },
                    // Fase 6B: Normalizar guiones duplicados
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"\s+-\s+(?:\s+-)*",
                        ReplaceText = " - ",
                        ReplaceAll = true,
                        Name = "Fase 6B: Normalizar Guiones Duplicados"
                    },
                    // Fase 6C: Recorte de guiones y espacios en extremos
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"^\s*-\s*|\s*-\s*$|^\s+|\s+$",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Fase 6C: Limpiar Guiones y Espacios en Extremos"
                    },
                    // Fase 6D: Limpieza de espacios y sanitización final
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        TrimWhitespace = true,
                        SanitizeInvalidChars = true,
                        Name = "Fase 6D: Colapsar Espacios y Limpieza Final"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🌐 Limpieza: URLs y Dominios Publicitarios (Fase 1)",
                Category = "Limpieza",
                Description = "Elimina URLs completas, prefijos habituales (www.) y dominios (.org, .to, .net...) incluyendo corchetes o guiones pegados.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)(?:https?:\/\/)?(?:www\.)?[\w-]+\.(?:com|org|net|is|lat|lol|me|cc|biz|vip|re|nu|top|club|pro|info|to|in|fm)\b(?:\.[\w-]+)?(?:\s*-\s*)?",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Eliminar URLs y Dominios"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        TrimWhitespace = true,
                        Name = "Colapsar Espacios"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "📺 Limpieza: Códecs, Calidades y Formatos (Fase 2)",
                Category = "Limpieza",
                Description = "Elimina tags de codificación, resolución y captura de audio/vídeo (2160p, 1080p, BluRay, x265, DTS, TrueHD, Atmos...).",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)\b(?:2160p|1080p|720p|480p|4K|UHD|HDR\d*|DV|BluRay|BDRip|BRRip|WEBRip|WEB-DL|WEB|HDTV|CAMRip|CAM|HDCAM|TS|DVDRip|DivX(?:-[\d.]+)?|XviD|Remux|Remaster(?:ed)?|Criterion(?:\.Collection)?|PROPER|REPACK|EXTENDED|Unrated|IMAX|x264|x265|HEVC|H\.?264|H\.?265|10bit|DTS(?:-HD(?:\.MA)?)?|TrueHD|Atmos|DDPlus\d*\.?\d*|DDP\d*\.?\d*|DD\d*\.?\d*|AC3(?:\.5\.1)?|AAC(?:\d*\.?\d*)?|6CH|FLAC|Lossless|MP3|320kbps|24bit(?:-\d+kHz)?|SACD-DSD\d*)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Eliminar Tags de Calidad y Códecs"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        TrimWhitespace = true,
                        Name = "Colapsar Espacios"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "📡 Limpieza: Plataformas y Servicios Streaming (Fase 3)",
                Category = "Limpieza",
                Description = "Descarta plataformas de streaming, canales y tags de origen (NF, AMZN, DSNP, ATVP, HULU, HBO, iTunes...).",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)\b(?:NF|AMZN|DSNP|ATVP|HULU|HBO(?:-MAX)?|BBC\.iPlayer|CR|Tidal|Qobuz|iTunes|Apple\.Music|Weekly\.Shonen|MangaPlus|ComiXology)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Eliminar Plataformas de Streaming"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        TrimWhitespace = true,
                        Name = "Colapsar Espacios"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🗣️ Limpieza: Idiomas, Doblajes y Subtítulos (Fase 4)",
                Category = "Limpieza",
                Description = "Elimina menciones a idiomas, doblajes y subtítulos genéricos (Dual, Castellano, Latino, SPANiSH, Multi-Audio, KORSUB...).",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)\b(?:Dual(?:\.Audio)?|Castellano|Latino|SPANiSH|Catalan|Ingles|English|German|JAP(?:ANESE)?|Hindi-Eng|Multi(?:-Audio)?|KORSUB|Subtitulos|sub-espanol)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Eliminar Idiomas y Subtítulos"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        TrimWhitespace = true,
                        Name = "Colapsar Espacios"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "👥 Limpieza: Grupos Scene, Trackers y Corchetes (Fase 5)",
                Category = "Limpieza",
                Description = "Limpia grupos comunes de Scene/P2P tras guion (-FLUX, -YIFY...), corchetes residuales [...] y menciones a trackers.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"(?i)-(?:ROVERS|FLUX|NTb|SYNCOPY|CiNEFiLE|PSA|FGT|YIFY|EVO|Galaxy\w+|CMRG|TERMiNAL|ShortbreaD|TrollHD|SuccessfulCrab|MeGusta|CAKES|AVS|BiN|TOMMY|PHOENiX|danke-Empire|Zone-Empire|Empire|Minutemen-\w+)\b",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Eliminar Grupos Scene/P2P"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"\[[^\]]*\]",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Eliminar Corchetes [...]"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        TrimWhitespace = true,
                        Name = "Colapsar Espacios"
                    }
                ]
            },
            new RenamerPreset
            {
                Name = "🧹 Limpieza: Normalización de Separadores y Espacios (Fase 6)",
                Category = "Limpieza",
                Description = "Reemplaza puntos y barras bajas por espacios, normaliza guiones repetidos y limpia espacios/guiones en extremos.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"[._]+",
                        ReplaceText = " ",
                        ReplaceAll = true,
                        Name = "Puntos y Guiones Bajos a Espacios"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"\s+-\s+(?:\s+-)*",
                        ReplaceText = " - ",
                        ReplaceAll = true,
                        Name = "Normalizar Guiones"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.SearchReplace,
                        ApplyTo = ApplyToTarget.NameOnly,
                        UseRegex = true,
                        SearchText = @"^\s*-\s*|\s*-\s*$|^\s+|\s+$",
                        ReplaceText = "",
                        ReplaceAll = true,
                        Name = "Limpiar Extremos"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        CollapseSpaces = true,
                        TrimWhitespace = true,
                        SanitizeInvalidChars = true,
                        Name = "Colapsar Espacios y Limpieza"
                    }
                ]
            }
        ];
    }
}
