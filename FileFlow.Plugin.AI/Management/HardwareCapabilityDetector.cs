using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nivel de compatibilidad de un modelo con la máquina actual.
/// </summary>
public enum ModelCompatibility
{
    /// <summary>
    /// El equipo supera los requisitos y el modelo funcionará de manera óptima y fluida.
    /// </summary>
    Recommended,

    /// <summary>
    /// El equipo puede ejecutar el modelo, pero usará CPU o consumirá una porción considerable de recursos.
    /// </summary>
    Playable,

    /// <summary>
    /// La máquina no tiene suficiente memoria RAM o aceleración para ejecutar este modelo con seguridad.
    /// </summary>
    InsufficientHardware
}

/// <summary>
/// Resumen de especificaciones del sistema analizadas para la ejecución de IA.
/// </summary>
public record SystemHardwareSpecs(
    long TotalRamBytes,
    int LogicalCores,
    bool HasDirectMlGpu,
    string HardwareTier
)
{
    public double TotalRamGb => TotalRamBytes / (1024.0 * 1024.0 * 1024.0);
}

/// <summary>
/// Analizador de capacidades de hardware para inferencia local de IA.
/// Evalúa RAM, núcleos de CPU y aceleración GPU DirectML para seleccionar modelos óptimos.
/// </summary>
public static class HardwareCapabilityDetector
{
    private static readonly Lazy<SystemHardwareSpecs> _specs = new(DetectSpecs);

    /// <summary>
    /// Especificaciones de hardware detectadas en el sistema.
    /// </summary>
    public static SystemHardwareSpecs Specs => _specs.Value;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private static SystemHardwareSpecs DetectSpecs()
    {
        long totalRam = 0;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    totalRam = (long)memStatus.ullTotalPhys;
                }
            }
            catch
            {
                // Fallback a GC Memory Info
            }
        }

        if (totalRam <= 0)
        {
            try
            {
                totalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            }
            catch
            {
                totalRam = 8L * 1024 * 1024 * 1024; // Asumir 8 GB por defecto
            }
        }

        int cores = Environment.ProcessorCount;
        bool hasDml = CheckDirectMlAvailability();

        string tier;
        double ramGb = totalRam / (1024.0 * 1024.0 * 1024.0);

        if (ramGb >= 15.0 && hasDml && cores >= 6)
        {
            tier = "Performance";
        }
        else if (ramGb >= 7.0 && cores >= 4)
        {
            tier = "Balanced";
        }
        else
        {
            tier = "Lightweight";
        }

        return new SystemHardwareSpecs(totalRam, cores, hasDml, tier);
    }

    private static bool CheckDirectMlAvailability()
    {
        try
        {
            using var options = new SessionOptions();
            options.AppendExecutionProvider_DML(0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Evalúa la compatibilidad de un modelo específico de IA con la máquina actual.
    /// </summary>
    public static ModelCompatibility GetCompatibility(AiModelInfo model)
    {
        var specs = Specs;

        // Comprobación de RAM
        if (specs.TotalRamBytes > 0 && specs.TotalRamBytes < model.MinRamBytes)
        {
            return ModelCompatibility.InsufficientHardware;
        }

        // Si el modelo recomienda GPU potente y no hay DirectML
        if (model.GpuRecommended && !specs.HasDirectMlGpu)
        {
            return ModelCompatibility.Playable;
        }

        if (model.HardwareTier == "Performance" && specs.HardwareTier == "Lightweight")
        {
            return ModelCompatibility.Playable;
        }

        return ModelCompatibility.Recommended;
    }

    /// <summary>
    /// Selecciona el modelo más óptimo del catálogo para una tarea de IA según el hardware de la máquina.
    /// </summary>
    public static AiModelInfo GetOptimalModelForTask(AiTaskType task, bool preferSpeed = false)
    {
        var models = AiModelManager.GetModelsForTask(task);
        if (models.Count == 0)
        {
            throw new InvalidOperationException($"No hay modelos registrados en el catálogo para la tarea '{task}'.");
        }

        if (models.Count == 1)
        {
            return models[0];
        }

        var specs = Specs;

        // Si el usuario prefiere velocidad o el equipo es modesto, elegir el modelo más ligero
        if (preferSpeed || specs.HardwareTier == "Lightweight")
        {
            return models
                .OrderBy(m => m.MinSizeBytes)
                .First();
        }

        // Si el equipo es de alto rendimiento y tiene DirectML GPU
        if (specs.HardwareTier == "Performance" && specs.HasDirectMlGpu)
        {
            var bestModel = models
                .OrderByDescending(m => m.HardwareTier == "Performance")
                .ThenByDescending(m => m.MinSizeBytes)
                .FirstOrDefault();

            if (bestModel != null) return bestModel;
        }

        // Para equipos equilibrados (Balanced): buscar modelo Balanced o el primero compatible recomendado
        var balanced = models.FirstOrDefault(m => m.HardwareTier == "Balanced")
                       ?? models.FirstOrDefault(m => GetCompatibility(m) == ModelCompatibility.Recommended)
                       ?? models[0];

        return balanced;
    }
}
