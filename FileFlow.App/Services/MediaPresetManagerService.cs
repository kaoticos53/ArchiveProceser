using System.IO;
using System.Text.Json;

namespace FileFlow.App.Services;

public class MediaPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OutputExtension { get; set; } = ".mp4";
    public string FfmpegArguments { get; set; } = "-c:v libx264 -crf 22 -c:a aac -b:a 192k";
    public string Category { get; set; } = "Video"; // Audio, Video, Animation, Custom
    public bool IsSystemDefault { get; set; } = false;
}

public class MediaPresetManagerService
{
    private static readonly Lazy<MediaPresetManagerService> _instance = new(() => new MediaPresetManagerService());
    public static MediaPresetManagerService Instance => _instance.Value;

    private readonly string _presetsFilePath;
    private readonly List<MediaPreset> _presets = [];
    private readonly object _lock = new();

    public event EventHandler? PresetsChanged;

    private MediaPresetManagerService()
    {
        string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileFlowStudio");
        Directory.CreateDirectory(appDataDir);
        _presetsFilePath = Path.Combine(appDataDir, "media_presets.json");

        LoadPresets();
    }

    public IReadOnlyList<MediaPreset> GetPresets()
    {
        lock (_lock)
        {
            return _presets.ToList().AsReadOnly();
        }
    }

    public List<string> GetPresetNames()
    {
        lock (_lock)
        {
            return _presets.Select(p => p.Name).ToList();
        }
    }

    public MediaPreset? GetPresetByName(string name)
    {
        lock (_lock)
        {
            return _presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void SavePreset(MediaPreset preset)
    {
        lock (_lock)
        {
            int idx = _presets.FindIndex(p => p.Id == preset.Id || p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _presets[idx] = preset;
            }
            else
            {
                _presets.Add(preset);
            }
            PersistToDisk();
        }
        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool DeletePreset(string presetId)
    {
        lock (_lock)
        {
            var target = _presets.FirstOrDefault(p => p.Id == presetId);
            if (target != null && !target.IsSystemDefault)
            {
                _presets.Remove(target);
                PersistToDisk();
                PresetsChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }
        return false;
    }

    public void ResetToDefaults()
    {
        lock (_lock)
        {
            _presets.Clear();
            _presets.AddRange(GetDefaultPresets());
            PersistToDisk();
        }
        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LoadPresets()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_presetsFilePath))
                {
                    string json = File.ReadAllText(_presetsFilePath);
                    var loaded = JsonSerializer.Deserialize<List<MediaPreset>>(json);
                    if (loaded != null && loaded.Count > 0)
                    {
                        _presets.Clear();
                        _presets.AddRange(loaded);
                        return;
                    }
                }
            }
            catch
            {
                // Fallback to default presets on read error
            }

            _presets.Clear();
            _presets.AddRange(GetDefaultPresets());
            PersistToDisk();
        }
    }

    private void PersistToDisk()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_presets, options);
            File.ReadAllText(_presetsFilePath); // Test read permission
            File.WriteAllText(_presetsFilePath, json);
        }
        catch
        {
            // Ignore write errors to prevent app crashes
        }
    }

    public static List<MediaPreset> GetDefaultPresets()
    {
        return new List<MediaPreset>
        {
            new MediaPreset
            {
                Id = "preset-mp3",
                Name = "Extraer Audio MP3",
                Description = "Extrae la pista de audio principal y la convierte a MP3 a 192 kbps.",
                OutputExtension = ".mp3",
                FfmpegArguments = "-vn -c:a libmp3lame -b:a 192k",
                Category = "Audio",
                IsSystemDefault = true
            },
            new MediaPreset
            {
                Id = "preset-m4a",
                Name = "Extraer Audio AAC (M4A)",
                Description = "Extrae audio de alta fidelidad en formato AAC (M4A) a 256 kbps.",
                OutputExtension = ".m4a",
                FfmpegArguments = "-vn -c:a aac -b:a 256k",
                Category = "Audio",
                IsSystemDefault = true
            },
            new MediaPreset
            {
                Id = "preset-flac",
                Name = "Extraer Audio FLAC Lossless",
                Description = "Extrae el audio sin pérdida de calidad en formato FLAC.",
                OutputExtension = ".flac",
                FfmpegArguments = "-vn -c:a flac",
                Category = "Audio",
                IsSystemDefault = true
            },
            new MediaPreset
            {
                Id = "preset-1080p",
                Name = "Convertir 1080p H.264 (Universal MP4)",
                Description = "Reescala o convierte el vídeo a 1080p Full HD con códec H.264 y audio AAC.",
                OutputExtension = ".mp4",
                FfmpegArguments = "-vf \"scale=iw*min(1920/iw\\,1080/ih):ih*min(1920/iw\\,1080/ih)\" -c:v libx264 -crf 22 -preset medium -c:a aac -b:a 192k",
                Category = "Video",
                IsSystemDefault = true
            },
            new MediaPreset
            {
                Id = "preset-720p",
                Name = "Convertir 720p H.264 (MP4 Rápido)",
                Description = "Conversión rápida a 720p HD optimizada para streaming y compartición ligera.",
                OutputExtension = ".mp4",
                FfmpegArguments = "-vf \"scale=iw*min(1280/iw\\,720/ih):ih*min(1280/iw\\,720/ih)\" -c:v libx264 -crf 24 -preset fast -c:a aac -b:a 128k",
                Category = "Video",
                IsSystemDefault = true
            },
            new MediaPreset
            {
                Id = "preset-4k-hevc",
                Name = "Convertir 4K H.265 / HEVC",
                Description = "Compresión de alta eficiencia H.265/HEVC ideal para resoluciones 4K.",
                OutputExtension = ".mp4",
                FfmpegArguments = "-c:v libx265 -crf 24 -c:a aac -b:a 192k",
                Category = "Video",
                IsSystemDefault = true
            },
            new MediaPreset
            {
                Id = "preset-webm",
                Name = "WebM VP9 Open Video",
                Description = "Formato web abierto WebM con códec VP9 y audio Opus.",
                OutputExtension = ".webm",
                FfmpegArguments = "-c:v libvpx-vp9 -b:v 2M -c:a libopus -b:a 128k",
                Category = "Video",
                IsSystemDefault = true
            },
            new MediaPreset
            {
                Id = "preset-gif",
                Name = "Convertir a GIF Animado",
                Description = "Genera una animación GIF optimizada a 15 fps y ancho de 480px.",
                OutputExtension = ".gif",
                FfmpegArguments = "-vf \"fps=15,scale=480:-1:flags=lanczos\"",
                Category = "Animation",
                IsSystemDefault = true
            },
            new MediaPreset
            {
                Id = "preset-mobile",
                Name = "Móvil Ultra-Comprimido H.264",
                Description = "Máxima reducción de tamaño para envío por correo o mensajería.",
                OutputExtension = ".mp4",
                FfmpegArguments = "-vf \"scale=480:-1\" -c:v libx264 -crf 28 -preset ultrafast -c:a aac -b:a 96k",
                Category = "Video",
                IsSystemDefault = true
            },
            new MediaPreset
            {
                Id = "preset-custom",
                Name = "Personalizado / Argumentos Libres",
                Description = "Usa argumentos CLI arbitrarios pasados en los parámetros del nodo.",
                OutputExtension = ".mp4",
                FfmpegArguments = "-c:v libx264 -crf 23 -c:a aac",
                Category = "Custom",
                IsSystemDefault = true
            }
        };
    }
}
