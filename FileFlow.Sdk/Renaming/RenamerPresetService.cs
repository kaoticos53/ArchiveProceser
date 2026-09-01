using System.Text.Json;

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
/// </summary>
public static class RenamerPresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<RenamerPreset> GetBuiltinPresets()
    {
        return
        [
            new RenamerPreset
            {
                Name = "Fotografía Digital (Fecha EXIF + Modelo + Contador)",
                Category = "Fotografía",
                Description = "Organiza imágenes con año-mes-día, modelo de cámara y contador incremental de 3 dígitos.",
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
                Name = "Música y Audio (Pista + Artista + Título)",
                Category = "Audio",
                Description = "Estandariza canciones con número de pista a 2 dígitos, artista y título de canción.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.NewName,
                        ApplyTo = ApplyToTarget.NameOnly,
                        Pattern = "<Audio:TrackNumber> - <Audio:Artist> - <Audio:Title>",
                        Name = "Plantilla ID3"
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
                Name = "Web & SEO Cleaner (Slug Limpio en Minúsculas)",
                Category = "Web / SEO",
                Description = "Convierte espacios en guiones, elimina caracteres especiales y pasa todo a minúsculas.",
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
                        Name = "Reemplazar Espacios y Símbolos por Guiones"
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
                Name = "Documentos Corporativos (Año + Carpeta + GUID)",
                Category = "Empresarial",
                Description = "Añade año actual, nombre de departamento/carpeta superior y código único.",
                Steps =
                [
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.NewName,
                        ApplyTo = ApplyToTarget.NameOnly,
                        Pattern = "<Year>_<DirName>_<FileNameNoExt>",
                        Name = "Prefijo Empresarial"
                    },
                    new RenameMethodStep
                    {
                        MethodType = RenameMethodType.TrimClean,
                        ApplyTo = ApplyToTarget.FullName,
                        SanitizeInvalidChars = true,
                        Name = "Limpieza"
                    }
                ]
            }
        ];
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
}
