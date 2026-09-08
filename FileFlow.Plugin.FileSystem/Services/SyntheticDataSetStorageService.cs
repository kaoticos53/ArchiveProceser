using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileFlow.Plugin.FileSystem.UI.Services;
using FileFlow.Sdk.SyntheticData;

namespace FileFlow.Plugin.FileSystem.Services;

/// <summary>
/// Implementación thread-safe del servicio de persistencia y gestión de conjuntos de datos ficticios.
/// Carga datasets incorporados a partir del banco oficial y gestiona los datasets personalizados
/// del usuario en la carpeta %AppData%\FileFlow\SyntheticDataSets\.
/// </summary>
public sealed class SyntheticDataSetStorageService : ISyntheticDataSetStorageService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Lazy<SyntheticDataSetStorageService> _lazyInstance = new(() => new SyntheticDataSetStorageService());
    public static SyntheticDataSetStorageService Instance => _lazyInstance.Value;

    private readonly string _storageDirectory;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, SyntheticDataSet> _dataSets = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public event EventHandler? DataSetsChanged;

    public SyntheticDataSetStorageService(string? customStorageDirectory = null)
    {
        _storageDirectory = customStorageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileFlow",
            "SyntheticDataSets");
    }

    public IReadOnlyList<SyntheticDataSet> GetAllDataSets()
    {
        EnsureInitialized();
        lock (_lock)
        {
            return _dataSets.Values
                .OrderByDescending(d => d.IsBuiltIn)
                .ThenBy(d => d.Category)
                .ThenBy(d => d.Name)
                .Select(d => d.Clone(d.Name))
                .ToList();
        }
    }

    public SyntheticDataSet? GetDataSetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        EnsureInitialized();
        lock (_lock)
        {
            if (_dataSets.TryGetValue(id, out var ds))
            {
                return ds.Clone(ds.Name);
            }
            return null;
        }
    }

    public SyntheticDataSet? GetDataSetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        EnsureInitialized();
        lock (_lock)
        {
            var match = _dataSets.Values.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
            return match?.Clone(match.Name);
        }
    }

    public void SaveDataSet(SyntheticDataSet dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        EnsureInitialized();

        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(dataSet.Id))
            {
                dataSet.Id = Guid.NewGuid().ToString("N");
            }

            dataSet.IsBuiltIn = false;
            dataSet.LastModifiedAt = DateTime.UtcNow;

            Directory.CreateDirectory(_storageDirectory);
            string filePath = Path.Combine(_storageDirectory, $"{dataSet.Id}.json");
            string json = JsonSerializer.Serialize(dataSet, JsonOpts);
            File.WriteAllText(filePath, json);

            _dataSets[dataSet.Id] = dataSet.Clone(dataSet.Name);
        }

        DataSetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool DeleteDataSet(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        EnsureInitialized();

        lock (_lock)
        {
            if (_dataSets.TryGetValue(id, out var ds))
            {
                if (ds.IsBuiltIn)
                {
                    // No se permite borrar datasets incorporados
                    return false;
                }

                _dataSets.Remove(id);
                string filePath = Path.Combine(_storageDirectory, $"{id}.json");
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { /* Ignore */ }
                }

                DataSetsChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return false;
        }
    }

    public SyntheticDataSet CloneDataSet(string sourceId, string newName)
    {
        EnsureInitialized();
        SyntheticDataSet? source;
        lock (_lock)
        {
            _dataSets.TryGetValue(sourceId, out source);
        }

        if (source == null)
        {
            throw new KeyNotFoundException($"Dataset con ID '{sourceId}' no encontrado.");
        }

        var clone = source.Clone(newName, generateNewId: true);
        SaveDataSet(clone);
        return clone;
    }

    public string ExportDataSetToJson(SyntheticDataSet dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        return JsonSerializer.Serialize(dataSet, JsonOpts);
    }

    public SyntheticDataSet ImportDataSetFromJson(string jsonContent, bool autoSave = true)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            throw new ArgumentException("El contenido JSON no puede estar vacío.", nameof(jsonContent));
        }

        var imported = JsonSerializer.Deserialize<SyntheticDataSet>(jsonContent, JsonOpts)
            ?? throw new InvalidOperationException("No se pudo deserializar el dataset sintético.");

        imported.Id = Guid.NewGuid().ToString("N");
        imported.IsBuiltIn = false;
        imported.CreatedAt = DateTime.UtcNow;
        imported.LastModifiedAt = DateTime.UtcNow;

        if (autoSave)
        {
            SaveDataSet(imported);
        }

        return imported;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            // 1. Cargar datasets incorporados desde el catálogo oficial
            LoadBuiltInDataSets();

            // 2. Cargar datasets personalizados del usuario desde %AppData%
            LoadUserDataSets();

            _initialized = true;
        }
    }

    private void LoadBuiltInDataSets()
    {
        try
        {
            var sampleItems = RenamerSampleDataProvider.GetSampleItems(out _);
            if (sampleItems.Count == 0) return;

            // Agrupar por categoría
            var grouped = sampleItems
                .GroupBy(i =>
                {
                    if (i.Metadata.TryGetValue("Category", out var cat) && cat != null)
                    {
                        return cat.ToString()!;
                    }
                    return "General";
                })
                .ToList();

            foreach (var group in grouped)
            {
                string categoryName = group.Key;
                string dsId = $"builtin_{categoryName.ToLowerInvariant().Replace(' ', '_')}";

                var dataSet = new SyntheticDataSet
                {
                    Id = dsId,
                    Name = $"{categoryName} (Oficial)",
                    Category = categoryName,
                    Description = $"Conjunto de pruebas oficial para {categoryName}.",
                    IsBuiltIn = true,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    LastModifiedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                };

                foreach (var item in group)
                {
                    string fileName = Path.GetFileName(item.CurrentPath);
                    string relPath = $"{categoryName}/{fileName}";

                    var def = new SyntheticFileDefinition
                    {
                        RelativePath = relPath,
                        FileSizeBytes = item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024 * 1024,
                        IsDirectory = item.IsDirectory,
                        Metadata = new Dictionary<string, object?>(item.Metadata, StringComparer.OrdinalIgnoreCase)
                    };

                    dataSet.Items.Add(def);
                }

                _dataSets[dataSet.Id] = dataSet;
            }
        }
        catch
        {
            // Fallback silencioso si renamer_samples no estuviera disponible
        }
    }

    private void LoadUserDataSets()
    {
        try
        {
            if (!Directory.Exists(_storageDirectory)) return;

            var files = Directory.GetFiles(_storageDirectory, "*.json");
            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var ds = JsonSerializer.Deserialize<SyntheticDataSet>(json, JsonOpts);
                    if (ds != null && !string.IsNullOrWhiteSpace(ds.Id))
                    {
                        ds.IsBuiltIn = false;
                        _dataSets[ds.Id] = ds;
                    }
                }
                catch
                {
                    // Ignorar archivos corruptos individuales
                }
            }
        }
        catch
        {
            // Ignore error loading directory
        }
    }
}
