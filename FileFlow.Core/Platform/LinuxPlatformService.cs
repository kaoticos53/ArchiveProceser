using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FileFlow.Sdk.Platform;

namespace FileFlow.Core.Platform;

/// <summary>
/// Adaptador de servicios de plataforma para Linux.
/// Soporta la especificación FreeDesktop.org Trash, ejecución en sh/bash y xdg-open.
/// </summary>
public sealed class LinuxPlatformService : IOsPlatformService
{
    private static readonly Lazy<LinuxPlatformService> _instance = new(() => new LinuxPlatformService());
    public static LinuxPlatformService Instance => _instance.Value;

    public OSPlatform Platform => OSPlatform.Linux;

    public bool MoveToTrash(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!File.Exists(path) && !Directory.Exists(path)) return false;

        string fullPath = Path.GetFullPath(path);

        // 1. Intentar comando estándar 'gio trash'
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "gio",
                Arguments = $"trash \"{fullPath}\"",
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

        // 2. Fallback según FreeDesktop.org Trash Specification (~/.local/share/Trash)
        try
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(homeDir, ".local", "share");
            string trashFilesDir = Path.Combine(xdgData, "Trash", "files");
            string trashInfoDir = Path.Combine(xdgData, "Trash", "info");

            Directory.CreateDirectory(trashFilesDir);
            Directory.CreateDirectory(trashInfoDir);

            string fileName = Path.GetFileName(fullPath);
            string destFile = Path.Combine(trashFilesDir, fileName);
            string infoFile = Path.Combine(trashInfoDir, $"{fileName}.trashinfo");

            int counter = 1;
            while (File.Exists(destFile) || Directory.Exists(destFile))
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                destFile = Path.Combine(trashFilesDir, $"{nameWithoutExt}_{counter}{ext}");
                infoFile = Path.Combine(trashInfoDir, $"{nameWithoutExt}_{counter}{ext}.trashinfo");
                counter++;
            }

            // Mover archivo o carpeta
            if (File.Exists(fullPath))
            {
                File.Move(fullPath, destFile);
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Move(fullPath, destFile);
            }

            // Escribir archivo de metadatos .trashinfo
            string trashInfoContent = $"[Trash Info]\nPath={fullPath}\nDeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}\n";
            File.WriteAllText(infoFile, trashInfoContent);

            return true;
        }
        catch
        {
            // 3. Fallback a eliminación normal
            try
            {
                if (File.Exists(path)) { File.Delete(path); return true; }
                if (Directory.Exists(path)) { Directory.Delete(path, true); return true; }
            }
            catch { }
            return false;
        }
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

    public string GetDefaultShellExecutable() => "/bin/sh";

    public string GetDefaultShellArguments(string command) => $"-c \"{command.Replace("\"", "\\\"")}\"";

    public bool OpenFolderInFileManager(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
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
        string dir = Path.GetDirectoryName(filePath) ?? filePath;
        return OpenFolderInFileManager(dir);
    }

    public bool OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
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
