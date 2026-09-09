using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.Network.Transports;

/// <summary>
/// Estrategia de transporte para transferencias HTTP / HTTPS (GET download y POST/PUT multipart upload).
/// </summary>
public sealed class HttpTransportStrategy : INetworkTransportStrategy
{
    public string ProtocolName => "HTTP";

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
        string targetUrl = NetworkTemplateHelper.ResolveRemotePath(request.SourceUrl, item);
        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            context.Log($"URL de descarga HTTP inválida: '{targetUrl}'", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        string effectiveFileName = !string.IsNullOrWhiteSpace(request.FileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(request.FileNameOverride, item)
            : Path.GetFileName(uri.LocalPath);

        if (string.IsNullOrWhiteSpace(effectiveFileName))
        {
            effectiveFileName = $"download_{DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.dat";
        }

        string localFilePath = Path.Combine(request.DestinationDirectory, effectiveFileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría HTTP {targetUrl} hacia {localFilePath}", LogLevel.Information, localFilePath);
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
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, request.TimeoutSeconds)));

            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await _sharedClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var remoteStream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false))
            await using (var localStream = await storage.OpenWriteAsync(localFilePath, timeoutCts.Token).ConfigureAwait(false))
            {
                await remoteStream.CopyToAsync(localStream, timeoutCts.Token).ConfigureAwait(false);
            }

            long sizeBytes = await storage.GetFileSizeAsync(localFilePath, cancellationToken).ConfigureAwait(false);
            context.Log($"Descarga HTTP completada: {targetUrl} -> {localFilePath} ({sizeBytes} bytes)", LogLevel.Information, localFilePath);
            var result = CreateDownloadResult(item, localFilePath, targetUrl, targetUrl, sizeBytes);
            await context.EmitAsync("Out", result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"Error en descarga HTTP desde {targetUrl}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    public async Task UploadAsync(
        NetworkUploadRequest request,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string targetUrl = NetworkTemplateHelper.ResolveRemotePath(request.TargetUrl, item);
        string httpMethod = request.HttpMethod.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            context.Log($"URL de subida HTTP inválida: '{targetUrl}'", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se enviaría {httpMethod} {item.CurrentPath} hacia {targetUrl}", LogLevel.Information, item.CurrentPath);
            EnrichUploadMetadata(item, targetUrl, uri.AbsolutePath, item.FileSizeBytes);
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(120));

            using var req = new HttpRequestMessage(httpMethod == "PUT" ? HttpMethod.Put : HttpMethod.Post, uri);
            if (!string.IsNullOrWhiteSpace(request.AuthHeader))
            {
                req.Headers.TryAddWithoutValidation("Authorization", request.AuthHeader);
            }

            var storage = context.GetStorage();
            using var form = new MultipartFormDataContent();
            await using var fileStream = await storage.OpenReadAsync(item.CurrentPath, timeoutCts.Token).ConfigureAwait(false);
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(streamContent, "file", Path.GetFileName(item.CurrentPath));
            req.Content = form;

            using var response = await _sharedClient.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            context.Log($"Subida HTTP completada: {item.CurrentPath} -> {targetUrl} (Status {response.StatusCode})", LogLevel.Information, item.CurrentPath);
            EnrichUploadMetadata(item, targetUrl, uri.AbsolutePath, item.FileSizeBytes);
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"Error en subida HTTP hacia {targetUrl}: {ex.Message}", LogLevel.Error, item.CurrentPath);
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
        result.Metadata["Protocol"] = "HTTP";
        result.Metadata["Network:DownloadedFromUrl"] = remoteUrl;
        result.Metadata["Network:RemotePath"] = remotePath;
        result.Metadata["Network:Protocol"] = "HTTP";
        result.Metadata["Network:DownloadTimestamp"] = DateTime.UtcNow.ToString("O");
        result.ExecutionLog.Add($"Descarga HTTP completada desde {remoteUrl} a {localPath}");
        return result;
    }

    private static void EnrichUploadMetadata(FileItemContext item, string remoteUrl, string remoteDirectory, long sizeBytes)
    {
        item.Metadata["RemoteUrl"] = remoteUrl;
        item.Metadata["RemoteDirectory"] = remoteDirectory;
        item.Metadata["Protocol"] = "HTTP";
        item.Metadata["BytesTransferred"] = sizeBytes;
        item.Metadata["Network:UploadedToUrl"] = remoteUrl;
        item.Metadata["Network:RemoteDirectory"] = remoteDirectory;
        item.Metadata["Network:Protocol"] = "HTTP";
        item.Metadata["Network:UploadTimestamp"] = DateTime.UtcNow.ToString("O");
        item.Metadata["Network:BytesTransferred"] = sizeBytes;
        item.ExecutionLog.Add($"Subida HTTP exitosa a {remoteUrl}");
    }
}
