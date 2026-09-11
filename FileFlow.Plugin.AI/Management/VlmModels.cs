using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileFlow.Plugin.AI.Management;

/// <summary>
/// Representa el perfil de conexión y parámetros técnicos asociados a un proveedor VLM.
/// </summary>
public sealed class VlmProviderProfile : ObservableObject
{
    private string _providerId = string.Empty;
    private string _displayName = string.Empty;
    private string _endpointUrl = "http://localhost:1234/v1";
    private string _modelName = "qwen2.5-vl-7b-instruct";
    private string _apiKey = "lm-studio";
    private double _temperature = 0.1;
    private int _maxTokens = 2048;
    private int _maxImageDimension = 1536;
    private int _timeoutSeconds = 120;
    private int _concurrencyLimit = 1;
    private bool _isBuiltIn;

    [JsonPropertyName("concurrencyLimit")]
    public int ConcurrencyLimit
    {
        get => _concurrencyLimit;
        set => SetProperty(ref _concurrencyLimit, value);
    }

    [JsonPropertyName("providerId")]
    public string ProviderId
    {
        get => _providerId;
        set => SetProperty(ref _providerId, value);
    }

    [JsonPropertyName("displayName")]
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    [JsonPropertyName("endpointUrl")]
    public string EndpointUrl
    {
        get => _endpointUrl;
        set => SetProperty(ref _endpointUrl, value);
    }

    [JsonPropertyName("modelName")]
    public string ModelName
    {
        get => _modelName;
        set => SetProperty(ref _modelName, value);
    }

    [JsonPropertyName("apiKey")]
    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    [JsonPropertyName("temperature")]
    public double Temperature
    {
        get => _temperature;
        set => SetProperty(ref _temperature, value);
    }

    [JsonPropertyName("maxTokens")]
    public int MaxTokens
    {
        get => _maxTokens;
        set => SetProperty(ref _maxTokens, value);
    }

    [JsonPropertyName("maxImageDimension")]
    public int MaxImageDimension
    {
        get => _maxImageDimension;
        set => SetProperty(ref _maxImageDimension, value);
    }

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds
    {
        get => _timeoutSeconds;
        set => SetProperty(ref _timeoutSeconds, value);
    }

    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        set => SetProperty(ref _isBuiltIn, value);
    }

    public VlmProviderProfile Clone()
    {
        return new VlmProviderProfile
        {
            ProviderId = ProviderId,
            DisplayName = DisplayName,
            EndpointUrl = EndpointUrl,
            ModelName = ModelName,
            ApiKey = ApiKey,
            Temperature = Temperature,
            MaxTokens = MaxTokens,
            MaxImageDimension = MaxImageDimension,
            TimeoutSeconds = TimeoutSeconds,
            ConcurrencyLimit = ConcurrencyLimit,
            IsBuiltIn = IsBuiltIn
        };
    }
}

/// <summary>
/// Representa una plantilla de tarea y prompts para modelos de visión y lenguaje.
/// </summary>
public sealed class VlmTemplateDefinition : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _systemPrompt = string.Empty;
    private string _userPrompt = string.Empty;
    private bool _forceJsonOutput;
    private bool _saveAsNewFile;
    private bool _isBuiltIn;
    private string _targetLanguage = "Español";
    private string? _jsonSchema;

    [JsonPropertyName("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    [JsonPropertyName("description")]
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    [JsonPropertyName("systemPrompt")]
    public string SystemPrompt
    {
        get => _systemPrompt;
        set => SetProperty(ref _systemPrompt, value);
    }

    [JsonPropertyName("userPrompt")]
    public string UserPrompt
    {
        get => _userPrompt;
        set => SetProperty(ref _userPrompt, value);
    }

    [JsonPropertyName("forceJsonOutput")]
    public bool ForceJsonOutput
    {
        get => _forceJsonOutput;
        set => SetProperty(ref _forceJsonOutput, value);
    }

    [JsonPropertyName("saveAsNewFile")]
    public bool SaveAsNewFile
    {
        get => _saveAsNewFile;
        set => SetProperty(ref _saveAsNewFile, value);
    }

    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        set => SetProperty(ref _isBuiltIn, value);
    }

    [JsonPropertyName("targetLanguage")]
    public string TargetLanguage
    {
        get => _targetLanguage;
        set => SetProperty(ref _targetLanguage, value);
    }

    [JsonPropertyName("jsonSchema")]
    public string? JsonSchema
    {
        get => _jsonSchema;
        set => SetProperty(ref _jsonSchema, value);
    }

    public VlmTemplateDefinition Clone()
    {
        return new VlmTemplateDefinition
        {
            Id = Id,
            Name = Name,
            Description = Description,
            SystemPrompt = SystemPrompt,
            UserPrompt = UserPrompt,
            ForceJsonOutput = ForceJsonOutput,
            SaveAsNewFile = SaveAsNewFile,
            IsBuiltIn = IsBuiltIn,
            TargetLanguage = TargetLanguage,
            JsonSchema = JsonSchema
        };
    }
}
