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

        // Use SourceRootPath from metadata if available
        string? effectiveRootPath = sourceRootPath;
        if (string.IsNullOrEmpty(effectiveRootPath) &&
            item.Metadata.TryGetValue("SourceRootPath", out var rootVal) &&
            rootVal != null)
        {
            effectiveRootPath = rootVal.ToString();
        }

        if (string.IsNullOrEmpty(effectiveRootPath))
        {
            effectiveRootPath = Path.GetDirectoryName(originalPath);
        }

        switch (varName.ToLowerInvariant())
        {
            case "filename":
                return Path.GetFileName(currentPath);

            case "filenamenoext":
                return Path.GetFileNameWithoutExtension(currentPath);

            case "extension":
            case "ext":
                return Path.GetExtension(currentPath).TrimStart('.');

            case "currentpath":
                return currentPath;

            case "originalpath":
                return originalPath;

            case "currentdir":
                return Path.GetDirectoryName(currentPath) ?? string.Empty;

            case "originaldir":
                return Path.GetDirectoryName(originalPath) ?? string.Empty;

            case "relativepath":
            case "relativedir":
            case "relativedirectory":
                return CalculateRelativeDirectory(currentPath, effectiveRootPath);

            case "relativefilepath":
                return CalculateRelativeFilePath(currentPath, effectiveRootPath);

            // New System & Environment Variables
            case "datenow":
                return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            case "timenow":
                return DateTime.Now.ToString("HH-mm-ss", CultureInfo.InvariantCulture);

            case "datetimenow":
                return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);

            case "counter":
            case "index":
                return item.Metadata.TryGetValue("Counter", out var cVal) && cVal != null ? cVal.ToString()! : "1";

            case "sizemb":
                return (item.FileSizeBytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture);

            case "sizekb":
                return (item.FileSizeBytes / 1024.0).ToString("F1", CultureInfo.InvariantCulture);

            case "sizebytes":
                return item.FileSizeBytes.ToString(CultureInfo.InvariantCulture);

            case "username":
                return Environment.UserName;

            case "machinename":
                return Environment.MachineName;

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
               name.Equals("RelativePath", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("RelativeDir", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("RelativeFilePath", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("DateTaken", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("DateNow", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("TimeNow", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("DateTimeNow", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Counter", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("SizeMB", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("SizeKB", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("SizeBytes", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("UserName", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("MachineName", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExecuteFunction(string fnName, List<string> args, FileItemContext item, string? sourceRootPath)
    {
        string arg0 = args.Count > 0 ? args[0] : string.Empty;

        switch (fnName.ToLowerInvariant())
        {
            case "year":
                return ExtractDatePart(arg0, "yyyy");

            case "month":
                return ExtractDatePart(arg0, "MM");

            case "day":
                return ExtractDatePart(arg0, "dd");

            case "formatdate":
                string fmt = args.Count > 1 ? args[1] : "yyyy-MM-dd";
                return ExtractDatePart(arg0, fmt);

            case "upper":
                return arg0.ToUpperInvariant();

            case "lower":
                return arg0.ToLowerInvariant();

            case "trim":
                return arg0.Trim();

            case "replace":
                if (args.Count >= 3)
                {
                    return arg0.Replace(args[1], args[2], StringComparison.OrdinalIgnoreCase);
                }
                return arg0;

            case "default":
                return string.IsNullOrWhiteSpace(arg0) && args.Count > 1 ? args[1] : arg0;

            // New Practical Functions
            case "sanitize":
                return SanitizeFileName(arg0);

            case "padleft":
                if (args.Count >= 2 && int.TryParse(args[1], out int len))
                {
                    char padChar = args.Count >= 3 && args[2].Length > 0 ? args[2][0] : '0';
                    return arg0.PadLeft(len, padChar);
                }
                return arg0;

            case "substring":
                if (args.Count >= 2 && int.TryParse(args[1], out int startIndex))
                {
                    if (startIndex < 0 || startIndex >= arg0.Length) return string.Empty;
                    if (args.Count >= 3 && int.TryParse(args[2], out int length))
                    {
                        length = Math.Min(length, arg0.Length - startIndex);
                        return arg0.Substring(startIndex, length);
                    }
                    return arg0[startIndex..];
                }
                return arg0;

            case "regexmatch":
                if (args.Count >= 2)
                {
                    try
                    {
                        var match = Regex.Match(arg0, args[1]);
                        return match.Success ? match.Value : string.Empty;
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
                return arg0;

            case "regexreplace":
                if (args.Count >= 3)
                {
                    try
                    {
                        return Regex.Replace(arg0, args[1], args[2]);
                    }
                    catch
                    {
                        return arg0;
                    }
                }
                return arg0;

            case "coalesce":
                foreach (var arg in args)
                {
                    if (!string.IsNullOrWhiteSpace(arg)) return arg;
                }
                return string.Empty;

            case "fileagedays":
                return CalculateDaysElapsed(arg0);

            default:
                return arg0;
        }
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct();
        foreach (char c in invalidChars)
        {
            input = input.Replace(c, '-');
        }
        return input;
    }

    private static string CalculateDaysElapsed(string dateInput)
    {
        if (DateTime.TryParse(dateInput, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt) ||
            DateTime.TryParse(dateInput, out dt))
        {
            int days = (int)(DateTime.Now - dt).TotalDays;
            return days.ToString(CultureInfo.InvariantCulture);
        }
        return "0";
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

    private static string CalculateRelativeDirectory(string fullPath, string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(fullPath))
        {
            return string.Empty;
        }

        try
        {
            string normFull = Path.GetFullPath(fullPath);
            string normRoot = Path.GetFullPath(rootPath);

            string relPath = Path.GetRelativePath(normRoot, normFull);
            if (relPath.Equals(".", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string? relDir = Path.GetDirectoryName(relPath);
            return string.IsNullOrEmpty(relDir) || relDir.Equals(".", StringComparison.Ordinal) ? string.Empty : relDir;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string CalculateRelativeFilePath(string fullPath, string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(fullPath))
        {
            return Path.GetFileName(fullPath);
        }

        try
        {
            string normFull = Path.GetFullPath(fullPath);
            string normRoot = Path.GetFullPath(rootPath);
            string rel = Path.GetRelativePath(normRoot, normFull);
            return rel.Equals(".", StringComparison.Ordinal) ? Path.GetFileName(fullPath) : rel;
        }
        catch
        {
            return Path.GetFileName(fullPath);
        }
    }
}
