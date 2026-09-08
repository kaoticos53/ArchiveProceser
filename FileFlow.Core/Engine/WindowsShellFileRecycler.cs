using FileFlow.Core.Platform;

namespace FileFlow.Core.Engine;

/// <summary>
/// Proporciona eliminación segura enviando archivos y carpetas a la Papelera de reciclaje mediante el adaptador de plataforma.
/// </summary>
public class WindowsShellFileRecycler : IFileRecycler
{
    private static readonly Lazy<WindowsShellFileRecycler> _instance = new(() => new WindowsShellFileRecycler());
    public static WindowsShellFileRecycler Instance => _instance.Value;

    /// <inheritdoc />
    public bool Recycle(string path) => SendToRecycleBin(path);

    /// <summary>
    /// Envía un archivo o directorio a la Papelera de reciclaje de Windows sin solicitar confirmación interactiva.
    /// </summary>
    public static bool SendToRecycleBin(string path) =>
        WindowsPlatformService.Instance.MoveToTrash(path);
}
