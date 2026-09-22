using FileFlow.Plugin.AI.Inference;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Almacén de sesiones ONNX de audio (Silero VAD y Piper TTS).
///
/// <para>
/// No añade lógica propia: es la misma abstracción <see cref="OnnxSessionStore"/> que usan visión y
/// embeddings, configurada con la única decisión que le es propia —estos grafos se ejecutan siempre en
/// CPU multihilo, sin DirectML— y registrada en <see cref="OnnxSessionRegistry"/> para que sus cargas y
/// descargas lleguen al evento único de estado.
/// </para>
/// </summary>
internal static class AudioSessionStore
{
    public static readonly OnnxSessionStore Instance = new(
        "Audio",
        OnnxSessionFactory.CreateCpuSession);

    static AudioSessionStore()
    {
        OnnxSessionRegistry.Register(Instance);
    }
}
