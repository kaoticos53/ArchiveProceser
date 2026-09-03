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
    /// Obtiene o inicializa de forma diferida (thread-safe) la sesión ONNX asociada al modelo.
    /// </summary>
    public static InferenceSession GetOrCreateSession(string modelPath)
    {
        var lazy = _sessionCache.GetOrAdd(modelPath, path => new Lazy<InferenceSession>(() =>
        {
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = 1,
                IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
            };

            // Intentar GPU DirectML primero, caer en CPU si no está disponible
            try
            {
                options.AppendExecutionProvider_DML(0);
                return new InferenceSession(path, options);
            }
            catch
            {
                return CreateCpuSession(path);
            }
        }));

        return lazy.Value;
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
