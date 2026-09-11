using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Plugin.AI.Management;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.AI.ViewModels;

/// <summary>
/// ViewModel de la ventana modal de configuración avanzada para el nodo de IA Multimodal (VLM).
/// Administra perfiles de proveedores, prueba de conexión HTTP y ciclo de vida CRUD de plantillas.
/// </summary>
public sealed partial class MultimodalVlmConfigViewModel : ObservableObject
{
    private readonly VlmConfigurationStorageService _storageService;
    private readonly MultimodalVisionLlmNode? _targetNode;
    private readonly HttpClient _httpClient;

    [ObservableProperty]
    private int _selectedTabIndex;

    // --- Pestaña 1: Proveedores ---
    [ObservableProperty]
    private ObservableCollection<VlmProviderProfile> _providers = [];

    [ObservableProperty]
    private VlmProviderProfile? _selectedProvider;

    [ObservableProperty]
    private ObservableCollection<string> _availableModels = [];

    [ObservableProperty]
    private bool _isDetectingModels;

    [ObservableProperty]
    private bool _isTestingConnection;

    [ObservableProperty]
    private string _connectionTestStatus = string.Empty;

    [ObservableProperty]
    private bool? _connectionTestSuccess;

    // --- Pestaña 2: Plantillas ---
    [ObservableProperty]
    private ObservableCollection<VlmTemplateDefinition> _templates = [];

    [ObservableProperty]
    private VlmTemplateDefinition? _selectedTemplate;

    [ObservableProperty]
    private string _templateFilterText = string.Empty;

    // --- Pestaña 3: Vista previa de prompt ---
    [ObservableProperty]
    private string _previewAdditionalPrompt = string.Empty;

    [ObservableProperty]
    private string _previewSystemPrompt = string.Empty;

    [ObservableProperty]
    private string _previewUserPrompt = string.Empty;

    [ObservableProperty]
    private string _previewFinalPrompt = string.Empty;

    // --- Estado general ---
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public Action? RequestClose { get; set; }

    public MultimodalVlmConfigViewModel(
        MultimodalVisionLlmNode? targetNode = null,
        VlmConfigurationStorageService? storageService = null,
        HttpClient? customHttpClient = null)
    {
        _targetNode = targetNode;
        _storageService = storageService ?? VlmConfigurationStorageService.Instance;
        _httpClient = customHttpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

        LoadConfiguration();

        if (_targetNode != null)
        {
            SelectNodeProviderAndTemplate();
        }
    }

    private void LoadConfiguration()
    {
        var providersList = _storageService.LoadProviders();
        Providers = new ObservableCollection<VlmProviderProfile>(providersList);
        SelectedProvider = Providers.FirstOrDefault();

        var templatesList = _storageService.LoadTemplates();
        Templates = new ObservableCollection<VlmTemplateDefinition>(templatesList);
        SelectedTemplate = Templates.FirstOrDefault();

        UpdatePromptPreview();
    }

    private void SelectNodeProviderAndTemplate()
    {
        if (_targetNode == null) return;

        // Seleccionar proveedor del nodo
        if (_targetNode.Parameters.TryGetValue("Provider", out var pVal) && pVal != null)
        {
            string provName = pVal.ToString()!;
            var matchedProv = Providers.FirstOrDefault(p =>
                string.Equals(p.ProviderId, provName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.DisplayName, provName, StringComparison.OrdinalIgnoreCase));
            if (matchedProv != null)
            {
                SelectedProvider = matchedProv;
            }
        }

        // Seleccionar plantilla del nodo
        if (_targetNode.Parameters.TryGetValue("TaskPreset", out var tpVal) && tpVal != null)
        {
            string tplName = tpVal.ToString()!;
            var matchedTpl = Templates.FirstOrDefault(t =>
                string.Equals(t.Id, tplName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.Name, tplName, StringComparison.OrdinalIgnoreCase));
            if (matchedTpl != null)
            {
                SelectedTemplate = matchedTpl;
            }
        }

        // Cargar prompt adicional del nodo para vista previa
        if (_targetNode.Parameters.TryGetValue("AdditionalPrompt", out var apVal) && apVal != null)
        {
            PreviewAdditionalPrompt = apVal.ToString()!;
        }
    }

    partial void OnSelectedProviderChanged(VlmProviderProfile? value)
    {
        ConnectionTestStatus = string.Empty;
        ConnectionTestSuccess = null;
        AvailableModels.Clear();

        if (value != null)
        {
            if (!string.IsNullOrWhiteSpace(value.ModelName))
            {
                AvailableModels.Add(value.ModelName);
            }
            _ = DetectModelsForProviderAsync(value);
        }
    }

    partial void OnSelectedTemplateChanged(VlmTemplateDefinition? value)
    {
        UpdatePromptPreview();
    }

    partial void OnPreviewAdditionalPromptChanged(string value)
    {
        UpdatePromptPreview();
    }

    public void UpdatePromptPreview()
    {
        if (SelectedTemplate == null)
        {
            PreviewSystemPrompt = string.Empty;
            PreviewUserPrompt = string.Empty;
            PreviewFinalPrompt = string.Empty;
            return;
        }

        PreviewSystemPrompt = SelectedTemplate.SystemPrompt;
        PreviewUserPrompt = SelectedTemplate.UserPrompt;

        string final = $"[SYSTEM PROMPT]:\n{SelectedTemplate.SystemPrompt}\n\n[USER PROMPT]:\n{SelectedTemplate.UserPrompt}";

        if (!string.IsNullOrWhiteSpace(PreviewAdditionalPrompt))
        {
            final += $"\n\n[INSTRUCCIONES ADICIONALES (Nodo)]:\n{PreviewAdditionalPrompt}";
        }

        PreviewFinalPrompt = final;
    }

    [RelayCommand]
    public void NewProvider()
    {
        var newProv = new VlmProviderProfile
        {
            ProviderId = "Custom_" + Guid.NewGuid().ToString("N")[..8],
            DisplayName = LocalizationManager.Instance.GetString("VlmConfig_NewProviderName", "Nuevo Proveedor Personalizado"),
            EndpointUrl = "http://localhost:1234/v1",
            ModelName = "default",
            ApiKey = "none",
            Temperature = 0.1,
            MaxTokens = 2048,
            MaxImageDimension = 1536,
            TimeoutSeconds = 120,
            IsBuiltIn = false
        };

        Providers.Add(newProv);
        SelectedProvider = newProv;
        StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_ProviderCreated", "Proveedor creado. Recuerda pulsar 'Guardar'.");
    }

    [RelayCommand]
    public void DuplicateProvider()
    {
        if (SelectedProvider == null) return;

        var cloned = SelectedProvider.Clone();
        cloned.ProviderId = "Custom_" + Guid.NewGuid().ToString("N")[..8];
        cloned.DisplayName += " (" + LocalizationManager.Instance.GetString("Common_Copy", "Copia") + ")";
        cloned.IsBuiltIn = false;

        Providers.Add(cloned);
        SelectedProvider = cloned;
        StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_ProviderDuplicated", "Proveedor duplicado.");
    }

    [RelayCommand]
    public void DeleteProvider()
    {
        if (SelectedProvider == null) return;

        if (SelectedProvider.IsBuiltIn)
        {
            StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_CannotDeleteBuiltInProvider", "No se pueden eliminar los proveedores integrados del sistema.");
            return;
        }

        var toRemove = SelectedProvider;
        int index = Providers.IndexOf(toRemove);
        Providers.Remove(toRemove);

        SelectedProvider = Providers.ElementAtOrDefault(Math.Max(0, index - 1));
        _storageService.SaveProviders(Providers);
        StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_ProviderDeleted", "Proveedor eliminado.");
    }

    [RelayCommand]
    public async Task RefreshModelsAsync()
    {
        if (SelectedProvider == null) return;

        IsDetectingModels = true;
        StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_DetectingModels", "Consultando modelos disponibles en el servidor...");

        try
        {
            await DetectModelsForProviderAsync(SelectedProvider);
            StatusMessage = string.Format(
                LocalizationManager.Instance.GetString("VlmConfig_ModelsDetectedCount", "Detectados {0} modelos en el servidor."),
                AvailableModels.Count
            );
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(LocalizationManager.Instance.GetString("VlmConfig_DetectModelsFailed", "Error al consultar modelos: {0}"), ex.Message);
        }
        finally
        {
            IsDetectingModels = false;
        }
    }

    private async Task DetectModelsForProviderAsync(VlmProviderProfile provider)
    {
        if (provider.ProviderId.Contains("In-Process", StringComparison.OrdinalIgnoreCase) ||
            provider.EndpointUrl.StartsWith("in-process", StringComparison.OrdinalIgnoreCase))
        {
            if (!AvailableModels.Contains("FileFlow-Structural-VLM"))
                AvailableModels.Add("FileFlow-Structural-VLM");
            if (!AvailableModels.Contains("FileFlow-InProcess-Fast"))
                AvailableModels.Add("FileFlow-InProcess-Fast");
            return;
        }

        string baseUri = provider.EndpointUrl.TrimEnd('/');
        string modelsUrl = baseUri.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUri}/models"
            : $"{baseUri}/v1/models";

        var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
            if (!string.IsNullOrWhiteSpace(provider.ApiKey) && provider.ApiKey != "none")
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", provider.ApiKey);
            }

            using var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                ParseOpenAiModels(json, detected);
            }
        }
        catch { }

        if (detected.Count == 0)
        {
            try
            {
                string ollamaRoot = baseUri.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                    ? baseUri[..^3].TrimEnd('/')
                    : baseUri;
                string ollamaTagsUrl = $"{ollamaRoot}/api/tags";

                using var reqOllama = new HttpRequestMessage(HttpMethod.Get, ollamaTagsUrl);
                using var respOllama = await _httpClient.SendAsync(reqOllama);
                if (respOllama.IsSuccessStatusCode)
                {
                    string jsonOllama = await respOllama.Content.ReadAsStringAsync();
                    ParseOllamaModels(jsonOllama, detected);
                }
            }
            catch { }
        }

        foreach (var m in detected)
        {
            if (!AvailableModels.Contains(m))
            {
                AvailableModels.Add(m);
            }
        }

        if (string.IsNullOrWhiteSpace(provider.ModelName) && AvailableModels.Count > 0)
        {
            provider.ModelName = AvailableModels[0];
        }
    }

    private static void ParseOpenAiModels(string json, HashSet<string> result)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataEl.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                    {
                        string? id = idEl.GetString();
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            result.Add(id);
                        }
                    }
                }
            }
        }
        catch { }
    }

    private static void ParseOllamaModels(string json, HashSet<string> result)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("models", out var modelsEl) && modelsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in modelsEl.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                    {
                        string? name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            result.Add(name);
                        }
                    }
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        if (SelectedProvider == null) return;

        IsTestingConnection = true;
        ConnectionTestStatus = LocalizationManager.Instance.GetString("VlmConfig_TestingConnection", "Comprobando conexión...");
        ConnectionTestSuccess = null;

        try
        {
            if (SelectedProvider.ProviderId.Contains("In-Process", StringComparison.OrdinalIgnoreCase) ||
                SelectedProvider.EndpointUrl.StartsWith("in-process", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(250);
                ConnectionTestSuccess = true;
                ConnectionTestStatus = LocalizationManager.Instance.GetString("VlmConfig_InProcessReady", "✅ Motor interno listo. No requiere red ni procesos externos.");
                await DetectModelsForProviderAsync(SelectedProvider);
                return;
            }

            string baseUri = SelectedProvider.EndpointUrl.TrimEnd('/');
            string modelsUrl = baseUri.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? $"{baseUri}/models"
                : $"{baseUri}/v1/models";

            using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
            if (!string.IsNullOrWhiteSpace(SelectedProvider.ApiKey) && SelectedProvider.ApiKey != "none")
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SelectedProvider.ApiKey);
            }

            using var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                ParseOpenAiModels(json, detected);
                foreach (var m in detected)
                {
                    if (!AvailableModels.Contains(m))
                        AvailableModels.Add(m);
                }

                ConnectionTestSuccess = true;
                ConnectionTestStatus = string.Format(
                    LocalizationManager.Instance.GetString("VlmConfig_ConnectionSuccess", "🟢 Conexión exitosa. Servidor activo ({0} modelos detectados)."),
                    detected.Count > 0 ? detected.Count.ToString() : "OK"
                );
            }
            else
            {
                ConnectionTestSuccess = false;
                ConnectionTestStatus = string.Format(
                    LocalizationManager.Instance.GetString("VlmConfig_ConnectionHttpError", "🔴 El servidor respondió con código {0} ({1})."),
                    (int)response.StatusCode,
                    response.StatusCode
                );
            }
        }
        catch (Exception ex)
        {
            ConnectionTestSuccess = false;
            ConnectionTestStatus = string.Format(
                LocalizationManager.Instance.GetString("VlmConfig_ConnectionFailed", "🔴 Error de conexión: {0}"),
                ex.Message
            );
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    public void NewTemplate()
    {
        var newTpl = new VlmTemplateDefinition
        {
            Id = "Custom_" + Guid.NewGuid().ToString("N")[..8],
            Name = LocalizationManager.Instance.GetString("VlmConfig_NewTemplateName", "Nueva Plantilla Personalizada"),
            Description = LocalizationManager.Instance.GetString("VlmConfig_NewTemplateDesc", "Descripción de la plantilla..."),
            SystemPrompt = "Eres un asistente de visión multimodal analítico.",
            UserPrompt = "Analiza la imagen adjunta y extrae la información requerida.",
            ForceJsonOutput = false,
            SaveAsNewFile = false,
            IsBuiltIn = false,
            TargetLanguage = "Español"
        };

        Templates.Add(newTpl);
        SelectedTemplate = newTpl;
        StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_TemplateCreated", "Plantilla creada. Recuerda pulsar 'Guardar'.");
    }

    [RelayCommand]
    public void DuplicateTemplate()
    {
        if (SelectedTemplate == null) return;

        var cloned = SelectedTemplate.Clone();
        cloned.Id = "Custom_" + Guid.NewGuid().ToString("N")[..8];
        cloned.Name += " (" + LocalizationManager.Instance.GetString("Common_Copy", "Copia") + ")";
        cloned.IsBuiltIn = false;

        Templates.Add(cloned);
        SelectedTemplate = cloned;
        StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_TemplateDuplicated", "Plantilla duplicada.");
    }

    [RelayCommand]
    public void DeleteTemplate()
    {
        if (SelectedTemplate == null) return;

        if (SelectedTemplate.IsBuiltIn)
        {
            StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_CannotDeleteBuiltIn", "No se pueden eliminar plantillas integradas del sistema.");
            return;
        }

        var toRemove = SelectedTemplate;
        int index = Templates.IndexOf(toRemove);
        Templates.Remove(toRemove);

        SelectedTemplate = Templates.ElementAtOrDefault(Math.Max(0, index - 1));
        _storageService.SaveTemplates(Templates);
        StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_TemplateDeleted", "Plantilla eliminada.");
    }

    [RelayCommand]
    public void RestoreBuiltIn()
    {
        if (SelectedTemplate == null || !SelectedTemplate.IsBuiltIn) return;

        var restored = _storageService.ResetTemplateToFactory(SelectedTemplate.Id);
        if (restored != null)
        {
            int idx = Templates.IndexOf(SelectedTemplate);
            if (idx >= 0)
            {
                Templates[idx] = restored;
                SelectedTemplate = restored;
            }
            StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_TemplateRestored", "Plantilla restaurada a los valores de fábrica.");
        }
    }

    // --- Pestaña 4: Prueba con Archivo de Muestra (1 ciclo interactivo) ---
    [ObservableProperty]
    private string _sampleFilePath = string.Empty;

    [ObservableProperty]
    private bool _isExecutingSample;

    [ObservableProperty]
    private string _sampleExecutionStatus = string.Empty;

    [ObservableProperty]
    private string _sampleRawResponse = string.Empty;

    [ObservableProperty]
    private string _sampleExtractedJson = string.Empty;

    [ObservableProperty]
    private long _sampleDurationMs;

    [ObservableProperty]
    private int _sampleTokens;

    [ObservableProperty]
    private ObservableCollection<DiscoveredVariableItem> _sampleDiscoveredVariables = [];

    [ObservableProperty]
    private bool _canApplyVariables;

    [RelayCommand]
    public void SelectSampleFile()
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = LocalizationManager.Instance.GetString("VlmConfig_SelectSampleFileTitle", "Seleccionar Archivo de Imagen o Documento de Muestra"),
                Filter = "Archivos de Imagen y Documentos (*.jpg;*.jpeg;*.png;*.webp;*.pdf;*.txt)|*.jpg;*.jpeg;*.png;*.webp;*.pdf;*.txt|Todos los archivos (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                SampleFilePath = dlg.FileName;
            }
        }
        catch
        {
            // Ignorar en entornos sin UI
        }
    }

    [RelayCommand]
    public async Task RunSampleInferenceAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SampleFilePath) || !System.IO.File.Exists(SampleFilePath))
        {
            SampleExecutionStatus = LocalizationManager.Instance.GetString("VlmConfig_SampleFileNotFound", "Selecciona un archivo de prueba existente.");
            return;
        }

        if (SelectedProvider == null || SelectedTemplate == null)
        {
            SampleExecutionStatus = LocalizationManager.Instance.GetString("VlmConfig_SelectProviderTemplate", "Selecciona un proveedor y una plantilla válidos.");
            return;
        }

        IsExecutingSample = true;
        SampleExecutionStatus = LocalizationManager.Instance.GetString("VlmConfig_RunningSample", "🧠 Ejecutando inferencia de prueba...");
        SampleRawResponse = string.Empty;
        SampleExtractedJson = string.Empty;
        SampleDiscoveredVariables.Clear();
        CanApplyVariables = false;

        try
        {
            string provider = SelectedProvider.DisplayName;
            var adapter = VlmAdapterFactory.Create(provider);

            string base64Uri = string.Empty;
            if (adapter is not InProcessVlmAdapter)
            {
                using var stream = System.IO.File.OpenRead(SampleFilePath);
                using var image = await SixLabors.ImageSharp.Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgb24>(stream, cancellationToken);
                base64Uri = MultimodalVlmClientEngine.PrepareImageAsBase64Jpeg(image, SelectedProvider.MaxImageDimension);
            }

            VlmTaskPreset preset = VlmTaskPreset.ExtractInvoiceReceiptJson;
            if (Enum.TryParse<VlmTaskPreset>(SelectedTemplate.Id, true, out var parsedPreset))
            {
                preset = parsedPreset;
            }

            string userPrompt = !string.IsNullOrWhiteSpace(PreviewAdditionalPrompt)
                ? $"{SelectedTemplate.UserPrompt}\n\n[Instrucciones Adicionales]:\n{PreviewAdditionalPrompt}"
                : SelectedTemplate.UserPrompt;

            var dummyItem = new FileFlow.Sdk.FileItemContext(SampleFilePath);
            var dummyContext = FileFlow.Sdk.NullFlowExecutionContext.Instance;

            var request = new VlmExecutionRequest(
                ImagePath: SampleFilePath,
                Base64ImageDataUrl: base64Uri,
                SystemPrompt: SelectedTemplate.SystemPrompt,
                UserPrompt: userPrompt,
                TaskPreset: preset,
                TargetLanguage: SelectedTemplate.TargetLanguage,
                ForceJsonOutput: SelectedTemplate.ForceJsonOutput,
                MaxTokens: SelectedProvider.MaxTokens,
                Temperature: SelectedProvider.Temperature,
                MaxImageDimension: SelectedProvider.MaxImageDimension,
                EndpointUrl: SelectedProvider.EndpointUrl,
                ModelName: SelectedProvider.ModelName,
                ApiKey: SelectedProvider.ApiKey,
                Timeout: TimeSpan.FromSeconds(SelectedProvider.TimeoutSeconds),
                CustomHttpClient: _httpClient,
                Context: dummyContext,
                Item: dummyItem,
                ConcurrencyLimit: 1,
                JsonSchema: SelectedTemplate.JsonSchema ?? MultimodalVlmClientEngine.GetPresetJsonSchema(preset)
            );

            var result = await adapter.ExecuteAsync(request, cancellationToken);

            // Extraer y aplanar variables en lista local antes de enviar a la UI
            string? sourceToFlatten = !string.IsNullOrWhiteSpace(result.ExtractedJson) ? result.ExtractedJson : result.RawText;
            var flattened = FileFlow.Plugin.AI.Utilities.JsonMetadataFlattener.Flatten(sourceToFlatten);

            var discoveredList = new List<DiscoveredVariableItem>();
            foreach (var kvp in flattened)
            {
                discoveredList.Add(new DiscoveredVariableItem(kvp.Key, kvp.Value, $"{{{kvp.Key}}}"));
            }

            // Añadir también las variables universales VLM
            discoveredList.Add(new DiscoveredVariableItem("AI:VlmResponse", result.RawText.Length > 60 ? result.RawText[..60] + "..." : result.RawText, "{AI:VlmResponse}"));
            if (!string.IsNullOrWhiteSpace(result.ExtractedJson))
            {
                discoveredList.Add(new DiscoveredVariableItem("AI:VlmJson", result.ExtractedJson.Length > 60 ? result.ExtractedJson[..60] + "..." : result.ExtractedJson, "{AI:VlmJson}"));
            }
            if (!string.IsNullOrWhiteSpace(result.DetectedCategory))
            {
                discoveredList.Add(new DiscoveredVariableItem("AI:VlmCategory", result.DetectedCategory, "{AI:VlmCategory}"));
            }

            void ApplySuccessUi()
            {
                SampleRawResponse = result.RawText;
                SampleExtractedJson = result.ExtractedJson ?? string.Empty;
                SampleDurationMs = result.DurationMs;
                SampleTokens = result.TotalTokens;
                SampleDiscoveredVariables.Clear();
                foreach (var item in discoveredList)
                {
                    SampleDiscoveredVariables.Add(item);
                }

                CanApplyVariables = SampleDiscoveredVariables.Count > 0 && _targetNode != null;
                string statusTemplate = LocalizationManager.Instance.GetString("VlmConfig_SampleSuccess", "✨ Inferencia completada con éxito en {0} ms ({1} tokens). {2} variables detectadas.");
                SampleExecutionStatus = string.Format(statusTemplate, result.DurationMs, result.TotalTokens, flattened.Count);
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(ApplySuccessUi);
            }
            else
            {
                ApplySuccessUi();
            }
        }
        catch (Exception ex)
        {
            void ApplyErrorUi()
            {
                string errorTemplate = LocalizationManager.Instance.GetString("VlmConfig_SampleError", "❌ Error durante la inferencia de prueba: {0}");
                SampleExecutionStatus = string.Format(errorTemplate, ex.Message);
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(ApplyErrorUi);
            }
            else
            {
                ApplyErrorUi();
            }
        }
        finally
        {
            void ApplyFinallyUi()
            {
                IsExecutingSample = false;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(ApplyFinallyUi);
            }
            else
            {
                ApplyFinallyUi();
            }
        }
    }

    [RelayCommand]
    public void ApplyDiscoveredVariables()
    {
        if (_targetNode == null || SampleDiscoveredVariables.Count == 0) return;

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in SampleDiscoveredVariables)
        {
            dict[v.Name] = v.Value;
        }

        string json = System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
        _targetNode.Parameters["DiscoveredVariables"] = json;

        StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_VariablesApplied", $"✅ {dict.Count} variables importadas al nodo correctamente.");
    }

    [RelayCommand]
    public void SaveAll()
    {
        _storageService.SaveProviders(Providers);
        _storageService.SaveTemplates(Templates);

        if (_targetNode != null && SelectedProvider != null)
        {
            _targetNode.Parameters["Provider"] = !string.IsNullOrWhiteSpace(SelectedProvider.DisplayName) ? SelectedProvider.DisplayName : SelectedProvider.ProviderId;
            _targetNode.Parameters["EndpointUrl"] = SelectedProvider.EndpointUrl;
            _targetNode.Parameters["ModelName"] = SelectedProvider.ModelName;
            _targetNode.Parameters["ApiKey"] = SelectedProvider.ApiKey;
            _targetNode.Parameters["Temperature"] = SelectedProvider.Temperature;
            _targetNode.Parameters["MaxTokens"] = SelectedProvider.MaxTokens;
            _targetNode.Parameters["MaxImageDimension"] = SelectedProvider.MaxImageDimension;
            _targetNode.Parameters["TimeoutSeconds"] = SelectedProvider.TimeoutSeconds;
            _targetNode.Parameters["MaxConcurrency"] = SelectedProvider.ConcurrencyLimit;
        }

        if (_targetNode != null && SelectedTemplate != null)
        {
            _targetNode.Parameters["TaskPreset"] = !string.IsNullOrWhiteSpace(SelectedTemplate.Name) ? SelectedTemplate.Name : SelectedTemplate.Id;
            _targetNode.Parameters["ForceJsonOutput"] = SelectedTemplate.ForceJsonOutput;
            _targetNode.Parameters["SaveAsNewFile"] = SelectedTemplate.SaveAsNewFile;
            _targetNode.Parameters["TargetLanguage"] = SelectedTemplate.TargetLanguage;
        }

        if (_targetNode != null && !string.IsNullOrWhiteSpace(PreviewAdditionalPrompt))
        {
            _targetNode.Parameters["AdditionalPrompt"] = PreviewAdditionalPrompt;
        }

        StatusMessage = LocalizationManager.Instance.GetString("VlmConfig_SavedSuccess", "Configuración guardada correctamente.");
    }

    [RelayCommand]
    public void ApplyAndClose()
    {
        SaveAll();
        RequestClose?.Invoke();
    }
}

/// <summary>
/// Representa una variable descubierta en una prueba de 1 ciclo interactivo.
/// </summary>
public sealed record DiscoveredVariableItem(string Name, string Value, string Token);

