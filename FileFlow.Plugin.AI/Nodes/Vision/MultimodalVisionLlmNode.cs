using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.TemplateEngine;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Nodo de Inteligencia Artificial Multimodal (Vision-Language Model - VLM).
/// Conecta con servidores locales (LM Studio, Ollama), remotos compatibles con la API de OpenAI,
/// o ejecuta el motor interno In-Process de FileFlow Studio sin dependencias externas.
/// Permite extracción directa de facturas y recibos a JSON, traducción visual, OCR con resumen y auditoría de calidad.
/// </summary>
[NodeDefinition("MultimodalVisionLlmNode_Name", "ImageVision", "MultimodalVisionLlmNode_Desc", PipelineRole.Analyze,
    "vlm", "multimodal", "qwen", "qwen2.5-vl", "vision", "llm", "lm studio", "ollama", "in-process", "factura", "recibo", "ocr", "traduccion")]
public sealed class MultimodalVisionLlmNode : FlowNodeBase, IModelLifecycleNode
{
    public event Action? ModelStatusChanged;

    public HttpClient? CustomHttpClient { get; set; }

    public override string Name => LocalizationManager.Instance.GetString("MultimodalVisionLlmNode_Name", "IA Multimodal VLM (Qwen2.5-VL)");
    public override string Category => "ImageVision";
    public override string Description => LocalizationManager.Instance.GetString("MultimodalVisionLlmNode_Desc", "Procesa imágenes y documentos con modelos de Visión-Lenguaje (Qwen2.5-VL) locales vía LM Studio, Ollama o motor in-process.");

    public bool IsModelLoaded
    {
        get
        {
            string provider = Parameters.TryGetValue("Provider", out var pVal) ? pVal?.ToString() ?? string.Empty : string.Empty;
            if (provider.Contains("In-Process", StringComparison.OrdinalIgnoreCase) || provider.Contains("Internal", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return true;
        }
    }

    public string? ModelIdentifier
    {
        get
        {
            string provider = Parameters.TryGetValue("Provider", out var pVal) ? pVal?.ToString() ?? VlmAdapterFactory.ProviderLmStudio : VlmAdapterFactory.ProviderLmStudio;
            string model = Parameters.TryGetValue("ModelName", out var mVal) ? mVal?.ToString() ?? "qwen2.5-vl-7b-instruct" : "qwen2.5-vl-7b-instruct";
            if (provider.Contains("In-Process", StringComparison.OrdinalIgnoreCase) || provider.Contains("Internal", StringComparison.OrdinalIgnoreCase))
            {
                return "FileFlow In-Process VLM";
            }
            return $"{provider} ({model})";
        }
    }

    public Task PreloadModelAsync(CancellationToken cancellationToken = default)
    {
        ModelStatusChanged?.Invoke();
        return Task.CompletedTask;
    }

    public void UnloadModel()
    {
        ModelStatusChanged?.Invoke();
    }

    public MultimodalVisionLlmNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Structured", typeof(FileItemContext), PortDirection.Output, "Structured"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Provider"] = VlmAdapterFactory.ProviderLmStudio;
        Parameters["EndpointUrl"] = "http://localhost:1234/v1";
        Parameters["ModelName"] = "qwen2.5-vl-7b-instruct";
        Parameters["ApiKey"] = "lm-studio";
        Parameters["TaskPreset"] = "ExtractInvoiceReceiptJson";
        Parameters["SystemPrompt"] = string.Empty;
        Parameters["UserPrompt"] = string.Empty;
        Parameters["TargetLanguage"] = "Español";
        Parameters["ForceJsonOutput"] = false;
        Parameters["MaxImageDimension"] = 1536;
        Parameters["Temperature"] = 0.1;
        Parameters["MaxTokens"] = 2048;
        Parameters["SaveAsNewFile"] = false;
        Parameters["TimeoutSeconds"] = 120;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Provider", ParameterEditorType.Dropdown, DefaultValue: VlmAdapterFactory.ProviderLmStudio,
            Options: VlmAdapterFactory.AvailableProviders,
            HelpText: "Servidor local (LM Studio, Ollama) o motor interno In-Process para ejecutar el modelo de visión.", DisplayOrder: 1),

        new("EndpointUrl", ParameterEditorType.Text, DefaultValue: "http://localhost:1234/v1",
            HelpText: "URL base de la API compatible con OpenAI (/v1/chat/completions). No requerida para In-Process.", DisplayOrder: 2),

        new("ModelName", ParameterEditorType.Text, DefaultValue: "qwen2.5-vl-7b-instruct",
            HelpText: "Identificador del modelo cargado en LM Studio u Ollama (ej. qwen2.5-vl-7b-instruct, qwen2.5-vl-3b, default).", DisplayOrder: 3),

        new("ApiKey", ParameterEditorType.Text, DefaultValue: "lm-studio",
            HelpText: "Clave de API para el endpoint (opcional para servidores locales).", DisplayOrder: 4),

        new("TaskPreset", ParameterEditorType.Dropdown, DefaultValue: "ExtractInvoiceReceiptJson",
            Options: [
                "ExtractInvoiceReceiptJson",
                "DocumentOcrAndSummary",
                "TranslateDocument",
                "ClassifyAndTag",
                "QualityInspection",
                "CustomPrompt"
            ],
            HelpText: "Plantilla predefinida para la tarea visual que optimiza los prompts automáticamente.", DisplayOrder: 5),

        new("SystemPrompt", ParameterEditorType.MultiLineText, DefaultValue: string.Empty,
            HelpText: "Instrucciones de sistema para el modelo VLM. Si se deja en blanco, usa la plantilla del preset.", DisplayOrder: 6),

        new("UserPrompt", ParameterEditorType.MultiLineText, DefaultValue: string.Empty,
            HelpText: "Pregunta o instrucción de usuario. Admite variables de plantilla {FileName}, {Date}, {Tag}, etc.", DisplayOrder: 7),

        new("TargetLanguage", ParameterEditorType.Text, DefaultValue: "Español",
            HelpText: "Idioma de destino para traducciones o resúmenes.", DisplayOrder: 8),

        new("ForceJsonOutput", ParameterEditorType.Toggle, DefaultValue: false,
            HelpText: "Obliga al modelo a responder estrictamente en formato JSON válido.", DisplayOrder: 9),

        new("MaxImageDimension", ParameterEditorType.Number, DefaultValue: 1536, Min: 256, Max: 4096, Step: 128,
            HelpText: "Dimensión máxima (ancho o alto) a la que se escala la imagen para optimizar VRAM y ancho de banda.", DisplayOrder: 10),

        new("Temperature", ParameterEditorType.Slider, DefaultValue: 0.1, Min: 0.0, Max: 1.0, Step: 0.05,
            HelpText: "Temperatura de muestreo (0.0 para máxima precisión fáctica; valores más altos para creatividad).", DisplayOrder: 11),

        new("MaxTokens", ParameterEditorType.Number, DefaultValue: 2048, Min: 64, Max: 8192, Step: 256,
            HelpText: "Límite máximo de tokens generados en la respuesta.", DisplayOrder: 12),

        new("SaveAsNewFile", ParameterEditorType.Toggle, DefaultValue: false,
            HelpText: "Guarda la salida generada como un nuevo archivo (.json o .md) junto a la imagen original.", DisplayOrder: 13),

        new("TimeoutSeconds", ParameterEditorType.Number, DefaultValue: 120, Min: 10, Max: 600, Step: 10,
            HelpText: "Tiempo máximo de espera en segundos para la respuesta del modelo.", DisplayOrder: 14)
    ];

    public override async Task ExecuteAsync(string inputPortName, FileItemContext item, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();

        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !await storage.FileExistsAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
        {
            Log(context, $"El archivo de entrada no existe o no es accesible: '{item.CurrentPath}'.", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
            return;
        }

        try
        {
            // 1. Leer parámetros
            string provider = Parameters.TryGetValue("Provider", out var pVal) ? pVal?.ToString() ?? VlmAdapterFactory.ProviderLmStudio : VlmAdapterFactory.ProviderLmStudio;
            string endpointUrl = Parameters.TryGetValue("EndpointUrl", out var eVal) ? eVal?.ToString() ?? "http://localhost:1234/v1" : "http://localhost:1234/v1";
            string modelName = Parameters.TryGetValue("ModelName", out var mVal) ? mVal?.ToString() ?? "qwen2.5-vl-7b-instruct" : "qwen2.5-vl-7b-instruct";
            string apiKey = Parameters.TryGetValue("ApiKey", out var kVal) ? kVal?.ToString() ?? "lm-studio" : "lm-studio";
            string taskPresetStr = Parameters.TryGetValue("TaskPreset", out var tpVal) ? tpVal?.ToString() ?? "ExtractInvoiceReceiptJson" : "ExtractInvoiceReceiptJson";
            string customSystemPrompt = Parameters.TryGetValue("SystemPrompt", out var spVal) ? spVal?.ToString() ?? string.Empty : string.Empty;
            string rawUserPrompt = Parameters.TryGetValue("UserPrompt", out var upVal) ? upVal?.ToString() ?? string.Empty : string.Empty;
            string targetLanguage = Parameters.TryGetValue("TargetLanguage", out var tlVal) ? tlVal?.ToString() ?? "Español" : "Español";
            bool forceJsonOutput = Parameters.TryGetValue("ForceJsonOutput", out var fjVal) ? ParameterHelper.GetBoolean(fjVal, false) : false;
            int maxImageDimension = Parameters.TryGetValue("MaxImageDimension", out var midVal) ? ParameterHelper.GetInt32(midVal, 1536) : 1536;
            double temperature = Parameters.TryGetValue("Temperature", out var tVal) ? ParameterHelper.GetDouble(tVal, 0.1) : 0.1;
            int maxTokens = Parameters.TryGetValue("MaxTokens", out var mtVal) ? ParameterHelper.GetInt32(mtVal, 2048) : 2048;
            bool saveAsNewFile = Parameters.TryGetValue("SaveAsNewFile", out var sfVal) ? ParameterHelper.GetBoolean(sfVal, false) : false;
            int timeoutSeconds = Parameters.TryGetValue("TimeoutSeconds", out var tsVal) ? ParameterHelper.GetInt32(tsVal, 120) : 120;

            // Ajustar endpoint por defecto según proveedor si no se especificó uno personalizado
            if (provider.Contains("Ollama", StringComparison.OrdinalIgnoreCase) && endpointUrl == "http://localhost:1234/v1")
            {
                endpointUrl = "http://localhost:11434/v1";
            }

            if (!Enum.TryParse<VlmTaskPreset>(taskPresetStr, true, out var preset))
            {
                preset = VlmTaskPreset.ExtractInvoiceReceiptJson;
            }

            // Forzar JSON si el preset es de extracción a JSON
            if (preset == VlmTaskPreset.ExtractInvoiceReceiptJson)
            {
                forceJsonOutput = true;
            }

            // 2. Resolver prompts (preset por defecto si está vacío)
            var (defaultSystemPrompt, defaultUserPrompt) = MultimodalVlmClientEngine.GetPresetPrompts(preset, targetLanguage);
            string finalSystemPrompt = !string.IsNullOrWhiteSpace(customSystemPrompt) ? customSystemPrompt : defaultSystemPrompt;

            string baseUserPrompt = !string.IsNullOrWhiteSpace(rawUserPrompt) ? rawUserPrompt : defaultUserPrompt;
            string evaluatedUserPrompt = VariableTemplateResolver.Resolve(baseUserPrompt, item);

            // 3. Resolver adaptador correspondiente
            var adapter = VlmAdapterFactory.Create(provider);

            string base64Uri = string.Empty;
            if (adapter is not InProcessVlmAdapter)
            {
                Log(context, $"👁️ Codificando imagen para inferencia HTTP: '{item.FileName}'...", LogLevel.Debug, item);
                await using (var stream = await storage.OpenReadAsync(item.CurrentPath, cancellationToken).ConfigureAwait(false))
                {
                    using var image = await Image.LoadAsync<Rgb24>(stream, cancellationToken).ConfigureAwait(false);
                    base64Uri = MultimodalVlmClientEngine.PrepareImageAsBase64Jpeg(image, maxImageDimension);
                }
            }

            Log(context, $"🧠 Ejecutando VLM [{adapter.ProviderName}] (Preset: '{preset}')...", LogLevel.Information, item);

            // 4. Inferencia con el adaptador seleccionado
            var request = new VlmExecutionRequest(
                ImagePath: item.CurrentPath,
                Base64ImageDataUrl: base64Uri,
                SystemPrompt: finalSystemPrompt,
                UserPrompt: evaluatedUserPrompt,
                TaskPreset: preset,
                TargetLanguage: targetLanguage,
                ForceJsonOutput: forceJsonOutput,
                MaxTokens: maxTokens,
                Temperature: temperature,
                MaxImageDimension: maxImageDimension,
                EndpointUrl: endpointUrl,
                ModelName: modelName,
                ApiKey: apiKey,
                Timeout: TimeSpan.FromSeconds(timeoutSeconds),
                CustomHttpClient: CustomHttpClient,
                Context: context,
                Item: item
            );

            var result = await adapter.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

            // 5. Inyectar metadatos en el contexto del elemento
            item.Metadata["AI:VlmResponse"] = result.RawText;
            item.Metadata["AI:VlmModel"] = result.ModelUsed;
            item.Metadata["AI:VlmTokens"] = result.TotalTokens;
            item.Metadata["AI:VlmDurationMs"] = result.DurationMs;
            item.Metadata["AI:VlmProvider"] = adapter.ProviderName;

            if (!string.IsNullOrWhiteSpace(result.ExtractedJson))
            {
                item.Metadata["AI:VlmJson"] = result.ExtractedJson;
            }

            if (!string.IsNullOrWhiteSpace(result.DetectedCategory))
            {
                item.Metadata["AI:VlmCategory"] = result.DetectedCategory;
            }

            Log(context, $"✨ Inferencia VLM ({adapter.ProviderName}) completada en {result.DurationMs} ms ({result.TotalTokens} tokens).", LogLevel.Information, item);

            // 6. Guardar archivo si fue configurado
            if (saveAsNewFile)
            {
                string originalDir = !string.IsNullOrWhiteSpace(item.CurrentPath)
                    ? Path.GetDirectoryName(item.CurrentPath) ?? Directory.GetCurrentDirectory()
                    : Directory.GetCurrentDirectory();

                string origNameWithoutExt = !string.IsNullOrWhiteSpace(item.CurrentPath)
                    ? Path.GetFileNameWithoutExtension(item.CurrentPath)
                    : "vlm_output";

                bool hasJson = !string.IsNullOrWhiteSpace(result.ExtractedJson);
                string targetExt = hasJson ? ".json" : ".md";
                string contentToWrite = hasJson ? result.ExtractedJson! : result.RawText;

                string targetPath = Path.Combine(originalDir, $"{origNameWithoutExt}_vlm{targetExt}");
                await storage.WriteAllTextAsync(targetPath, contentToWrite, ct: cancellationToken).ConfigureAwait(false);

                Log(context, $"💾 Archivo generado por VLM guardado en: '{targetPath}'.", LogLevel.Information, item);
            }

            // 7. Enrutamiento a puertos de salida
            if (!string.IsNullOrWhiteSpace(result.ExtractedJson))
            {
                await context.EmitAsync("Structured", item).ConfigureAwait(false);
            }

            await context.EmitAsync("Out", item).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log(context, $"❌ Error durante la inferencia VLM para '{item.FileName}': {ex.Message}", LogLevel.Error, item);
            await context.EmitAsync("Error", item).ConfigureAwait(false);
        }
    }
}
