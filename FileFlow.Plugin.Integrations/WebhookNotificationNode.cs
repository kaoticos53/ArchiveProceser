using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Integrations;

[NodeDefinition("WebhookNotificationNode_Name", "Integrations", "WebhookNotificationNode_Desc", PipelineRole.Control,
    "webhook", "http", "post", "notificacion", "api", "rest", "json", "slack", "discord")]
public class WebhookNotificationNode : IFlowNode
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        EnableMultipleHttp2Connections = true
    });

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
        string url = Parameters.TryGetValue("Url", out var uVal) ? ParameterHelper.GetString(uVal, "https://httpbin.org/post") : "https://httpbin.org/post";
        string payloadTemplate = Parameters.TryGetValue("PayloadTemplate", out var pVal) ? ParameterHelper.GetString(pVal, "{}") : "{}";
        int timeoutSec = Parameters.TryGetValue("TimeoutSeconds", out var tVal) ? ParameterHelper.GetInt32(tVal, 15) : 15;

        string resolvedUrl = VariableTemplateResolver.Resolve(url, item);
        string resolvedPayload = VariableTemplateResolver.Resolve(payloadTemplate, item);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {

            if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var parsedUri) ||
                (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
            {
                context.Log($"[Webhook] URL inválida o protocolo no soportado: '{resolvedUrl}'", LogLevel.Error, item);
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
                context.Log($"[Webhook] [DryRun] Planificado envío HTTP POST a '{resolvedUrl}'", LogLevel.Information, item);
                await context.EmitAsync("Out", item);
                return;
            }

            using var content = new StringContent(resolvedPayload, Encoding.UTF8, "application/json");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

            using var response = await HttpClient.PostAsync(resolvedUrl, content, cts.Token).ConfigureAwait(false);
            sw.Stop();

            string respBody = string.Empty;
            try
            {
                respBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch { }

            string detailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                url = resolvedUrl,
                statusCode = (int)response.StatusCode,
                statusText = response.StatusCode.ToString(),
                payloadSample = resolvedPayload.Length > 250 ? resolvedPayload[..250] + "..." : resolvedPayload,
                responseSample = respBody.Length > 250 ? respBody[..250] + "..." : respBody
            });

            if (response.IsSuccessStatusCode)
            {
                item.AddLog($"Webhook POST succeeded ({response.StatusCode}) to {resolvedUrl}");
                context.Log($"[Webhook] Notificación enviada con éxito (HTTP {(int)response.StatusCode}): '{resolvedUrl}'", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);
                await context.EmitAsync("Out", item);
            }
            else
            {
                item.AddLog($"Webhook POST returned error {response.StatusCode}");
                context.Log($"[Webhook] Servidor respondió con error HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", LogLevel.Warning, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);
                await context.EmitAsync("Failed", item);
            }
        }
        catch (Exception ex)
        {
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"url\": \"{url.Replace("\"", "\\\"")}\"}}";
            context.Log($"[Webhook] Error al enviar notificación HTTP: {ex.Message}", LogLevel.Error, item, durationMs: 0.0, detailsJson: errJson);
            item.AddLog($"Webhook Exception: {ex.Message}");
            await context.EmitAsync("Failed", item);
        }
    }
}
