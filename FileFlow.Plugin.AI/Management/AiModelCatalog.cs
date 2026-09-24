using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Catálogo de modelos de IA (cargado desde recurso embebido JSON) y estado local de los archivos
/// de modelo en disco: directorio de almacenamiento, disponibilidad, tamaño y eliminación.
/// </summary>
public static class AiModelCatalog
{
    // Catálogo de modelos cargado desde recurso embebido JSON
    public static readonly IReadOnlyDictionary<string, AiModelInfo> Catalog = LoadCatalog();

    private static IReadOnlyDictionary<string, AiModelInfo> LoadCatalog()
    {
        var dict = new Dictionary<string, AiModelInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var assembly = typeof(AiModelCatalog).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("ai_models_catalog.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                    string json = reader.ReadToEnd();
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, AiModelInfo>>(json);
                    if (loaded != null)
                    {
                        foreach (var kvp in loaded)
                        {
                            dict[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading embedded AI models catalog: {ex.Message}");
        }
        return dict;
    }

    /// <summary>
    /// Directorio donde el proceso busca los modelos descargados. En producción es el del perfil del usuario;
    /// las pruebas lo fijan para reproducir el ciclo «descarga el modelo a mitad de sesión» sin escribir en los
    /// datos reales. Es <c>internal</c> a propósito: sólo el ensamblado de pruebas lo toca, y deja el
    /// comportamiento de producción intacto.
    /// </summary>
    internal static string? ModelsDirectoryOverride { get; set; }

    /// <summary>
    /// Directorio local donde se almacenan los archivos de modelo descargados, con resistencia a fallos
    /// mediante rutas de respaldo en AppData o Temp.
    /// </summary>
    public static string ModelsDirectory
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ModelsDirectoryOverride))
            {
                string overridden = ModelsDirectoryOverride;
                try
                {
                    if (!Directory.Exists(overridden)) Directory.CreateDirectory(overridden);
                }
                catch { }
                return overridden;
            }

            string dir = FileFlow.Sdk.Storage.AppPaths.ModelsDirectory;
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return dir;
            }
            catch
            {
                // Fallback de ultra-resistencia a AppData estándar o Temp
                string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileFlow", "models");
                try
                {
                    if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
                    return fallback;
                }
                catch { }
                return Path.Combine(Path.GetTempPath(), "FileFlow", "models");
            }
        }
    }

    public static string GetModelPath(string modelFileName)
        => Path.Combine(ModelsDirectory, modelFileName);

    public static bool IsModelAvailable(string modelId)
    {
        if (!Catalog.TryGetValue(modelId, out var info)) return false;
        string path = GetModelPath(info.FileName);
        if (!File.Exists(path)) return false;
        return new FileInfo(path).Length >= info.MinSizeBytes;
    }

    public static long? GetModelDiskSizeBytes(string modelId)
    {
        if (!Catalog.TryGetValue(modelId, out var info)) return null;
        string path = GetModelPath(info.FileName);
        if (!File.Exists(path)) return null;
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return null;
        }
    }

    public static bool DeleteModel(string modelId)
    {
        if (!Catalog.TryGetValue(modelId, out var info)) return false;
        string path = GetModelPath(info.FileName);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    public static IReadOnlyList<AiModelInfo> GetModelsForTask(AiTaskType taskType)
    {
        return Catalog.Values
            .Where(m => m.TaskType == taskType)
            .ToList();
    }
}
