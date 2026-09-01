using System.IO;
using System.Text.Json;
using FileFlow.Sdk.Renaming;

namespace FileFlow.Plugin.FileSystem.UI.Services;

/// <summary>
/// Servicio de gestión, almacenamiento y presets de expresiones regulares dentro del plugin FileSystem.
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
                Name = "Temporada y Episodio (SnnEnn)",
                Category = "Series y Vídeo",
                Pattern = @"[sS](\d+)[eE](\d+)",
                Replacement = "S${1}E${2}",
                Description = "Normaliza patrones como s01e05 o S1E2 a estándar S01E05.",
                SampleInput = "game_of_thrones.s08e06.720p.mkv",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Resolución y Calidad (1080p, 4K, etc.)",
                Category = "Series y Vídeo",
                Pattern = @"(1080p|720p|4k|2160p|480p|web-dl|bluray|hdtv)",
                Replacement = "",
                Description = "Identifica y permite eliminar etiquetas comunes de calidad multimedia.",
                SampleInput = "Pelicula.2024.1080p.BluRay.x264.mp4",
                IsBuiltIn = true
            },

            // Fechas y Años
            new RegexPatternItem
            {
                Name = "Año en 4 dígitos (19xx o 20xx)",
                Category = "Fechas",
                Pattern = @"\b(19\d\d|20\d\d)\b",
                Replacement = "${1}",
                Description = "Extrae el año en cuatro dígitos entre 1900 y 2099.",
                SampleInput = "Informe_Financiero_2024_Final.pdf",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Fecha YYYY-MM-DD",
                Category = "Fechas",
                Pattern = @"(\d{4})[-_](\d{2})[-_](\d{2})",
                Replacement = "$1-$2-$3",
                Description = "Reconoce y normaliza fechas en formato ISO estándar YYYY-MM-DD.",
                SampleInput = "backup_2023_11_25_full.tar.gz",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Fecha DD-MM-YYYY a ISO",
                Category = "Fechas",
                Pattern = @"(\d{2})[-_](\d{2})[-_](\d{4})",
                Replacement = "$3-$2-$1",
                Description = "Invierte fechas europeas (DD-MM-YYYY) a formato cronológico (YYYY-MM-DD).",
                SampleInput = "factura_31_12_2024.pdf",
                IsBuiltIn = true
            },

            // Limpieza y Sanitización
            new RegexPatternItem
            {
                Name = "Eliminar Corchetes y su contenido",
                Category = "Limpieza",
                Pattern = @"\[.*?\]",
                Replacement = "",
                Description = "Elimina cualquier bloque encerrado entre corchetes como [YTS], [1080p] o [Grupo].",
                SampleInput = "[Fansub] Anime_Episodio_01 [1080p].mkv",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Eliminar Paréntesis y su contenido",
                Category = "Limpieza",
                Pattern = @"\(.*?\)",
                Replacement = "",
                Description = "Elimina cualquier bloque encerrado entre paréntesis como (Director Cut).",
                SampleInput = "Track 01 (Remastered 2024).flac",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Colapsar Espacios / Guiones Múltiples",
                Category = "Limpieza",
                Pattern = @"[\s_\-]+",
                Replacement = "_",
                Description = "Reemplaza cualquier secuencia de espacios, guiones o barras bajas por un único guion bajo.",
                SampleInput = "mi   archivo---con___espacios.txt",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Eliminar Caracteres Especiales",
                Category = "Limpieza",
                Pattern = @"[^\w\s\.\-]",
                Replacement = "",
                Description = "Elimina símbolos no alfanuméricos como ¡!¿?#$%&/()=",
                SampleInput = "¿Quién_es_el_número_#1?.docx",
                IsBuiltIn = true
            },

            // Numeración y Prefijos
            new RegexPatternItem
            {
                Name = "Extraer Primer Número",
                Category = "Numeración",
                Pattern = @"\b(\d+)\b",
                Replacement = "$1",
                Description = "Captura el primer número entero independiente encontrado en el texto.",
                SampleInput = "Capítulo 4 - El Resurgimiento.mp4",
                IsBuiltIn = true
            },
            new RegexPatternItem
            {
                Name = "Eliminar Números al Inicio",
                Category = "Numeración",
                Pattern = @"^\d+[\s\.\-_]*",
                Replacement = "",
                Description = "Limpia prefijos de track o numeración al principio como '01 - ', '002.', '12_'",
                SampleInput = "01 - Bohemian Rhapsody.mp3",
                IsBuiltIn = true
            }
        ];
    }

    public IReadOnlyList<RegexPatternItem> GetUserPatterns()
    {
        lock (_lock)
        {
            return _userPatterns.ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<RegexPatternItem> GetAllPatterns()
    {
        var list = new List<RegexPatternItem>(GetBuiltInPatterns());
        list.AddRange(GetUserPatterns());
        return list.AsReadOnly();
    }

    public void AddUserPattern(RegexPatternItem item)
    {
        if (item == null) return;
        lock (_lock)
        {
            item.IsBuiltIn = false;
            _userPatterns.RemoveAll(p => p.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            _userPatterns.Add(item);
            SaveUserPatterns();
        }
    }

    public bool DeleteUserPattern(string patternName)
    {
        lock (_lock)
        {
            int removed = _userPatterns.RemoveAll(p => p.Name.Equals(patternName, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                SaveUserPatterns();
                return true;
            }
            return false;
        }
    }

    private void LoadUserPatterns()
    {
        lock (_lock)
        {
            _userPatterns.Clear();
            if (!File.Exists(_storageFilePath)) return;

            try
            {
                string json = File.ReadAllText(_storageFilePath);
                var items = JsonSerializer.Deserialize<List<RegexPatternItem>>(json, JsonOptions);
                if (items != null)
                {
                    _userPatterns.AddRange(items);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading user regex library: {ex.Message}");
            }
        }
    }

    private void SaveUserPatterns()
    {
        try
        {
            string json = JsonSerializer.Serialize(_userPatterns, JsonOptions);
            File.WriteAllText(_storageFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving user regex library: {ex.Message}");
        }
    }
}
