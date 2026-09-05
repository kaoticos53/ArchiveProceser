using System.Diagnostics;
using System.IO;

namespace FileFlow.App.Services;

/// <summary>
/// Adaptador de infraestructura para <see cref="IProcessLauncherService"/> utilizando <see cref="Process"/>.
/// </summary>
public class ProcessLauncherService : IProcessLauncherService
{
    private static readonly Lazy<ProcessLauncherService> _instance = new(() => new ProcessLauncherService());
    public static ProcessLauncherService Instance => _instance.Value;

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

    public bool OpenFolder(string folderPath)
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

    public bool OpenFileInExplorer(string filePath)
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
