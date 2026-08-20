using System.Globalization;
using System.Text.RegularExpressions;

namespace FileFlow.Sdk.TemplateEngine;

public static class VariableTemplateResolver
{
    private static readonly Regex TokenRegex = new(@"\{(?<expr>[^}]+)\}", RegexOptions.Compiled);
    private static readonly Regex FunctionRegex = new(@"^(?<fn>\w+)\((?<args>.*)\)$", RegexOptions.Compiled);

    public static string Resolve(string template, FileItemContext item, string? sourceRootPath = null)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains('{'))
        {
            return template;
        }

        return TokenRegex.Replace(template, match =>
        {
            string expr = match.Groups["expr"].Value.Trim();
            return EvaluateExpression(expr, item, sourceRootPath);
        });
    }

    private static string EvaluateExpression(string expr, FileItemContext item, string? sourceRootPath)
    {
        var fnMatch = FunctionRegex.Match(expr);
        if (fnMatch.Success)
        {
            string fnName = fnMatch.Groups["fn"].Value;
            string argsStr = fnMatch.Groups["args"].Value;

            var args = ParseArguments(argsStr, item, sourceRootPath);
            return ExecuteFunction(fnName, args, item, sourceRootPath);
        }

        return GetVariableValue(expr, item, sourceRootPath);
    }

    public static string GetVariableValue(string varName, FileItemContext item, string? sourceRootPath)
    {
        string currentPath = item.CurrentPath ?? string.Empty;
        string originalPath = item.OriginalPath ?? string.Empty;

        switch (varName.ToLowerInvariant())
        {
            case "filename":
            case "nombrearchivo":
                return Path.GetFileName(currentPath);

            case "filenamenoext":
            case "nombrearchivosinext":
                return Path.GetFileNameWithoutExtension(currentPath);

            case "extension":
            case "ext":
                return Path.GetExtension(currentPath).TrimStart('.');

            case "currentpath":
            case "rutaactual":
                return currentPath;

            case "originalpath":
            case "rutaoriginal":
                return originalPath;

            case "currentdir":
            case "directorioactual":
                return Path.GetDirectoryName(currentPath) ?? string.Empty;

            case "originaldir":
            case "directoriooriginal":
                return Path.GetDirectoryName(originalPath) ?? string.Empty;

            case "relativepath":
            case "rutarelativa":
                return CalculateRelativePath(currentPath, sourceRootPath ?? Path.GetDirectoryName(originalPath));

            default:
                if (item.Metadata.TryGetValue(varName, out var metaVal) && metaVal != null)
                {
                    return metaVal.ToString() ?? string.Empty;
                }
                return string.Empty;
        }
    }

    private static List<string> ParseArguments(string argsStr, FileItemContext item, string? sourceRootPath)
    {
        if (string.IsNullOrWhiteSpace(argsStr)) return [];

        var result = new List<string>();
        var parts = argsStr.Split(',');
        foreach (var p in parts)
        {
            string trimmed = p.Trim(' ', '"', '\'');
            if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            {
                string token = trimmed[1..^1];
                result.Add(GetVariableValue(token, item, sourceRootPath));
            }
            else
            {
                // Try resolving as variable first, fallback to literal string
                string resolved = GetVariableValue(trimmed, item, sourceRootPath);
                result.Add(string.IsNullOrEmpty(resolved) && !IsKnownVariable(trimmed) ? trimmed : resolved);
            }
        }
        return result;
    }

    private static bool IsKnownVariable(string name)
    {
        return name.Equals("FileName", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("FileNameNoExt", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Extension", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("CurrentPath", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("OriginalPath", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("DateTaken", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExecuteFunction(string fnName, List<string> args, FileItemContext item, string? sourceRootPath)
    {
        string arg0 = args.Count > 0 ? args[0] : string.Empty;

        switch (fnName.ToLowerInvariant())
        {
            case "year":
            case "año":
            case "anio":
                return ExtractDatePart(arg0, "yyyy");

            case "month":
            case "mes":
                return ExtractDatePart(arg0, "MM");

            case "day":
            case "dia":
                return ExtractDatePart(arg0, "dd");

            case "formatdate":
            case "formatofecha":
                string fmt = args.Count > 1 ? args[1] : "yyyy-MM-dd";
                return ExtractDatePart(arg0, fmt);

            case "upper":
            case "mayusculas":
                return arg0.ToUpperInvariant();

            case "lower":
            case "minusculas":
                return arg0.ToLowerInvariant();

            case "trim":
                return arg0.Trim();

            case "replace":
            case "reemplazar":
                if (args.Count >= 3)
                {
                    return arg0.Replace(args[1], args[2], StringComparison.OrdinalIgnoreCase);
                }
                return arg0;

            case "default":
            case "predeterminado":
                return string.IsNullOrWhiteSpace(arg0) && args.Count > 1 ? args[1] : arg0;

            default:
                return arg0;
        }
    }

    private static string ExtractDatePart(string dateInput, string format)
    {
        if (DateTime.TryParse(dateInput, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt) ||
            DateTime.TryParse(dateInput, out dt))
        {
            return dt.ToString(format, CultureInfo.InvariantCulture);
        }
        return dateInput;
    }

    private static string CalculateRelativePath(string fullPath, string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(fullPath))
        {
            return Path.GetFileName(fullPath);
        }

        try
        {
            return Path.GetRelativePath(rootPath, fullPath);
        }
        catch
        {
            return Path.GetFileName(fullPath);
        }
    }
}
