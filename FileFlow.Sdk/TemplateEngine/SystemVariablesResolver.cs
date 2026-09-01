using System.Globalization;
using FileFlow.Sdk.TemplateEngine.Resolvers;

namespace FileFlow.Sdk.TemplateEngine;

/// <summary>
/// Resolutor transversal de variables y tokens de sistema, fecha, rutas y metadatos.
/// </summary>
public static class SystemVariablesResolver
{
    public static string GetVariableValue(string varName, FileItemContext item, string? sourceRootPath)
    {
        if (string.IsNullOrWhiteSpace(varName)) return string.Empty;

        varName = varName.Trim();
        if (varName.StartsWith('{') && varName.EndsWith('}'))
        {
            varName = varName[1..^1].Trim();
        }
        if (varName.StartsWith('$'))
        {
            varName = varName.TrimStart('$').TrimStart('{').TrimEnd('}').Trim();
        }

        string currentPath = item.CurrentPath ?? string.Empty;
        string originalPath = item.OriginalPath ?? string.Empty;

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

        if (varName.Contains(':'))
        {
            var parts = varName.Split(':', 3);
            string domain = parts[0].Trim();
            string key = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            string? modifier = parts.Length > 2 ? parts[2].Trim() : null;

            if (DomainVariableResolver.TryResolve(domain, key, modifier, item, currentPath, out string domainResult))
            {
                return domainResult;
            }
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

            case "parentdir":
            case "dirname":
                string? pDir = Path.GetDirectoryName(currentPath);
                return string.IsNullOrEmpty(pDir) ? string.Empty : Path.GetFileName(pDir);

            case "inc nr":
            case "incnr":
            case "counter":
            case "index":
                return item.Metadata.TryGetValue("Counter", out var cVal) && cVal != null ? cVal.ToString()! : "1";

            case "file count":
            case "filecount":
            case "totalcount":
                return item.Metadata.TryGetValue("TotalFileCount", out var tcVal) && tcVal != null ? tcVal.ToString()! : "1";

            case "year":
                return DateTime.Now.ToString("yyyy", CultureInfo.InvariantCulture);

            case "month":
                return DateTime.Now.ToString("MM", CultureInfo.InvariantCulture);

            case "day":
                return DateTime.Now.ToString("dd", CultureInfo.InvariantCulture);

            case "hour":
                return DateTime.Now.ToString("HH", CultureInfo.InvariantCulture);

            case "min":
            case "minute":
                return DateTime.Now.ToString("mm", CultureInfo.InvariantCulture);

            case "sec":
            case "second":
                return DateTime.Now.ToString("ss", CultureInfo.InvariantCulture);

            case "img width":
            case "imagewidth":
                return item.Metadata.TryGetValue("Exif:ImageWidth", out var iw) || item.Metadata.TryGetValue("Img:Width", out iw) ? iw?.ToString() ?? string.Empty : string.Empty;

            case "img height":
            case "imageheight":
                return item.Metadata.TryGetValue("Exif:ImageHeight", out var ih) || item.Metadata.TryGetValue("Img:Height", out ih) ? ih?.ToString() ?? string.Empty : string.Empty;

            case "relativepath":
            case "relativedir":
            case "relativedirectory":
                return PathRelativeCalculator.CalculateRelativeDirectory(currentPath, effectiveRootPath);

            case "relativefilepath":
                return PathRelativeCalculator.CalculateRelativeFilePath(currentPath, effectiveRootPath);

            case "datenow":
                return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            case "timenow":
                return DateTime.Now.ToString("HH-mm-ss", CultureInfo.InvariantCulture);

            case "datetimenow":
                return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);

            case "globaloutputdir":
            case "defaultoutputdir":
                if (item.Metadata.TryGetValue("GlobalOutputDir", out var godVal) && godVal != null && !string.IsNullOrWhiteSpace(godVal.ToString()))
                {
                    return godVal.ToString()!;
                }
                return Path.Combine(Environment.CurrentDirectory, "Output");

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
                if (item.Metadata.TryGetValue($"Regex:{varName}", out var regVal) && regVal != null)
                {
                    return regVal.ToString() ?? string.Empty;
                }
                return string.Empty;
        }
    }

    public static string CalculateRelativeDirectory(string fullPath, string? rootPath)
    {
        return PathRelativeCalculator.CalculateRelativeDirectory(fullPath, rootPath);
    }

    public static string CalculateRelativeFilePath(string fullPath, string? rootPath)
    {
        return PathRelativeCalculator.CalculateRelativeFilePath(fullPath, rootPath);
    }
}
