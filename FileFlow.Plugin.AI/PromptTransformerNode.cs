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
public class PromptTransformerNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("PromptTransformerNode_Name", "Transformador Dinámico de Prompts");
    public string Category => "LanguageAI";
    public string Description => LocalizationManager.Instance.GetString("PromptTransformerNode_Desc", "Evalúa plantillas dinámicas con variables de metadatos, traduce a inglés y expande sinónimos visuales.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Transformed", typeof(FileItemContext), PortDirection.Output, "Transformed"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PromptTemplate"] = "{AI:Category}, gafas de sol, {UserTag}, coche rojo",
        ["TargetLanguage"] = "English",
        ["ExpandSynonyms"] = false
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("PromptTemplate", ParameterEditorType.MultiLineText, DefaultValue: "{AI:Category}, gafas de sol, {UserTag}, coche rojo", DisplayOrder: 1),
        new("TargetLanguage", ParameterEditorType.Dropdown, DefaultValue: "English", Options: ["English", "Spanish", "French", "German"], DisplayOrder: 2),
        new("ExpandSynonyms", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 3)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            string template = Parameters.TryGetValue("PromptTemplate", out var ptVal) ? ptVal?.ToString() ?? string.Empty : string.Empty;
            string targetLang = Parameters.TryGetValue("TargetLanguage", out var tlVal) ? tlVal?.ToString() ?? "English" : "English";
            bool expandSynonyms = Parameters.TryGetValue("ExpandSynonyms", out var esVal) ? ParameterHelper.GetBoolean(esVal, false) : false;

            if (string.IsNullOrWhiteSpace(template))
            {
                context.Log($"[PromptTransformer] Plantilla de prompt vacía para {item.FileName}.", LogLevel.Warning, item);
                await context.EmitAsync("Error", item).ConfigureAwait(false);
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

            context.Log($"[PromptTransformer] ✨ Prompt transformado: '{evaluated}' ➔ '{translated}'", LogLevel.Information, item);

            await context.EmitAsync("Transformed", item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[PromptTransformer] ❌ Error evaluando prompt: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
