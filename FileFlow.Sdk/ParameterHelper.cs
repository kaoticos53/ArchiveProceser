using System.Text.Json;
using System.Text.RegularExpressions;

namespace FileFlow.Sdk;

/// <summary>
/// Proporciona conversión y extracción segura de parámetros para nodos de flujo,
/// evitando desbordamientos e InvalidCastException cuando los valores provienen de JsonElement, WPF UI o cadenas.
/// </summary>
public static class ParameterHelper
{
    private static readonly Regex NumberExtractionRegex = new(@"[-+]?\d+(?:[\.,]\d+)?", RegexOptions.Compiled);

    public static bool GetBoolean(object? value, bool defaultValue = false)
    {
        if (value == null) return defaultValue;

        if (value is bool b) return b;

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(element.GetString(), out var parsed) ? parsed : defaultValue,
                JsonValueKind.Number => element.TryGetInt64(out var num) && num != 0,
                _ => defaultValue
            };
        }

        if (value is string s)
        {
            return bool.TryParse(s, out var parsed) ? parsed : defaultValue;
        }

        try
        {
            return Convert.ToBoolean(value);
        }
        catch
        {
            return defaultValue;
        }
    }

    public static int GetInt32(object? value, int defaultValue = 0)
    {
        if (value == null) return defaultValue;

        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is double d) return (int)d;

        string strVal = string.Empty;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsedInt))
            {
                return parsedInt;
            }
            if (element.ValueKind == JsonValueKind.String)
            {
                strVal = element.GetString() ?? string.Empty;
            }
        }
        else if (value is string s)
        {
            strVal = s;
        }

        if (!string.IsNullOrWhiteSpace(strVal))
        {
            if (int.TryParse(strVal, out var directInt)) return directInt;

            string t = strVal.Trim();

            if (t.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            {
                var match = NumberExtractionRegex.Match(t);
                if (match.Success && double.TryParse(match.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ms))
                    return (int)ms;
            }
            else if (t.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                var match = NumberExtractionRegex.Match(t);
                if (match.Success && double.TryParse(match.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double sec))
                    return (int)sec;
            }
            else if (t.EndsWith("m", StringComparison.OrdinalIgnoreCase) && !t.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            {
                var match = NumberExtractionRegex.Match(t);
                if (match.Success && double.TryParse(match.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double min))
                    return (int)(min * 60);
            }
            else if (t.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                var match = NumberExtractionRegex.Match(t);
                if (match.Success && double.TryParse(match.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hours))
                    return (int)(hours * 3600);
            }

            var genMatch = NumberExtractionRegex.Match(t);
            if (genMatch.Success && double.TryParse(genMatch.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal))
            {
                return (int)parsedVal;
            }
        }

        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return defaultValue;
        }
    }

    public static double GetDouble(object? value, double defaultValue = 0.0)
    {
        if (value == null) return defaultValue;

        if (value is double d) return d;
        if (value is float f) return f;
        if (value is int i) return i;
        if (value is long l) return l;

        string strVal = string.Empty;

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var parsedDouble))
            {
                return parsedDouble;
            }
            if (element.ValueKind == JsonValueKind.String)
            {
                strVal = element.GetString() ?? string.Empty;
            }
        }
        else if (value is string s)
        {
            strVal = s;
        }

        if (!string.IsNullOrWhiteSpace(strVal))
        {
            if (double.TryParse(strVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var directDbl))
                return directDbl;

            var match = NumberExtractionRegex.Match(strVal.Trim());
            if (match.Success && double.TryParse(match.Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal))
            {
                return parsedVal;
            }
        }

        try
        {
            return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return defaultValue;
        }
    }

    public static string GetString(object? value, string defaultValue = "")
    {
        if (value == null) return defaultValue;

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? defaultValue,
                JsonValueKind.Null => defaultValue,
                JsonValueKind.Undefined => defaultValue,
                _ => element.GetRawText()
            };
        }

        return value.ToString() ?? defaultValue;
    }

    /// <summary>
    /// Resuelve una plantilla de ruta de salida. Si la ruta resuelta es relativa,
    /// se ancla automáticamente bajo la Ruta Global de Salida (GlobalOutputDir) si está configurada,
    /// o bajo el directorio origen del archivo (SourceRootPath / OriginalPath / CurrentPath).
    /// </summary>
    public static string ResolveOutputPath(string targetPathPattern, FileItemContext context, string? globalOutputDir = null, string? sourceRootPath = null)
    {
        if (string.IsNullOrWhiteSpace(targetPathPattern))
        {
            targetPathPattern = "{FileName}";
        }

        string? effectiveGlobalOutputDir = globalOutputDir;
        if (string.IsNullOrWhiteSpace(effectiveGlobalOutputDir) &&
            context.Metadata.TryGetValue("GlobalOutputDir", out var godVal) && godVal != null)
        {
            effectiveGlobalOutputDir = godVal.ToString();
        }

        string resolved = TemplateEngine.VariableTemplateResolver.Resolve(targetPathPattern, context, sourceRootPath);

        if (string.IsNullOrWhiteSpace(resolved))
        {
            resolved = Path.GetFileName(context.CurrentPath);
        }

        // Si la ruta resultante comienza con separador sin ser UNC (ej. \Output tras {RelativeDir}\Output cuando RelativeDir es vacio),
        // se normaliza quitando el separador inicial para poder combinarlo correctamente con el directorio base.
        if ((resolved.StartsWith('\\') || resolved.StartsWith('/')) && !resolved.StartsWith(@"\\") && !resolved.StartsWith("//"))
        {
            resolved = resolved.TrimStart('\\', '/');
            if (string.IsNullOrWhiteSpace(resolved))
            {
                resolved = Path.GetFileName(context.CurrentPath);
            }
        }

        if (Path.IsPathFullyQualified(resolved))
        {
            return resolved;
        }

        // Comprobar si el patrón original solicitaba explícitamente una ruta relativa al directorio de origen
        bool isExplicitlySourceRelative = targetPathPattern.Contains("{RelativeDir}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{RelativeDirectory}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{RelativePath}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{RelativeFilePath}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{SourceDir}", StringComparison.OrdinalIgnoreCase) ||
                                         targetPathPattern.Contains("{OriginalDir}", StringComparison.OrdinalIgnoreCase);

        // Directorio base de origen
        string? baseDir = null;
        if (context.Metadata.TryGetValue("SourceRootPath", out var srpVal) && srpVal != null && !string.IsNullOrWhiteSpace(srpVal.ToString()))
        {
            baseDir = srpVal.ToString();
        }
        else if (!string.IsNullOrWhiteSpace(sourceRootPath))
        {
            baseDir = sourceRootPath;
        }
        else
        {
            string? itemPath = !string.IsNullOrWhiteSpace(context.OriginalPath) ? context.OriginalPath : context.CurrentPath;
            if (!string.IsNullOrWhiteSpace(itemPath))
            {
                baseDir = Path.GetDirectoryName(itemPath);
            }
        }

        // 1. Si el patrón era explícitamente relativo al origen (ej. "{RelativeDir}\Output"), anclar bajo el directorio origen
        if (isExplicitlySourceRelative && !string.IsNullOrWhiteSpace(baseDir))
        {
            return Path.GetFullPath(Path.Combine(baseDir, resolved));
        }

        // 2. Si hay GlobalOutputDir configurado, anclar bajo GlobalOutputDir
        if (!string.IsNullOrWhiteSpace(effectiveGlobalOutputDir))
        {
            return Path.GetFullPath(Path.Combine(effectiveGlobalOutputDir, resolved));
        }

        // 3. Fallback: anclar bajo el directorio del archivo origen
        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            return Path.GetFullPath(Path.Combine(baseDir, resolved));
        }

        return resolved;
    }
}
