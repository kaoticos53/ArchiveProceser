using System.IO;
using System.Text.Json;
using FileFlow.Sdk.Storage;
using Microsoft.Win32;

namespace FileFlow.App.Services;

public class ExternalToolsConfig
{
    public string FfmpegPath { get; set; } = string.Empty;
    public string FfprobePath { get; set; } = string.Empty;
    public string SevenZipPath { get; set; } = string.Empty;
    public string PythonPath { get; set; } = string.Empty;
}

public class ExternalToolsService
{
    private static readonly Lazy<ExternalToolsService> _instance = new(() => new ExternalToolsService());
    public static ExternalToolsService Instance => _instance.Value;

    private readonly string _configFilePath;
    private ExternalToolsConfig _config = new();
    private readonly Lock _lock = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _resolvedToolCache = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? ToolsConfigChanged;

    private ExternalToolsService()
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

    public string FfmpegExecutable => ResolveToolPath("ffmpeg.exe", _config.FfmpegPath);
    public string FfprobeExecutable => ResolveToolPath("ffprobe.exe", _config.FfprobePath);
    public string SevenZipExecutable => ResolveToolPath("7z.exe", _config.SevenZipPath);
    public string PythonExecutable => ResolveToolPath("python.exe", _config.PythonPath);

    public void SaveConfig(ExternalToolsConfig newConfig)
    {
        lock (_lock)
        {
            _config = newConfig;
            _resolvedToolCache.Clear();
            PersistToDisk();
        }
        ToolsConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<ExternalToolsConfig> AutoDetectToolsAsync()
    {
        return Task.Run(() =>
        {
            var detected = new ExternalToolsConfig
            {
                FfmpegPath = FindExecutable("ffmpeg.exe", "ffmpeg"),
                FfprobePath = FindExecutable("ffprobe.exe", "ffprobe"),
                SevenZipPath = FindExecutable("7z.exe", "7-Zip"),
                PythonPath = FindExecutable("python.exe", "Python")
            };

            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(detected.FfmpegPath)) _config.FfmpegPath = detected.FfmpegPath;
                if (!string.IsNullOrWhiteSpace(detected.FfprobePath)) _config.FfprobePath = detected.FfprobePath;
                if (!string.IsNullOrWhiteSpace(detected.SevenZipPath)) _config.SevenZipPath = detected.SevenZipPath;
                if (!string.IsNullOrWhiteSpace(detected.PythonPath)) _config.PythonPath = detected.PythonPath;

                _resolvedToolCache.Clear();
                PersistToDisk();
            }

            ToolsConfigChanged?.Invoke(this, EventArgs.Empty);
            return Config;
        });
    }

    private string FindExecutable(string exeName, string hintFolder)
    {
        // 1. Check System PATH environment variable
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            foreach (string dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string full = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }
        }

        // 2. Check common installation directories
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidateDirs =
        [
            @"C:\ffmpeg\bin",
            @"C:\ffmpeg",
            @"C:\tools",
            @"C:\ProgramData\chocolatey\bin",
            Path.Combine(pf, hintFolder),
            Path.Combine(pf, hintFolder, "bin"),
            Path.Combine(pf, "FFmpeg", "bin"),
            Path.Combine(pf, "7-Zip"),
            Path.Combine(pfx86, "7-Zip"),
            Path.Combine(localAppData, "Programs", hintFolder),
            Path.Combine(localAppData, "Microsoft", "WinGet", "Packages"),
            Path.Combine(appData, "Scoop", "apps", hintFolder, "current")
        ];

        foreach (string dir in candidateDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    string full = Path.Combine(dir, exeName);
                    if (File.Exists(full)) return full;

                    // Recursive search 1 level deep for packages like WinGet / Scoop
                    foreach (string subDir in Directory.GetDirectories(dir))
                    {
                        string subFull = Path.Combine(subDir, exeName);
                        if (File.Exists(subFull)) return subFull;
                        string binSubFull = Path.Combine(subDir, "bin", exeName);
                        if (File.Exists(binSubFull)) return binSubFull;
                    }
                }
            }
            catch { }
        }

        // 3. Check Windows Registry App Paths
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}");
            if (key != null)
            {
                string? val = key.GetValue(null)?.ToString();
                if (!string.IsNullOrWhiteSpace(val) && File.Exists(val))
                {
                    return val;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    private string ResolveToolPath(string exeName, string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string resolved = AppPaths.ResolveApplicationPath(configuredPath);
            if (File.Exists(resolved))
            {
                return resolved;
            }
        }

        return _resolvedToolCache.GetOrAdd(exeName, name =>
        {
            // Búsqueda en carpeta local portable 'tools/'
            string localTool = AppPaths.ResolveApplicationPath(Path.Combine("tools", name));
            if (File.Exists(localTool)) return localTool;

            string localToolSub = AppPaths.ResolveApplicationPath(Path.Combine("tools", Path.GetFileNameWithoutExtension(name), name));
            if (File.Exists(localToolSub)) return localToolSub;

            string localToolBin = AppPaths.ResolveApplicationPath(Path.Combine("tools", Path.GetFileNameWithoutExtension(name), "bin", name));
            if (File.Exists(localToolBin)) return localToolBin;

            string autoFound = FindExecutable(name, Path.GetFileNameWithoutExtension(name));
            return !string.IsNullOrWhiteSpace(autoFound) ? autoFound : name;
        });
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
                        return;
                    }
                }
            }
            catch { }

            _config = new ExternalToolsConfig();
        }
    }

    private void PersistToDisk()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(_configFilePath, json);
        }
        catch { }
    }
}
