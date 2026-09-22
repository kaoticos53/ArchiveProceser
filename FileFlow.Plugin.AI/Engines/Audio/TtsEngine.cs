using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Síntesis de voz neural (Piper TTS) con fallback a un generador armónico local
/// cuando el modelo ONNX no está disponible o está corrupto.
/// </summary>
internal static class TtsEngine
{
    /// <summary>
    /// Sintetiza voz neural a partir de texto usando Piper TTS hacia un archivo .wav PCM de 16 bits.
    /// </summary>
    public static async Task<double> SynthesizeSpeechAsync(
        string? modelPath,
        string text,
        string outputWavPath,
        double speechRate = 1.0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("El texto para síntesis vocal no puede estar vacío.", nameof(text));
        }

        return await Task.Run(() =>
        {
            string? dir = Path.GetDirectoryName(outputWavPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            const int sampleRate = 22050;

            // Si el modelo ONNX existe físicamente, ejecutar síntesis neural
            if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
            {
                try
                {
                    return RunPiperInference(modelPath, text, outputWavPath, speechRate, sampleRate);
                }
                catch
                {
                    // Caída en generador armónico de voz si el modelo ONNX está corrupto
                }
            }

            // Generador sintético harmónico de voz (para pruebas o fallback local offline)
            return AudioWaveUtilities.GenerateCadenceSpeechWav(text, outputWavPath, speechRate, sampleRate);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static double RunPiperInference(
        string modelPath,
        string text,
        string outputWavPath,
        double speechRate,
        int sampleRate)
    {
        var session = AudioSessionCache.GetOrCreateSession(modelPath);

        // Convertir caracteres a IDs de token para Piper
        long[] tokens = text.Select(c => (long)c).ToArray();
        var inputTensor = new DenseTensor<long>(tokens, [1, tokens.Length]);
        var lengthTensor = new DenseTensor<long>(new[] { (long)tokens.Length }, [1]);
        var scalesTensor = new DenseTensor<float>(new[] { 0.667f, (float)(1.0 / Math.Clamp(speechRate, 0.5, 2.0)), 0.8f }, [3]);

        var inputs = new System.Collections.Generic.List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputNames[0], inputTensor)
        };

        if (session.InputNames.Any(n => n.Contains("length", StringComparison.OrdinalIgnoreCase)))
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("input_lengths", lengthTensor));
        }
        if (session.InputNames.Any(n => n.Contains("scale", StringComparison.OrdinalIgnoreCase)))
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("scales", scalesTensor));
        }

        float[] audioFloats;
        lock (AudioSessionCache.InferenceLock)
        {
            using var outputs = session.Run(inputs);
            audioFloats = outputs.First().AsTensor<float>().ToArray();
        }

        AudioWaveUtilities.WritePcm16Wav(outputWavPath, audioFloats, sampleRate);
        return (double)audioFloats.Length / sampleRate;
    }
}
