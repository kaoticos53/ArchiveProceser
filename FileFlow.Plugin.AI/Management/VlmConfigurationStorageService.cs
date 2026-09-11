using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using FileFlow.Plugin.AI;

namespace FileFlow.Plugin.AI.Management;

/// <summary>
/// Servicio de persistencia y gestión de configuración para perfiles de proveedores VLM y plantillas de tareas.
/// Almacena los datos en %AppData%/FileFlow/ de forma thread-safe con primitivas Lock de .NET 9.
/// </summary>
public sealed class VlmConfigurationStorageService
{
    private static readonly Lock _syncLock = new();
    private static VlmConfigurationStorageService? _instance;

    public static VlmConfigurationStorageService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    _instance ??= new VlmConfigurationStorageService();
                }
            }
            return _instance;
        }
    }

    private readonly string _storageDirectory;
    private readonly string _providersFilePath;
    private readonly string _templatesFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public VlmConfigurationStorageService(string? customDirectory = null)
    {
        _storageDirectory = customDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileFlow"
        );

        _providersFilePath = Path.Combine(_storageDirectory, "vlm_providers.json");
        _templatesFilePath = Path.Combine(_storageDirectory, "vlm_templates.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public List<VlmProviderProfile> LoadProviders()
    {
        lock (_syncLock)
        {
            try
            {
                if (File.Exists(_providersFilePath))
                {
                    string json = File.ReadAllText(_providersFilePath);
                    var list = JsonSerializer.Deserialize<List<VlmProviderProfile>>(json, _jsonOptions);
                    if (list != null && list.Count > 0)
                    {
                        EnsureDefaultProviders(list);
                        return list;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VlmConfigStorage] Error al leer proveedores: {ex.Message}");
            }

            var defaults = GetDefaultProviders();
            SaveProvidersInternal(defaults);
            return defaults;
        }
    }

    public void SaveProviders(IEnumerable<VlmProviderProfile> providers)
    {
        lock (_syncLock)
        {
            SaveProvidersInternal(providers);
        }
    }

    private void SaveProvidersInternal(IEnumerable<VlmProviderProfile> providers)
    {
        try
        {
            Directory.CreateDirectory(_storageDirectory);
            string json = JsonSerializer.Serialize(providers.ToList(), _jsonOptions);
            File.WriteAllText(_providersFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VlmConfigStorage] Error al guardar proveedores: {ex.Message}");
        }
    }

    public VlmProviderProfile GetProviderProfile(string? providerName)
    {
        var providers = LoadProviders();
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return providers.First();
        }

        var match = providers.FirstOrDefault(p =>
            string.Equals(p.DisplayName, providerName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.ProviderId, providerName, StringComparison.OrdinalIgnoreCase))
            ?? providers.FirstOrDefault(p =>
                providerName.Contains(p.ProviderId, StringComparison.OrdinalIgnoreCase) ||
                providerName.Contains(p.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                p.DisplayName.Contains(providerName, StringComparison.OrdinalIgnoreCase));

        return match ?? providers.First();
    }

    public List<VlmTemplateDefinition> LoadTemplates()
    {
        lock (_syncLock)
        {
            try
            {
                if (File.Exists(_templatesFilePath))
                {
                    string json = File.ReadAllText(_templatesFilePath);
                    var list = JsonSerializer.Deserialize<List<VlmTemplateDefinition>>(json, _jsonOptions);
                    if (list != null && list.Count > 0)
                    {
                        EnsureBuiltInTemplates(list);
                        return list;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VlmConfigStorage] Error al leer plantillas: {ex.Message}");
            }

            var defaults = GetBuiltInTemplates();
            SaveTemplatesInternal(defaults);
            return defaults;
        }
    }

    public void SaveTemplates(IEnumerable<VlmTemplateDefinition> templates)
    {
        lock (_syncLock)
        {
            SaveTemplatesInternal(templates);
        }
    }

    private void SaveTemplatesInternal(IEnumerable<VlmTemplateDefinition> templates)
    {
        try
        {
            Directory.CreateDirectory(_storageDirectory);
            string json = JsonSerializer.Serialize(templates.ToList(), _jsonOptions);
            File.WriteAllText(_templatesFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VlmConfigStorage] Error al guardar plantillas: {ex.Message}");
        }
    }

    public VlmTemplateDefinition? ResetTemplateToFactory(string templateId)
    {
        lock (_syncLock)
        {
            var builtIns = GetBuiltInTemplates();
            var factoryTemplate = builtIns.FirstOrDefault(t => string.Equals(t.Id, templateId, StringComparison.OrdinalIgnoreCase));
            if (factoryTemplate == null)
            {
                return null;
            }

            var templates = LoadTemplates();
            int index = templates.FindIndex(t => string.Equals(t.Id, templateId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                templates[index] = factoryTemplate.Clone();
            }
            else
            {
                templates.Add(factoryTemplate.Clone());
            }

            SaveTemplatesInternal(templates);
            return factoryTemplate;
        }
    }

    private static void EnsureDefaultProviders(List<VlmProviderProfile> list)
    {
        var defaults = GetDefaultProviders();
        foreach (var def in defaults)
        {
            if (!list.Any(p => string.Equals(p.ProviderId, def.ProviderId, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(def);
            }
        }
    }

    private static void EnsureBuiltInTemplates(List<VlmTemplateDefinition> list)
    {
        var builtIns = GetBuiltInTemplates();
        foreach (var b in builtIns)
        {
            if (!list.Any(t => string.Equals(t.Id, b.Id, StringComparison.OrdinalIgnoreCase)))
            {
                list.Insert(0, b);
            }
        }
    }

    public VlmProviderProfile? ResetProviderToFactory(string providerId)
    {
        lock (_syncLock)
        {
            var defaults = GetDefaultProviders();
            var factoryProvider = defaults.FirstOrDefault(p =>
                string.Equals(p.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.DisplayName, providerId, StringComparison.OrdinalIgnoreCase));
            if (factoryProvider == null)
            {
                return null;
            }

            var providers = LoadProviders();
            int index = providers.FindIndex(p =>
                string.Equals(p.ProviderId, factoryProvider.ProviderId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                providers[index] = factoryProvider.Clone();
            }
            else
            {
                providers.Add(factoryProvider.Clone());
            }

            SaveProvidersInternal(providers);
            return factoryProvider;
        }
    }

    public static List<VlmProviderProfile> GetDefaultProviders()
    {
        return
        [
            new VlmProviderProfile
            {
                ProviderId = VlmAdapterFactory.ProviderLmStudio,
                DisplayName = "LM Studio (Local Server)",
                EndpointUrl = "http://localhost:1234/v1",
                ModelName = "qwen2.5-vl-7b-instruct",
                ApiKey = "lm-studio",
                Temperature = 0.1,
                MaxTokens = 2048,
                MaxImageDimension = 1024,
                TimeoutSeconds = 120,
                ConcurrencyLimit = 1,
                IsBuiltIn = true
            },
            new VlmProviderProfile
            {
                ProviderId = VlmAdapterFactory.ProviderOllama,
                DisplayName = "Ollama (Local Server)",
                EndpointUrl = "http://localhost:11434/v1",
                ModelName = "qwen2.5-vl-7b-instruct",
                ApiKey = "ollama",
                Temperature = 0.1,
                MaxTokens = 2048,
                MaxImageDimension = 1024,
                TimeoutSeconds = 120,
                ConcurrencyLimit = 1,
                IsBuiltIn = true
            },
            new VlmProviderProfile
            {
                ProviderId = VlmAdapterFactory.ProviderInProcess,
                DisplayName = "FileFlow In-Process (Motor Interno)",
                EndpointUrl = "in-process://local",
                ModelName = "FileFlow-Structural-VLM",
                ApiKey = "none",
                Temperature = 0.0,
                MaxTokens = 2048,
                MaxImageDimension = 1024,
                TimeoutSeconds = 60,
                ConcurrencyLimit = 2,
                IsBuiltIn = true
            },
            new VlmProviderProfile
            {
                ProviderId = VlmAdapterFactory.ProviderCustom,
                DisplayName = "OpenAI / Custom Endpoint",
                EndpointUrl = "https://api.openai.com/v1",
                ModelName = "gpt-4o-mini",
                ApiKey = "sk-...",
                Temperature = 0.1,
                MaxTokens = 2048,
                MaxImageDimension = 1024,
                TimeoutSeconds = 120,
                ConcurrencyLimit = 4,
                IsBuiltIn = true
            }
        ];
    }

    public static List<VlmTemplateDefinition> GetBuiltInTemplates()
    {
        var (invSys, invUsr) = MultimodalVlmClientEngine.GetPresetPrompts(VlmTaskPreset.ExtractInvoiceReceiptJson, "Español");
        var (ocrSys, ocrUsr) = MultimodalVlmClientEngine.GetPresetPrompts(VlmTaskPreset.DocumentOcrAndSummary, "Español");
        var (traSys, traUsr) = MultimodalVlmClientEngine.GetPresetPrompts(VlmTaskPreset.TranslateDocument, "Español");
        var (clsSys, clsUsr) = MultimodalVlmClientEngine.GetPresetPrompts(VlmTaskPreset.ClassifyAndTag, "Español");
        var (qltSys, qltUsr) = MultimodalVlmClientEngine.GetPresetPrompts(VlmTaskPreset.QualityInspection, "Español");
        var (cstSys, cstUsr) = MultimodalVlmClientEngine.GetPresetPrompts(VlmTaskPreset.CustomPrompt, "Español");

        return
        [
            new VlmTemplateDefinition
            {
                Id = "ExtractInvoiceReceiptJson",
                Name = "Extracción de Facturas y Recibos (JSON)",
                Description = "Extrae emisor, receptor, NIF, fechas, bases imponibles, IVA, importes totales y desglose de líneas de artículos en formato JSON estricto.",
                SystemPrompt = invSys,
                UserPrompt = invUsr,
                ForceJsonOutput = true,
                JsonSchema = MultimodalVlmClientEngine.GetPresetJsonSchema(VlmTaskPreset.ExtractInvoiceReceiptJson),
                SaveAsNewFile = false,
                IsBuiltIn = true,
                TargetLanguage = "Español"
            },
            new VlmTemplateDefinition
            {
                Id = "DocumentOcrAndSummary",
                Name = "OCR y Resumen Ejecutivo",
                Description = "Transcribe el texto visible en el documento escaneado y elabora una síntesis ejecutiva estructurada destacando conclusiones.",
                SystemPrompt = ocrSys,
                UserPrompt = ocrUsr,
                ForceJsonOutput = false,
                SaveAsNewFile = false,
                IsBuiltIn = true,
                TargetLanguage = "Español"
            },
            new VlmTemplateDefinition
            {
                Id = "TranslateDocument",
                Name = "Traducción Visual de Documentos",
                Description = "Traduce fielmente todo el texto visible manteniendo la jerarquía de párrafos, tablas y encabezados en Markdown.",
                SystemPrompt = traSys,
                UserPrompt = traUsr,
                ForceJsonOutput = false,
                SaveAsNewFile = false,
                IsBuiltIn = true,
                TargetLanguage = "Español"
            },
            new VlmTemplateDefinition
            {
                Id = "ClassifyAndTag",
                Name = "Clasificación y Etiquetado Visual",
                Description = "Categoriza la imagen en tipologías documentales o fotográficas, calculando puntuación de confianza y asignando etiquetas descriptivas.",
                SystemPrompt = clsSys,
                UserPrompt = clsUsr,
                ForceJsonOutput = true,
                JsonSchema = MultimodalVlmClientEngine.GetPresetJsonSchema(VlmTaskPreset.ClassifyAndTag),
                SaveAsNewFile = false,
                IsBuiltIn = true,
                TargetLanguage = "Español"
            },
            new VlmTemplateDefinition
            {
                Id = "QualityInspection",
                Name = "Inspección de Calidad Formal",
                Description = "Audita la legibilidad del texto, detecta presencia de firmas manuscritas o sellos oficiales y evalúa defectos de digitalización.",
                SystemPrompt = qltSys,
                UserPrompt = qltUsr,
                ForceJsonOutput = true,
                JsonSchema = MultimodalVlmClientEngine.GetPresetJsonSchema(VlmTaskPreset.QualityInspection),
                SaveAsNewFile = false,
                IsBuiltIn = true,
                TargetLanguage = "Español"
            },
            new VlmTemplateDefinition
            {
                Id = "CustomPrompt",
                Name = "Prompt Libre / Personalizado",
                Description = "Plantilla base abierta para instrucciones libres y consultas multimodales personalizadas.",
                SystemPrompt = cstSys,
                UserPrompt = cstUsr,
                ForceJsonOutput = false,
                SaveAsNewFile = false,
                IsBuiltIn = true,
                TargetLanguage = "Español"
            }
        ];
    }
}
