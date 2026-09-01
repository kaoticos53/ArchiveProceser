using System.IO;
using System.Text.Json;
using FileFlow.Sdk.Renaming;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio de gestión, almacenamiento y presets de expresiones regulares.
/// </summary>
public sealed class RegexLibraryService
{
    private static readonly Lazy<RegexLibraryService> _instance = new(() => new RegexLibraryService());
    public static RegexLibraryService Instance => _instance.Value;

    private readonly string _storageFilePath;
    private readonly List<RegexPatternItem> _userPatterns = [];
    private readonly System.Threading.Lock _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public RegexLibraryService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string fileFlowDir = Path.Combine(appData, "FileFlow");
        Directory.CreateDirectory(fileFlowDir);
        _storageFilePath = Path.Combine(fileFlowDir, "regex_library.json");

        LoadUserPatterns();
    }

    public RegexLibraryService(string customStorageFilePath)
    {
        _storageFilePath = customStorageFilePath;
        string? dir = Path.GetDirectoryName(customStorageFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        LoadUserPatterns();
    }

    public IReadOnlyList<RegexPatternItem> GetBuiltInPatterns()
    {
        return
        [
            // Series y Episodios
            new RegexPatternItem
            {
                Name = "Temporada y Episodio (NxN)",
                Category = "Series y Vídeo",
                Pattern = @"(\d+)[xX](\d+)",
                Replacement = "${1}x${2}",
                Description = "Detecta formatos como 1x02 o 12x05 separando temporada y capítulo en grupos $1 y $2.",
                SampleInput = "serie_guapa_1x02_hdtv.mov",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Formato Estándar S01E02",
                Category = "Series y Vídeo",
                Pattern = @"(?i)S(\d+)E(\d+)",
                Replacement = "S$1E$2",
                Description = "Detecta marcas de temporada y episodio tipo S01E02 o s1e2.",
                SampleInput = "Breaking.Bad.S01E02.Pilot.mkv",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Capítulo / Episodio / Parte",
                Category = "Series y Vídeo",
                Pattern = @"(?i)\b(?:Cap[íi]tulo|Cap\.?|Episodio|Ep\.?|Parte|Part\.?)\s*(\d+)",
                Replacement = "Cap $1",
                Description = "Detecta 'Capitulo 1', 'Ep. 02', 'Parte 3' y captura el número en $1.",
                SampleInput = "Documental Historia Capitulo 3.mp4",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Etiquetas de Calidad y Códecs",
                Category = "Series y Vídeo",
                Pattern = @"(?i)\b(?:1080p|720p|2160p|4k|x264|x265|hevc|web-dl|bluray|hdrip|dvdrip)\b",
                Replacement = "",
                Description = "Coincide con resoluciones y etiquetas técnicas comunes para facilitar su eliminación o reemplazo.",
                SampleInput = "Pelicula.2023.1080p.BluRay.x264.mkv",
                IsBuiltIn = true
            },

            // Fechas y Horas
            new RegexPatternItem
            {
                Name = "Fecha ISO (YYYY-MM-DD)",
                Category = "Fechas y Tiempos",
                Pattern = @"(\d{4})[-_\.](\d{2})[-_\.](\d{2})",
                Replacement = "$1-$2-$3",
                Description = "Detecta fechas en formato año-mes-día separadas por guión, punto o guión bajo.",
                SampleInput = "informe_2026_09_01_borrador.docx",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Fecha Europea (DD-MM-YYYY)",
                Category = "Fechas y Tiempos",
                Pattern = @"(\d{2})[-_\.](\d{2})[-_\.](\d{4})",
                Replacement = "$3-$2-$1",
                Description = "Detecta formato día-mes-año y permite reordenarlo a formato ISO $3-$2-$1.",
                SampleInput = "factura_01_09_2026.pdf",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Timestamp de Cámara (YYYYMMDD_HHMMSS)",
                Category = "Fechas y Tiempos",
                Pattern = @"(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})",
                Replacement = "$1-$2-$3 $4.$5.$6",
                Description = "Detecta marcas temporales continuas como 20260901_143022.",
                SampleInput = "VID_20260901_143022.mp4",
                IsBuiltIn = true
            },

            // Limpieza y Depuración
            new RegexPatternItem
            {
                Name = "Eliminar Texto entre Paréntesis ()",
                Category = "Limpieza de Nombres",
                Pattern = @"\s*\([^\)]*\)",
                Replacement = "",
                Description = "Elimina cualquier contenido encerrado entre paréntesis como '(2023)' o '(Oficial)'.",
                SampleInput = "Cancion Fabulosa (Official Video) (2023).mp3",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Eliminar Texto entre Corchetes []",
                Category = "Limpieza de Nombres",
                Pattern = @"\s*\[[^\]]*\]",
                Replacement = "",
                Description = "Elimina cualquier contenido entre corchetes como '[1080p]' o '[Torrent]'.",
                SampleInput = "Video_Vacaciones_[FullHD]_[Audio_5.1].mp4",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Colapsar Espacios y Guiones Múltiples",
                Category = "Limpieza de Nombres",
                Pattern = @"[-_\.\s]{2,}",
                Replacement = "-",
                Description = "Reemplaza secuencias repetidas de guiones, espacios o puntos por un único guión.",
                SampleInput = "documento---nuevo___final   v2.pdf",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Eliminar Caracteres No Alfanuméricos",
                Category = "Limpieza de Nombres",
                Pattern = @"[^\w\.\-\s]",
                Replacement = "",
                Description = "Elimina símbolos extraños como #, !, ?, @, $, % preservando letras, números, puntos y guiones.",
                SampleInput = "Factura #123! @Septiembre?.pdf",
                IsBuiltIn = true
            },

            // Estructura y Archivos
            new RegexPatternItem
            {
                Name = "Separar Prefijo por Guión Bajo",
                Category = "Estructura de Archivos",
                Pattern = @"^([^_]+)_(.+)$",
                Replacement = "$1 - $2",
                Description = "Divide el nombre en dos mitades a partir del primer guión bajo.",
                SampleInput = "CLIENTE_Contrato_Firmado.pdf",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Número Secuencial al Inicio",
                Category = "Estructura de Archivos",
                Pattern = @"^(\d+)\s*[-_.]?\s*(.+)$",
                Replacement = "$1 - $2",
                Description = "Captura el número de pista o secuencia al principio del archivo.",
                SampleInput = "05 Track Title.flac",
                IsBuiltIn = true
            }
        ];
    }

    public IReadOnlyList<RegexPatternItem> GetUserPatterns()
    {
        lock (_lock)
        {
            return _userPatterns.Select(p => p.Clone()).ToList();
        }
    }

    public void SaveUserPattern(RegexPatternItem pattern)
    {
        lock (_lock)
        {
            var existingIndex = _userPatterns.FindIndex(p => p.Id.Equals(pattern.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                _userPatterns[existingIndex] = pattern.Clone();
            }
            else
            {
                _userPatterns.Add(pattern.Clone());
            }
            PersistToDisk();
        }
    }

    public bool DeleteUserPattern(string patternId)
    {
        lock (_lock)
        {
            int removed = _userPatterns.RemoveAll(p => p.Id.Equals(patternId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                PersistToDisk();
                return true;
            }
            return false;
        }
    }

    public string ExportToJson()
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(_userPatterns, JsonOptions);
        }
    }

    public int ImportFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;

        try
        {
            var imported = JsonSerializer.Deserialize<List<RegexPatternItem>>(json, JsonOptions);
            if (imported == null || imported.Count == 0) return 0;

            int addedCount = 0;
            lock (_lock)
            {
                foreach (var item in imported)
                {
                    if (string.IsNullOrWhiteSpace(item.Pattern)) continue;
                    item.Id = Guid.NewGuid().ToString("N");
                    item.IsBuiltIn = false;
                    _userPatterns.Add(item);
                    addedCount++;
                }
                PersistToDisk();
            }
            return addedCount;
        }
        catch
        {
            return 0;
        }
    }

    private void LoadUserPatterns()
    {
        lock (_lock)
        {
            _userPatterns.Clear();
            if (File.Exists(_storageFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_storageFilePath);
                    var loaded = JsonSerializer.Deserialize<List<RegexPatternItem>>(json, JsonOptions);
                    if (loaded != null)
                    {
                        _userPatterns.AddRange(loaded);
                    }
                }
                catch
                {
                    // Manejo silencioso ante archivos corruptos
                }
            }
        }
    }

    private void PersistToDisk()
    {
        try
        {
            string json = JsonSerializer.Serialize(_userPatterns, JsonOptions);
            File.WriteAllText(_storageFilePath, json);
        }
        catch
        {
            // Evitar bloqueos de E/S
        }
    }
}
