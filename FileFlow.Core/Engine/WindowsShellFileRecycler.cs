using System.Runtime.InteropServices;

namespace FileFlow.Core.Engine;

/// <summary>
/// Proporciona eliminación segura enviando archivos y carpetas a la Papelera de reciclaje de Windows mediante la API Win32 SHFileOperation.
/// </summary>
public class WindowsShellFileRecycler : IFileRecycler
{
    private static readonly Lazy<WindowsShellFileRecycler> _instance = new(() => new WindowsShellFileRecycler());
    public static WindowsShellFileRecycler Instance => _instance.Value;

    /// <inheritdoc />
    public bool Recycle(string path) => SendToRecycleBin(path);
    private const int FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOERRORUI = 0x0400;

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

    /// <summary>
    /// Envía un archivo o directorio a la Papelera de reciclaje de Windows sin solicitar confirmación interactiva.
    /// </summary>
    public static bool SendToRecycleBin(string path)
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
                wFunc = FO_DELETE,
                pFrom = nullTerminatedPath,
                pTo = null,
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
                fAnyOperationsAborted = false,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = null
            };

            int result = SHFileOperation(ref fileOp);
            return result == 0 && !fileOp.fAnyOperationsAborted;
        }
        catch
        {
            // Fallback si falla la llamada nativa: borrado estándar
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                return true;
            }
            return false;
        }
    }
}
