using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Gestiona la persistencia y personalización de URLs de descarga de modelos de IA.
/// </summary>
public static class AiModelUrlConfig
{
    private static readonly Lock _configLock = new();
    private static readonly ConcurrentDictionary<string, List<string>> _customUrls = new(StringComparer.OrdinalIgnoreCase);

    private static string ConfigFilePath => Path.Combine(AppPaths.ConfigDirectory, "ai_models_config.json");

    static AiModelUrlConfig()
    {
        LoadConfig();
    }

    /// <summary>
    /// Carga la configuración de URLs personalizadas desde disco.
    /// </summary>
    public static void LoadConfig()
    {
        lock (_configLock)
        {
            try
            {
                string path = ConfigFilePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    if (dict != null)
                    {
                        _customUrls.Clear();
                        foreach (var kvp in dict)
                        {
                            var list = kvp.Value?
                                .Where(u => !string.IsNullOrWhiteSpace(u))
                                .Select(u => u.Trim())
                                .ToList() ?? [];
                            if (list.Count > 0)
                            {
                                _customUrls[kvp.Key] = list;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AiModelUrlConfig] Error loading config: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Guarda la configuración de URLs personalizadas en disco.
    /// </summary>
    public static void SaveConfig()
    {
        lock (_configLock)
        {
            try
            {
                string path = ConfigFilePath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var dict = new Dictionary<string, List<string>>(_customUrls, StringComparer.OrdinalIgnoreCase);
                string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AiModelUrlConfig] Error saving config: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Devuelve la lista oficial de URLs predeterminadas de fábrica para un modelo.
    /// </summary>
    public static IReadOnlyList<string> GetDefaultUrls(string modelId)
    {
        if (AiModelManager.Catalog.TryGetValue(modelId, out var info))
        {
            if (info.DefaultUrls != null && info.DefaultUrls.Count > 0)
            {
                return info.DefaultUrls;
            }
            return string.IsNullOrEmpty(info.DownloadUrl) ? [] : [info.DownloadUrl];
        }
        return [];
    }

    /// <summary>
    /// Devuelve la lista de URLs configuradas para un modelo (personalizadas si existen; en caso contrario, predeterminadas).
    /// </summary>
    public static IReadOnlyList<string> GetConfiguredUrls(string modelId)
    {
        if (_customUrls.TryGetValue(modelId, out var list) && list.Count > 0)
        {
            return list;
        }
        return GetDefaultUrls(modelId);
    }

    /// <summary>
    /// Indica si un modelo tiene URLs personalizadas definidas por el usuario.
    /// </summary>
    public static bool HasCustomUrls(string modelId)
    {
        return _customUrls.TryGetValue(modelId, out var list) && list.Count > 0;
    }

    /// <summary>
    /// Configura URLs personalizadas para un modelo determinado y las persiste en disco.
    /// </summary>
    public static void SetCustomUrls(string modelId, IEnumerable<string> urls)
    {
        var cleaned = urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleaned.Count == 0)
        {
            ResetCustomUrls(modelId);
            return;
        }

        var defaultUrls = GetDefaultUrls(modelId);
        if (cleaned.SequenceEqual(defaultUrls, StringComparer.OrdinalIgnoreCase))
        {
            ResetCustomUrls(modelId);
            return;
        }

        _customUrls[modelId] = cleaned;
        SaveConfig();
    }

    /// <summary>
    /// Restablece las URLs de un modelo a sus valores oficiales por defecto.
    /// </summary>
    public static void ResetCustomUrls(string modelId)
    {
        _customUrls.TryRemove(modelId, out _);
        SaveConfig();
    }

    /// <summary>
    /// Restablece todas las URLs de todos los modelos a sus valores oficiales por defecto.
    /// </summary>
    public static void ResetAllCustomUrls()
    {
        _customUrls.Clear();
        SaveConfig();
    }
}
