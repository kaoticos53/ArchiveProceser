using System.IO;
using System.Text.Json;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.Scripting.Services;

/// <summary>
/// Modelo de datos para la definición de un script en la biblioteca.
/// </summary>
public sealed class ScriptDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public string Language { get; set; } = "CSharp"; // CSharp | JavaScript
    public List<string> InputPorts { get; set; } = ["In"];
    public List<string> OutputPorts { get; set; } = ["Out"];
    public string ScriptCode { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; } = false;
}

/// <summary>
/// Servicio de almacenamiento, serialización y gestión de la biblioteca de scripts de usuario y presets.
/// </summary>
public sealed class ScriptLibraryService
{
    private static readonly Lazy<ScriptLibraryService> _instance = new(() => new ScriptLibraryService());
    public static ScriptLibraryService Instance => _instance.Value;

    private readonly string _storageDirectory;
    private readonly List<ScriptDefinition> _userScripts = [];
    private readonly Lock _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ScriptLibraryService()
    {
        AppPaths.EnsureDirectories();
        _storageDirectory = AppPaths.ScriptsDirectory;

        LoadUserScripts();
    }

    public ScriptLibraryService(string customStorageDirectory)
    {
        _storageDirectory = customStorageDirectory;
        Directory.CreateDirectory(_storageDirectory);
        LoadUserScripts();
    }

    public IReadOnlyList<ScriptDefinition> GetBuiltInScripts()
    {
        string[] candidatePaths =
        [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "script_presets.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Config", "script_presets.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Config", "script_presets.json"),
            Path.Combine(AppContext.BaseDirectory, "Config", "script_presets.json")
        ];

        foreach (var path in candidatePaths.Distinct())
        {
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var items = JsonSerializer.Deserialize<List<ScriptDefinition>>(json, JsonOptions);
                    if (items != null && items.Count > 0)
                    {
                        return items;
                    }
                }
                catch
                {
                    // Fallback to in-memory definitions
                }
            }
        }

        return GetFallbackBuiltInScripts();
    }

    private static List<ScriptDefinition> GetFallbackBuiltInScripts()
    {
        return
        [
            // 1. C# - Enrutador Multicanal por Tipo de Archivo
            new ScriptDefinition
            {
                Name = "Enrutador Multicamino por Categoría (C#)",
                Category = "Enrutamiento",
                Description = "Clasifica y bifurca el archivo hacia salidas independientes (Imagenes, Videos, Documentos u Otros) evaluando su extensión.",
                Language = "CSharp",
                InputPorts = ["In"],
                OutputPorts = ["Imagenes", "Videos", "Documentos", "Otros"],
                IsBuiltIn = true,
                ScriptCode = @"// Enrutador multicamino por extensión
var ext = Path.GetExtension(Item.FileName).ToLowerInvariant();

if (new[] { "".jpg"", "".jpeg"", "".png"", "".webp"", "".bmp"", "".gif"" }.Contains(ext))
{
    Item.Metadata[""CategoriaMedia""] = ""Imagen"";
    await EmitAsync(""Imagenes"");
}
else if (new[] { "".mp4"", "".mkv"", "".avi"", "".mov"", "".wmv"", "".webm"" }.Contains(ext))
{
    Item.Metadata[""CategoriaMedia""] = ""Video"";
    await EmitAsync(""Videos"");
}
else if (new[] { "".pdf"", "".docx"", "".xlsx"", "".txt"", "".md"", "".epub"" }.Contains(ext))
{
    Item.Metadata[""CategoriaMedia""] = ""Documento"";
    await EmitAsync(""Documentos"");
}
else
{
    Item.Metadata[""CategoriaMedia""] = ""Desconocido"";
    await EmitAsync(""Otros"");
}
"
            },

            // 2. C# - Filtro Avanzado de Tamaño y Modificación de Metadatos
            new ScriptDefinition
            {
                Name = "Filtro de Tamaño y Normalización (C#)",
                Category = "Filtrado",
                Description = "Comprueba el tamaño en MB del archivo, añade tags al contexto y emite hacia 'Aprobados' o 'Descartados'.",
                Language = "CSharp",
                InputPorts = ["In"],
                OutputPorts = ["Aprobados", "Descartados"],
                IsBuiltIn = true,
                ScriptCode = @"// Filtro condicional por tamaño (ejemplo: 50 MB)
double sizeMb = Item.FileSizeBytes / (1024.0 * 1024.0);
Item.Metadata[""SizeMB_Calculado""] = sizeMb;

if (sizeMb >= 50.0)
{
    Item.Tags.Add(""ArchivoGrande"");
    Log($""Archivo grande detectado: {Item.FileName} ({sizeMb:F2} MB)"");
    await EmitAsync(""Aprobados"");
}
else
{
    Item.Tags.Add(""ArchivoPequeno"");
    Log($""Archivo descartado por tamaño menor a 50 MB: {Item.FileName}"", LogLevel.Warning);
    await EmitAsync(""Descartados"");
}
"
            },

            // 3. C# - Inyector de Variables y Fechas Dinámicas
            new ScriptDefinition
            {
                Name = "Inyector de Fecha y Metadatos de Sistema (C#)",
                Category = "Metadatos",
                Description = "Inyecta fecha y hora de procesamiento, usuario del sistema y máquina en los metadatos del contexto.",
                Language = "CSharp",
                InputPorts = ["In"],
                OutputPorts = ["Out"],
                IsBuiltIn = true,
                ScriptCode = @"// Inyección de variables de contexto
Item.Metadata[""ProcesadoPor""] = Environment.UserName;
Item.Metadata[""Maquina""] = Environment.MachineName;
Item.Metadata[""FechaProcesamientoUtc""] = DateTime.UtcNow.ToString(""yyyy-MM-dd HH:mm:ss"");
Item.Metadata[""AnoProcesamiento""] = DateTime.UtcNow.Year;

Log($""Metadatos inyectados correctamente en {Item.FileName}"");
await EmitAsync(""Out"");
"
            },

            // 4. JavaScript - Enrutador Condicional Rápido
            new ScriptDefinition
            {
                Name = "Enrutador por Extensión (JavaScript)",
                Category = "Enrutamiento",
                Description = "Script en JavaScript que desvía archivos según pertenezcan a multimedia o archivos comprimidos.",
                Language = "JavaScript",
                InputPorts = ["In"],
                OutputPorts = ["Multimedia", "Comprimidos", "Resto"],
                IsBuiltIn = true,
                ScriptCode = @"// Enrutador rápido en JavaScript
var filename = item.FileName.toLowerCase();

if (filename.endsWith('.mp4') || filename.endsWith('.mkv') || filename.endsWith('.mp3')) {
    item.Metadata['Tipo'] = 'Multimedia';
    emit('Multimedia', item);
} else if (filename.endsWith('.zip') || filename.endsWith('.rar') || filename.endsWith('.7z')) {
    item.Metadata['Tipo'] = 'Comprimido';
    emit('Comprimidos', item);
} else {
    emit('Resto', item);
}
"
            },

            // 5. JavaScript - Sanitizador y Limpiador de Nombres
            new ScriptDefinition
            {
                Name = "Validador y Sanitizador de Nombres (JavaScript)",
                Category = "Transformación",
                Description = "Evalúa si el nombre de archivo contiene caracteres prohibidos o espacios múltiples y emite a 'Validos' o 'RequiereLimpieza'.",
                Language = "JavaScript",
                InputPorts = ["In"],
                OutputPorts = ["Validos", "RequiereLimpieza"],
                IsBuiltIn = true,
                ScriptCode = @"// Validador de nombres en JavaScript
var name = item.FileName;
var tieneEspaciosDobles = name.indexOf('  ') !== -1;
var tieneCaracteresRaros = /[\¿\?\¡\!\#\$\%\&]/.test(name);

if (tieneEspaciosDobles || tieneCaracteresRaros) {
    console.warn('El archivo requiere normalización: ' + name);
    item.Metadata['SugerenciaLimpieza'] = name.replace(/\s+/g, '_');
    emit('RequiereLimpieza', item);
} else {
    emit('Validos', item);
}
"
            }
        ];
    }

    public IReadOnlyList<ScriptDefinition> GetUserScripts()
    {
        lock (_lock)
        {
            return _userScripts.ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<ScriptDefinition> GetAllScripts()
    {
        var list = new List<ScriptDefinition>(GetBuiltInScripts());
        list.AddRange(GetUserScripts());
        return list.AsReadOnly();
    }

    public void SaveUserScript(ScriptDefinition script)
    {
        if (script == null) return;
        lock (_lock)
        {
            script.IsBuiltIn = false;
            _userScripts.RemoveAll(s => s.Id == script.Id || s.Name.Equals(script.Name, StringComparison.OrdinalIgnoreCase));
            _userScripts.Add(script);

            try
            {
                string filePath = Path.Combine(_storageDirectory, $"{SanitizeFileName(script.Name)}.ffscript");
                string json = JsonSerializer.Serialize(script, JsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving user script: {ex.Message}");
            }
        }
    }

    public bool DeleteUserScript(string scriptIdOrName)
    {
        lock (_lock)
        {
            var match = _userScripts.FirstOrDefault(s => s.Id == scriptIdOrName || s.Name.Equals(scriptIdOrName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                _userScripts.Remove(match);
                string filePath = Path.Combine(_storageDirectory, $"{SanitizeFileName(match.Name)}.ffscript");
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }
                return true;
            }
            return false;
        }
    }

    private void LoadUserScripts()
    {
        lock (_lock)
        {
            _userScripts.Clear();
            if (!Directory.Exists(_storageDirectory)) return;

            foreach (string file in Directory.GetFiles(_storageDirectory, "*.ffscript"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var script = JsonSerializer.Deserialize<ScriptDefinition>(json, JsonOptions);
                    if (script != null)
                    {
                        script.IsBuiltIn = false;
                        _userScripts.Add(script);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading script file '{file}': {ex.Message}");
                }
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return string.IsNullOrWhiteSpace(name) ? "script" : name;
    }
}
