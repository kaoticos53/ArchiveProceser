using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FileFlow.Sdk.Platform;

namespace FileFlow.Core.Platform;

/// <summary>
/// Adaptador de servicios de plataforma para Microsoft Windows.
/// Encapsula de forma estricta las llamadas P/Invoke a Win32 (shell32.dll, kernel32.dll).
/// </summary>
public sealed class WindowsPlatformService : IOsPlatformService
{
    private static readonly Lazy<WindowsPlatformService> _instance = new(() => new WindowsPlatformService());
    public static WindowsPlatformService Instance => _instance.Value;

    public OSPlatform Platform => OSPlatform.Windows;

    public bool MoveToTrash(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return false;
        }

        try
        {
            // Windows SHFileOperation requiere que la cadena pFrom termine con doble terminador nulo (\0\0)
            string nullTerminatedPath = Path.GetFullPath(path) + "\0\0";

            var fileOp = new SHFILEOPSTRUCT
            {
                hwnd = IntPtr.Zero,
                wFunc = 0x0003, // FO_DELETE
                pFrom = nullTerminatedPath,
                pTo = null,
                fFlags = 0x0040 | 0x0010 | 0x0004 | 0x0400, // FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
                fAnyOperationsAborted = false,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = null
            };

            int result = SHFileOperation(ref fileOp);
            return result == 0 && !fileOp.fAnyOperationsAborted;
        }
        catch
        {
            // Fallback preventivo si falla la llamada nativa: borrado estándar
            try
            {
                if (File.Exists(path)) { File.Delete(path); return true; }
                if (Directory.Exists(path)) { Directory.Delete(path, true); return true; }
            }
            catch { }
            return false;
        }
    }

    public bool TrimWorkingSet()
    {
        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            return SetProcessWorkingSetSize(currentProcess.Handle, -1, -1);
        }
        catch
        {
            return false;
        }
    }

    public long GetAvailablePhysicalMemoryBytes()
    {
        try
        {
            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            if (GlobalMemoryStatusEx(ref memStatus) && memStatus.ullAvailPhys > 0)
            {
                return (long)memStatus.ullAvailPhys;
            }
        }
        catch { }

        try
        {
            return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        }
        catch
        {
            return 2L * 1024L * 1024L * 1024L; // 2 GB fallback
        }
    }

    public string GetDefaultShellExecutable() => "cmd.exe";

    public string GetDefaultShellArguments(string command) => $"/c {command}";

    public bool OpenFolderInFileManager(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;
        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(folderPath);
            if (!Directory.Exists(expanded))
            {
                Directory.CreateDirectory(expanded);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = expanded,
                UseShellExecute = true,
                Verb = "open"
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
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
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
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    #region Win32 P/Invoke Definitions

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation([In, Out] ref SHFILEOPSTRUCT lpFileOp);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(nint hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    #endregion
}
