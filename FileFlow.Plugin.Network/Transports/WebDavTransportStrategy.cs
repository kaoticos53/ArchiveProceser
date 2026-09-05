using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FileFlow.Sdk;

namespace FileFlow.Plugin.Network.Transports;

/// <summary>
/// Estrategia de transporte para transferencias WebDAV hacia Nextcloud, ownCloud o servidores corporativos.
/// </summary>
public sealed class WebDavTransportStrategy : INetworkTransportStrategy
{
    public string ProtocolName => "WEBDAV";

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
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            if (!string.IsNullOrEmpty(user))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{user}:{pass}");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }

            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var localStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await remoteStream.CopyToAsync(localStream, cancellationToken);
            }

            if (request.DeleteAfterDownload)
            {
                try
                {
                    using var deleteReq = new HttpRequestMessage(HttpMethod.Delete, uri);
                    await http.SendAsync(deleteReq, cancellationToken);
                }
                catch { }
            }

            var info = new FileInfo(localFilePath);
            context.Log($"Descarga WebDAV completada: {targetUrl} -> {localFilePath} ({info.Length} bytes)", LogLevel.Information, localFilePath);
            var result = CreateDownloadResult(item, localFilePath, targetUrl, targetUrl, info.Length);
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
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            if (!string.IsNullOrEmpty(user))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{user}:{pass}");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }

            await using var fileStream = File.OpenRead(item.CurrentPath);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await http.PutAsync(uri, content, cancellationToken);
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
