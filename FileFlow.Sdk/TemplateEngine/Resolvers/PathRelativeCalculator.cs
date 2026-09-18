using System;
using System.IO;

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
            string relPath = CrossPlatformPath.GetRelativePath(rootPath, fullPath);
            if (relPath.Equals(".", StringComparison.Ordinal) || relPath.Equals(fullPath, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string relDir = CrossPlatformPath.GetDirectoryName(relPath);
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
            return CrossPlatformPath.GetFileName(fullPath);
        }

        try
        {
            string rel = CrossPlatformPath.GetRelativePath(rootPath, fullPath);
            return rel.Equals(".", StringComparison.Ordinal) ? CrossPlatformPath.GetFileName(fullPath) : rel;
        }
        catch
        {
            return CrossPlatformPath.GetFileName(fullPath);
        }
    }
}
