using Microsoft.ML.OnnxRuntime;

namespace FileFlow.Plugin.AI.Inference;

/// <summary>
/// Fachada del almacén de sesiones ONNX de visión y texto: aceleración DirectML con fallback
/// automático a CPU multihilo para operadores no compatibles en tiempo de ejecución.
///
/// <para>
/// La caché, el evento y el bloqueo de inferencia ya no viven aquí: son responsabilidad de
/// <see cref="OnnxSessionStore"/>, del que este tipo sólo conserva una instancia por defecto. La API
/// pública se mantiene intacta para los adaptadores y nodos existentes, pero el evento
/// <see cref="SessionStateChanged"/> se reexpide desde <see cref="OnnxSessionRegistry"/>, de modo que
/// sus observadores reciben también los cambios de los almacenes de audio y embeddings.
/// </para>
/// </summary>
public static class OnnxSessionManager
{
    /// <summary>
    /// Almacén compartido por los motores de visión y texto. Se registra en el constructor estático
    /// para que el registro global pueda contarlo y liberarlo.
    /// </summary>
    internal static readonly OnnxSessionStore Default = new(
        "Vision",
        CreateVisionSession,
        accelerationProbe: ShouldUseDirectMl,
        cpuFallbackFactory: OnnxSessionFactory.CreateCpuSession,
        releaseRetainedResources: ReleaseRetainedImageResources);

    static OnnxSessionManager()
    {
        OnnxSessionRegistry.Register(Default);
    }

    /// <summary>
    /// Evento único de estado de sesiones del plugin. Se conserva como alias del registro para no
    /// romper la API previa de los nodos y adaptadores.
    /// </summary>
    public static event Action? SessionStateChanged
    {
        add => OnnxSessionRegistry.SessionStateChanged += value;
        remove => OnnxSessionRegistry.SessionStateChanged -= value;
    }

    /// <summary>Bloqueo compartido de inferencia del almacén por defecto.</summary>
    public static Lock InferenceLock => Default.InferenceLock;

    /// <summary>Indica si la sesión ONNX del modelo está materializada en el almacén por defecto.</summary>
    public static bool IsSessionLoaded(string modelPath) => Default.IsSessionLoaded(modelPath);

    /// <summary>Número de sesiones materializadas en el almacén por defecto.</summary>
    public static int GetLoadedSessionCount() => Default.GetLoadedSessionCount();

    /// <summary>Rutas de los modelos materializados en el almacén por defecto.</summary>
    public static IReadOnlyList<string> GetLoadedModelPaths() => Default.GetLoadedModelPaths();

    /// <summary>Descarga y libera deterministamente la sesión del modelo.</summary>
    public static bool UnloadSession(string modelPath) => Default.UnloadSession(modelPath);

    /// <summary>
    /// Obtiene o materializa la sesión del modelo aplicando aceleración GPU DirectML a los modelos
    /// pesados compatibles y CPU multihilo al resto.
    /// </summary>
    public static InferenceSession GetOrCreateSession(string modelPath) => Default.GetOrCreateSession(modelPath);

    /// <summary>
    /// Ejecuta la inferencia serializada, conmutando a CPU si DirectML falla por un operador no
    /// soportado (Shape/NMS).
    /// </summary>
    public static IDisposableReadOnlyCollection<DisposableNamedOnnxValue> RunInference(
        string modelPath,
        IReadOnlyList<NamedOnnxValue> inputs)
        => Default.RunInference(modelPath, inputs);

    /// <summary>Libera todas las sesiones de visión y texto en caché.</summary>
    public static void ClearSessionCache() => Default.Clear();

    /// <summary>
    /// Determina si un modelo debe beneficiarse de aceleración por GPU DirectML.
    /// Habilita GPU para modelos pesados de visión de convolución pura (Super-Resolución, Remoción de
    /// fondos, Matting, etc.) y reserva CPU para modelos con grafos complejos, atención dinámica o
    /// topologías heredadas.
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

    /// <summary>Crea una sesión configurada con acelerador GPU DirectML (DML).</summary>
    public static InferenceSession CreateDirectMlSession(string modelPath)
        => OnnxSessionFactory.CreateDirectMlSession(modelPath);

    /// <summary>Crea una sesión configurada exclusivamente para ejecución en CPU multihilo.</summary>
    public static InferenceSession CreateCpuSession(string modelPath)
        => OnnxSessionFactory.CreateCpuSession(modelPath);

    private static InferenceSession CreateVisionSession(string modelPath)
    {
        if (!ShouldUseDirectMl(modelPath))
        {
            return OnnxSessionFactory.CreateCpuSession(modelPath);
        }

        try
        {
            return OnnxSessionFactory.CreateDirectMlSession(modelPath);
        }
        catch
        {
            return OnnxSessionFactory.CreateCpuSession(modelPath);
        }
    }

    private static void ReleaseRetainedImageResources()
    {
        try
        {
            SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
        }
        catch { }
    }
}
