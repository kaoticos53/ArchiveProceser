using System;
using System.Collections.Generic;
using System.IO;
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
    // Catálogo de modelos (delegado a AiModelCatalog)
    public static IReadOnlyDictionary<string, AiModelInfo> Catalog => AiModelCatalog.Catalog;

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

    #region Directorio y Estado Local de Modelos (Delegación a AiModelCatalog)

    public static string ModelsDirectory => AiModelCatalog.ModelsDirectory;

    public static string GetModelPath(string modelFileName) => AiModelCatalog.GetModelPath(modelFileName);

    public static bool IsModelAvailable(string modelId) => AiModelCatalog.IsModelAvailable(modelId);

    public static long? GetModelDiskSizeBytes(string modelId) => AiModelCatalog.GetModelDiskSizeBytes(modelId);

    public static bool DeleteModel(string modelId) => AiModelCatalog.DeleteModel(modelId);

    public static IReadOnlyList<AiModelInfo> GetModelsForTask(AiTaskType taskType) => AiModelCatalog.GetModelsForTask(taskType);

    #endregion

    #region Resolución de Modelos en Ejecución

    /// <summary>
    /// Resuelve la ruta del modelo de IA a ejecutar según la elección del usuario (Auto o Modelo del Catálogo Oficial).
    /// </summary>
    public static async Task<string?> ResolveModelPathAsync(
        string? modelSelection,
        AiTaskType taskType,
        IFlowExecutionContext? context = null,
        FileItemContext? item = null,
        CancellationToken cancellationToken = default)
    {
        string targetModelId;

        // Caso 1: Modo Automático ("Auto" o no configurado) -> Selección por hardware
        if (string.IsNullOrWhiteSpace(modelSelection) || string.Equals(modelSelection, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            var optimalModel = HardwareCapabilityDetector.GetOptimalModelForTask(taskType);
            targetModelId = optimalModel.Id;
            context?.Log($"[AiModelManager] ⚡ Modo Automático: seleccionado '{optimalModel.FriendlyName}' basado en el hardware del equipo ({HardwareCapabilityDetector.Specs.HardwareTier}, RAM: {HardwareCapabilityDetector.Specs.TotalRamGb:F1} GB, GPU DirectML: {HardwareCapabilityDetector.Specs.HasDirectMlGpu}).", LogLevel.Debug, item);
        }
        else
        {
            targetModelId = modelSelection.Trim();
        }

        // Caso 2: Modelo del catálogo oficial (se asegura su descarga y existencia)
        return await EnsureModelAsync(targetModelId, context, item, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resuelve de forma síncrona la ruta del archivo del modelo si ya se encuentra descargado en disco.
    /// </summary>
    public static string? ResolveModelPathSync(string? modelSelection, AiTaskType taskType)
    {
        string targetModelId;
        if (string.IsNullOrWhiteSpace(modelSelection) || string.Equals(modelSelection, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            var optimalModel = HardwareCapabilityDetector.GetOptimalModelForTask(taskType);
            targetModelId = optimalModel.Id;
        }
        else
        {
            targetModelId = modelSelection.Trim();
        }

        if (Catalog.TryGetValue(targetModelId, out var info))
        {
            string path = GetModelPath(info.FileName);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    /// <summary>
    /// Obtiene el nombre amigable de visualización del modelo según la selección actual del usuario.
    /// </summary>
    public static string GetModelDisplayName(string? modelSelection, AiTaskType taskType)
    {
        if (string.IsNullOrWhiteSpace(modelSelection) || string.Equals(modelSelection, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            var optimal = HardwareCapabilityDetector.GetOptimalModelForTask(taskType);
            return $"{optimal.FriendlyName} (Auto)";
        }
        if (Catalog.TryGetValue(modelSelection.Trim(), out var info))
        {
            return info.FriendlyName;
        }
        return modelSelection;
    }

    #endregion
}
