using System.Collections.Concurrent;
using Microsoft.ML.OnnxRuntime;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Gestor centralizado y concurrente de sesiones InferenceSession de ONNX Runtime.
/// Soporta aceleración por hardware con DirectML y fallback automático y resiliente a CPU multihilo
/// en caso de operadores no compatibles en tiempo de ejecución.
/// </summary>
public static class OnnxSessionManager
{
    private static readonly ConcurrentDictionary<string, Lazy<InferenceSession>> _sessionCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock _inferenceLock = new();

    /// <summary>
    /// Bloqueo global de inferencia para sincronizar ejecuciones de tensores en modelos no reentrantes.
    /// </summary>
    public static Lock InferenceLock => _inferenceLock;

    /// <summary>
    /// Obtiene o inicializa de forma diferida (thread-safe) la sesión ONNX asociada al modelo,
    /// aplicando aceleración GPU DirectML para modelos pesados compatibles y CPU multihilo para modelos ligeros/heredados.
    /// </summary>
    public static InferenceSession GetOrCreateSession(string modelPath)
    {
        var lazy = _sessionCache.GetOrAdd(modelPath, path => new Lazy<InferenceSession>(() =>
        {
            if (ShouldUseDirectMl(path))
            {
                try
                {
                    return CreateDirectMlSession(path);
                }
                catch
                {
                    return CreateCpuSession(path);
                }
            }

            return CreateCpuSession(path);
        }));

        return lazy.Value;
    }

    /// <summary>
    /// Determina si un modelo debe beneficiarse de aceleración por GPU DirectML.
    /// Habilita GPU para modelos pesados de visión de convolución pura (Super-Resolución, Remoción de fondos, Matting, etc.)
    /// y reserva CPU para modelos con grafos complejos, atención dinámica o topologías heredadas.
    /// </summary>
    public static bool ShouldUseDirectMl(string modelPath)
    {
        if (!HardwareCapabilityDetector.Specs.HasDirectMlGpu)
            return false;

        string fileName = Path.GetFileName(modelPath);

        return fileName.Contains("realesr", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("rmbg", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("modnet", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("open_nsfw", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("mobilenetv2", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Crea una sesión configurada con acelerador GPU DirectML (DML).
    /// </summary>
    public static InferenceSession CreateDirectMlSession(string modelPath)
    {
        var dmlOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
        };
        dmlOptions.AppendExecutionProvider_DML(0);
        return new InferenceSession(modelPath, dmlOptions);
    }

    /// <summary>
    /// Crea una sesión configurada exclusivamente para ejecución en CPU multihilo.
    /// </summary>
    public static InferenceSession CreateCpuSession(string modelPath)
    {
        var cpuOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
        };
        return new InferenceSession(modelPath, cpuOptions);
    }

    /// <summary>
    /// Ejecuta la inferencia de forma segura. Si el proveedor DirectML falla por un operador no soportado (ej. Shape/NMS),
    /// conmuta automáticamente la sesión del modelo a CPU puro y reejecuta la inferencia sin fallar.
    /// </summary>
    public static IDisposableReadOnlyCollection<DisposableNamedOnnxValue> RunInference(string modelPath, IReadOnlyList<NamedOnnxValue> inputs)
    {
        var session = GetOrCreateSession(modelPath);

        lock (_inferenceLock)
        {
            try
            {
                return session.Run(inputs);
            }
            catch (Exception ex) when (IsDmlExecutionError(ex))
            {
                // Conmutar sesión en caché a CPU puro para este modelo
                var cpuSession = CreateCpuSession(modelPath);
                _sessionCache[modelPath] = new Lazy<InferenceSession>(() => cpuSession);

                try
                {
                    session.Dispose();
                }
                catch { }

                // Reintentar la ejecución en CPU
                return cpuSession.Run(inputs);
            }
        }
    }

    private static bool IsDmlExecutionError(Exception ex)
    {
        string msg = ex.ToString();
        return msg.Contains("DmlExecutionProvider", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("MLOperatorAuthorImpl", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("80070057", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("DirectML", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("node_Shape", StringComparison.OrdinalIgnoreCase) ||
               (ex is OnnxRuntimeException);
    }

    /// <summary>
    /// Libera todas las sesiones ONNX en caché de forma determinista.
    /// </summary>
    public static void ClearSessionCache()
    {
        foreach (var lazy in _sessionCache.Values)
        {
            if (lazy.IsValueCreated)
            {
                try { lazy.Value.Dispose(); } catch { }
            }
        }
        _sessionCache.Clear();
    }
}
