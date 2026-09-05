using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FileFlow.Sdk;

namespace FileFlow.Plugin.Network.Transports;

/// <summary>
/// Estrategia de transporte para transferencias HTTP / HTTPS (GET download y POST/PUT multipart upload).
/// </summary>
public sealed class HttpTransportStrategy : INetworkTransportStrategy
{
    public string ProtocolName => "HTTP";

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
            effectiveFileName = $"download_{DateTime.Now:yyyyMMdd_HHmmss}.dat";
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
            if (File.Exists(localFilePath) && !request.Overwrite)
            {
                context.Log($"El archivo destino {localFilePath} ya existe y Overwrite=false.", LogLevel.Warning, localFilePath);
                await context.EmitAsync("Error", item);
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(5, request.TimeoutSeconds)) };
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var localStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await remoteStream.CopyToAsync(localStream, cancellationToken);
            }

            var info = new FileInfo(localFilePath);
            context.Log($"Descarga HTTP completada: {targetUrl} -> {localFilePath} ({info.Length} bytes)", LogLevel.Information, localFilePath);
            var result = CreateDownloadResult(item, localFilePath, targetUrl, targetUrl, info.Length);
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
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            if (!string.IsNullOrWhiteSpace(request.AuthHeader))
            {
                http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", request.AuthHeader);
            }

            using var form = new MultipartFormDataContent();
            await using var fileStream = File.OpenRead(item.CurrentPath);
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(streamContent, "file", Path.GetFileName(item.CurrentPath));

            HttpResponseMessage response;
            if (httpMethod == "PUT")
            {
                response = await http.PutAsync(uri, form, cancellationToken);
            }
            else
            {
                response = await http.PostAsync(uri, form, cancellationToken);
            }

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
