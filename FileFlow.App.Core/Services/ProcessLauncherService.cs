using System.Diagnostics;
using FileFlow.Core.Platform;

namespace FileFlow.App.Services;

/// <summary>
/// Adaptador de infraestructura para <see cref="IProcessLauncherService"/> utilizando <see cref="OsPlatformServiceFactory"/>.
/// </summary>
public class ProcessLauncherService : IProcessLauncherService
{
    private static readonly Lazy<ProcessLauncherService> _instance = new(() => new ProcessLauncherService());
    public static ProcessLauncherService Instance => _instance.Value;

    public bool OpenUrl(string url) =>
        OsPlatformServiceFactory.Instance.OpenUrl(url);

    public bool OpenFolder(string folderPath) =>
        OsPlatformServiceFactory.Instance.OpenFolderInFileManager(folderPath);

    public bool OpenFileInExplorer(string filePath) =>
        OsPlatformServiceFactory.Instance.OpenFileInFileManager(filePath);

    public bool StartProcess(string fileName, string? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                psi.Arguments = arguments;
            }
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
