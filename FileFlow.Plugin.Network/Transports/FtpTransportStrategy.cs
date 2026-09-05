using System.IO;
using FileFlow.Sdk;
using FluentFTP;

namespace FileFlow.Plugin.Network.Transports;

/// <summary>
/// Estrategia de transporte para transferencias FTP / FTPS mediante FluentFTP.
/// </summary>
public sealed class FtpTransportStrategy : INetworkTransportStrategy
{
    public string ProtocolName => "FTP";

    public async Task DownloadAsync(
        NetworkDownloadRequest request,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string host = !string.IsNullOrWhiteSpace(request.Host) ? request.Host : "localhost";
        int port = request.Port > 0 ? request.Port : 21;
        string user = request.Username ?? string.Empty;
        string pass = request.Password ?? string.Empty;
        string encryptionStr = request.Encryption ?? "None";
        bool passive = request.PassiveMode;

        string remotePath = NetworkTemplateHelper.ResolveRemotePath(request.RemoteFilePath, item);
        string effectiveFileName = !string.IsNullOrWhiteSpace(request.FileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(request.FileNameOverride, item)
            : Path.GetFileName(remotePath);

        if (string.IsNullOrWhiteSpace(effectiveFileName)) effectiveFileName = "downloaded_ftp_file.bin";
        string localFilePath = Path.Combine(request.DestinationDirectory, effectiveFileName);
        string remoteUrl = $"ftp://{host}:{port}{remotePath}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría FTP {remoteUrl} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = CreateDownloadResult(item, localFilePath, remoteUrl, remotePath, 1024);
            await context.EmitAsync("Out", dryItem);
            return;
        }

        try
        {
            var config = new FtpConfig
            {
                DataConnectionType = passive ? FtpDataConnectionType.PASV : FtpDataConnectionType.PORT,
                EncryptionMode = encryptionStr.Equals("Explicit", StringComparison.OrdinalIgnoreCase) ? FtpEncryptionMode.Explicit :
                                 encryptionStr.Equals("Implicit", StringComparison.OrdinalIgnoreCase) ? FtpEncryptionMode.Implicit :
                                 FtpEncryptionMode.None
            };

            await using var client = new AsyncFtpClient(host, user, pass, port, config);
            await client.Connect(cancellationToken);

            var status = await client.DownloadFile(
                localFilePath,
                remotePath,
                request.Overwrite ? FtpLocalExists.Overwrite : FtpLocalExists.Skip,
                FtpVerify.None,
                null,
                cancellationToken);

            if (status == FtpStatus.Success || (status == FtpStatus.Skipped && File.Exists(localFilePath)))
            {
                if (request.DeleteAfterDownload)
                {
                    try { await client.DeleteFile(remotePath, cancellationToken); } catch { }
                }
                await client.Disconnect(cancellationToken);

                var info = new FileInfo(localFilePath);
                context.Log($"Descarga FTP completada: {remotePath} -> {localFilePath} ({info.Length} bytes)", LogLevel.Information, localFilePath);
                var result = CreateDownloadResult(item, localFilePath, remoteUrl, remotePath, info.Length);
                await context.EmitAsync("Out", result);
            }
            else
            {
                await client.Disconnect(cancellationToken);
                context.Log($"Fallo al descargar FTP {remotePath}. Estado: {status}", LogLevel.Warning, localFilePath);
                await context.EmitAsync("Error", item);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"Error en descarga FTP desde {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    public async Task UploadAsync(
        NetworkUploadRequest request,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string host = !string.IsNullOrWhiteSpace(request.Host) ? request.Host : "localhost";
        int port = request.Port > 0 ? request.Port : 21;
        string user = request.Username ?? string.Empty;
        string pass = request.Password ?? string.Empty;
        string encryptionStr = request.Encryption ?? "None";
        bool passive = request.PassiveMode;

        string remoteDir = NetworkTemplateHelper.ResolveRemotePath(request.RemoteDirectory, item).Replace('\\', '/');
        string remoteFilePath = string.IsNullOrWhiteSpace(remoteDir) || remoteDir == "/"
            ? $"/{Path.GetFileName(item.CurrentPath)}"
            : $"{remoteDir.TrimEnd('/')}/{Path.GetFileName(item.CurrentPath)}";

        string remoteUrl = $"ftp://{host}:{port}{remoteFilePath}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se subiría FTP {item.CurrentPath} hacia {remoteUrl}", LogLevel.Information, item.CurrentPath);
            EnrichUploadMetadata(item, remoteUrl, remoteDir, item.FileSizeBytes);
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
            var config = new FtpConfig
            {
                DataConnectionType = passive ? FtpDataConnectionType.PASV : FtpDataConnectionType.PORT,
                EncryptionMode = encryptionStr.Equals("Explicit", StringComparison.OrdinalIgnoreCase) ? FtpEncryptionMode.Explicit :
                                 encryptionStr.Equals("Implicit", StringComparison.OrdinalIgnoreCase) ? FtpEncryptionMode.Implicit :
                                 FtpEncryptionMode.None
            };

            await using var client = new AsyncFtpClient(host, user, pass, port, config);
            await client.Connect(cancellationToken);

            if (!string.IsNullOrWhiteSpace(remoteDir) && remoteDir != "/")
            {
                await client.CreateDirectory(remoteDir, true, cancellationToken);
            }

            var status = await client.UploadFile(
                item.CurrentPath,
                remoteFilePath,
                FtpRemoteExists.Overwrite,
                createRemoteDir: true,
                verifyOptions: FtpVerify.None,
                progress: null,
                token: cancellationToken);

            await client.Disconnect(cancellationToken);

            if (status == FtpStatus.Success || status == FtpStatus.Skipped)
            {
                context.Log($"Subida FTP completada: {item.CurrentPath} -> {remoteUrl}", LogLevel.Information, item.CurrentPath);
                EnrichUploadMetadata(item, remoteUrl, remoteDir, item.FileSizeBytes);
                await context.EmitAsync("Out", item);
            }
            else
            {
                context.Log($"Fallo en subida FTP de {item.CurrentPath}. Estado: {status}", LogLevel.Warning, item.CurrentPath);
                await context.EmitAsync("Error", item);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"Error en subida FTP hacia {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
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
        result.Metadata["Protocol"] = "FTP";
        result.Metadata["Network:DownloadedFromUrl"] = remoteUrl;
        result.Metadata["Network:RemotePath"] = remotePath;
        result.Metadata["Network:Protocol"] = "FTP";
        result.Metadata["Network:DownloadTimestamp"] = DateTime.UtcNow.ToString("O");
        result.ExecutionLog.Add($"Descarga FTP completada desde {remoteUrl} a {localPath}");
        return result;
    }

    private static void EnrichUploadMetadata(FileItemContext item, string remoteUrl, string remoteDirectory, long sizeBytes)
    {
        item.Metadata["RemoteUrl"] = remoteUrl;
        item.Metadata["RemoteDirectory"] = remoteDirectory;
        item.Metadata["Protocol"] = "FTP";
        item.Metadata["BytesTransferred"] = sizeBytes;
        item.Metadata["Network:UploadedToUrl"] = remoteUrl;
        item.Metadata["Network:RemoteDirectory"] = remoteDirectory;
        item.Metadata["Network:Protocol"] = "FTP";
        item.Metadata["Network:UploadTimestamp"] = DateTime.UtcNow.ToString("O");
        item.Metadata["Network:BytesTransferred"] = sizeBytes;
        item.ExecutionLog.Add($"Subida FTP exitosa a {remoteUrl}");
    }
}
