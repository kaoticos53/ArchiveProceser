using System.Net.Http.Json;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Integrations;

[NodeDefinition("WebhookNotificationNode_Name", "Integrations", "WebhookNotificationNode_Desc")]
public class WebhookNotificationNode : IFlowNode
{
    private static readonly HttpClient HttpClient = new();

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("WebhookNotificationNode_Name", "Notificador Webhook (HTTP POST)");
    public string Category => "Integrations";
    public string Description => LocalizationManager.Instance.GetString("WebhookNotificationNode_Desc", "Envía una petición HTTP POST con un cuerpo JSON dinámico hacia servicios externos (Discord, Slack, n8n, Zapier o servidores propios) al procesar cada archivo.");


    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Failed", typeof(FileItemContext), PortDirection.Output, "Failed")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Url"] = "https://httpbin.org/post",
        ["PayloadTemplate"] = "{\"file\": \"{FileName}\", \"size\": \"{SizeMB} MB\", \"status\": \"processed\"}",
        ["TimeoutSeconds"] = 15
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            string url = Parameters.TryGetValue("Url", out var uVal) ? ParameterHelper.GetString(uVal, "https://httpbin.org/post") : "https://httpbin.org/post";
            string payloadTemplate = Parameters.TryGetValue("PayloadTemplate", out var pVal) ? ParameterHelper.GetString(pVal, "{}") : "{}";
            int timeoutSec = Parameters.TryGetValue("TimeoutSeconds", out var tVal) ? ParameterHelper.GetInt32(tVal, 15) : 15;

            string resolvedUrl = VariableTemplateResolver.Resolve(url, item);
            string resolvedPayload = VariableTemplateResolver.Resolve(payloadTemplate, item);

            if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var parsedUri) ||
                (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
            {
                context.Log($"[WebhookNotificationNode] URL inválida o esquema no soportado: '{resolvedUrl}'", LogLevel.Error);
                item.AddLog($"Webhook Error: URL invalida '{resolvedUrl}'");
                await context.EmitAsync("Failed", item);
                return;
            }

            if (context.IsDryRun)
            {
                context.RegisterPlannedAction(new PlannedAction(
                    Guid.NewGuid(),
                    Id,
                    Name,
                    PlannedOperationType.Custom,
                    item.CurrentPath,
                    resolvedUrl,
                    $"HTTP POST to {resolvedUrl}"
                ));
                item.AddLog($"[DryRun] Planned Webhook POST to {resolvedUrl}");
                await context.EmitAsync("Out", item);
                return;
            }

            using var content = new StringContent(resolvedPayload, Encoding.UTF8, "application/json");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

            using var response = await HttpClient.PostAsync(resolvedUrl, content, cts.Token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                item.AddLog($"Webhook POST succeeded ({response.StatusCode}) to {resolvedUrl}");
                context.Log($"[WebhookNotificationNode] Sent webhook successfully ({response.StatusCode})", LogLevel.Information);
                await context.EmitAsync("Out", item);
            }
            else
            {
                item.AddLog($"Webhook POST returned error {response.StatusCode}");
                context.Log($"[WebhookNotificationNode] HTTP Error: {response.StatusCode}", LogLevel.Warning);
                await context.EmitAsync("Failed", item);
            }
        }
        catch (Exception ex)
        {
            context.Log($"[WebhookNotificationNode] Exception: {ex.Message}", LogLevel.Error);
            item.AddLog($"Webhook Exception: {ex.Message}");
            await context.EmitAsync("Failed", item);
        }
    }
}
