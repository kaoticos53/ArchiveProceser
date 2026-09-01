using System.Globalization;

namespace FileFlow.Sdk.TemplateEngine.Resolvers;

/// <summary>
/// Resolutor de variables estructuradas por dominio con sintaxis de dos puntos {Domain:Key:Modifier}.
/// </summary>
public static class DomainVariableResolver
{
    public static bool TryResolve(
        string domain, 
        string key, 
        string? modifier, 
        FileItemContext item, 
        string currentPath, 
        out string result)
    {
        switch (domain.ToLowerInvariant())
        {
            case "exif":
                if (item.Metadata.TryGetValue($"Exif:{key}", out var exifVal) ||
                    item.Metadata.TryGetValue(key, out exifVal))
                {
                    result = exifVal?.ToString() ?? string.Empty;
                    return true;
                }
                result = string.Empty;
                return true;

            case "regex":
                if (item.Metadata.TryGetValue($"Regex:{key}", out var regVal) ||
                    item.Metadata.TryGetValue(key, out regVal))
                {
                    result = regVal?.ToString() ?? string.Empty;
                    return true;
                }
                result = string.Empty;
                return true;

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
                    result = hashValStr.Length <= hashLen ? hashValStr : hashValStr[..hashLen];
                    return true;
                }
                result = hashValStr;
                return true;

            case "env":
                result = Environment.GetEnvironmentVariable(key) ?? string.Empty;
                return true;

            case "inc nr":
            case "incnr":
            case "increment":
                string incVal = item.Metadata.TryGetValue("Counter", out var inObj) && inObj != null ? inObj.ToString()! : "1";
                if (int.TryParse(incVal, out int incNum) && !string.IsNullOrEmpty(key))
                {
                    result = incNum.ToString(key, CultureInfo.InvariantCulture);
                    return true;
                }
                result = incVal;
                return true;

            case "audio":
            case "id3":
                if (item.Metadata.TryGetValue($"Audio:{key}", out var aVal) ||
                    item.Metadata.TryGetValue($"ID3:{key}", out aVal) ||
                    item.Metadata.TryGetValue(key, out aVal))
                {
                    result = aVal?.ToString() ?? string.Empty;
                    return true;
                }
                result = string.Empty;
                return true;

            case "video":
                if (item.Metadata.TryGetValue($"Video:{key}", out var vVal) ||
                    item.Metadata.TryGetValue(key, out vVal))
                {
                    result = vVal?.ToString() ?? string.Empty;
                    return true;
                }
                result = string.Empty;
                return true;

            case "img":
            case "image":
                if (item.Metadata.TryGetValue($"Img:{key}", out var iVal) ||
                    item.Metadata.TryGetValue($"Image:{key}", out iVal) ||
                    item.Metadata.TryGetValue(key, out iVal))
                {
                    result = iVal?.ToString() ?? string.Empty;
                    return true;
                }
                result = string.Empty;
                return true;

            case "date created":
            case "datecreated":
            case "date":
            case "creationdate":
                string format = string.IsNullOrEmpty(key) ? "yyyy-MM-dd" : key;
                if (item.Metadata.TryGetValue("CreationTimeUtc", out var ctVal) && ctVal is DateTime cdt)
                {
                    result = cdt.ToLocalTime().ToString(format, CultureInfo.InvariantCulture);
                    return true;
                }
                if (File.Exists(currentPath))
                {
                    result = File.GetCreationTime(currentPath).ToString(format, CultureInfo.InvariantCulture);
                    return true;
                }
                result = DateTime.Now.ToString(format, CultureInfo.InvariantCulture);
                return true;

            case "date modified":
            case "datemodified":
            case "modifieddate":
                string mFormat = string.IsNullOrEmpty(key) ? "yyyy-MM-dd" : key;
                if (item.Metadata.TryGetValue("LastWriteTimeUtc", out var mtVal) && mtVal is DateTime mdt)
                {
                    result = mdt.ToLocalTime().ToString(mFormat, CultureInfo.InvariantCulture);
                    return true;
                }
                if (File.Exists(currentPath))
                {
                    result = File.GetLastWriteTime(currentPath).ToString(mFormat, CultureInfo.InvariantCulture);
                    return true;
                }
                result = DateTime.Now.ToString(mFormat, CultureInfo.InvariantCulture);
                return true;

            case "date taken":
            case "datetaken":
                string tFormat = string.IsNullOrEmpty(key) ? "yyyy-MM-dd" : key;
                if (item.Metadata.TryGetValue("Exif:DateTimeOriginal", out var dtObj) ||
                    item.Metadata.TryGetValue("DateTaken", out dtObj))
                {
                    if (dtObj is DateTime dtVal)
                    {
                        result = dtVal.ToString(tFormat, CultureInfo.InvariantCulture);
                        return true;
                    }
                    if (DateTime.TryParse(dtObj?.ToString(), out var parsedDt))
                    {
                        result = parsedDt.ToString(tFormat, CultureInfo.InvariantCulture);
                        return true;
                    }
                }
                result = DateTime.Now.ToString(tFormat, CultureInfo.InvariantCulture);
                return true;

            case "now":
                string nFormat = string.IsNullOrEmpty(key) ? "yyyy-MM-dd_HH-mm-ss" : key;
                result = DateTime.Now.ToString(nFormat, CultureInfo.InvariantCulture);
                return true;

            case "index":
            case "counter":
                string idxVal = item.Metadata.TryGetValue("Counter", out var cObj) && cObj != null ? cObj.ToString()! : "1";
                if (int.TryParse(idxVal, out int num) && !string.IsNullOrEmpty(key))
                {
                    result = num.ToString(key, CultureInfo.InvariantCulture);
                    return true;
                }
                result = idxVal;
                return true;

            case "filesize":
            case "size":
                if (key.Equals("mb", StringComparison.OrdinalIgnoreCase))
                {
                    string spec = modifier ?? "F2";
                    result = (item.FileSizeBytes / (1024.0 * 1024.0)).ToString(spec, CultureInfo.InvariantCulture);
                    return true;
                }
                if (key.Equals("kb", StringComparison.OrdinalIgnoreCase))
                {
                    string spec = modifier ?? "F1";
                    result = (item.FileSizeBytes / 1024.0).ToString(spec, CultureInfo.InvariantCulture);
                    return true;
                }
                if (key.Equals("bytes", StringComparison.OrdinalIgnoreCase) || key.Equals("b", StringComparison.OrdinalIgnoreCase))
                {
                    result = item.FileSizeBytes.ToString(CultureInfo.InvariantCulture);
                    return true;
                }
                result = (item.FileSizeBytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture);
                return true;

            default:
                result = string.Empty;
                return false;
        }
    }
}
