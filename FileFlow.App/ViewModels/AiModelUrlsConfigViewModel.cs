using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Plugin.AI;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

namespace FileFlow.App.ViewModels;

/// <summary>
/// ViewModel desacoplado para la configuración y verificación de URLs de descarga de modelos de IA.
/// </summary>
public partial class AiModelUrlsConfigViewModel : ObservableObject
{
    private readonly string _modelId;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;
    private readonly HttpClient? _httpClient;

    [ObservableProperty]
    private string _modelName = string.Empty;

    [ObservableProperty]
    private string _category = "IA";

    [ObservableProperty]
    private string _fileInfoText = string.Empty;

    [ObservableProperty]
    private string _urlsText = string.Empty;

    [ObservableProperty]
    private string _urlsCountText = "0 URL(s)";

    [ObservableProperty]
    private string _statusBadgeText = string.Empty;

    [ObservableProperty]
    private bool _isCustomConfig;

    [ObservableProperty]
    private bool _isTestingUrls;

    [ObservableProperty]
    private string _testResultsText = string.Empty;

    [ObservableProperty]
    private bool _hasTestResults;

    public string ModelId => _modelId;

    public AiModelUrlsConfigViewModel(
        string modelId,
        IDialogService? dialogService = null,
        ILocalizationService? localizationService = null,
        HttpClient? httpClient = null)
    {
        _modelId = modelId;
        _dialogService = dialogService ?? (App.Services?.GetService(typeof(IDialogService)) as IDialogService) ?? NullDialogService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;
        _httpClient = httpClient;

        AiModelManager.Catalog.TryGetValue(_modelId, out var modelInfo);

        ModelName = modelInfo?.FriendlyName ?? _modelId;
        Category = modelInfo?.Category ?? "IA";
        FileInfoText = modelInfo != null
            ? $"📁 Archivo: {modelInfo.FileName} | Tamaño mínimo: {modelInfo.MinSizeBytes / 1_048_576.0:F1} MB"
            : string.Empty;

        LoadCurrentUrls();
    }

    public void LoadCurrentUrls()
    {
        var urls = AiModelManager.GetConfiguredUrls(_modelId);
        UrlsText = string.Join(Environment.NewLine, urls);
        UpdateBadge();
        UpdateUrlsCount();
    }

    partial void OnUrlsTextChanged(string value)
    {
        UpdateUrlsCount();
    }

    private void UpdateBadge()
    {
        IsCustomConfig = AiModelManager.HasCustomUrls(_modelId);
        StatusBadgeText = IsCustomConfig
            ? _loc.GetString("AiModelUrls_StatusCustom", "🔧 Personalizado")
            : _loc.GetString("AiModelUrls_StatusDefault", "📦 Oficial / Predeterminado");
    }

    private void UpdateUrlsCount()
    {
        var urls = ParseUrls();
        UrlsCountText = $"{urls.Count} URL(s)";
    }

    public List<string> ParseUrls()
    {
        if (string.IsNullOrWhiteSpace(UrlsText)) return [];

        return UrlsText
            .Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u) && (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [RelayCommand]
    public async Task TestUrlsAsync()
    {
        var urls = ParseUrls();
        if (urls.Count == 0)
        {
            HasTestResults = true;
            TestResultsText = "⚠️ No hay URLs válidas (que comiencen por http:// o https://) para probar.";
            return;
        }

        HasTestResults = true;
        IsTestingUrls = true;
        TestResultsText = "⏳ Comprobando conexiones con los servidores...";

        var results = new List<string>();
        HttpClient client = _httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(15),
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        bool shouldDisposeClient = _httpClient == null;

        try
        {
            if (shouldDisposeClient)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) FileFlowStudio/1.0");
            }

            for (int i = 0; i < urls.Count; i++)
            {
                string url = urls[i];
                try
                {
                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true);
                    if (response.IsSuccessStatusCode)
                    {
                        long? length = response.Content.Headers.ContentLength;
                        string sizeStr = length.HasValue ? $"{length.Value / 1_048_576.0:F1} MB" : "tamaño dinámico";
                        results.Add($"[{i + 1}/{urls.Count}] ✅ HTTP {(int)response.StatusCode} OK ({sizeStr}) -> {url}");
                    }
                    else
                    {
                        results.Add($"[{i + 1}/{urls.Count}] ❌ HTTP {(int)response.StatusCode} {response.ReasonPhrase} -> {url}");
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"[{i + 1}/{urls.Count}] ❌ Error: {ex.Message} -> {url}");
                }
            }
        }
        finally
        {
            if (shouldDisposeClient)
            {
                client.Dispose();
            }
            IsTestingUrls = false;
        }

        TestResultsText = string.Join(Environment.NewLine, results);
    }

    [RelayCommand]
    public void ResetToDefaults()
    {
        var defaults = AiModelManager.GetDefaultUrls(_modelId);
        UrlsText = string.Join(Environment.NewLine, defaults);
        HasTestResults = false;
        TestResultsText = string.Empty;
        UpdateUrlsCount();
    }

    public bool Save()
    {
        var urls = ParseUrls();
        if (urls.Count == 0)
        {
            _dialogService.ShowWarning(
                "Debe especificar al menos una URL de descarga válida para este modelo.",
                "URL requerida");
            return false;
        }

        AiModelManager.SetCustomUrls(_modelId, urls);
        UpdateBadge();
        return true;
    }
}
