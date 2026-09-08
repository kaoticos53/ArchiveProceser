using System.Globalization;
using FileFlow.Sdk.Storage;
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

            // Soporte para versiones de archivo {File:Tag}
            if (string.Equals(domain, "File", StringComparison.OrdinalIgnoreCase))
            {
                string? versionPath = item.GetVersionPath(key);
                if (!string.IsNullOrEmpty(versionPath))
                {
                    return versionPath;
                }
            }

            // Soporte para tamaños de versiones {FileSize:Tag}, {FileSizeKB:Tag}, {FileSizeMB:Tag}
            if (string.Equals(domain, "FileSize", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(domain, "FileSizeBytes", StringComparison.OrdinalIgnoreCase))
            {
                string? versionPath = item.GetVersionPath(key);
                if (!string.IsNullOrEmpty(versionPath) && File.Exists(versionPath))
                {
                    return new FileInfo(versionPath).Length.ToString(CultureInfo.InvariantCulture);
                }
                if (item.Metadata.TryGetValue($"FileSize:{key}", out var fSize) && fSize != null)
                {
                    return fSize.ToString()!;
                }
            }
            if (string.Equals(domain, "FileSizeKB", StringComparison.OrdinalIgnoreCase))
            {
                string? versionPath = item.GetVersionPath(key);
                if (!string.IsNullOrEmpty(versionPath) && File.Exists(versionPath))
                {
                    return (new FileInfo(versionPath).Length / 1024.0).ToString("F1", CultureInfo.InvariantCulture);
                }
                if (item.Metadata.TryGetValue($"FileSizeKB:{key}", out var fSize) && fSize != null)
                {
                    return fSize.ToString()!;
                }
            }
            if (string.Equals(domain, "FileSizeMB", StringComparison.OrdinalIgnoreCase))
            {
                string? versionPath = item.GetVersionPath(key);
                if (!string.IsNullOrEmpty(versionPath) && File.Exists(versionPath))
                {
                    return (new FileInfo(versionPath).Length / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture);
                }
                if (item.Metadata.TryGetValue($"FileSizeMB:{key}", out var fSize) && fSize != null)
                {
                    return fSize.ToString()!;
                }
            }

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
            case "filenamewithoutextension":
            case "filebasename":
                return Path.GetFileNameWithoutExtension(currentPath);

            case "extension":
            case "ext":
                return Path.GetExtension(currentPath).TrimStart('.');

            case "currentpath":
                return currentPath;

            case "originalpath":
                return originalPath;

            case "currentdir":
            case "sourcedir":
                return Path.GetDirectoryName(currentPath) ?? string.Empty;

            case "originaldir":
                return Path.GetDirectoryName(originalPath) ?? string.Empty;

            case "originaldirectoryname":
                string? oDir = Path.GetDirectoryName(originalPath);
                return string.IsNullOrEmpty(oDir) ? string.Empty : Path.GetFileName(oDir);

            case "parentdir":
            case "dirname":
                string? pDir = Path.GetDirectoryName(currentPath);
                return string.IsNullOrEmpty(pDir) ? string.Empty : Path.GetFileName(pDir);

            case "date":
                return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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
                string pathForRelDir = !string.IsNullOrWhiteSpace(originalPath) ? originalPath : currentPath;
                return PathRelativeCalculator.CalculateRelativeDirectory(pathForRelDir, effectiveRootPath);

            case "relativefilepath":
                string pathForRelFile = !string.IsNullOrWhiteSpace(originalPath) ? originalPath : currentPath;
                return PathRelativeCalculator.CalculateRelativeFilePath(pathForRelFile, effectiveRootPath);

            case "datenow":
                return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            case "timenow":
                return DateTime.Now.ToString("HH-mm-ss", CultureInfo.InvariantCulture);

            case "datetimenow":
                return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);

            case "globaloutputdir":
            case "defaultoutputdir":
            case "defaultglobaloutputdir":
            case "globaloutput":
            case "defaultoutput":
            case "globaloutputpath":
            case "defaultoutputpath":
            case "outputdir":
            case "defaultdir":
                if (item.Metadata.TryGetValue("GlobalOutputDir", out var godVal) && godVal != null && !string.IsNullOrWhiteSpace(godVal.ToString()))
                {
                    return godVal.ToString()!;
                }
                if (item.Metadata.TryGetValue("DefaultGlobalOutputDir", out var dgodVal) && dgodVal != null && !string.IsNullOrWhiteSpace(dgodVal.ToString()))
                {
                    return dgodVal.ToString()!;
                }
                if (item.Metadata.TryGetValue("DefaultOutputDir", out var dodVal) && dodVal != null && !string.IsNullOrWhiteSpace(dodVal.ToString()))
                {
                    return dodVal.ToString()!;
                }
                if (item.Metadata.TryGetValue("GlobalOutputPath", out var gopVal) && gopVal != null && !string.IsNullOrWhiteSpace(gopVal.ToString()))
                {
                    return gopVal.ToString()!;
                }
                if (item.Metadata.TryGetValue("DefaultOutputPath", out var dopVal) && dopVal != null && !string.IsNullOrWhiteSpace(dopVal.ToString()))
                {
                    return dopVal.ToString()!;
                }
                return AppPaths.DefaultGlobalOutputDir;

            case "tempdir":
            case "temporarydir":
            case "tempworkingdir":
            case "temp":
            case "tmp":
                if (item.Metadata.TryGetValue("TemporaryDirectory", out var tdVal) && tdVal != null && !string.IsNullOrWhiteSpace(tdVal.ToString()))
                {
                    return tdVal.ToString()!;
                }
                if (item.Metadata.TryGetValue("TempDir", out var td2Val) && td2Val != null && !string.IsNullOrWhiteSpace(td2Val.ToString()))
                {
                    return td2Val.ToString()!;
                }
                return AppPaths.DefaultTempDirectory;

            case "randomid":
            case "random":
                return Guid.NewGuid().ToString("N")[..8];

            case "guid":
            case "uuid":
                return Guid.NewGuid().ToString("D");

            case "originalfilesize":
            case "originalfilesizebytes":
            case "originalsize":
            case "originalsizebytes":
                if (TryGetFileSize(item, "OriginalFileSizeBytes", "OriginalFileSize", out long origBytes))
                {
                    return origBytes.ToString(CultureInfo.InvariantCulture);
                }
                return item.FileSizeBytes.ToString(CultureInfo.InvariantCulture);

            case "originalfilesizekb":
            case "originalsizekb":
                TryGetFileSize(item, "OriginalFileSizeBytes", "OriginalFileSize", out long origKbBytes);
                return (origKbBytes / 1024.0).ToString("F1", CultureInfo.InvariantCulture);

            case "originalfilesizemb":
            case "originalsizemb":
                TryGetFileSize(item, "OriginalFileSizeBytes", "OriginalFileSize", out long origMbBytes);
                return (origMbBytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture);

            case "outputfilesize":
            case "outputfilesizebytes":
            case "outputsize":
            case "outputsizebytes":
                if (TryGetFileSize(item, "OutputFileSizeBytes", "OutputFileSize", out long outBytes))
                {
                    return outBytes.ToString(CultureInfo.InvariantCulture);
                }
                return item.FileSizeBytes.ToString(CultureInfo.InvariantCulture);

            case "outputfilesizekb":
            case "outputsizekb":
                TryGetFileSize(item, "OutputFileSizeBytes", "OutputFileSize", out long outKbBytes);
                if (outKbBytes == 0) outKbBytes = item.FileSizeBytes;
                return (outKbBytes / 1024.0).ToString("F1", CultureInfo.InvariantCulture);

            case "outputfilesizemb":
            case "outputsizemb":
                TryGetFileSize(item, "OutputFileSizeBytes", "OutputFileSize", out long outMbBytes);
                if (outMbBytes == 0) outMbBytes = item.FileSizeBytes;
                return (outMbBytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture);

            case "savedbytes":
                if (item.Metadata.TryGetValue("SavedBytes", out var sbVal) && sbVal != null && long.TryParse(sbVal.ToString(), out long sBytes))
                {
                    return sBytes.ToString(CultureInfo.InvariantCulture);
                }
                if (TryGetFileSize(item, "OriginalFileSizeBytes", "OriginalFileSize", out long oBytes) &&
                    TryGetFileSize(item, "OutputFileSizeBytes", "OutputFileSize", out long resBytes))
                {
                    return (oBytes - resBytes).ToString(CultureInfo.InvariantCulture);
                }
                return "0";

            case "savedpercent":
            case "savedpct":
                if (item.Metadata.TryGetValue("SavedPercent", out var spVal) && spVal != null)
                {
                    if (spVal is IFormattable formattableSp)
                    {
                        return formattableSp.ToString(null, CultureInfo.InvariantCulture);
                    }
                    return Convert.ToString(spVal, CultureInfo.InvariantCulture) ?? "0.0";
                }
                return "0.0";

            case "compressionratio":
                if (item.Metadata.TryGetValue("CompressionRatio", out var crVal) && crVal != null)
                {
                    if (crVal is IFormattable formattableCr)
                    {
                        return formattableCr.ToString(null, CultureInfo.InvariantCulture);
                    }
                    return Convert.ToString(crVal, CultureInfo.InvariantCulture) ?? "1.0";
                }
                return "1.0";

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

    private static bool TryGetFileSize(FileItemContext item, string primaryKey, string secondaryKey, out long size)
    {
        size = 0;
        if (item.Metadata.TryGetValue(primaryKey, out var val) && val != null && long.TryParse(val.ToString(), out size))
        {
            return true;
        }
        if (item.Metadata.TryGetValue(secondaryKey, out var val2) && val2 != null && long.TryParse(val2.ToString(), out size))
        {
            return true;
        }
        return false;
    }
}
