using System.Globalization;

namespace FileFlow.Sdk.TemplateEngine;

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

            switch (domain.ToLowerInvariant())
            {
                case "exif":
                    if (item.Metadata.TryGetValue($"Exif:{key}", out var exifVal) ||
                        item.Metadata.TryGetValue(key, out exifVal))
                    {
                        return exifVal?.ToString() ?? string.Empty;
                    }
                    return string.Empty;

                case "regex":
                    if (item.Metadata.TryGetValue($"Regex:{key}", out var regVal) ||
                        item.Metadata.TryGetValue(key, out regVal))
                    {
                        return regVal?.ToString() ?? string.Empty;
                    }
                    return string.Empty;

                case "hash":
                    string hashKey = $"Hash:{key}";
                    string hashValStr = string.Empty;
                    if (item.Metadata.TryGetValue(hashKey, out var hVal) && hVal != null)
                    {
                        hashValStr = hVal.ToString() ?? string.Empty;
                    }
                    else if (item.Metadata.TryGetValue("Hash", out var directHash) && directHash != null)
                    {
                        hashValStr = directHash.ToString() ?? string.Empty;
                    }

                    if (!string.IsNullOrEmpty(hashValStr) && !string.IsNullOrEmpty(modifier) && int.TryParse(modifier, out int hashLen) && hashLen > 0)
                    {
                        return hashValStr.Length <= hashLen ? hashValStr : hashValStr[..hashLen];
                    }
                    return hashValStr;

                case "env":
                    return Environment.GetEnvironmentVariable(key) ?? string.Empty;

                case "inc nr":
                case "incnr":
                case "increment":
                    string incVal = item.Metadata.TryGetValue("Counter", out var inObj) && inObj != null ? inObj.ToString()! : "1";
                    if (int.TryParse(incVal, out int incNum) && !string.IsNullOrEmpty(key))
                    {
                        return incNum.ToString(key, CultureInfo.InvariantCulture);
                    }
                    return incVal;

                case "audio":
                case "id3":
                    if (item.Metadata.TryGetValue($"Audio:{key}", out var aVal) ||
                        item.Metadata.TryGetValue($"ID3:{key}", out aVal) ||
                        item.Metadata.TryGetValue(key, out aVal))
                    {
                        return aVal?.ToString() ?? string.Empty;
                    }
                    return string.Empty;

                case "video":
                    if (item.Metadata.TryGetValue($"Video:{key}", out var vVal) ||
                        item.Metadata.TryGetValue(key, out vVal))
                    {
                        return vVal?.ToString() ?? string.Empty;
                    }
                    return string.Empty;

                case "img":
                case "image":
                    if (item.Metadata.TryGetValue($"Img:{key}", out var iVal) ||
                        item.Metadata.TryGetValue($"Image:{key}", out iVal) ||
                        item.Metadata.TryGetValue(key, out iVal))
                    {
                        return iVal?.ToString() ?? string.Empty;
                    }
                    return string.Empty;

                case "date created":
                case "datecreated":
                case "date":
                case "creationdate":
                    string format = string.IsNullOrEmpty(key) ? "yyyy-MM-dd" : key;
                    if (item.Metadata.TryGetValue("CreationTimeUtc", out var ctVal) && ctVal is DateTime cdt)
                    {
                        return cdt.ToLocalTime().ToString(format, CultureInfo.InvariantCulture);
                    }
                    if (File.Exists(currentPath))
                    {
                        return File.GetCreationTime(currentPath).ToString(format, CultureInfo.InvariantCulture);
                    }
                    return DateTime.Now.ToString(format, CultureInfo.InvariantCulture);

                case "date modified":
                case "datemodified":
                case "modifieddate":
                    string mFormat = string.IsNullOrEmpty(key) ? "yyyy-MM-dd" : key;
                    if (item.Metadata.TryGetValue("LastWriteTimeUtc", out var mtVal) && mtVal is DateTime mdt)
                    {
                        return mdt.ToLocalTime().ToString(mFormat, CultureInfo.InvariantCulture);
                    }
                    if (File.Exists(currentPath))
                    {
                        return File.GetLastWriteTime(currentPath).ToString(mFormat, CultureInfo.InvariantCulture);
                    }
                    return DateTime.Now.ToString(mFormat, CultureInfo.InvariantCulture);

                case "date taken":
                case "datetaken":
                    string tFormat = string.IsNullOrEmpty(key) ? "yyyy-MM-dd" : key;
                    if (item.Metadata.TryGetValue("Exif:DateTimeOriginal", out var dtObj) ||
                        item.Metadata.TryGetValue("DateTaken", out dtObj))
                    {
                        if (dtObj is DateTime dtVal)
                        {
                            return dtVal.ToString(tFormat, CultureInfo.InvariantCulture);
                        }
                        if (DateTime.TryParse(dtObj?.ToString(), out var parsedDt))
                        {
                            return parsedDt.ToString(tFormat, CultureInfo.InvariantCulture);
                        }
                    }
                    return DateTime.Now.ToString(tFormat, CultureInfo.InvariantCulture);

                case "now":
                    string nFormat = string.IsNullOrEmpty(key) ? "yyyy-MM-dd_HH-mm-ss" : key;
                    return DateTime.Now.ToString(nFormat, CultureInfo.InvariantCulture);

                case "index":
                case "counter":
                    string idxVal = item.Metadata.TryGetValue("Counter", out var cObj) && cObj != null ? cObj.ToString()! : "1";
                    if (int.TryParse(idxVal, out int num) && !string.IsNullOrEmpty(key))
                    {
                        return num.ToString(key, CultureInfo.InvariantCulture);
                    }
                    return idxVal;

                case "filesize":
                case "size":
                    if (key.Equals("mb", StringComparison.OrdinalIgnoreCase))
                    {
                        string spec = modifier ?? "F2";
                        return (item.FileSizeBytes / (1024.0 * 1024.0)).ToString(spec, CultureInfo.InvariantCulture);
                    }
                    if (key.Equals("kb", StringComparison.OrdinalIgnoreCase))
                    {
                        string spec = modifier ?? "F1";
                        return (item.FileSizeBytes / 1024.0).ToString(spec, CultureInfo.InvariantCulture);
                    }
                    if (key.Equals("bytes", StringComparison.OrdinalIgnoreCase) || key.Equals("b", StringComparison.OrdinalIgnoreCase))
                    {
                        return item.FileSizeBytes.ToString(CultureInfo.InvariantCulture);
                    }
                    return (item.FileSizeBytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture);
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
                return CalculateRelativeDirectory(currentPath, effectiveRootPath);

            case "relativefilepath":
                return CalculateRelativeFilePath(currentPath, effectiveRootPath);

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

    public static string CalculateRelativeFilePath(string fullPath, string? rootPath)
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
