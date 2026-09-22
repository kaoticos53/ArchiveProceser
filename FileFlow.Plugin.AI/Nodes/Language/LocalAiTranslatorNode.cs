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
/// Nodo de Traducción Neuronal Multilingüe (NLLB-200 / MarianMT).
/// Traduce el contenido de archivos de texto/subtítulos o campos de metadatos de otros nodos
/// sin alterar el archivo original por defecto.
/// </summary>
[NodeDefinition("LocalAiTranslatorNode_Name", "LanguageAI", "LocalAiTranslatorNode_Desc", PipelineRole.Transform,
    "traducir", "traduccion", "idiomas", "marian", "nllb", "ingles", "español", "translator")]
public sealed class LocalAiTranslatorNode : AiFlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("LocalAiTranslatorNode_Name", "Traductor Neuronal Local (NLLB-200 / MarianMT)");
    public override string Category => "LanguageAI";
    public override string Description => LocalizationManager.Instance.GetString("LocalAiTranslatorNode_Desc", "Traduce documentos, subtítulos y metadatos con modelos neuronales locales NLLB-200 y MarianMT.");
    public override AiTaskType TaskType => AiTaskType.TextTranslation;

    public LocalAiTranslatorNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Translated", typeof(FileItemContext), PortDirection.Output, "Translated"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Model"] = "Auto";
        Parameters["SourceLanguage"] = "AutoDetect";
        Parameters["TargetLanguage"] = "Spanish";
        Parameters["InputSource"] = "FileContent";
        Parameters["MetadataKeyName"] = "Ocr:Text";
        Parameters["OutputMode"] = "InjectMetadata";
        Parameters["TargetFileNamePattern"] = "{FileNameWithoutExt}_{TargetLang}{Ext}";
        Parameters["TranslateSrtTimestamps"] = true;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "nllb-200-600m", "marian-es-en", "marian-en-es"],
            HelpText: "Modelo neuronal de traducción ('Auto' selecciona según el hardware y los idiomas).", DisplayOrder: 1),

        new("SourceLanguage", ParameterEditorType.Dropdown, DefaultValue: "AutoDetect",
            Options: ["AutoDetect", "Spanish", "English", "French", "German", "Italian", "Portuguese", "Chinese", "Japanese", "Russian"], DisplayOrder: 2),

        new("TargetLanguage", ParameterEditorType.Dropdown, DefaultValue: "Spanish",
            Options: ["Spanish", "English", "French", "German", "Italian", "Portuguese", "Chinese", "Japanese", "Russian"], DisplayOrder: 3),

        new("InputSource", ParameterEditorType.Dropdown, DefaultValue: "FileContent",
            Options: ["FileContent", "MetadataKey"], DisplayOrder: 4),

        new("MetadataKeyName", ParameterEditorType.Text, DefaultValue: "Ocr:Text", DisplayOrder: 5),

        new("OutputMode", ParameterEditorType.Dropdown, DefaultValue: "InjectMetadata",
            Options: ["InjectMetadata", "CreateNewFile", "Both"], DisplayOrder: 6),

        new("TargetFileNamePattern", ParameterEditorType.Text, DefaultValue: "{FileNameWithoutExt}_{TargetLang}{Ext}", DisplayOrder: 7),

        new("TranslateSrtTimestamps", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 8)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            string sourceLang = GetParameter("SourceLanguage", "AutoDetect");
            string targetLang = GetParameter("TargetLanguage", "Spanish");
            string inputSource = GetParameter("InputSource", "FileContent");
            string metadataKey = GetParameter("MetadataKeyName", "Ocr:Text");
            string outputMode = GetParameter("OutputMode", "InjectMetadata");
            string fileNamePattern = GetParameter("TargetFileNamePattern", "{FileNameWithoutExt}_{TargetLang}{Ext}");
            bool translateSrtTimestamps = GetParameter("TranslateSrtTimestamps", true);

            string textToTranslate = string.Empty;
            bool isSrt = false;

            if (string.Equals(inputSource, "MetadataKey", StringComparison.OrdinalIgnoreCase))
            {
                if (item.Metadata.TryGetValue(metadataKey, out var metaVal) && metaVal != null)
                {
                    textToTranslate = metaVal.ToString() ?? string.Empty;
                }
                else
                {
                    Log(context, $"[LocalAiTranslator] Metadato '{metadataKey}' no encontrado en el elemento.", LogLevel.Warning, item);
                    await EmitAsync(context, item, "Error").ConfigureAwait(false);
                    return;
                }
            }
            else
            {
                // FileContent
                if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
                {
                    Log(context, $"[LocalAiTranslator] Archivo no encontrado: '{item.CurrentPath}'", LogLevel.Error, item);
                    await EmitAsync(context, item, "Error").ConfigureAwait(false);
                    return;
                }

                string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
                isSrt = ext == ".srt";

                textToTranslate = await File.ReadAllTextAsync(item.CurrentPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(textToTranslate))
            {
                Log(context, $"[LocalAiTranslator] Texto vacío a traducir para {item.FileName}.", LogLevel.Warning, item);
                await EmitAsync(context, item, "Error").ConfigureAwait(false);
                return;
            }

            string? resolvedModelPath = await ResolveModelPathAsync(context, item, cancellationToken).ConfigureAwait(false);

            Log(context, $"[LocalAiTranslator] 🌐 Traduciendo ({sourceLang} ➔ {targetLang}) para '{item.FileName}'...", LogLevel.Information, item);

            string translatedText = await LanguageInferenceEngine.TranslateAsync(
                textToTranslate,
                sourceLang,
                targetLang,
                isSrt && translateSrtTimestamps,
                resolvedModelPath,
                cancellationToken).ConfigureAwait(false);

            // Inyectar metadatos en el contexto del elemento
            string detectedOrSource = LanguageInferenceEngine.NormalizeLanguageCode(sourceLang, textToTranslate);
            string targetCode = LanguageInferenceEngine.NormalizeLanguageCode(targetLang);

            item.Metadata["AI:SourceLanguage"] = detectedOrSource;
            item.Metadata["AI:TargetLanguage"] = targetCode;
            item.Metadata["AI:TranslatedText"] = translatedText;
            item.Metadata["AI:TranslationModel"] = !string.IsNullOrEmpty(resolvedModelPath)
                ? Path.GetFileNameWithoutExtension(resolvedModelPath)
                : "NLLB-200 / MarianMT";

            // Guardar archivo nuevo si se solicita
            if (string.Equals(outputMode, "CreateNewFile", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(outputMode, "Both", StringComparison.OrdinalIgnoreCase))
            {
                string originalDir = !string.IsNullOrWhiteSpace(item.CurrentPath)
                    ? Path.GetDirectoryName(item.CurrentPath) ?? Directory.GetCurrentDirectory()
                    : Directory.GetCurrentDirectory();

                string origNameWithoutExt = !string.IsNullOrWhiteSpace(item.CurrentPath)
                    ? Path.GetFileNameWithoutExtension(item.CurrentPath)
                    : "documento";

                string origExt = !string.IsNullOrWhiteSpace(item.CurrentPath)
                    ? Path.GetExtension(item.CurrentPath)
                    : ".txt";

                string resolvedFileName = fileNamePattern
                    .Replace("{FileNameWithoutExt}", origNameWithoutExt, StringComparison.OrdinalIgnoreCase)
                    .Replace("{TargetLang}", targetCode, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Ext}", origExt, StringComparison.OrdinalIgnoreCase);

                string targetPath = Path.Combine(originalDir, resolvedFileName);

                await File.WriteAllTextAsync(targetPath, translatedText, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                Log(context, $"[LocalAiTranslator] 💾 Archivo traducido guardado en: '{targetPath}'", LogLevel.Information, item);

                if (string.Equals(outputMode, "CreateNewFile", StringComparison.OrdinalIgnoreCase))
                {
                    item.CurrentPath = targetPath;
                    item.FileSizeBytes = new FileInfo(targetPath).Length;
                }
            }

            await EmitAsync(context, item, "Translated").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[LocalAiTranslator] ❌ Error en traducción: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }
}
