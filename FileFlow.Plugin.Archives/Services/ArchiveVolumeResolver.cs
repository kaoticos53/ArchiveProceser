namespace FileFlow.Plugin.Archives.Services;

public static class ArchiveVolumeResolver
{
    public static List<string> FindRelatedVolumeFiles(string archivePath)
    {
        var volumes = new List<string> { archivePath };
        string? dir = Path.GetDirectoryName(archivePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return volumes;

        string fileNameNoExt = Path.GetFileNameWithoutExtension(archivePath);

        var matchPart = System.Text.RegularExpressions.Regex.Match(fileNameNoExt, @"^(.*?\.part)\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchPart.Success)
        {
            string prefix = matchPart.Groups[1].Value;
            var siblingParts = Directory.EnumerateFiles(dir, prefix + "*.rar", SearchOption.TopDirectoryOnly);
            foreach (var f in siblingParts)
            {
                if (!volumes.Contains(f, StringComparer.OrdinalIgnoreCase))
                    volumes.Add(f);
            }
            return volumes;
        }

        string baseName = Path.GetFileNameWithoutExtension(archivePath);
        var siblingZips = Directory.EnumerateFiles(dir, baseName + ".z*", SearchOption.TopDirectoryOnly);
        foreach (var f in siblingZips)
        {
            if (!volumes.Contains(f, StringComparer.OrdinalIgnoreCase))
                volumes.Add(f);
        }

        return volumes;
    }

    public static bool IsPrimaryArchiveFile(string filePath)
    {
        string fileName = Path.GetFileName(filePath).ToLowerInvariant();
        if (fileName.EndsWith(".part01.rar") || fileName.EndsWith(".part1.rar")) return true;
        if (System.Text.RegularExpressions.Regex.IsMatch(fileName, @"\.part(?!0*1\.)\d+\.rar$")) return false;
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".tgz" or ".bz2";
    }

    public static bool IsSecondaryVolumeFile(string filePath)
    {
        string fileName = Path.GetFileName(filePath).ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(fileName, @"\.(r\d{2,3}|z\d{2,3}|part(?!0*1\.)\d+\.rar)$");
    }

    public static string? GetCommonRootFolder(List<string> entryKeys)
    {
        if (entryKeys.Count == 0) return null;

        string firstKey = entryKeys[0];
        int slashIndex = firstKey.IndexOf('/');
        if (slashIndex <= 0) return null;

        string root = firstKey[..slashIndex];

        foreach (string key in entryKeys)
        {
            if (!key.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return root;
    }
}
