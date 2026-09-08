using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.Storage;

namespace FileFlow.Core.Services;

/// <summary>
/// Servicio centralizado de resolución y configuración de ejecutables de herramientas externas.
/// Soporta autodetección multiplataforma (Windows, Linux, macOS) y carpetas portables.
/// </summary>
public class ExternalToolsService : IExternalToolsService
{
    private static readonly Lazy<ExternalToolsService> _instance = new(() => new ExternalToolsService());
    public static ExternalToolsService Instance => _instance.Value;

    private readonly string _configFilePath;
    private ExternalToolsConfig _config = new();
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, string> _resolvedToolCache = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? ToolsConfigChanged;

    public ExternalToolsService()
    {
        AppPaths.EnsureDirectories();
        _configFilePath = AppPaths.ExternalToolsFile;
        LoadConfig();
    }

    public ExternalToolsConfig Config
    {
        get
        {
            lock (_lock)
            {
                return new ExternalToolsConfig
                {
                    FfmpegPath = _config.FfmpegPath,
                    FfprobePath = _config.FfprobePath,
                    SevenZipPath = _config.SevenZipPath,
                    PythonPath = _config.PythonPath
                };
            }
        }
    }

    public string FfmpegExecutable => ResolveToolPath("ffmpeg", _config.FfmpegPath);
    public string FfprobeExecutable => ResolveToolPath("ffprobe", _config.FfprobePath);
    public string SevenZipExecutable => ResolveToolPath("7z", _config.SevenZipPath);
    public string PythonExecutable => ResolveToolPath("python", _config.PythonPath);

    public string ResolveToolPath(string toolName) =>
        ResolveToolPath(toolName, GetConfiguredPathForTool(toolName));

    public bool IsToolAvailable(string toolName)
    {
        string path = ResolveToolPath(toolName);
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (File.Exists(path)) return true;

        // Fallback rápido sin bloquear threads pesadamente
        return false;
    }

    public async Task<bool> IsToolAvailableAsync(string toolName, CancellationToken cancellationToken = default)
    {
        string path = ResolveToolPath(toolName);
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (File.Exists(path)) return true;

        try
        {
            var result = await FileFlow.Sdk.Platform.ProcessRunner.Instance.RunAsync(new FileFlow.Sdk.Platform.ProcessExecutionRequest
            {
                FileName = path,
                Arguments = "-version",
                Timeout = TimeSpan.FromSeconds(2)
            }, cancellationToken).ConfigureAwait(false);
            return result.Success;
        }
        catch { }

        return false;
    }

    public void SaveConfig(ExternalToolsConfig newConfig)
    {
        lock (_lock)
        {
            _config = new ExternalToolsConfig
            {
                FfmpegPath = newConfig.FfmpegPath?.Trim() ?? string.Empty,
                FfprobePath = newConfig.FfprobePath?.Trim() ?? string.Empty,
                SevenZipPath = newConfig.SevenZipPath?.Trim() ?? string.Empty,
                PythonPath = newConfig.PythonPath?.Trim() ?? string.Empty
            };
            _resolvedToolCache.Clear();

            try
            {
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch { }
        }

        ToolsConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<ExternalToolsConfig> AutoDetectToolsAsync()
    {
        return Task.Run(() =>
        {
            string ffmpeg = FindExecutable(OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg", "ffmpeg");
            string ffprobe = FindExecutable(OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe", "ffprobe");
            string sevenZip = FindExecutable(OperatingSystem.IsWindows() ? "7z.exe" : "7z", "7-Zip");
            string python = FindExecutable(OperatingSystem.IsWindows() ? "python.exe" : "python3", "Python");

            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(ffmpeg)) _config.FfmpegPath = ffmpeg;
                if (!string.IsNullOrWhiteSpace(ffprobe)) _config.FfprobePath = ffprobe;
                if (!string.IsNullOrWhiteSpace(sevenZip)) _config.SevenZipPath = sevenZip;
                if (!string.IsNullOrWhiteSpace(python)) _config.PythonPath = python;

                _resolvedToolCache.Clear();

                try
                {
                    string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_configFilePath, json);
                }
                catch { }
            }

            ToolsConfigChanged?.Invoke(this, EventArgs.Empty);
            return Config;
        });
    }

    private string GetConfiguredPathForTool(string toolName)
    {
        string norm = toolName.ToLowerInvariant();
        if (norm.Contains("ffmpeg")) return _config.FfmpegPath;
        if (norm.Contains("ffprobe")) return _config.FfprobePath;
        if (norm.Contains("7z")) return _config.SevenZipPath;
        if (norm.Contains("python")) return _config.PythonPath;
        return string.Empty;
    }

    private string ResolveToolPath(string baseName, string configuredPath)
    {
        string exeName = OperatingSystem.IsWindows() && !baseName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? $"{baseName}.exe"
            : baseName;

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string resolved = AppPaths.ResolveApplicationPath(configuredPath);
            if (File.Exists(resolved)) return resolved;
        }

        return _resolvedToolCache.GetOrAdd(exeName, name =>
        {
            // 1. Búsqueda en carpeta local portable 'tools/'
            string localTool = AppPaths.ResolveApplicationPath(Path.Combine("tools", name));
            if (File.Exists(localTool)) return localTool;

            string localToolSub = AppPaths.ResolveApplicationPath(Path.Combine("tools", Path.GetFileNameWithoutExtension(name), name));
            if (File.Exists(localToolSub)) return localToolSub;

            string localToolBin = AppPaths.ResolveApplicationPath(Path.Combine("tools", Path.GetFileNameWithoutExtension(name), "bin", name));
            if (File.Exists(localToolBin)) return localToolBin;

            // 2. Búsqueda en sistema
            string autoFound = FindExecutable(name, Path.GetFileNameWithoutExtension(name));
            return !string.IsNullOrWhiteSpace(autoFound) ? autoFound : name;
        });
    }

    private static string FindExecutable(string exeName, string baseToolName)
    {
        // 1. Variable PATH
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            char separator = OperatingSystem.IsWindows() ? ';' : ':';
            foreach (string dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string full = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }
        }

        // 2. Rutas estándar en Linux/macOS
        if (!OperatingSystem.IsWindows())
        {
            string[] unixDirs = ["/usr/bin", "/usr/local/bin", "/opt/homebrew/bin", "/opt/local/bin"];
            foreach (var dir in unixDirs)
            {
                string full = Path.Combine(dir, exeName);
                if (File.Exists(full)) return full;
            }
        }

        // 3. Rutas estándar en Windows (Program Files)
        if (OperatingSystem.IsWindows())
        {
            string[] candidateDirs = [
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            ];

            foreach (string baseDir in candidateDirs)
            {
                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir)) continue;

                try
                {
                    foreach (string subDir in Directory.GetDirectories(baseDir, $"*{baseToolName}*", SearchOption.TopDirectoryOnly))
                    {
                        string directFull = Path.Combine(subDir, exeName);
                        if (File.Exists(directFull)) return directFull;

                        string binSubFull = Path.Combine(subDir, "bin", exeName);
                        if (File.Exists(binSubFull)) return binSubFull;
                    }
                }
                catch { }
            }
        }

        return string.Empty;
    }

    private void LoadConfig()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    var loaded = JsonSerializer.Deserialize<ExternalToolsConfig>(json);
                    if (loaded != null)
                    {
                        _config = loaded;
                    }
                }
            }
            catch { }
        }
    }
}
