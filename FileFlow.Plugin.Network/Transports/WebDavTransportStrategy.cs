using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.Network.Transports;

/// <summary>
/// Estrategia de transporte para transferencias WebDAV hacia Nextcloud, ownCloud o servidores corporativos.
/// </summary>
public sealed class WebDavTransportStrategy : INetworkTransportStrategy
{
    public string ProtocolName => "WEBDAV";

    private static readonly HttpClient _sharedClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        ConnectTimeout = TimeSpan.FromSeconds(30)
    });

    public async Task DownloadAsync(
        NetworkDownloadRequest request,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string serverUrlRaw = request.ServerUrl ?? string.Empty;
        string user = request.Username ?? string.Empty;
        string pass = request.Password ?? string.Empty;

        string targetUrl = NetworkTemplateHelper.ResolveRemotePath(serverUrlRaw, item);
        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            context.Log($"URL de WebDAV inválida: '{targetUrl}'", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        string effectiveFileName = !string.IsNullOrWhiteSpace(request.FileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(request.FileNameOverride, item)
            : Path.GetFileName(uri.LocalPath);

        if (string.IsNullOrWhiteSpace(effectiveFileName)) effectiveFileName = "downloaded_webdav_file.bin";
        string localFilePath = Path.Combine(request.DestinationDirectory, effectiveFileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría WebDAV {targetUrl} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = CreateDownloadResult(item, localFilePath, targetUrl, targetUrl, 1024);
            await context.EmitAsync("Out", dryItem);
            return;
        }

        try
        {
            var storage = context.GetStorage();
            if (await storage.FileExistsAsync(localFilePath, cancellationToken) && !request.Overwrite)
            {
                context.Log($"El archivo destino {localFilePath} ya existe y Overwrite=false.", LogLevel.Warning, localFilePath);
                await context.EmitAsync("Error", item);
                return;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 120)));

            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrEmpty(user))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{user}:{pass}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }

            using var response = await _sharedClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var remoteStream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false))
            await using (var localStream = await storage.OpenWriteAsync(localFilePath, timeoutCts.Token).ConfigureAwait(false))
            {
                await remoteStream.CopyToAsync(localStream, timeoutCts.Token).ConfigureAwait(false);
            }

            if (request.DeleteAfterDownload)
            {
                try
                {
                    using var deleteReq = new HttpRequestMessage(HttpMethod.Delete, uri);
                    if (!string.IsNullOrEmpty(user))
                    {
                        var authBytes = Encoding.ASCII.GetBytes($"{user}:{pass}");
                        deleteReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
                    }
                    using var delResp = await _sharedClient.SendAsync(deleteReq, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception delEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebDavTransportStrategy] Delete after download failed: {delEx.Message}");
                }
            }

            long sizeBytes = await storage.GetFileSizeAsync(localFilePath, cancellationToken).ConfigureAwait(false);
            context.Log($"Descarga WebDAV completada: {targetUrl} -> {localFilePath} ({sizeBytes} bytes)", LogLevel.Information, localFilePath);
            var result = CreateDownloadResult(item, localFilePath, targetUrl, targetUrl, sizeBytes);
            await context.EmitAsync("Out", result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"Error en descarga WebDAV desde {targetUrl}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    public async Task UploadAsync(
        NetworkUploadRequest request,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string serverUrlRaw = request.ServerUrl ?? string.Empty;
        string user = request.Username ?? string.Empty;
        string pass = request.Password ?? string.Empty;

        string targetUrl = NetworkTemplateHelper.ResolveRemotePath(serverUrlRaw, item).TrimEnd('/');
        string targetFileUrl = $"{targetUrl}/{Path.GetFileName(item.CurrentPath)}";

        if (!Uri.TryCreate(targetFileUrl, UriKind.Absolute, out var uri))
        {
            context.Log($"URL de destino WebDAV inválida: '{targetFileUrl}'", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se subiría WebDAV {item.CurrentPath} hacia {targetFileUrl}", LogLevel.Information, item.CurrentPath);
            EnrichUploadMetadata(item, targetFileUrl, targetUrl, item.FileSizeBytes);
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(120));

            var storage = context.GetStorage();
            await using var fileStream = await storage.OpenReadAsync(item.CurrentPath, timeoutCts.Token).ConfigureAwait(false);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var req = new HttpRequestMessage(HttpMethod.Put, uri) { Content = content };
            if (!string.IsNullOrEmpty(user))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{user}:{pass}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }

            using var response = await _sharedClient.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            context.Log($"Subida WebDAV completada: {item.CurrentPath} -> {targetFileUrl}", LogLevel.Information, item.CurrentPath);
            EnrichUploadMetadata(item, targetFileUrl, targetUrl, item.FileSizeBytes);
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"Error en subida WebDAV hacia {targetFileUrl}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private static FileItemContext CreateDownloadResult(FileItemContext source, string localPath, string remoteUrl, string remotePath, long sizeBytes)
    {
        var result = source.DeepClone();
        result.CurrentPath = localPath;
        result.FileSizeBytes = sizeBytes;
        result.Metadata["DownloadedPath"] = localPath;
        result.Metadata["RemoteUrl"] = remoteUrl;
        result.Metadata["RemotePath"] = remotePath;
        result.Metadata["Protocol"] = "WebDAV";
        result.Metadata["Network:DownloadedFromUrl"] = remoteUrl;
        result.Metadata["Network:RemotePath"] = remotePath;
        result.Metadata["Network:Protocol"] = "WebDAV";
        result.Metadata["Network:DownloadTimestamp"] = DateTime.UtcNow.ToString("O");
        result.ExecutionLog.Add($"Descarga WebDAV completada desde {remoteUrl} a {localPath}");
        return result;
    }

    private static void EnrichUploadMetadata(FileItemContext item, string remoteUrl, string remoteDirectory, long sizeBytes)
    {
        item.Metadata["RemoteUrl"] = remoteUrl;
        item.Metadata["RemoteDirectory"] = remoteDirectory;
        item.Metadata["Protocol"] = "WebDAV";
        item.Metadata["BytesTransferred"] = sizeBytes;
        item.Metadata["Network:UploadedToUrl"] = remoteUrl;
        item.Metadata["Network:RemoteDirectory"] = remoteDirectory;
        item.Metadata["Network:Protocol"] = "WebDAV";
        item.Metadata["Network:UploadTimestamp"] = DateTime.UtcNow.ToString("O");
        item.Metadata["Network:BytesTransferred"] = sizeBytes;
        item.ExecutionLog.Add($"Subida WebDAV exitosa a {remoteUrl}");
    }
}
