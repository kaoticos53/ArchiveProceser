using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Utilidades de procesamiento de ondas de audio, resampleo, conversión de canales y exportación PCM con NAudio.
/// </summary>
public static class AudioWaveUtilities
{
    /// <summary>
    /// Lee y decodifica un archivo de audio (WAV, MP3, etc.), resampleando a 16.000 Hz mono con muestras float [-1.0, 1.0].
    /// </summary>
    public static async Task<(float[] Samples, int SampleRate, double TotalDuration)> ReadAudioSamplesAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var reader = new AudioFileReader(filePath);
            ISampleProvider source = reader;

            // Resamplear a 16.000 Hz si difiere
            if (reader.WaveFormat.SampleRate != 16000)
            {
                source = new WdlResamplingSampleProvider(source, 16000);
            }

            // Mezclar a mono si es estéreo
            if (source.WaveFormat.Channels > 1)
            {
                source = new StereoToMonoSampleProvider(source);
            }

            var sampleList = new List<float>();
            float[] buffer = new float[4096];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int i = 0; i < read; i++)
                {
                    sampleList.Add(buffer[i]);
                }
            }

            float[] samples = [.. sampleList];
            double duration = (double)samples.Length / 16000.0;
            return (samples, 16000, duration);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exporta segmentos seleccionados de audio a un nuevo archivo WAV PCM de 16 bits.
    /// </summary>
    public static async Task ExportTrimmedWavAsync(
        float[] samples,
        int sampleRate,
        IReadOnlyList<SpeechSegment> segments,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            var trimmedSamples = new List<float>();
            foreach (var seg in segments)
            {
                int startIdx = Math.Clamp((int)(seg.StartSeconds * sampleRate), 0, samples.Length);
                int endIdx = Math.Clamp((int)(seg.EndSeconds * sampleRate), 0, samples.Length);
                int count = endIdx - startIdx;
                if (count > 0)
                {
                    trimmedSamples.AddRange(samples.AsSpan(startIdx, count).ToArray());
                }
            }

            WritePcm16Wav(outputPath, [.. trimmedSamples], sampleRate);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Escribe un arreglo de muestras float en un archivo WAV PCM lineal de 16 bits.
    /// </summary>
    public static void WritePcm16Wav(string outputPath, float[] samples, int sampleRate)
    {
        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        var waveFormat = new WaveFormat(sampleRate, 16, 1);
        using var writer = new WaveFileWriter(outputPath, waveFormat);

        byte[] pcmBuffer = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short s = (short)Math.Clamp((int)(samples[i] * 32767f), -32768, 32767);
            pcmBuffer[i * 2] = (byte)(s & 0xff);
            pcmBuffer[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }

        writer.Write(pcmBuffer, 0, pcmBuffer.Length);
    }

    /// <summary>
    /// Generador harmónico de voz sintetizada modulada por palabras y sílabas (fallback algorítmico sin modelo neural).
    /// </summary>
    public static double GenerateCadenceSpeechWav(
        string text,
        string outputWavPath,
        double speechRate,
        int sampleRate)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double wordDuration = 0.28 / Math.Clamp(speechRate, 0.5, 2.0);
        int totalSamples = (int)(words.Length * wordDuration * sampleRate) + (sampleRate / 4);

        float[] samples = new float[totalSamples];
        int sampleIndex = 0;

        foreach (var word in words)
        {
            int wordSamples = (int)(wordDuration * sampleRate);
            double baseFreq = 160.0 + (word.Length * 8.0 % 60.0); // Modulación formant vocal

            for (int i = 0; i < wordSamples && sampleIndex < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Sin(Math.PI * i / wordSamples); // Envolvente de sílaba
                float s = (float)(env * (0.6 * Math.Sin(2 * Math.PI * baseFreq * t) + 0.3 * Math.Sin(4 * Math.PI * baseFreq * t)));
                samples[sampleIndex++] = s;
            }

            // Pequeña pausa entre palabras (30 ms)
            int pauseSamples = (int)(0.03 * sampleRate);
            sampleIndex = Math.Min(totalSamples, sampleIndex + pauseSamples);
        }

        WritePcm16Wav(outputWavPath, samples, sampleRate);
        return (double)totalSamples / sampleRate;
    }
}
