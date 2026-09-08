using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FileFlow.Sdk.Platform;

namespace FileFlow.Core.Platform;

/// <summary>
/// Adaptador de servicios de plataforma para Apple macOS (OSX).
/// Soporta Finder trash via AppleScript, ejecución en zsh y comandos 'open'.
/// </summary>
public sealed class MacPlatformService : IOsPlatformService
{
    private static readonly Lazy<MacPlatformService> _instance = new(() => new MacPlatformService());
    public static MacPlatformService Instance => _instance.Value;

    public OSPlatform Platform => OSPlatform.OSX;

    public bool MoveToTrash(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!File.Exists(path) && !Directory.Exists(path)) return false;

        string fullPath = Path.GetFullPath(path);

        // 1. Intentar AppleScript oficial a través de Finder
        try
        {
            string script = $"tell application \"Finder\" to delete POSIX file \"{fullPath}\"";
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e \"{script.Replace("\"", "\\\"")}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            if (proc != null)
            {
                proc.WaitForExit(3000);
                if (proc.ExitCode == 0) return true;
            }
        }
        catch { }

        // 2. Fallback a ~/.Trash
        try
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string trashDir = Path.Combine(homeDir, ".Trash");
            if (Directory.Exists(trashDir))
            {
                string fileName = Path.GetFileName(fullPath);
                string destFile = Path.Combine(trashDir, fileName);
                int counter = 1;
                while (File.Exists(destFile) || Directory.Exists(destFile))
                {
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    destFile = Path.Combine(trashDir, $"{nameWithoutExt}_{counter}{ext}");
                    counter++;
                }

                if (File.Exists(fullPath)) File.Move(fullPath, destFile);
                else if (Directory.Exists(fullPath)) Directory.Move(fullPath, destFile);
                return true;
            }
        }
        catch { }

        // 3. Fallback a eliminación normal
        try
        {
            if (File.Exists(path)) { File.Delete(path); return true; }
            if (Directory.Exists(path)) { Directory.Delete(path, true); return true; }
        }
        catch { }
        return false;
    }

    public bool TrimWorkingSet() => true;

    public long GetAvailablePhysicalMemoryBytes()
    {
        try
        {
            return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        }
        catch
        {
            return 2L * 1024L * 1024L * 1024L; // 2 GB fallback
        }
    }

    public string GetDefaultShellExecutable() => "/bin/zsh";

    public string GetDefaultShellArguments(string command) => $"-c \"{command.Replace("\"", "\\\"")}\"";

    public bool OpenFolderInFileManager(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = false
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool OpenFileInFileManager(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"-R \"{filePath}\"",
                UseShellExecute = false
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"\"{url}\"",
                UseShellExecute = false
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
