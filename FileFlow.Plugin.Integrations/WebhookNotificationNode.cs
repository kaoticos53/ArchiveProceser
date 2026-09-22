using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Integrations;

[NodeDefinition("WebhookNotificationNode_Name", "Integrations", "WebhookNotificationNode_Desc", PipelineRole.Control,
    "webhook", "http", "post", "notificacion", "api", "rest", "json", "slack", "discord")]
public sealed class WebhookNotificationNode : FlowNodeBase
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        EnableMultipleHttp2Connections = true
    });

    public override string Name => LocalizationManager.Instance.GetString("WebhookNotificationNode_Name", "Notificador Webhook (HTTP POST)");
    public override string Category => "Integrations";
    public override string Description => LocalizationManager.Instance.GetString("WebhookNotificationNode_Desc", "Envía una petición HTTP POST con un cuerpo JSON dinámico hacia servicios externos (Discord, Slack, n8n, Zapier o servidores propios) al procesar cada archivo.");

    public WebhookNotificationNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Failed", typeof(FileItemContext), PortDirection.Output, "Failed")
        ];

        Parameters["Url"] = "https://httpbin.org/post";
        Parameters["PayloadTemplate"] = "{\"file\": \"{FileName}\", \"size\": \"{SizeMB} MB\", \"status\": \"processed\"}";
        Parameters["TimeoutSeconds"] = 15;
    }

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string url = GetParameter("Url", "https://httpbin.org/post");
        string payloadTemplate = GetParameter("PayloadTemplate", "{}");
        int timeoutSec = GetParameter("TimeoutSeconds", 15);

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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"url\": \"{url.Replace("\"", "\\\"")}\"}}";
            context.Log($"[Webhook] Error al enviar notificación HTTP: {ex.Message}", LogLevel.Error, item, durationMs: 0.0, detailsJson: errJson);
            item.AddLog($"Webhook Exception: {ex.Message}");
            await context.EmitAsync("Failed", item);
        }
    }
}
