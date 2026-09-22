using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Segmento temporal de voz detectado en un archivo de audio.
/// </summary>
public record SpeechSegment(double StartSeconds, double EndSeconds)
{
    public double DurationSeconds => Math.Max(0.0, EndSeconds - StartSeconds);
}

/// <summary>
/// Resultado del análisis de actividad vocal con Silero VAD.
/// </summary>
public record VadAnalysisResult(
    bool VoiceDetected,
    double SpeechRatio,
    double SpeechDurationSeconds,
    double TotalDurationSeconds,
    IReadOnlyList<SpeechSegment> Segments,
    string? TrimmedAudioPath);

/// <summary>
/// Detección de actividad vocal (VAD) con Silero ONNX y fallback analítico de energía RMS,
/// incluyendo agrupación de segmentos, padding y exportación de audio sin silencios.
/// </summary>
internal static class VadEngine
{
    /// <summary>
    /// Analiza un archivo de audio con Silero VAD para detectar voz humana y opcionalmente recortar silencios.
    /// </summary>
    public static async Task<VadAnalysisResult> DetectVoiceActivityAsync(
        string? modelPath,
        string audioFilePath,
        double threshold = 0.5,
        int minSpeechDurationMs = 250,
        int paddingDurationMs = 200,
        string? outputTrimmedPath = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Leer y convertir audio a 16.000 Hz mono float[-1.0, 1.0]
        var (samples, sampleRate, totalDuration) = await AudioWaveUtilities.ReadAudioSamplesAsync(audioFilePath, cancellationToken).ConfigureAwait(false);

        if (samples.Length == 0)
        {
            return new VadAnalysisResult(false, 0.0, 0.0, totalDuration, [], null);
        }

        const int chunkSize = 512; // 32 ms a 16kHz
        int numChunks = samples.Length / chunkSize;
        var chunkProbabilities = new float[numChunks];

        // 2. Inferencia: Silero VAD ONNX o Fallback RMS si no hay modelo físico
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            RunSileroVadInference(modelPath, samples, chunkSize, numChunks, chunkProbabilities);
        }
        else
        {
            // Fallback analítico de energía RMS para entornos sin modelo descargado
            RunRmsEnergyVadFallback(samples, chunkSize, numChunks, chunkProbabilities);
        }

        // 3. Agrupar probabilidades en segmentos temporales aplicando umbral y padding
        double chunkDuration = (double)chunkSize / sampleRate;
        double minSpeechSec = (double)minSpeechDurationMs / 1000.0;
        double paddingSec = (double)paddingDurationMs / 1000.0;

        var rawSegments = new List<SpeechSegment>();
        bool inSpeech = false;
        double segStart = 0.0;

        for (int i = 0; i < numChunks; i++)
        {
            float prob = chunkProbabilities[i];
            double currentTime = i * chunkDuration;

            if (!inSpeech && prob >= threshold)
            {
                inSpeech = true;
                segStart = currentTime;
            }
            else if (inSpeech && prob < (threshold * 0.7)) // histeresis
            {
                inSpeech = false;
                double segEnd = currentTime;
                if ((segEnd - segStart) >= minSpeechSec)
                {
                    rawSegments.Add(new SpeechSegment(segStart, segEnd));
                }
            }
        }

        if (inSpeech)
        {
            double segEnd = numChunks * chunkDuration;
            if ((segEnd - segStart) >= minSpeechSec)
            {
                rawSegments.Add(new SpeechSegment(segStart, segEnd));
            }
        }

        // 4. Aplicar padding y fusionar segmentos superpuestos
        var mergedSegments = MergeSegmentsWithPadding(rawSegments, paddingSec, totalDuration);

        double speechDuration = mergedSegments.Sum(s => s.DurationSeconds);
        double speechRatio = totalDuration > 0 ? Math.Clamp(speechDuration / totalDuration, 0.0, 1.0) : 0.0;
        bool voiceDetected = mergedSegments.Count > 0 && speechRatio >= 0.01;

        // 5. Generar archivo WAV sin silencios si se especificó outputTrimmedPath y hay voz
        string? trimmedPath = null;
        if (!string.IsNullOrWhiteSpace(outputTrimmedPath) && voiceDetected)
        {
            trimmedPath = outputTrimmedPath;
            await AudioWaveUtilities.ExportTrimmedWavAsync(samples, sampleRate, mergedSegments, trimmedPath, cancellationToken).ConfigureAwait(false);
        }

        return new VadAnalysisResult(
            voiceDetected,
            speechRatio,
            speechDuration,
            totalDuration,
            mergedSegments,
            trimmedPath);
    }

    private static void RunSileroVadInference(
        string modelPath,
        float[] samples,
        int chunkSize,
        int numChunks,
        float[] outputProbabilities)
    {
        var session = AudioSessionCache.GetOrCreateSession(modelPath);

        // Tensores de estado Silero VAD v4 / v5
        DenseTensor<float>? stateTensor = null;
        DenseTensor<float>? hTensor = null;
        DenseTensor<float>? cTensor = null;

        bool hasState = session.InputNames.Any(n => n.Equals("state", StringComparison.OrdinalIgnoreCase));
        if (hasState)
        {
            stateTensor = new DenseTensor<float>([2, 1, 128]);
        }
        else
        {
            hTensor = new DenseTensor<float>([2, 1, 64]);
            cTensor = new DenseTensor<float>([2, 1, 64]);
        }

        var srTensor = new DenseTensor<long>(new[] { 16000L }, [1]);
        var chunkTensor = new DenseTensor<float>([1, chunkSize]);

        lock (AudioSessionCache.InferenceLock)
        {
            for (int i = 0; i < numChunks; i++)
            {
                int srcOffset = i * chunkSize;
                for (int j = 0; j < chunkSize; j++)
                {
                    chunkTensor[0, j] = samples[srcOffset + j];
                }

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(session.InputNames[0], chunkTensor)
                };

                // Enviar SR si el modelo lo requiere
                if (session.InputNames.Any(n => n.Equals("sr", StringComparison.OrdinalIgnoreCase)))
                {
                    inputs.Add(NamedOnnxValue.CreateFromTensor("sr", srTensor));
                }

                // Enviar estados recurrentes
                if (stateTensor != null)
                {
                    inputs.Add(NamedOnnxValue.CreateFromTensor("state", stateTensor));
                }
                else if (hTensor != null && cTensor != null)
                {
                    inputs.Add(NamedOnnxValue.CreateFromTensor("h", hTensor));
                    inputs.Add(NamedOnnxValue.CreateFromTensor("c", cTensor));
                }

                using var outputs = session.Run(inputs);
                var outTensor = outputs.First().AsTensor<float>();
                outputProbabilities[i] = outTensor.GetValue(0);

                // Actualizar estados para el siguiente chunk
                foreach (var outVal in outputs)
                {
                    if (outVal.Name.Equals("state", StringComparison.OrdinalIgnoreCase))
                    {
                        stateTensor = (DenseTensor<float>)outVal.AsTensor<float>().ToDenseTensor();
                    }
                    else if (outVal.Name.Equals("hn", StringComparison.OrdinalIgnoreCase) && hTensor != null)
                    {
                        hTensor = (DenseTensor<float>)outVal.AsTensor<float>().ToDenseTensor();
                    }
                    else if (outVal.Name.Equals("cn", StringComparison.OrdinalIgnoreCase) && cTensor != null)
                    {
                        cTensor = (DenseTensor<float>)outVal.AsTensor<float>().ToDenseTensor();
                    }
                }
            }
        }
    }

    private static void RunRmsEnergyVadFallback(
        float[] samples,
        int chunkSize,
        int numChunks,
        float[] outputProbabilities)
    {
        for (int i = 0; i < numChunks; i++)
        {
            int offset = i * chunkSize;
            double sumSq = 0.0;
            for (int j = 0; j < chunkSize; j++)
            {
                float s = samples[offset + j];
                sumSq += s * s;
            }

            double rms = Math.Sqrt(sumSq / chunkSize);
            // Mapeo sigmoidal de energía RMS a probabilidad [0, 1]
            float prob = (float)(1.0 / (1.0 + Math.Exp(-(rms - 0.02) * 100.0)));
            outputProbabilities[i] = prob;
        }
    }

    private static List<SpeechSegment> MergeSegmentsWithPadding(
        List<SpeechSegment> rawSegments,
        double paddingSec,
        double totalDuration)
    {
        if (rawSegments.Count == 0) return [];

        var padded = rawSegments.Select(s => new SpeechSegment(
            Math.Max(0.0, s.StartSeconds - paddingSec),
            Math.Min(totalDuration, s.EndSeconds + paddingSec)
        )).OrderBy(s => s.StartSeconds).ToList();

        var merged = new List<SpeechSegment> { padded[0] };
        for (int i = 1; i < padded.Count; i++)
        {
            var last = merged[^1];
            var current = padded[i];

            if (current.StartSeconds <= last.EndSeconds)
            {
                // Superposición o continuidad: extender segmento
                merged[^1] = new SpeechSegment(last.StartSeconds, Math.Max(last.EndSeconds, current.EndSeconds));
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }
}
