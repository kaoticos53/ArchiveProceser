using System.Diagnostics;
using System.IO;

namespace FileFlow.App.Services;

/// <summary>
/// Localizador y lanzador de recursos de la aplicación (Manuales, Documentación y Ejemplos de Flujos).
/// </summary>
public static class AppResourceLocator
{
    public static string? FindFileInAppOrRepo(string installedSubdir, string installedFileName, string repoRelativePath)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string installedPath = Path.Combine(baseDir, installedSubdir, installedFileName);
        if (File.Exists(installedPath)) return installedPath;

        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    public static string? FindDirectoryInAppOrRepo(string installedSubdir, string repoRelativePath)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string installedPath = Path.Combine(baseDir, installedSubdir);
        if (Directory.Exists(installedPath)) return installedPath;

        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    public static bool TryOpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
