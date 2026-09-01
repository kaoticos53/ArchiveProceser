namespace FileFlow.Sdk.TemplateEngine.Resolvers;

/// <summary>
/// Calculador de rutas relativas para plantillas de nombres y directorios.
/// </summary>
public static class PathRelativeCalculator
{
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
