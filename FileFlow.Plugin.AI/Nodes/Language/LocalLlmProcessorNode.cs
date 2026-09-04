using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Plugin.AI.Inference;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de Procesamiento y Razonamiento con Modelos LLM Locales (Qwen 2.5 / Phi-3.5).
/// Permite generar resúmenes ejecutivos, extracción de datos estructurados a JSON y ejecución de prompts libres.
/// </summary>
[NodeDefinition("LocalLlmProcessorNode_Name", "LanguageAI", "LocalLlmProcessorNode_Desc", PipelineRole.Analyze,
    "llm", "ia", "phi", "resumen", "extraer json", "razonamiento", "generativo", "chat")]
public class LocalLlmProcessorNode : IFlowNode, IModelLifecycleNode
{
    public event Action? ModelStatusChanged;

    public LocalLlmProcessorNode()
    {
        OnnxSessionManager.SessionStateChanged += () => ModelStatusChanged?.Invoke();
    }

    public bool IsModelLoaded
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.TextGenerationLlm);
            return modelPath != null && OnnxSessionManager.IsSessionLoaded(modelPath);
        }
    }

    public string? ModelIdentifier
    {
        get
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            return AiModelManager.GetModelDisplayName(modelChoice, AiTaskType.TextGenerationLlm);
        }
    }

    public async Task PreloadModelAsync(CancellationToken cancellationToken = default)
    {
        string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
        string? modelPath = await AiModelManager.ResolveModelPathAsync(modelChoice, AiTaskType.TextGenerationLlm, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            OnnxSessionManager.GetOrCreateSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public void UnloadModel()
    {
        string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
        string? modelPath = AiModelManager.ResolveModelPathSync(modelChoice, AiTaskType.TextGenerationLlm);
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            OnnxSessionManager.UnloadSession(modelPath);
        }
        ModelStatusChanged?.Invoke();
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("LocalLlmProcessorNode_Name", "Procesador LLM Local (Qwen 2.5 / Phi-3.5)");
    public string Category => "LanguageAI";
    public string Description => LocalizationManager.Instance.GetString("LocalLlmProcessorNode_Desc", "Genera resúmenes ejecutivos, extrae datos estructurados a JSON y procesa prompts locales con LLM in-process.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Processed", typeof(FileItemContext), PortDirection.Output, "Processed"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Model"] = "Auto",
        ["TaskType"] = "Summarize",
        ["SystemPrompt"] = "Eres un analista documental experto y conciso.",
        ["UserPrompt"] = "Resume el siguiente contenido: {Ocr:Text}",
        ["OutputFormat"] = "Markdown",
        ["SaveAsNewFile"] = false,
        ["Temperature"] = 0.2,
        ["MaxTokens"] = 1024
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Model", ParameterEditorType.Dropdown, DefaultValue: "Auto",
            Options: ["Auto", "qwen2.5-1.5b-instruct"],
            HelpText: "Modelo LLM local ('Auto' selecciona según el hardware del equipo).", DisplayOrder: 1),

        new("TaskType", ParameterEditorType.Dropdown, DefaultValue: "Summarize",
            Options: ["Summarize", "ExtractStructuredData", "TranslateAndExplain", "CustomPrompt"], DisplayOrder: 2),

        new("SystemPrompt", ParameterEditorType.MultiLineText, DefaultValue: "Eres un analista documental experto y conciso.", DisplayOrder: 3),

        new("UserPrompt", ParameterEditorType.MultiLineText, DefaultValue: "Resume el siguiente contenido: {Ocr:Text}", DisplayOrder: 4),

        new("OutputFormat", ParameterEditorType.Dropdown, DefaultValue: "Markdown",
            Options: ["Markdown", "PlainText", "JSON"], DisplayOrder: 5),

        new("SaveAsNewFile", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 6),

        new("Temperature", ParameterEditorType.Slider, DefaultValue: 0.2, Min: 0.0, Max: 1.0, Step: 0.05, DisplayOrder: 7),

        new("MaxTokens", ParameterEditorType.Number, DefaultValue: 1024, Min: 64, Max: 4096, DisplayOrder: 8)
    ];

    public async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            string modelChoice = Parameters.TryGetValue("Model", out var mVal) ? mVal?.ToString() ?? "Auto" : "Auto";
            string taskType = Parameters.TryGetValue("TaskType", out var ttVal) ? ttVal?.ToString() ?? "Summarize" : "Summarize";
            string systemPrompt = Parameters.TryGetValue("SystemPrompt", out var spVal) ? spVal?.ToString() ?? string.Empty : string.Empty;
            string rawUserPrompt = Parameters.TryGetValue("UserPrompt", out var upVal) ? upVal?.ToString() ?? string.Empty : string.Empty;
            string outputFormat = Parameters.TryGetValue("OutputFormat", out var ofVal) ? ofVal?.ToString() ?? "Markdown" : "Markdown";
            bool saveAsNewFile = Parameters.TryGetValue("SaveAsNewFile", out var sfVal) ? ParameterHelper.GetBoolean(sfVal, false) : false;
            double temperature = Parameters.TryGetValue("Temperature", out var tVal) ? ParameterHelper.GetDouble(tVal, 0.2) : 0.2;
            int maxTokens = Parameters.TryGetValue("MaxTokens", out var mtVal) ? ParameterHelper.GetInt32(mtVal, 1024) : 1024;

            // 1. Si existe archivo físico de texto y no hay metadato explícito de texto, leerlo
            string fileContent = string.Empty;
            if (!string.IsNullOrWhiteSpace(item.CurrentPath) && File.Exists(item.CurrentPath))
            {
                string ext = Path.GetExtension(item.CurrentPath).ToLowerInvariant();
                if (ext is ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".html" or ".srt" or ".log")
                {
                    fileContent = await File.ReadAllTextAsync(item.CurrentPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                }
            }

            // Evaluar variables de plantilla en el prompt del usuario ({Ocr:Text}, {Document:Text}, {Tag}, etc.)
            string evaluatedPrompt = VariableTemplateResolver.Resolve(rawUserPrompt, item);

            // Si el prompt evaluado es igual al original y tenemos contenido de archivo, anexarlo
            if (!string.IsNullOrWhiteSpace(fileContent))
            {
                if (string.IsNullOrWhiteSpace(evaluatedPrompt) || 
                    evaluatedPrompt == rawUserPrompt ||
                    (!evaluatedPrompt.Contains(fileContent) && !item.Metadata.ContainsKey("Ocr:Text")))
                {
                    evaluatedPrompt = string.IsNullOrWhiteSpace(evaluatedPrompt) || evaluatedPrompt == rawUserPrompt
                        ? fileContent
                        : $"{evaluatedPrompt.Trim()}\n\n{fileContent}";
                }
            }

            if (string.IsNullOrWhiteSpace(evaluatedPrompt))
            {
                context.Log($"[LocalLlmProcessor] Prompt vacío o sin contenido a procesar para {item.FileName}.", LogLevel.Warning, item);
                await context.EmitAsync("Error", item).ConfigureAwait(false);
                return;
            }

            string? resolvedModelPath = await AiModelManager.ResolveModelPathAsync(
                modelChoice,
                AiTaskType.TextGenerationLlm,
                context,
                item,
                cancellationToken).ConfigureAwait(false);

            context.Log($"[LocalLlmProcessor] 🧠 Procesando LLM ({taskType} | Temp {temperature:F2}) para '{item.FileName}'...", LogLevel.Information, item);

            // 2. Ejecutar inferencia LLM
            var result = await LanguageInferenceEngine.GenerateLlmAsync(
                taskType,
                systemPrompt,
                evaluatedPrompt,
                outputFormat,
                temperature,
                maxTokens,
                resolvedModelPath,
                cancellationToken).ConfigureAwait(false);

            // 3. Inyectar metadatos en el contexto del elemento
            item.Metadata["AI:LlmResponse"] = result.ResponseText;
            item.Metadata["AI:Summary"] = result.SummaryText;
            item.Metadata["AI:ExtractedDataJson"] = result.ExtractedDataJson;
            item.Metadata["AI:TokensGenerated"] = result.TokensGenerated;
            item.Metadata["AI:LlmModel"] = !string.IsNullOrEmpty(resolvedModelPath)
                ? Path.GetFileNameWithoutExtension(resolvedModelPath)
                : "Qwen 2.5 1.5B";

            // 4. Guardar como archivo nuevo si fue configurado
            if (saveAsNewFile)
            {
                string originalDir = !string.IsNullOrWhiteSpace(item.CurrentPath)
                    ? Path.GetDirectoryName(item.CurrentPath) ?? Directory.GetCurrentDirectory()
                    : Directory.GetCurrentDirectory();

                string origNameWithoutExt = !string.IsNullOrWhiteSpace(item.CurrentPath)
                    ? Path.GetFileNameWithoutExtension(item.CurrentPath)
                    : "llm_output";

                string targetExt = outputFormat.ToUpperInvariant() switch
                {
                    "JSON" => ".json",
                    "PLAINTEXT" => ".txt",
                    _ => ".md"
                };

                string targetPath = Path.Combine(originalDir, $"{origNameWithoutExt}_analisis{targetExt}");
                await File.WriteAllTextAsync(targetPath, result.ResponseText, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

                context.Log($"[LocalLlmProcessor] 💾 Resultado LLM guardado en: '{targetPath}'", LogLevel.Information, item);
            }

            await context.EmitAsync("Processed", item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"[LocalLlmProcessor] ❌ Error en procesamiento LLM: {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
