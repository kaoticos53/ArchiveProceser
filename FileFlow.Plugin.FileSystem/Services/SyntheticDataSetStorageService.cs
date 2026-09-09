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

                    if (def.IsArchive)
                    {
                        PopulateRealisticArchiveEntries(def, fileName, categoryName);
                    }

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

    private static void PopulateRealisticArchiveEntries(SyntheticFileDefinition def, string fileName, string categoryName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        string mediaType = def.Metadata.TryGetValue("MediaType", out var mVal) && mVal != null
            ? mVal.ToString()!
            : string.Empty;

        // 1. Cómics y Manga (.cbr = RAR, .cbz = ZIP con imágenes de páginas)
        if (ext is ".cbr" or ".cbz" || string.Equals(mediaType, "Comic", StringComparison.OrdinalIgnoreCase))
        {
            PopulateComicArchiveEntries(def);
            return;
        }

        // 2. Álbumes y colecciones de Música (.zip, .rar, .7z)
        bool isMusic = string.Equals(mediaType, "Music", StringComparison.OrdinalIgnoreCase)
            || string.Equals(categoryName, "Música", StringComparison.OrdinalIgnoreCase)
            || def.Metadata.ContainsKey("Audio:Artist")
            || fileName.Contains("[FLAC]", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("320kbps", StringComparison.OrdinalIgnoreCase);

        if (isMusic)
        {
            PopulateMusicArchiveEntries(def);
            return;
        }

        // 3. Películas o Series comprimidas / Release packs
        bool isVideo = string.Equals(mediaType, "Movie", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, "Series", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, "Video", StringComparison.OrdinalIgnoreCase)
            || string.Equals(categoryName, "Películas", StringComparison.OrdinalIgnoreCase)
            || string.Equals(categoryName, "Series", StringComparison.OrdinalIgnoreCase);

        if (isVideo)
        {
            PopulateVideoArchiveEntries(def, fileName);
            return;
        }

        // 4. Software e instaladores comprimidos
        bool isSoftware = string.Equals(mediaType, "Installer", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("setup", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("Portable", StringComparison.OrdinalIgnoreCase);

        if (isSoftware)
        {
            PopulateSoftwareArchiveEntries(def);
            return;
        }

        // 5. Fallback genérico para documentos o paquetes misceláneos
        PopulateGenericArchiveEntries(def);
    }

    private static void PopulateComicArchiveEntries(SyntheticFileDefinition def)
    {
        int pageCount = 24;
        if (def.Metadata.TryGetValue("Doc:PageCount", out var pcObj) && pcObj != null && int.TryParse(pcObj.ToString(), out int parsedPc) && parsedPc > 0)
        {
            pageCount = Math.Min(parsedPc, 36);
        }

        long pageAvgBytes = Math.Max(70000, def.FileSizeBytes / Math.Max(1, pageCount));

        // Portada
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("000_cover.jpg", (long)(pageAvgBytes * 1.2)));

        // Páginas correlativas del cómic
        for (int p = 1; p <= pageCount; p++)
        {
            def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition($"page_{p:D3}.jpg", pageAvgBytes));
        }

        // Metadatos estándares de ComicRack y créditos de digitalización
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("ComicInfo.xml", 2560));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("scangroup_info.txt", 512));
    }

    private static void PopulateMusicArchiveEntries(SyntheticFileDefinition def)
    {
        string artist = def.Metadata.TryGetValue("Audio:Artist", out var aVal) && aVal != null
            ? aVal.ToString()!
            : "Artista";
        string album = def.Metadata.TryGetValue("Audio:Album", out var albVal) && albVal != null
            ? albVal.ToString()!
            : "Album";

        bool isFlac = def.FileName.Contains("FLAC", StringComparison.OrdinalIgnoreCase);
        string audioExt = isFlac ? ".flac" : ".mp3";
        int trackCount = 10;
        long trackAvgBytes = Math.Max(2_500_000, def.FileSizeBytes / Math.Max(1, trackCount));

        string[] sampleTitles =
        [
            "Intro", "Neon Lights", "Midnight Drive", "Lost Echoes", "Horizon",
            "Solar Flare", "Shadows", "Reflections", "Afterglow", "Outro Finale"
        ];

        // Lista de canciones individuales del álbum
        for (int i = 0; i < trackCount; i++)
        {
            string trackNum = $"{i + 1:D2}";
            string title = i < sampleTitles.Length ? sampleTitles[i] : $"Pista {i + 1}";
            string trackFileName = $"{trackNum} - {artist} - {title}{audioExt}";
            def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition(trackFileName, trackAvgBytes));
        }

        // Arte de tapa y hojas de corte (CUE/M3U/NFO) típicas de lanzamientos de audio
        long coverBytes = isFlac ? 2_000_000 : 450_000;
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("cover.jpg", coverBytes));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("folder.jpg", coverBytes));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition($"{artist} - {album}.cue", 3200));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("playlist.m3u", 1024));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("release.nfo", 2048));
    }

    private static void PopulateVideoArchiveEntries(SyntheticFileDefinition def, string fileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        long mainVideoSize = (long)(def.FileSizeBytes * 0.92);

        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition($"{baseName}.mkv", mainVideoSize));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("Subs/es_Castellano.srt", 85_000));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("Subs/es_Latino.srt", 82_000));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("Subs/en_English.srt", 78_000));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("Sample/sample.mkv", Math.Max(15_000_000, (long)(def.FileSizeBytes * 0.04))));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition($"{baseName}.nfo", 3072));
    }

    private static void PopulateSoftwareArchiveEntries(SyntheticFileDefinition def)
    {
        long exeSize = (long)(def.FileSizeBytes * 0.75);
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("setup.exe", exeSize));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("data1.cab", Math.Max(100_000, (long)(def.FileSizeBytes * 0.20))));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("Crack/instructions.txt", 1500));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("Crack/keygen.exe", 250_000));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("Leeme_Instalacion.txt", 2048));
    }

    private static void PopulateGenericArchiveEntries(SyntheticFileDefinition def)
    {
        long partSize = Math.Max(1024, def.FileSizeBytes / 3);
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("documento_interno.pdf", partSize));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("datos_extra.csv", Math.Max(512, partSize / 4)));
        def.SimulatedArchiveEntries.Add(new SyntheticArchiveEntryDefinition("leeme.txt", 1024));
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
