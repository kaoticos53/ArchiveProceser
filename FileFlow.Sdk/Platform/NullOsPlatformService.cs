using System.Runtime.InteropServices;

namespace FileFlow.Sdk.Platform;

/// <summary>
/// Implementación por defecto y fallback seguro para <see cref="IOsPlatformService"/>.
/// </summary>
public class NullOsPlatformService : IOsPlatformService
{
    private static readonly Lazy<NullOsPlatformService> _instance = new(() => new NullOsPlatformService());
    public static NullOsPlatformService Instance => _instance.Value;

    public OSPlatform Platform
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OSPlatform.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return OSPlatform.Linux;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OSPlatform.OSX;
            return OSPlatform.Create("UNKNOWN");
        }
    }

    public bool MoveToTrash(string path)
    {
        try
        {
            if (File.Exists(path)) { File.Delete(path); return true; }
            if (Directory.Exists(path)) { Directory.Delete(path, true); return true; }
        }
        catch { }
        return false;
    }

    public bool TrimWorkingSet() => false;

    public long GetAvailablePhysicalMemoryBytes()
    {
        try
        {
            return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        }
        catch
        {
            return 1024L * 1024L * 1024L; // 1 GB fallback
        }
    }

    public string GetDefaultShellExecutable() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/sh";

    public string GetDefaultShellArguments(string command) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {command}" : $"-c \"{command}\"";

    public bool OpenFolderInFileManager(string folderPath) => false;
    public bool OpenFileInFileManager(string filePath) => false;
    public bool OpenUrl(string url) => false;
}
