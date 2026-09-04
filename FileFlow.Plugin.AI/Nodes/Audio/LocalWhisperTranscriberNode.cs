using System.IO;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Whisper.net;

namespace FileFlow.Plugin.AI;

[NodeDefinition("LocalWhisperTranscriberNode_Name", "AudioVoice", "LocalWhisperTranscriberNode_Desc", PipelineRole.Analyze,
    "audio", "voz", "transcribir", "subtitulos", "srt", "speech", "whisper", "mp3", "wav")]
public class LocalWhisperTranscriberNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("LocalWhisperTranscriberNode_Name", "Transcriptor de Voz a Texto (Whisper)");
    public string Category => "AudioVoice";
    public string Description => LocalizationManager.Instance.GetString("LocalWhisperTranscriberNode_Desc", "Transcribe archivos de audio a texto y subtítulos .srt usando el modelo Whisper de forma local y privada.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ModelSize"] = "Auto",
        ["Language"] = "Auto",
        ["GenerateSrtSubtitles"] = false,
        ["OutputDirectory"] = "{GlobalOutputDir}"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("ModelSize", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "Tiny", "Base", "Small"],
            HelpText: "Tamaño del modelo Whisper ('Auto' selecciona según el hardware del equipo).", DisplayOrder: 1),
        new("Language", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "es", "en", "fr", "de", "it"], DisplayOrder: 2),
        new("GenerateSrtSubtitles", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 3),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 4)
    ];

    private static readonly HashSet<string> _audioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".m4a", ".ogg", ".flac", ".wma", ".mp4", ".mkv", ".avi"
    };

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[Whisper] Archivo de audio no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_audioExtensions.Contains(ext))
        {
            context.Log($"[Whisper] Formato no compatible para transcripción ({ext}): {item.FileName}", LogLevel.Warning, item);
            await context.EmitAsync("Out", item).ConfigureAwait(false);
            return;
        }

        try
        {
            string modelSize = Parameters.TryGetValue("ModelSize", out var ms) ? ms?.ToString() ?? "Auto" : "Auto";
            string lang = Parameters.TryGetValue("Language", out var l) ? l?.ToString() ?? "Auto" : "Auto";
            bool generateSrt = Parameters.TryGetValue("GenerateSrtSubtitles", out var gs) && ParameterHelper.GetBoolean(gs, false);
            string detectedLang = lang.Equals("Auto", StringComparison.OrdinalIgnoreCase) ? "auto" : lang;

            string targetSelection = modelSize.Equals("Auto", StringComparison.OrdinalIgnoreCase)
                ? "Auto"
                : $"whisper-{modelSize.ToLowerInvariant()}";

            context.Log($"[Whisper] Iniciando transcripción de '{item.FileName}' (modelo: {modelSize}, idioma: {lang})...", LogLevel.Information, item);

            // Resolver modelo automáticamente o desde selección/archivo
            string? modelPath = await AiModelManager.ResolveModelPathAsync(
                targetSelection,
                AiTaskType.SpeechToText,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            if (modelPath == null)
            {
                context.Log($"[Whisper] ⚠️ Modelo Whisper ({modelSize}) no disponible. El nodo pasa el archivo sin transcribir.", LogLevel.Warning, item);
                await context.EmitAsync("Out", item).ConfigureAwait(false);
                return;
            }

            // Convertir a WAV 16kHz mono si es necesario (NAudio)
            string wavPath = await EnsureWavFormatAsync(item.CurrentPath, ext, context, item, cancellationToken).ConfigureAwait(false);

            try
            {
                string transcriptText;
                var segments = new List<(TimeSpan Start, TimeSpan End, string Text)>();

                // Inferencia real con Whisper.net
                using var factory = WhisperFactory.FromPath(modelPath);
                var builder = factory.CreateBuilder().WithThreads(Math.Max(1, Environment.ProcessorCount / 2));

                if (!detectedLang.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    builder = builder.WithLanguage(detectedLang);

                if (generateSrt)
                    builder = builder.WithSegmentEventHandler((segment) => { /* handled below */ });

                await using var processor = builder.Build();

                var sb = new StringBuilder();

                await using var fileStream = File.OpenRead(wavPath);
                await foreach (var segment in processor.ProcessAsync(fileStream, cancellationToken).ConfigureAwait(false))
                {
                    sb.AppendLine(segment.Text.Trim());
                    segments.Add((segment.Start, segment.End, segment.Text.Trim()));
                }

                transcriptText = sb.ToString().Trim();

                int wordCount = transcriptText.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

                item.Metadata["Transcript"] = transcriptText;
                item.Metadata["Transcript:Language"] = detectedLang;
                item.Metadata["Transcript:WordCount"] = wordCount;
                item.Metadata["Transcript:SegmentCount"] = segments.Count;
                item.Metadata["Transcript:Model"] = modelSize;

                if (generateSrt && segments.Count > 0)
                {
                    string srtPath = await GenerateSrtFileAsync(item, segments, context, cancellationToken).ConfigureAwait(false);
                    item.Metadata["Transcript:SrtPath"] = srtPath;
                    context.Log($"[Whisper] Subtítulos SRT generados: {Path.GetFileName(srtPath)}", LogLevel.Information, item);
                }

                context.Log($"[Whisper] ✅ Transcripción completada: {wordCount} palabras, {segments.Count} segmentos.", LogLevel.Information, item);
            }
            finally
            {
                // Limpiar archivo WAV temporal si fue generado
                if (!wavPath.Equals(item.CurrentPath, StringComparison.OrdinalIgnoreCase) && File.Exists(wavPath))
                {
                    try { File.Delete(wavPath); } catch { }
                }
            }

            await context.EmitAsync("Out", item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[Whisper] Error transcribiendo {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Convierte el audio a WAV 16kHz mono usando NAudio si es necesario.
    /// </summary>
    private static async Task<string> EnsureWavFormatAsync(
        string inputPath, string ext,
        IFlowExecutionContext context, FileItemContext item,
        CancellationToken cancellationToken)
    {
        // WAV ya correcto: intentar leerlo directamente
        if (ext == ".wav")
        {
            try
            {
                using var testReader = new WaveFileReader(inputPath);
                if (testReader.WaveFormat.SampleRate == 16000 && testReader.WaveFormat.Channels == 1)
                    return inputPath;
            }
            catch { }
        }

        // Necesitamos convertir: generar WAV temporal 16kHz mono
        string tempWav = Path.Combine(Path.GetTempPath(), $"fileflow_whisper_{Guid.NewGuid():N}.wav");
        context.Log($"[Whisper] Convirtiendo audio a WAV 16kHz mono: {Path.GetFileName(inputPath)}...", LogLevel.Debug, item);

        await Task.Run(() =>
        {
            using var reader = new AudioFileReader(inputPath);
            ISampleProvider source = reader;

            // Resamplear a 16kHz
            if (reader.WaveFormat.SampleRate != 16000)
                source = new WdlResamplingSampleProvider(source, 16000);

            // Convertir a mono si es estéreo
            if (source.WaveFormat.Channels > 1)
                source = new StereoToMonoSampleProvider(source);

            WaveFileWriter.CreateWaveFile16(tempWav, source);
        }, cancellationToken).ConfigureAwait(false);

        return tempWav;
    }

    private static async Task<string> GenerateSrtFileAsync(
        FileItemContext item,
        List<(TimeSpan Start, TimeSpan End, string Text)> segments,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string outDir = item.Metadata.TryGetValue("GlobalOutputDir", out var gOut) && gOut is string g && !string.IsNullOrWhiteSpace(g)
            ? g
            : Path.GetDirectoryName(item.CurrentPath) ?? Path.GetTempPath();

        Directory.CreateDirectory(outDir);

        string baseName = Path.GetFileNameWithoutExtension(item.FileName);
        string srtPath = Path.Combine(outDir, $"{baseName}.srt");

        var sb = new StringBuilder();
        for (int i = 0; i < segments.Count; i++)
        {
            var (start, end, text) = segments[i];
            sb.AppendLine((i + 1).ToString());
            sb.AppendLine($"{FormatSrtTime(start)} --> {FormatSrtTime(end)}");
            sb.AppendLine(text);
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(srtPath, sb.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return srtPath;
    }

    private static string FormatSrtTime(TimeSpan ts)
        => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
}
