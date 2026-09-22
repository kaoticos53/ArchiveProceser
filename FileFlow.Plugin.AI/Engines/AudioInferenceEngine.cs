using System;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI.Inference;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Fachada pública del motor de inferencia neural para procesamiento de audio.
/// Mantiene el ciclo de vida de las sesiones ONNX y delega el trabajo pesado en los
/// módulos especializados del subespacio <c>Engines/Audio</c>:
/// <see cref="VadEngine"/> (Silero VAD), <see cref="TtsEngine"/> (Piper TTS),
/// <see cref="AudioSessionStore"/> (almacén compartido de sesiones) y
/// <see cref="AudioWaveUtilities"/> (decodificación, resampling y exportación PCM).
/// Soporta aceleración ONNX, normalización NAudio a 16kHz mono y fallback inteligente basado en energía.
/// </summary>
public static class AudioInferenceEngine
{
    /// <summary>
    /// Se dispara cuando una sesión ONNX de audio se carga o se libera. Reexpide el evento único del
    /// registro de sesiones, el mismo que observan los nodos de cualquier motor.
    /// </summary>
    public static event Action? SessionStateChanged
    {
        add => OnnxSessionRegistry.SessionStateChanged += value;
        remove => OnnxSessionRegistry.SessionStateChanged -= value;
    }

    public static bool IsSessionLoaded(string modelPath)
        => AudioSessionStore.Instance.IsSessionLoaded(modelPath);

    public static int GetLoadedSessionCount()
        => AudioSessionStore.Instance.GetLoadedSessionCount();

    public static bool UnloadSession(string modelPath)
        => AudioSessionStore.Instance.UnloadSession(modelPath);

    /// <summary>
    /// Analiza un archivo de audio con Silero VAD para detectar voz humana y opcionalmente recortar silencios.
    /// </summary>
    public static Task<VadAnalysisResult> DetectVoiceActivityAsync(
        string? modelPath,
        string audioFilePath,
        double threshold = 0.5,
        int minSpeechDurationMs = 250,
        int paddingDurationMs = 200,
        string? outputTrimmedPath = null,
        CancellationToken cancellationToken = default)
        => VadEngine.DetectVoiceActivityAsync(modelPath, audioFilePath, threshold, minSpeechDurationMs, paddingDurationMs, outputTrimmedPath, cancellationToken);

    /// <summary>
    /// Sintetiza voz neural a partir de texto usando Piper TTS hacia un archivo .wav PCM de 16 bits.
    /// </summary>
    public static Task<double> SynthesizeSpeechAsync(
        string? modelPath,
        string text,
        string outputWavPath,
        double speechRate = 1.0,
        CancellationToken cancellationToken = default)
        => TtsEngine.SynthesizeSpeechAsync(modelPath, text, outputWavPath, speechRate, cancellationToken);

    /// <summary>
    /// Libera la caché de sesiones de audio ONNX.
    /// </summary>
    public static void ClearSessionCache()
        => AudioSessionStore.Instance.Clear();
}
