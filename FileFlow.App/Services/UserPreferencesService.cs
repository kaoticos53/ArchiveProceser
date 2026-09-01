using System.IO;
using System.Text.Json;
using FileFlow.Sdk.Storage;

namespace FileFlow.App.Services;

public class UserPreferencesData
{
    public HashSet<string> FavoriteNodeTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> NodeUsageCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Persistent Application Settings
    public string DefaultGlobalOutputDir { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FileFlowStudio", "Output");
    public string ActiveTheme { get; set; } = "Dark";
    public bool IsCompactToolbox { get; set; } = false;
    public int MaxParallelThreads { get; set; } = Environment.ProcessorCount;
    public bool DefaultDryRunState { get; set; } = true;
    public string DefaultConflictStrategy { get; set; } = "RenameIncremental";
    public string DefaultLogLevel { get; set; } = "Information";
    public bool AutoScrollConsole { get; set; } = true;
    public int MaxLogEntries { get; set; } = 50000;
    public bool EnableAutoSave { get; set; } = true;
    public int AutoSaveIntervalMinutes { get; set; } = 5;
}

public class UserPreferencesService
{
    private static readonly Lazy<UserPreferencesService> _instance = new(() => new UserPreferencesService());
    public static UserPreferencesService Instance => _instance.Value;

    private readonly string _filePath;
    private readonly Lock _lock = new();
    private UserPreferencesData _data = new();

    public event Action? PreferencesChanged;

    private UserPreferencesService()
    {
        AppPaths.EnsureDirectories();
        _filePath = AppPaths.UserPreferencesFile;
        Load();
    }

    public UserPreferencesData Preferences
    {
        get
        {
            lock (_lock)
            {
                return _data;
            }
        }
    }

    public void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var loaded = JsonSerializer.Deserialize<UserPreferencesData>(json);
                    if (loaded != null)
                    {
                        _data = loaded;
                        _data.FavoriteNodeTypes ??= new(StringComparer.OrdinalIgnoreCase);
                        _data.NodeUsageCounts ??= new(StringComparer.OrdinalIgnoreCase);
                        if (string.IsNullOrWhiteSpace(_data.DefaultGlobalOutputDir))
                        {
                            _data.DefaultGlobalOutputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FileFlowStudio", "Output");
                        }
                    }
                }
            }
            catch
            {
                _data = new UserPreferencesData();
            }
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }
        PreferencesChanged?.Invoke();
    }

    public void UpdatePreferences(Action<UserPreferencesData> updateAction)
    {
        lock (_lock)
        {
            updateAction(_data);
        }
        Save();
    }

    public bool IsFavorite(string typeName)
    {
        lock (_lock)
        {
            return _data.FavoriteNodeTypes.Contains(typeName);
        }
    }

    public bool ToggleFavorite(string typeName)
    {
        bool newState;
        lock (_lock)
        {
            if (_data.FavoriteNodeTypes.Contains(typeName))
            {
                _data.FavoriteNodeTypes.Remove(typeName);
                newState = false;
            }
            else
            {
                _data.FavoriteNodeTypes.Add(typeName);
                newState = true;
            }
        }
        Save();
        return newState;
    }

    public void IncrementNodeUsage(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return;

        lock (_lock)
        {
            if (_data.NodeUsageCounts.TryGetValue(typeName, out int current))
            {
                _data.NodeUsageCounts[typeName] = current + 1;
            }
            else
            {
                _data.NodeUsageCounts[typeName] = 1;
            }
        }
        Save();
    }

    public int GetUsageCount(string typeName)
    {
        lock (_lock)
        {
            return _data.NodeUsageCounts.TryGetValue(typeName, out int count) ? count : 0;
        }
    }

    public List<string> GetFavoriteNodeTypes()
    {
        lock (_lock)
        {
            return _data.FavoriteNodeTypes.ToList();
        }
    }

    public List<(string TypeName, int Count)> GetTopUsedNodeTypes(int limit = 5)
    {
        lock (_lock)
        {
            return _data.NodeUsageCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(limit)
                .Select(kvp => (kvp.Key, kvp.Value))
                .ToList();
        }
    }
}
