using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using FileFlow.Plugin.AI;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.Views.Components;

public partial class AiModelUrlsConfigDialog : Window
{
    private readonly string _modelId;
    private readonly AiModelInfo? _modelInfo;

    public AiModelUrlsConfigDialog(string modelId)
    {
        InitializeComponent();
        _modelId = modelId;

        AiModelManager.Catalog.TryGetValue(_modelId, out _modelInfo);

        TxtModelName.Text = _modelInfo?.FriendlyName ?? _modelId;
        TxtCategory.Text = _modelInfo?.Category ?? "IA";
        TxtFileName.Text = _modelInfo != null
            ? $"📁 Archivo: {_modelInfo.FileName} | Tamaño mínimo: {_modelInfo.MinSizeBytes / 1_048_576.0:F1} MB"
            : string.Empty;

        LoadCurrentUrls();
    }

    private void LoadCurrentUrls()
    {
        var urls = AiModelManager.GetConfiguredUrls(_modelId);
        TxtUrls.Text = string.Join(Environment.NewLine, urls);
        UpdateBadge();
        UpdateUrlsCount();
    }

    private void UpdateBadge()
    {
        bool hasCustom = AiModelManager.HasCustomUrls(_modelId);
        if (hasCustom)
        {
            TxtStatusBadge.Text = LocalizationManager.Instance.GetString("AiModelUrls_StatusCustom", "🔧 Personalizado");
            BorderStatusBadge.BorderBrush = System.Windows.Media.Brushes.Goldenrod;
        }
        else
        {
            TxtStatusBadge.Text = LocalizationManager.Instance.GetString("AiModelUrls_StatusDefault", "📦 Oficial / Predeterminado");
            BorderStatusBadge.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderDarkBrush");
        }
    }

    private void UpdateUrlsCount()
    {
        var urls = GetUrlsFromTextBox();
        TxtUrlsCount.Text = $"{urls.Count} URL(s)";
    }

    private List<string> GetUrlsFromTextBox()
    {
        return TxtUrls.Text
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u) && (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void TxtUrls_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateUrlsCount();
    }

    private async void TestUrls_Click(object sender, RoutedEventArgs e)
    {
        var urls = GetUrlsFromTextBox();
        if (urls.Count == 0)
        {
            BorderTestResults.Visibility = Visibility.Visible;
            TxtTestResults.Text = "⚠️ No hay URLs válidas (que comiencen por http:// o https://) para probar.";
            return;
        }

        BorderTestResults.Visibility = Visibility.Visible;
        TxtTestResults.Text = "⏳ Comprobando conexiones con los servidores...";

        var results = new List<string>();
        using var client = new HttpClient(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(15),
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) FileFlowStudio/1.0");

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

        TxtTestResults.Text = string.Join(Environment.NewLine, results);
    }

    private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
    {
        var defaults = AiModelManager.GetDefaultUrls(_modelId);
        TxtUrls.Text = string.Join(Environment.NewLine, defaults);
        BorderTestResults.Visibility = Visibility.Collapsed;
        UpdateUrlsCount();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var urls = GetUrlsFromTextBox();
        if (urls.Count == 0)
        {
            MessageBox.Show(
                "Debe especificar al menos una URL de descarga válida para este modelo.",
                "URL requerida",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        AiModelManager.SetCustomUrls(_modelId, urls);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
