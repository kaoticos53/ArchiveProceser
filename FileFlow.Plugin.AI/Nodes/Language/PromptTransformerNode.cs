using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo para la transformación dinámica de prompts en tiempo de ejecución.
/// Resuelve plantillas con metadatos del elemento ({Tag}, {Metadata:Key}), traduce conceptos al idioma
/// objetivo (ej. inglés para Grounding DINO / YOLO) y expande sinónimos visuales opcionalmente.
/// </summary>
[NodeDefinition("PromptTransformerNode_Name", "LanguageAI", "PromptTransformerNode_Desc", PipelineRole.Transform,
    "prompt", "enriquecer", "estilo", "transformar prompt", "asistente", "ia", "plantilla")]
public sealed class PromptTransformerNode : AiFlowNodeBase
{
    /// <summary>Par MarianMT español→inglés que consumen los motores de prompts visuales (Grounding DINO / YOLO).</summary>
    private const string TranslatorModelId = "marian-es-en";

    public override string Name => LocalizationManager.Instance.GetString("PromptTransformerNode_Name", "Transformador Dinámico de Prompts");
    public override string Category => "LanguageAI";
    public override string Description => LocalizationManager.Instance.GetString("PromptTransformerNode_Desc", "Evalúa plantillas dinámicas con variables de metadatos, traduce a inglés y expande sinónimos visuales.");
    public override AiTaskType TaskType => AiTaskType.TextTranslation;

    /// <summary>
    /// El idioma destino del prompt está fijado por diseño, así que el nodo no declara un parámetro 'Model'.
    /// </summary>
    protected override string DefaultModelSelection => TranslatorModelId;

    public PromptTransformerNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Transformed", typeof(FileItemContext), PortDirection.Output, "Transformed"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["PromptTemplate"] = "{AI:Category}, gafas de sol, {UserTag}, coche rojo";
        Parameters["TargetLanguage"] = "English";
        Parameters["ExpandSynonyms"] = false;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("PromptTemplate", ParameterEditorType.MultiLineText, DefaultValue: "{AI:Category}, gafas de sol, {UserTag}, coche rojo", DisplayOrder: 1),
        new("TargetLanguage", ParameterEditorType.Dropdown, DefaultValue: "English", Options: ["English", "Spanish", "French", "German"], DisplayOrder: 2),
        new("ExpandSynonyms", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 3)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            string template = GetParameter("PromptTemplate", string.Empty);
            string targetLang = GetParameter("TargetLanguage", "English");
            bool expandSynonyms = GetParameter("ExpandSynonyms", false);

            if (string.IsNullOrWhiteSpace(template))
            {
                Log(context, $"[PromptTransformer] Plantilla de prompt vacía para {item.FileName}.", LogLevel.Warning, item);
                await EmitAsync(context, item, "Error").ConfigureAwait(false);
                return;
            }

            var (evaluated, translated) = await LanguageInferenceEngine.TransformPromptAsync(
                template,
                targetLang,
                expandSynonyms,
                item,
                cancellationToken).ConfigureAwait(false);

            item.Metadata["AI:EvaluatedPrompt"] = evaluated;
            item.Metadata["AI:TranslatedPrompt"] = translated;

            Log(context, $"[PromptTransformer] ✨ Prompt transformado: '{evaluated}' ➔ '{translated}'", LogLevel.Information, item);

            await EmitAsync(context, item, "Transformed").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"[PromptTransformer] ❌ Error evaluando prompt: {ex.Message}", LogLevel.Error, item);
            await EmitAsync(context, item, "Error").ConfigureAwait(false);
        }
    }
}
