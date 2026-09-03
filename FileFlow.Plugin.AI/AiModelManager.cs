using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Gestor centralizado del ciclo de vida, catálogo y resolución de modelos de IA locales.
/// Coordina la selección óptima según hardware y delega la descarga y configuración en submódulos especializados.
/// </summary>
public static class AiModelManager
{
    // Catálogo de modelos cargado desde recurso embebido JSON
    public static readonly IReadOnlyDictionary<string, AiModelInfo> Catalog = LoadCatalog();

    private static IReadOnlyDictionary<string, AiModelInfo> LoadCatalog()
    {
        var dict = new Dictionary<string, AiModelInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var assembly = typeof(AiModelManager).Assembly;
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

    #region Configuración y URLs (Delegación a AiModelUrlConfig)

    public static void LoadConfig() => AiModelUrlConfig.LoadConfig();
    public static void SaveConfig() => AiModelUrlConfig.SaveConfig();
    public static IReadOnlyList<string> GetDefaultUrls(string modelId) => AiModelUrlConfig.GetDefaultUrls(modelId);
    public static IReadOnlyList<string> GetConfiguredUrls(string modelId) => AiModelUrlConfig.GetConfiguredUrls(modelId);
    public static bool HasCustomUrls(string modelId) => AiModelUrlConfig.HasCustomUrls(modelId);
    public static void SetCustomUrls(string modelId, IEnumerable<string> urls) => AiModelUrlConfig.SetCustomUrls(modelId, urls);
    public static void ResetCustomUrls(string modelId) => AiModelUrlConfig.ResetCustomUrls(modelId);
    public static void ResetAllCustomUrls() => AiModelUrlConfig.ResetAllCustomUrls();

    #endregion

    #region Estado de Descarga y Errores (Delegación a AiModelDownloader)

    public static string? LastError => AiModelDownloader.LastError;

    public static Task<string?> DownloadModelWithProgressAsync(
        string modelId,
        IProgress<double>? progress = null,
        Action<string>? statusLogger = null,
        CancellationToken cancellationToken = default)
        => AiModelDownloader.DownloadModelWithProgressAsync(modelId, progress, statusLogger, cancellationToken);

    public static Task<string?> EnsureModelAsync(
        string modelId,
        IFlowExecutionContext? context,
        FileItemContext? item,
        CancellationToken cancellationToken)
        => DownloadModelWithProgressAsync(
            modelId,
            progress: null,
            statusLogger: msg => context?.Log($"[AiModelManager] {msg}", LogLevel.Information, item),
            cancellationToken: cancellationToken);

    #endregion

    #region Directorio y Estado Local de Modelos

    public static string ModelsDirectory
    {
        get
        {
            string appBaseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Modo portable: data/models relativo al ejecutable
            if (File.Exists(Path.Combine(appBaseDir, "portable.dat")) ||
                Directory.Exists(Path.Combine(appBaseDir, "data")))
            {
                string portableDir = Path.Combine(appBaseDir, "data", "models");
                Directory.CreateDirectory(portableDir);
                return portableDir;
            }

            // Modo estándar: %AppData%/FileFlow/Models
            string standardDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FileFlow", "Models");
            Directory.CreateDirectory(standardDir);
            return standardDir;
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

    #endregion

    #region Resolución de Modelos en Ejecución

    /// <summary>
    /// Resuelve la ruta del modelo de IA a ejecutar según la elección del usuario (Auto, Catálogo Oficial o Archivo Local Personalizado).
    /// </summary>
    public static async Task<string?> ResolveModelPathAsync(
        string? modelSelection,
        string? customModelPath,
        AiTaskType taskType,
        IFlowExecutionContext context,
        FileItemContext? item = null,
        CancellationToken cancellationToken = default)
    {
        // Caso 1: Archivo local personalizado ("Custom")
        if (string.Equals(modelSelection, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(customModelPath))
            {
                context.Log($"[AiModelManager] ⚠️ Se ha seleccionado modelo personalizado ('Custom') pero la ruta de archivo está vacía.", LogLevel.Error, item);
                return null;
            }

            string fullPath = Path.GetFullPath(customModelPath);
            if (!File.Exists(fullPath))
            {
                context.Log($"[AiModelManager] ❌ Archivo de modelo personalizado no encontrado: '{fullPath}'", LogLevel.Error, item);
                return null;
            }

            context.Log($"[AiModelManager] 📦 Usando modelo personalizado: '{Path.GetFileName(fullPath)}' ({new FileInfo(fullPath).Length / (1024.0 * 1024.0):F1} MB)", LogLevel.Information, item);
            return fullPath;
        }

        string targetModelId;

        // Caso 2: Modo Automático ("Auto" o no configurado) -> Selección por hardware
        if (string.IsNullOrWhiteSpace(modelSelection) || string.Equals(modelSelection, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            var optimalModel = HardwareCapabilityDetector.GetOptimalModelForTask(taskType);
            targetModelId = optimalModel.Id;
            context.Log($"[AiModelManager] ⚡ Modo Automático: seleccionado '{optimalModel.FriendlyName}' basado en el hardware del equipo ({HardwareCapabilityDetector.Specs.HardwareTier}, RAM: {HardwareCapabilityDetector.Specs.TotalRamGb:F1} GB, GPU DirectML: {HardwareCapabilityDetector.Specs.HasDirectMlGpu}).", LogLevel.Information, item);
        }
        else
        {
            targetModelId = modelSelection.Trim();
        }

        // Caso 3: Modelo del catálogo oficial (se asegura su descarga y existencia)
        return await EnsureModelAsync(targetModelId, context, item, cancellationToken).ConfigureAwait(false);
    }

    #endregion
}
