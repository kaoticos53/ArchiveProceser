using System;
using System.Collections.Generic;
using System.IO;

namespace FileFlow.Sdk;

/// <summary>
/// Utilidades de manipulación de rutas multi-plataforma independientes del sistema operativo anfitrión.
/// Permite manipular y calcular rutas de Windows o Unix tanto en Windows como en Linux/macOS.
/// </summary>
public static class CrossPlatformPath
{
    public static readonly char[] InvalidFileNameChars =
    [
        '\"', '<', '>', '|', ':', '*', '?', '\\', '/', '\0',
        (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
        (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
        (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30, (char)31
    ];

    public static readonly System.Buffers.SearchValues<char> InvalidFileNameCharsSearch =
        System.Buffers.SearchValues.Create(InvalidFileNameChars);

    public static string GetFileName(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        int lastSep = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return lastSep >= 0 ? path[(lastSep + 1)..] : path;
    }

    public static string GetFileNameWithoutExtension(string? path)
    {
        string fn = GetFileName(path);
        int dotIndex = fn.LastIndexOf('.');
        return dotIndex > 0 ? fn[..dotIndex] : fn;
    }

    public static string GetExtension(string? path)
    {
        string fn = GetFileName(path);
        int dotIndex = fn.LastIndexOf('.');
        return dotIndex >= 0 ? fn[dotIndex..] : string.Empty;
    }

    public static string GetDirectoryName(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        int lastSep = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return lastSep >= 0 ? path[..lastSep] : string.Empty;
    }

    public static bool IsWindowsDrivePath(string? path)
    {
        return !string.IsNullOrEmpty(path) &&
               path.Length >= 2 &&
               char.IsLetter(path[0]) &&
               path[1] == ':' &&
               (path.Length == 2 || path[2] == '\\' || path[2] == '/');
    }

    public static bool IsWindowsUncPath(string? path)
    {
        return !string.IsNullOrEmpty(path) &&
               (path.StartsWith(@"\\") || path.StartsWith("//"));
    }

    public static bool IsPathFullyQualified(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (path.StartsWith('/')) return true;
        if (IsWindowsDrivePath(path) || IsWindowsUncPath(path)) return true;
        return Path.IsPathFullyQualified(path);
    }

    public static string NormalizeWindowsPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        string drive = string.Empty;
        string rest = path;

        if (IsWindowsDrivePath(path))
        {
            drive = path[..2];
            rest = path[2..];
        }
        else if (IsWindowsUncPath(path))
        {
            int nextSlash = path.IndexOfAny(['\\', '/'], 2);
            if (nextSlash > 0)
            {
                drive = path[..nextSlash];
                rest = path[nextSlash..];
            }
        }

        var parts = rest.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();
        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            }
            else
            {
                stack.Add(part);
            }
        }

        string combinedRest = string.Join('\\', stack);
        return string.IsNullOrEmpty(drive) ? combinedRest : drive + "\\" + combinedRest;
    }

    public static string Combine(string basePath, string relativePath)
    {
        if (string.IsNullOrEmpty(basePath)) return relativePath ?? string.Empty;
        if (string.IsNullOrEmpty(relativePath)) return basePath;

        bool isWin = IsWindowsDrivePath(basePath) || IsWindowsUncPath(basePath);
        if (isWin)
        {
            string combined = basePath.TrimEnd('\\', '/') + "\\" + relativePath.TrimStart('\\', '/').Replace('/', '\\');
            return NormalizeWindowsPath(combined);
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.GetFullPath(Path.Combine(basePath, relativePath));
        }
        else
        {
            string rel = relativePath.Replace('\\', '/').TrimStart('/');
            string baseP = basePath.Replace('\\', '/');
            if (baseP.StartsWith('/'))
            {
                return Path.GetFullPath(Path.Combine(baseP, rel));
            }
            return Path.Combine(baseP, rel);
        }
    }

    public static string GetRelativePath(string rootPath, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(fullPath))
        {
            return fullPath ?? string.Empty;
        }

        string normRoot = rootPath.Replace('\\', '/').TrimEnd('/');
        string normFull = fullPath.Replace('\\', '/');

        if (normFull.StartsWith(normRoot, StringComparison.OrdinalIgnoreCase))
        {
            string rel = normFull[normRoot.Length..].TrimStart('/');
            if (string.IsNullOrEmpty(rel)) return ".";
            return rootPath.Contains('\\') ? rel.Replace('/', '\\') : rel;
        }

        if (OperatingSystem.IsWindows() || (!IsWindowsDrivePath(rootPath) && !IsWindowsDrivePath(fullPath)))
        {
            return Path.GetRelativePath(rootPath, fullPath);
        }

        return fullPath;
    }
}
