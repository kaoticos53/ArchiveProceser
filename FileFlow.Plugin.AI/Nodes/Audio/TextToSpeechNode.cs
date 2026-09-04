using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de síntesis vocal neural que convierte texto plano o variables de metadatos en archivos de audio .wav (Piper TTS).
/// Permite generar locuciones habladas para audiolibros, resúmenes ejecutivos o doblaje de traducciones.
/// </summary>
[NodeDefinition("TextToSpeechNode_Name", "AudioVoice", "TextToSpeechNode_Desc", PipelineRole.Transform,
    "tts", "piper", "voz", "hablar", "sintesis", "texto a voz", "audio", "locucion", "speech")]
public class TextToSpeechNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("TextToSpeechNode_Name", "Conversor de Texto a Voz (Piper TTS)");
    public string Category => "AudioVoice";
    public string Description => LocalizationManager.Instance.GetString("TextToSpeechNode_Desc", "Sintetiza locuciones de voz natural a partir de texto o metadatos usando Piper TTS.");

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
        ["Model"] = "Auto",
        ["InputSource"] = "FileContent",
        ["MetadataKeyName"] = "AI:Translation",
        ["CustomTextTemplate"] = "",
        ["SpeechRate"] = 1.0,
        ["OutputDirectory"] = "{GlobalOutputDir}"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "piper-es-davefx", "piper-en-lessac"],
            HelpText: "Voz y modelo neural de síntesis TTS ('Auto' selecciona según hardware).", DisplayOrder: 1),
        new("InputSource", ParameterEditorType.Dropdown, DefaultValue: "FileContent",
            Options: ["FileContent", "MetadataKey", "CustomText"],
            HelpText: "Origen del texto a locutar (archivo entrante, metadato de otro nodo o plantilla).", DisplayOrder: 2),
        new("MetadataKeyName", ParameterEditorType.Text, DefaultValue: "AI:Translation",
            HelpText: "Nombre de la clave de metadatos si InputSource es 'MetadataKey'.", DisplayOrder: 3),
        new("CustomTextTemplate", ParameterEditorType.MultiLineText, DefaultValue: "",
            HelpText: "Texto fijo o plantilla si InputSource es 'CustomText'.", DisplayOrder: 4),
        new("SpeechRate", ParameterEditorType.Slider, DefaultValue: 1.0, Min: 0.5, Max: 2.0, Step: 0.1,
            HelpText: "Velocidad de locución de la voz (1.0 = velocidad normal).", DisplayOrder: 5),
        new("OutputDirectory", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}",
            HelpText: "Carpeta de destino donde se guardarán los archivos .wav generados.", DisplayOrder: 6)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            string inputSource = Parameters.TryGetValue("InputSource", out var isVal) ? isVal?.ToString() ?? "FileContent" : "FileContent";
            string textToSynthesize = string.Empty;

            if (string.Equals(inputSource, "FileContent", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
                {
                    context.Log($"[PiperTTS] Archivo de texto no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
                    await context.EmitAsync("Error", item).ConfigureAwait(false);
                    return;
                }

                textToSynthesize = await File.ReadAllTextAsync(item.CurrentPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(inputSource, "MetadataKey", StringComparison.OrdinalIgnoreCase))
            {
                string key = Parameters.TryGetValue("MetadataKeyName", out var kVal) ? kVal?.ToString() ?? "AI:Translation" : "AI:Translation";
                if (item.Metadata.TryGetValue(key, out var metaVal) && metaVal != null)
                {
                    textToSynthesize = metaVal.ToString() ?? string.Empty;
                }
                else
                {
                    context.Log($"[PiperTTS] ⚠️ Clave de metadatos '{key}' no encontrada en el elemento.", LogLevel.Warning, item);
                }
            }
            else if (string.Equals(inputSource, "CustomText", StringComparison.OrdinalIgnoreCase))
            {
                textToSynthesize = Parameters.TryGetValue("CustomTextTemplate", out var ctVal) ? ctVal?.ToString() ?? string.Empty : string.Empty;
                textToSynthesize = textToSynthesize
                    .Replace("{FileName}", item.FileName)
                    .Replace("{OriginalPath}", item.OriginalPath);
            }

            if (string.IsNullOrWhiteSpace(textToSynthesize))
            {
                context.Log($"[PiperTTS] ⚠️ No hay texto disponible para sintetizar en {item.FileName}.", LogLevel.Warning, item);
                await context.EmitAsync("Error", item).ConfigureAwait(false);
                return;
            }

            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            double speechRate = Parameters.TryGetValue("SpeechRate", out var srVal) ? ParameterHelper.GetDouble(srVal, 1.0) : 1.0;
            string outputDirRaw = Parameters.TryGetValue("OutputDirectory", out var odVal) ? odVal?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";

            string? modelPath = await AiModelManager.ResolveModelPathAsync(
                modelChoice,
                AiTaskType.TextToSpeech,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            context.Log($"[PiperTTS] 🔊 Sintetizando audio para '{item.FileName}' ({textToSynthesize.Length} caracteres)...", LogLevel.Information, item);

            string targetDir = string.IsNullOrWhiteSpace(outputDirRaw) || outputDirRaw.Contains("{GlobalOutputDir}")
                ? Path.Combine(Path.GetDirectoryName(item.CurrentPath) ?? Directory.GetCurrentDirectory(), "Processed")
                : Path.GetFullPath(outputDirRaw);

            Directory.CreateDirectory(targetDir);

            string targetFileName = $"{Path.GetFileNameWithoutExtension(item.CurrentPath)}_tts.wav";
            string targetPath = Path.Combine(targetDir, targetFileName);

            double audioDuration = await AudioInferenceEngine.SynthesizeSpeechAsync(
                modelPath,
                textToSynthesize,
                targetPath,
                speechRate,
                cancellationToken).ConfigureAwait(false);

            var newItem = item.DeepClone();
            newItem.CurrentPath = targetPath;
            newItem.FileSizeBytes = new FileInfo(targetPath).Length;
            newItem.Metadata["AI:AudioGenerated"] = true;
            newItem.Metadata["AI:AudioDurationSeconds"] = audioDuration;
            newItem.Metadata["AI:TtsModel"] = string.IsNullOrWhiteSpace(modelPath) ? "piper-tts" : Path.GetFileNameWithoutExtension(modelPath);

            context.Log($"[PiperTTS] ✅ Audio sintetizado con éxito: '{targetFileName}' ({audioDuration:F1}s).", LogLevel.Information, newItem);
            await context.EmitAsync("Out", newItem).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Log($"[PiperTTS] ❌ Error generando audio para {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
