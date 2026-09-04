using System.Collections.Generic;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Catálogo de modelos IA con URLs de descarga, tamaños esperados y descriptores.
/// </summary>
public record AiModelInfo(
    string Id,
    string FileName,
    string DownloadUrl,
    long MinSizeBytes,
    string Description,
    string FriendlyName = "",
    string Category = "",
    IReadOnlyList<string>? DefaultUrls = null,
    AiTaskType TaskType = AiTaskType.ObjectDetection,
    long MinRamBytes = 2_000_000_000,
    bool GpuRecommended = false,
    string HardwareTier = "Lightweight"
)
{
    /// <summary>
    /// Lista de URLs de descarga configuradas (devuelve las personalizadas si existen; de lo contrario, las predeterminadas).
    /// </summary>
    public IReadOnlyList<string> DownloadUrls => AiModelManager.GetConfiguredUrls(Id);
}
