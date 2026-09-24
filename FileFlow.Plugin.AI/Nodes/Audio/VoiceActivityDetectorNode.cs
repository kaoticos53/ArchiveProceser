using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de pipeline para detección de actividad vocal (VAD) y eliminación de silencios con Silero VAD.
/// Bifurca el flujo entre 'Speech' y 'Silent', o recorta los tramos mudos generando un nuevo archivo de audio.
/// </summary>
[NodeDefinition("VoiceActivityDetectorNode_Name", "AudioVoice", "VoiceActivityDetectorNode_Desc", PipelineRole.Filter,
    "vad", "silero", "voz", "silencio", "recortar silencios", "audio", "speech", "speech detection")]
public sealed class VoiceActivityDetectorNode : AudioAiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("VoiceActivityDetectorNode_Name", "Detector de Actividad Vocal (Silero VAD)");
    public override string Category => "AudioVoice";
    public override string Description => LocalizationManager.Instance.GetString("VoiceActivityDetectorNode_Desc", "Detecta voz humana y recorta silencios en archivos de audio con Silero VAD.");
    public override AiTaskType TaskType => AiTaskType.VoiceActivityDetection;

    public VoiceActivityDetectorNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Speech", typeof(FileItemContext), PortDirection.Output, "Speech"),
            new NodePort("Silent", typeof(FileItemContext), PortDirection.Output, "Silent"),
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Model"] = "Auto";
        Parameters["Mode"] = "DetectOnly";
        Parameters["SensitivityThreshold"] = 0.5;
        Parameters["MinSpeechDurationMs"] = 250;
        Parameters["PaddingDurationMs"] = 200;
        Parameters["OutputDirectory"] = "{GlobalOutputDir}";
        Parameters["SkipIfExists"] = false;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "silero-vad"],
            HelpText: "Modelo neural de detección de actividad vocal ('Auto' selecciona según hardware).", DisplayOrder: 1),
        new("Mode", ParameterEditorType.Dropdown, DefaultValue: "DetectOnly",
            Options: ["DetectOnly", "TrimSilence"],
            HelpText: "Modo de operación: solo detectar y clasificar o recortar silencios generando nuevo audio.", DisplayOrder: 2),
        new("SensitivityThreshold", ParameterEditorType.Slider, DefaultValue: 0.5, Min: 0.1, Max: 0.9, Step: 0.05,
            HelpText: "Umbral de probabilidad para considerar que un bloque de audio contiene voz.", DisplayOrder: 3),
        new("MinSpeechDurationMs", ParameterEditorType.Number, DefaultValue: 250, Min: 50, Max: 2000,
            HelpText: "Duración mínima en milisegundos de una intervención vocal para considerarse válida.", DisplayOrder: 4),
        new("PaddingDurationMs", ParameterEditorType.Number, DefaultValue: 200, Min: 0, Max: 1000,
            HelpText: "Margen de seguridad en milisegundos antes y después de cada tramo de voz.", DisplayOrder: 5),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}",
            HelpText: "Carpeta donde se guardará el audio recortado si el modo es 'TrimSilence'.", DisplayOrder: 6),
        new("SkipIfExists", ParameterEditorType.Toggle, DefaultValue: false,
            HelpText: "Si el archivo resultante ya existe en destino, omite el procesamiento y reutiliza el archivo.", DisplayOrder: 7)
    ];

    private static readonly HashSet<string> _supportedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".m4a", ".ogg", ".flac", ".wma", ".aac", ".mp4", ".mkv", ".avi"
    };

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
        {
            Log(context, $"[SileroVAD] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_supportedAudioExtensions.Contains(ext))
        {
            Log(context, $"[SileroVAD] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            item.Metadata["AI:VoiceDetected"] = false;
            await EmitAsync(context, item, "Silent").ConfigureAwait(false);
            await EmitAsync(context, item, "Out").ConfigureAwait(false);
            return;
        }

        try
        {
            string mode = GetParameter("Mode", "DetectOnly");
            double threshold = GetParameter("SensitivityThreshold", 0.5);
            int minSpeechMs = GetParameter("MinSpeechDurationMs", 250);
            int paddingMs = GetParameter("PaddingDurationMs", 200);
            string outputDirRaw = GetParameter("OutputDirectory", "{GlobalOutputDir}");
            bool skipIfExists = GetParameter("SkipIfExists", false);

            string? trimmedWavPath = null;
            if (string.Equals(mode, "TrimSilence", StringComparison.OrdinalIgnoreCase))
            {
                string targetDir = ResolveTargetDirectory(outputDirRaw, item);
                Directory.CreateDirectory(targetDir);
                trimmedWavPath = Path.Combine(targetDir, $"{Path.GetFileNameWithoutExtension(item.CurrentPath)}_trimmed.wav");

                if (skipIfExists && File.Exists(trimmedWavPath))
                {
                    Log(context, $"[SileroVAD] ⏭️ El archivo de salida ya existe ('{Path.GetFileName(trimmedWavPath)}'). Omitiendo inferencia.", LogLevel.Information, item);

                    var trimmedItem = CreateTrimmedAudioItem(item, trimmedWavPath);
                    await EmitAsync(context, trimmedItem, "Speech").ConfigureAwait(false);
                    await EmitAsync(context, trimmedItem, "Out").ConfigureAwait(false);
                    return;
                }
            }

            string? modelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);

            Log(context, $"[SileroVAD] 🎙️ Analizando actividad vocal en '{item.FileName}'...", LogLevel.Information, item);

            var analysis = await AudioInferenceEngine.DetectVoiceActivityAsync(
                modelPath,
                item.CurrentPath,
                threshold,
                minSpeechMs,
                paddingMs,
                trimmedWavPath,
                cancellationToken).ConfigureAwait(false);

            // Actualizar metadatos
            item.Metadata["AI:VoiceDetected"] = analysis.VoiceDetected;
            item.Metadata["AI:SpeechRatio"] = analysis.SpeechRatio;
            item.Metadata["AI:SpeechDurationSeconds"] = analysis.SpeechDurationSeconds;
            item.Metadata["AI:SpeechSegmentsCount"] = analysis.Segments.Count;
            item.Metadata["AI:SpeechSegmentsJson"] = JsonSerializer.Serialize(analysis.Segments);
            item.Metadata["AI:VadModel"] = string.IsNullOrWhiteSpace(modelPath) ? "silero-vad" : Path.GetFileNameWithoutExtension(modelPath);

            var emitItem = item;
            if (!string.IsNullOrWhiteSpace(analysis.TrimmedAudioPath) && File.Exists(analysis.TrimmedAudioPath))
            {
                emitItem = CreateTrimmedAudioItem(item, analysis.TrimmedAudioPath);
            }

            Log(context, $"[SileroVAD] Voz detectada: {analysis.VoiceDetected} (ratio: {analysis.SpeechRatio:P1}, duración voz: {analysis.SpeechDurationSeconds:F1}s / {analysis.TotalDurationSeconds:F1}s, segmentos: {analysis.Segments.Count}).",
                LogLevel.Information, emitItem);

            // Bifurcación
            if (analysis.VoiceDetected)
            {
                await EmitAsync(context, emitItem, "Speech").ConfigureAwait(false);
            }
            else
            {
                await EmitAsync(context, emitItem, "Silent").ConfigureAwait(false);
            }

            await EmitAsync(context, emitItem, "Out").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[SileroVAD] ❌ Error procesando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deriva el elemento de salida apuntando al .wav recortado, preservando los metadatos ya escritos sobre el original.
    /// </summary>
    private static FileItemContext CreateTrimmedAudioItem(FileItemContext source, string trimmedAudioPath)
    {
        var trimmed = source.DeepClone();
        trimmed.CurrentPath = trimmedAudioPath;
        trimmed.PhysicalPath = trimmedAudioPath;
        trimmed.FileSizeBytes = new FileInfo(trimmedAudioPath).Length;
        trimmed.Metadata["AI:VoiceDetected"] = true;
        trimmed.Metadata["AI:SilenceTrimmed"] = true;
        return trimmed;
    }

    /// <summary>La carpeta la decide la regla compartida del plugin: ver <see cref="NodeOutputDirectory"/>.</summary>
    internal static string ResolveTargetDirectory(string outputDirRaw, FileItemContext item) =>
        NodeOutputDirectory.For(outputDirRaw, item);
}
