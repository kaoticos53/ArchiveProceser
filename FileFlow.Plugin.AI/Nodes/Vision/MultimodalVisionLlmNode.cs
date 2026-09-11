using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FileFlow.Plugin.AI.Management;
using FileFlow.Plugin.AI.UI;
using FileFlow.Plugin.AI.ViewModels;
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
[NodeDefinition("MultimodalVisionLlmNode_Name", "LanguageAI", "MultimodalVisionLlmNode_Desc", PipelineRole.Analyze,
    "vlm", "multimodal", "qwen", "qwen2.5-vl", "vision", "llm", "lm studio", "ollama", "in-process", "factura", "recibo", "ocr", "traduccion")]
public sealed class MultimodalVisionLlmNode : FlowNodeBase, IModelLifecycleNode, INodeCustomActionProvider
{
    public event Action? ModelStatusChanged;

    public HttpClient? CustomHttpClient { get; set; }

    public override string Name => LocalizationManager.Instance.GetString("MultimodalVisionLlmNode_Name", "IA Multimodal VLM (Qwen2.5-VL)");
    public override string Category => "LanguageAI";
    public override string Description => LocalizationManager.Instance.GetString("MultimodalVisionLlmNode_Desc", "Procesa imágenes y documentos con modelos de Visión-Lenguaje (Qwen2.5-VL) locales vía LM Studio, Ollama o motor in-process.");

    public override int MaxConcurrency
    {
        get
        {
            if (Parameters.TryGetValue("MaxConcurrency", out var mcVal) && mcVal is not null)
            {
                int custom = ParameterHelper.GetInt32(mcVal, 0);
                if (custom > 0) return custom;
            }
            string provider = Parameters.TryGetValue("Provider", out var pVal) ? pVal?.ToString() ?? string.Empty : string.Empty;
            var profile = VlmConfigurationStorageService.Instance.GetProviderProfile(provider);
            return Math.Max(1, profile.ConcurrencyLimit);
        }
    }

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

    public override IReadOnlyList<NodeActionDescriptor> CustomActions =>
    [
        new("OpenVlmConfig", LocalizationManager.Instance.GetString("MultimodalVisionLlmNode_ActionConfig", "⚙️ Configurar Proveedores y Plantillas..."), "⚙️", "Abrir la ventana de configuración detallada para gestionar endpoints de proveedores de IA y editar plantillas de tareas")
    ];

    public void ExecuteCustomAction(string actionId, object? context = null)
    {
        if (string.Equals(actionId, "OpenVlmConfig", StringComparison.OrdinalIgnoreCase))
        {
            var vm = new MultimodalVlmConfigViewModel(this);
            var window = new MultimodalVlmConfigWindow(vm);
            if (context is Window ownerWindow)
            {
                window.Owner = ownerWindow;
            }
            else if (Application.Current?.MainWindow != null)
            {
                window.Owner = Application.Current.MainWindow;
            }
            window.ShowDialog();
        }
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

        Parameters["Provider"] = "LM Studio (Local Server)";
        Parameters["EndpointUrl"] = "http://localhost:1234/v1";
        Parameters["ModelName"] = "qwen2.5-vl-7b-instruct";
        Parameters["ApiKey"] = "lm-studio";
        Parameters["TaskPreset"] = "Extracción de Facturas y Recibos (JSON)";
        Parameters["SystemPrompt"] = string.Empty;
        Parameters["UserPrompt"] = string.Empty;
        Parameters["AdditionalPrompt"] = string.Empty;
        Parameters["TargetLanguage"] = "Español";
        Parameters["ForceJsonOutput"] = false;
        Parameters["MaxImageDimension"] = 1024;
        Parameters["Temperature"] = 0.1;
        Parameters["MaxTokens"] = 2048;
        Parameters["SaveAsNewFile"] = false;
        Parameters["TimeoutSeconds"] = 120;
        Parameters["MaxConcurrency"] = 1;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors
    {
        get
        {
            var providers = VlmConfigurationStorageService.Instance.LoadProviders();
            var providerOptions = providers.Select(p => p.DisplayName).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToArray();
            if (providerOptions.Length == 0)
            {
                providerOptions = VlmAdapterFactory.AvailableProviders;
            }

            var templates = VlmConfigurationStorageService.Instance.LoadTemplates();
            var presetOptions = templates.Select(t => t.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToArray();
            if (presetOptions.Length == 0)
            {
                presetOptions =
                [
                    "Extracción de Facturas y Recibos (JSON)",
                    "OCR y Resumen Ejecutivo",
                    "Traducción Visual de Documentos",
                    "Clasificación y Etiquetado Visual",
                    "Inspección de Calidad Formal",
                    "Prompt Libre / Personalizado"
                ];
            }

            string defaultPreset = presetOptions.FirstOrDefault() ?? "Extracción de Facturas y Recibos (JSON)";

            return
            [
                new("Provider", ParameterEditorType.Dropdown, DefaultValue: providerOptions.FirstOrDefault() ?? "LM Studio (Local Server)",
                    Options: providerOptions,
                    HelpText: "Servidor local (LM Studio, Ollama), motor interno In-Process o proveedor personalizado para ejecutar el modelo de visión.", DisplayOrder: 1),

                new("TaskPreset", ParameterEditorType.Dropdown, DefaultValue: defaultPreset,
                    Options: presetOptions,
                    HelpText: "Plantilla de tarea visual que optimiza automáticamente el rol y formato de respuesta.", DisplayOrder: 2),

                new("TargetLanguage", ParameterEditorType.EditableDropdown, DefaultValue: "Español",
                    Options: ["Español", "Inglés"],
                    HelpText: "Idioma de destino para transcripciones, resúmenes o traducciones.", DisplayOrder: 3),

                new("AdditionalPrompt", ParameterEditorType.MultiLineText, DefaultValue: string.Empty,
                    HelpText: "Instrucciones o notas adicionales para este nodo particular (admite variables de plantilla {FileName}, {Date}, etc.).", DisplayOrder: 4),

                new("MaxConcurrency", ParameterEditorType.Number, DefaultValue: 1,
                    Min: 1, Max: 32,
                    HelpText: "Número máximo de inferencias simultáneas hacia el servidor VLM (LM Studio, Ollama, etc.). Ajusta este valor según los slots y la GPU de tu servidor.", DisplayOrder: 5)
            ];
        }
    }

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
            // 1. Leer parámetros y sincronizar con perfil de proveedor si es necesario
            string provider = Parameters.TryGetValue("Provider", out var pVal) ? pVal?.ToString() ?? VlmAdapterFactory.ProviderLmStudio : VlmAdapterFactory.ProviderLmStudio;
            var providerProfile = VlmConfigurationStorageService.Instance.GetProviderProfile(provider);

            string endpointUrl = Parameters.TryGetValue("EndpointUrl", out var eVal) && !string.IsNullOrWhiteSpace(eVal?.ToString())
                ? eVal.ToString()!
                : providerProfile.EndpointUrl;

            string modelName = Parameters.TryGetValue("ModelName", out var mVal) && !string.IsNullOrWhiteSpace(mVal?.ToString())
                ? mVal.ToString()!
                : providerProfile.ModelName;

            string apiKey = Parameters.TryGetValue("ApiKey", out var kVal) && !string.IsNullOrWhiteSpace(kVal?.ToString())
                ? kVal.ToString()!
                : providerProfile.ApiKey;

            string taskPresetStr = Parameters.TryGetValue("TaskPreset", out var tpVal) ? tpVal?.ToString() ?? "ExtractInvoiceReceiptJson" : "ExtractInvoiceReceiptJson";
            string customSystemPrompt = Parameters.TryGetValue("SystemPrompt", out var spVal) ? spVal?.ToString() ?? string.Empty : string.Empty;
            string rawUserPrompt = Parameters.TryGetValue("UserPrompt", out var upVal) ? upVal?.ToString() ?? string.Empty : string.Empty;
            string additionalPrompt = Parameters.TryGetValue("AdditionalPrompt", out var apVal) ? apVal?.ToString() ?? string.Empty : string.Empty;
            string targetLanguage = Parameters.TryGetValue("TargetLanguage", out var tlVal) ? tlVal?.ToString() ?? "Español" : "Español";

            bool forceJsonOutput = Parameters.TryGetValue("ForceJsonOutput", out var fjVal)
                ? ParameterHelper.GetBoolean(fjVal, false)
                : false;

            int maxImageDimension = Parameters.TryGetValue("MaxImageDimension", out var midVal)
                ? ParameterHelper.GetInt32(midVal, providerProfile.MaxImageDimension)
                : providerProfile.MaxImageDimension;

            double temperature = Parameters.TryGetValue("Temperature", out var tVal)
                ? ParameterHelper.GetDouble(tVal, providerProfile.Temperature)
                : providerProfile.Temperature;

            int maxTokens = Parameters.TryGetValue("MaxTokens", out var mtVal)
                ? ParameterHelper.GetInt32(mtVal, providerProfile.MaxTokens)
                : providerProfile.MaxTokens;

            bool saveAsNewFile = Parameters.TryGetValue("SaveAsNewFile", out var sfVal)
                ? ParameterHelper.GetBoolean(sfVal, false)
                : false;

            int timeoutSeconds = Parameters.TryGetValue("TimeoutSeconds", out var tsVal)
                ? ParameterHelper.GetInt32(tsVal, providerProfile.TimeoutSeconds)
                : providerProfile.TimeoutSeconds;

            // Ajustar endpoint por defecto según proveedor si no se especificó uno personalizado
            if (provider.Contains("Ollama", StringComparison.OrdinalIgnoreCase) && endpointUrl == "http://localhost:1234/v1")
            {
                endpointUrl = "http://localhost:11434/v1";
            }

            // 2. Resolver plantilla y prompts desde el catálogo o preset integrado
            var storedTemplate = VlmConfigurationStorageService.Instance.LoadTemplates()
                .FirstOrDefault(t => string.Equals(t.Name, taskPresetStr, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(t.Id, taskPresetStr, StringComparison.OrdinalIgnoreCase));

            VlmTaskPreset preset = VlmTaskPreset.ExtractInvoiceReceiptJson;
            if (storedTemplate != null)
            {
                if (Enum.TryParse<VlmTaskPreset>(storedTemplate.Id, true, out var matchedPreset))
                {
                    preset = matchedPreset;
                }
                else
                {
                    preset = VlmTaskPreset.CustomPrompt;
                }
            }
            else if (Enum.TryParse<VlmTaskPreset>(taskPresetStr, true, out var parsedPreset))
            {
                preset = parsedPreset;
            }

            string templateSysPrompt;
            string templateUsrPrompt;

            if (storedTemplate != null)
            {
                templateSysPrompt = storedTemplate.SystemPrompt;
                templateUsrPrompt = storedTemplate.UserPrompt;
                if (storedTemplate.ForceJsonOutput) forceJsonOutput = true;
                if (storedTemplate.SaveAsNewFile) saveAsNewFile = true;
            }
            else
            {
                var (defaultSys, defaultUsr) = MultimodalVlmClientEngine.GetPresetPrompts(preset, targetLanguage);
                templateSysPrompt = defaultSys;
                templateUsrPrompt = defaultUsr;
            }

            // Forzar JSON si el preset es de extracción a JSON
            if (preset == VlmTaskPreset.ExtractInvoiceReceiptJson)
            {
                forceJsonOutput = true;
            }

            string finalSystemPrompt = !string.IsNullOrWhiteSpace(customSystemPrompt) ? customSystemPrompt : templateSysPrompt;
            string baseUserPrompt = !string.IsNullOrWhiteSpace(rawUserPrompt) ? rawUserPrompt : templateUsrPrompt;

            // Anexar instrucciones adicionales particulares si se especificaron
            if (!string.IsNullOrWhiteSpace(additionalPrompt))
            {
                baseUserPrompt = string.IsNullOrWhiteSpace(baseUserPrompt)
                    ? additionalPrompt
                    : $"{baseUserPrompt}\n\n[Instrucciones Adicionales]:\n{additionalPrompt}";
            }

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

            string? jsonSchema = storedTemplate?.JsonSchema ?? MultimodalVlmClientEngine.GetPresetJsonSchema(preset);

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
                Item: item,
                ConcurrencyLimit: MaxConcurrency,
                JsonSchema: jsonSchema
            );

            var result = await adapter.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            context.ReportExecutionDuration(result.DurationMs);

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

            // Aplanado recursivo y extracción de variables estructuradas
            string? jsonSource = !string.IsNullOrWhiteSpace(result.ExtractedJson) ? result.ExtractedJson : result.RawText;
            FileFlow.Plugin.AI.Utilities.JsonMetadataFlattener.FlattenAndInject(jsonSource, item.Metadata, prefix: "AI:Vlm:");

            if (!string.IsNullOrWhiteSpace(result.DetectedCategory))
            {
                item.Metadata["AI:VlmCategory"] = result.DetectedCategory;
                if (!item.Metadata.ContainsKey("categoria"))
                {
                    item.Metadata["categoria"] = result.DetectedCategory;
                }
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
