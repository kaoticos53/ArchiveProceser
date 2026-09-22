using Microsoft.ML.OnnxRuntime;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Factoría única de <see cref="InferenceSession"/>. Antes cada motor (visión, audio y embeddings)
/// repetía su propio bloque de <see cref="SessionOptions"/> con los mismos valores; ahora las tres
/// políticas viven aquí y un cambio de hilos de inferencia se aplica a todos los modelos por igual.
/// </summary>
internal static class OnnxSessionFactory
{
    /// <summary>Crea una sesión ligada a CPU multihilo, válida para cualquier grafo.</summary>
    public static InferenceSession CreateCpuSession(string modelPath)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4),
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR
        };

        return new InferenceSession(modelPath, options);
    }

    /// <summary>
    /// Crea una sesión acelerada con DirectML. Puede lanzar si el operador no está soportado por el
    /// proveedor; quien la invoque debe tener preparado el fallback a CPU.
    /// </summary>
    public static InferenceSession CreateDirectMlSession(string modelPath)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4),
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR
        };
        options.AppendExecutionProvider_DML(0);

        return new InferenceSession(modelPath, options);
    }
}
