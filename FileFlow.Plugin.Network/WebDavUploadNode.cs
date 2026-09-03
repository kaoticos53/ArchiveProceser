using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Network;

[NodeDefinition("WebDavUploadNode_Name", "Network", "WebDavUploadNode_Desc", PipelineRole.Sink,
    "webdav", "nextcloud", "owncloud", "nube", "cloud", "subir", "servidor")]
public class WebDavUploadNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("WebDavUploadNode_Name", "Subir a WebDAV / Nextcloud (WebDAV Upload)");
    public string Category => "Network";
    public string Description => LocalizationManager.Instance.GetString("WebDavUploadNode_Desc", "Sube archivos a servidores WebDAV, nubes privadas Nextcloud / ownCloud y almacenamiento NAS.");

    public IReadOnlyList<NodePort> Inputs { get; } =
    [
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    ];

    public IReadOnlyList<NodePort> Outputs { get; } =
    [
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    ];

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ServerUrl"] = "https://cloud.example.com/remote.php/dav/files/user",
        ["Username"] = "admin",
        ["Password"] = "",
        ["RemoteDirectory"] = "/Backups/{Year}/{Month}",
        ["TimeoutSeconds"] = 60
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("ServerUrl", ParameterEditorType.Text, DefaultValue: "https://cloud.example.com/remote.php/dav/files/user", DisplayOrder: 1),
        new("Username", ParameterEditorType.Text, DefaultValue: "admin", DisplayOrder: 2),
        new("Password", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 3),
        new("RemoteDirectory", ParameterEditorType.Text, DefaultValue: "/Backups/{Year}/{Month}", DisplayOrder: 4),
        new("TimeoutSeconds", ParameterEditorType.Number, DefaultValue: 60, DisplayOrder: 5)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            await context.EmitAsync("Error", item);
            return;
        }

        string serverUrl = Parameters.TryGetValue("ServerUrl", out var su) ? su?.ToString()?.TrimEnd('/') ?? string.Empty : string.Empty;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;
        string remoteDirTemplate = Parameters.TryGetValue("RemoteDirectory", out var rd) ? rd?.ToString() ?? "/Backups" : "/Backups";
        int timeoutSec = Parameters.TryGetValue("TimeoutSeconds", out var to) && int.TryParse(to?.ToString(), out int parsedTo) ? Math.Max(5, parsedTo) : 60;

        string resolvedDir = NetworkTemplateHelper.ResolveRemotePath(remoteDirTemplate, item).Trim('/');
        string targetUrl = $"{serverUrl}/{resolvedDir}/{Uri.EscapeDataString(item.FileName)}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Archivo {item.FileName} se subiría por WebDAV a {targetUrl}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = targetUrl;
            item.Metadata["RemotePath"] = $"{resolvedDir}/{item.FileName}";
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            using var http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(timeoutSec)
            };

            if (!string.IsNullOrWhiteSpace(user) || !string.IsNullOrWhiteSpace(pass))
            {
                var authBytes = Encoding.UTF8.GetBytes($"{user}:{pass}");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }

            // Asegurar directorios remotos con MKCOL
            await EnsureWebDavDirectoryAsync(http, serverUrl, resolvedDir, cancellationToken);

            // Subir el archivo mediante HTTP PUT
            await using var stream = File.OpenRead(item.CurrentPath);
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await http.PutAsync(targetUrl, content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                context.Log($"Archivo {item.FileName} subido correctamente por WebDAV a {targetUrl}", LogLevel.Information, item.CurrentPath);
                item.Metadata["RemoteUrl"] = targetUrl;
                item.Metadata["RemotePath"] = $"{resolvedDir}/{item.FileName}";
                item.Metadata["UploadedBytes"] = item.FileSizeBytes > 0 ? item.FileSizeBytes : new FileInfo(item.CurrentPath).Length;
                await context.EmitAsync("Out", item);
            }
            else
            {
                context.Log($"Fallo en la subida WebDAV de {item.FileName}. Código HTTP: {response.StatusCode} ({(int)response.StatusCode})", LogLevel.Warning, item.CurrentPath);
                await context.EmitAsync("Error", item);
            }
        }
        catch (Exception ex)
        {
            context.Log($"Error en transferencia WebDAV hacia {serverUrl}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private static async Task EnsureWebDavDirectoryAsync(HttpClient http, string serverUrl, string remoteDir, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteDir)) return;

        string[] parts = remoteDir.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = serverUrl;

        foreach (var part in parts)
        {
            current = $"{current}/{Uri.EscapeDataString(part)}";
            try
            {
                var mkcolRequest = new HttpRequestMessage(new HttpMethod("MKCOL"), current);
                await http.SendAsync(mkcolRequest, cancellationToken);
            }
            catch
            {
                // Ignorar si el directorio ya existe
            }
        }
    }
}
