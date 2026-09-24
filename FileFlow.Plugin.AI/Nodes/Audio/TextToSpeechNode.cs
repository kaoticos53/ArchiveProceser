using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de síntesis vocal neural que convierte texto plano o variables de metadatos en archivos de audio .wav (Piper TTS).
/// Permite generar locuciones habladas para audiolibros, resúmenes ejecutivos o doblaje de traducciones.
/// </summary>
[NodeDefinition("TextToSpeechNode_Name", "AudioVoice", "TextToSpeechNode_Desc", PipelineRole.Transform,
    "tts", "piper", "voz", "hablar", "sintesis", "texto a voz", "audio", "locucion", "speech")]
public sealed class TextToSpeechNode : AudioAiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("TextToSpeechNode_Name", "Conversor de Texto a Voz (Piper TTS)");
    public override string Category => "AudioVoice";
    public override string Description => LocalizationManager.Instance.GetString("TextToSpeechNode_Desc", "Sintetiza locuciones de voz natural a partir de texto o metadatos usando Piper TTS.");
    public override AiTaskType TaskType => AiTaskType.TextToSpeech;

    public TextToSpeechNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Model"] = "Auto";
        Parameters["InputSource"] = "FileContent";
        Parameters["MetadataKeyName"] = "AI:Translation";
        Parameters["CustomTextTemplate"] = "";
        Parameters["SpeechRate"] = 1.0;
        Parameters["OutputDirectory"] = "{GlobalOutputDir}";
        Parameters["SkipIfExists"] = false;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
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
            HelpText: "Carpeta de destino donde se guardarán los archivos .wav generados.", DisplayOrder: 6),
        new("SkipIfExists", ParameterEditorType.Toggle, DefaultValue: false,
            HelpText: "Si el archivo resultante ya existe en destino, omite la síntesis TTS y reutiliza el archivo.", DisplayOrder: 7)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            string inputSource = GetParameter("InputSource", "FileContent");
            string textToSynthesize = string.Empty;

            if (string.Equals(inputSource, "FileContent", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
                {
                    Log(context, $"[PiperTTS] Archivo de texto no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
                    await EmitAsync(context, item, "Error").ConfigureAwait(false);
                    return;
                }

                textToSynthesize = await File.ReadAllTextAsync(item.CurrentPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(inputSource, "MetadataKey", StringComparison.OrdinalIgnoreCase))
            {
                string key = GetParameter("MetadataKeyName", "AI:Translation");
                if (item.Metadata.TryGetValue(key, out var metaVal) && metaVal != null)
                {
                    textToSynthesize = metaVal.ToString() ?? string.Empty;
                }
                else
                {
                    Log(context, $"[PiperTTS] ⚠️ Clave de metadatos '{key}' no encontrada en el elemento.", LogLevel.Warning, item);
                }
            }
            else if (string.Equals(inputSource, "CustomText", StringComparison.OrdinalIgnoreCase))
            {
                textToSynthesize = (GetParameter("CustomTextTemplate", string.Empty) ?? string.Empty)
                    .Replace("{FileName}", item.FileName)
                    .Replace("{OriginalPath}", item.OriginalPath);
            }

            if (string.IsNullOrWhiteSpace(textToSynthesize))
            {
                Log(context, $"[PiperTTS] ⚠️ No hay texto disponible para sintetizar en {item.FileName}.", LogLevel.Warning, item);
                await EmitAsync(context, item, "Error").ConfigureAwait(false);
                return;
            }

            double speechRate = GetParameter("SpeechRate", 1.0);
            string outputDirRaw = GetParameter("OutputDirectory", "{GlobalOutputDir}");
            bool skipIfExists = GetParameter("SkipIfExists", false);

            string targetDir = ResolveTargetDirectory(outputDirRaw, item);
            Directory.CreateDirectory(targetDir);

            string targetFileName = $"{Path.GetFileNameWithoutExtension(item.CurrentPath)}_tts.wav";
            string targetPath = Path.Combine(targetDir, targetFileName);

            if (skipIfExists && File.Exists(targetPath))
            {
                Log(context, $"[PiperTTS] ⏭️ El archivo de audio ya existe ('{targetFileName}'). Omitiendo síntesis.", LogLevel.Information, item);
                await EmitAsync(context, CreateGeneratedAudioItem(item, targetPath)).ConfigureAwait(false);
                return;
            }

            string? modelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);

            Log(context, $"[PiperTTS] 🔊 Sintetizando audio para '{item.FileName}' ({textToSynthesize.Length} caracteres)...", LogLevel.Information, item);

            double audioDuration = await AudioInferenceEngine.SynthesizeSpeechAsync(
                modelPath,
                textToSynthesize,
                targetPath,
                speechRate,
                cancellationToken).ConfigureAwait(false);

            var newItem = CreateGeneratedAudioItem(item, targetPath);
            newItem.Metadata["AI:AudioDurationSeconds"] = audioDuration;
            newItem.Metadata["AI:TtsModel"] = string.IsNullOrWhiteSpace(modelPath) ? "piper-tts" : Path.GetFileNameWithoutExtension(modelPath);

            Log(context, $"[PiperTTS] ✅ Audio sintetizado con éxito: '{targetFileName}' ({audioDuration:F1}s).", LogLevel.Information, newItem);
            await EmitAsync(context, newItem).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[PiperTTS] ❌ Error generando audio para {item.FileName}: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deriva el elemento de salida apuntando al .wav recién generado (o reutilizado) en disco.
    /// </summary>
    private static FileItemContext CreateGeneratedAudioItem(FileItemContext source, string generatedAudioPath)
    {
        var generated = source.DeepClone();
        generated.CurrentPath = generatedAudioPath;
        generated.PhysicalPath = generatedAudioPath;
        generated.FileSizeBytes = new FileInfo(generatedAudioPath).Length;
        generated.Metadata["AI:AudioGenerated"] = true;
        return generated;
    }

    /// <summary>La carpeta la decide la regla compartida del plugin: ver <see cref="NodeOutputDirectory"/>.</summary>
    internal static string ResolveTargetDirectory(string outputDirRaw, FileItemContext item) =>
        NodeOutputDirectory.For(outputDirRaw, item);
}
