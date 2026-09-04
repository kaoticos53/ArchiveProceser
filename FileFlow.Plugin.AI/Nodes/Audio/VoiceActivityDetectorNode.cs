using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de pipeline para detección de actividad vocal (VAD) y eliminación de silencios con Silero VAD.
/// Bifurca el flujo entre 'Speech' y 'Silent', o recorta los tramos mudos generando un nuevo archivo de audio.
/// </summary>
[NodeDefinition("VoiceActivityDetectorNode_Name", "AudioVoice", "VoiceActivityDetectorNode_Desc", PipelineRole.Filter,
    "vad", "silero", "voz", "silencio", "recortar silencios", "audio", "speech", "speech detection")]
public class VoiceActivityDetectorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("VoiceActivityDetectorNode_Name", "Detector de Actividad Vocal (Silero VAD)");
    public string Category => "AudioVoice";
    public string Description => LocalizationManager.Instance.GetString("VoiceActivityDetectorNode_Desc", "Detecta voz humana y recorta silencios en archivos de audio con Silero VAD.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Speech", typeof(FileItemContext), PortDirection.Output, "Speech"),
        new NodePort("Silent", typeof(FileItemContext), PortDirection.Output, "Silent"),
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Model"] = "Auto",
        ["Mode"] = "DetectOnly",
        ["SensitivityThreshold"] = 0.5,
        ["MinSpeechDurationMs"] = 250,
        ["PaddingDurationMs"] = 200,
        ["OutputDirectory"] = "{GlobalOutputDir}"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
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
            HelpText: "Carpeta donde se guardará el audio recortado si el modo es 'TrimSilence'.", DisplayOrder: 6)
    ];

    private static readonly HashSet<string> _supportedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".m4a", ".ogg", ".flac", ".wma", ".aac", ".mp4", ".mkv", ".avi"
    };

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[SileroVAD] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
        if (!_supportedAudioExtensions.Contains(ext))
        {
            context.Log($"[SileroVAD] Formato no compatible ({ext}): {item.FileName}", LogLevel.Warning, item);
            item.Metadata["AI:VoiceDetected"] = false;
            await context.EmitAsync("Silent", item).ConfigureAwait(false);
            await context.EmitAsync("Out", item).ConfigureAwait(false);
            return;
        }

        try
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string mode = Parameters.TryGetValue("Mode", out var modeVal) ? modeVal?.ToString() ?? "DetectOnly" : "DetectOnly";
            double threshold = Parameters.TryGetValue("SensitivityThreshold", out var stVal) ? ParameterHelper.GetDouble(stVal, 0.5) : 0.5;
            int minSpeechMs = Parameters.TryGetValue("MinSpeechDurationMs", out var msmVal) ? ParameterHelper.GetInt32(msmVal, 250) : 250;
            int paddingMs = Parameters.TryGetValue("PaddingDurationMs", out var pdVal) ? ParameterHelper.GetInt32(pdVal, 200) : 200;
            string outputDirRaw = Parameters.TryGetValue("OutputDirectory", out var odVal) ? odVal?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";

            string? modelPath = await AiModelManager.ResolveModelPathAsync(
                modelChoice,
                AiTaskType.VoiceActivityDetection,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            context.Log($"[SileroVAD] 🎙️ Analizando actividad vocal en '{item.FileName}'...", LogLevel.Information, item);

            string? trimmedWavPath = null;
            if (string.Equals(mode, "TrimSilence", StringComparison.OrdinalIgnoreCase))
            {
                string targetDir = ParameterHelper.ResolveOutputPath(
                    string.IsNullOrWhiteSpace(outputDirRaw) ? "{GlobalOutputDir}" : outputDirRaw,
                    item);

                Directory.CreateDirectory(targetDir);
                trimmedWavPath = Path.Combine(targetDir, $"{Path.GetFileNameWithoutExtension(item.CurrentPath)}_trimmed.wav");
            }

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
                var trimmedItem = item.DeepClone();
                trimmedItem.CurrentPath = analysis.TrimmedAudioPath;
                trimmedItem.PhysicalPath = analysis.TrimmedAudioPath;
                trimmedItem.FileSizeBytes = new FileInfo(analysis.TrimmedAudioPath).Length;
                trimmedItem.Metadata["AI:SilenceTrimmed"] = true;
                emitItem = trimmedItem;
            }

            context.Log($"[SileroVAD] Voz detectada: {analysis.VoiceDetected} (ratio: {analysis.SpeechRatio:P1}, duración voz: {analysis.SpeechDurationSeconds:F1}s / {analysis.TotalDurationSeconds:F1}s, segmentos: {analysis.Segments.Count}).",
                LogLevel.Information, emitItem);

            // Bifurcación
            if (analysis.VoiceDetected)
            {
                await context.EmitAsync("Speech", emitItem).ConfigureAwait(false);
            }
            else
            {
                await context.EmitAsync("Silent", emitItem).ConfigureAwait(false);
            }

            await context.EmitAsync("Out", emitItem).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Log($"[SileroVAD] ❌ Error procesando {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
