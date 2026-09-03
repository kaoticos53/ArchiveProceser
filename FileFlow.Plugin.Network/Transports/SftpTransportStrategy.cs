using System.IO;
using FileFlow.Sdk;
using Renci.SshNet;

namespace FileFlow.Plugin.Network.Transports;

/// <summary>
/// Estrategia de transporte para transferencias SFTP / SSH mediante Renci.SshNet.
/// </summary>
public sealed class SftpTransportStrategy : INetworkTransportStrategy
{
    public string ProtocolName => "SFTP";

    public async Task DownloadAsync(
        NetworkDownloadRequest request,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string host = !string.IsNullOrWhiteSpace(request.Host) ? request.Host : "localhost";
        int port = request.Port > 0 ? request.Port : 22;
        string user = request.Username ?? string.Empty;
        string authMethod = request.AuthMethod ?? "Password";
        string pass = request.Password ?? string.Empty;
        string keyPath = request.PrivateKeyPath ?? string.Empty;
        string passphrase = request.PrivateKeyPassphrase ?? string.Empty;

        string remotePath = NetworkTemplateHelper.ResolveRemotePath(request.RemoteFilePath, item);
        string effectiveFileName = !string.IsNullOrWhiteSpace(request.FileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(request.FileNameOverride, item)
            : Path.GetFileName(remotePath);

        if (string.IsNullOrWhiteSpace(effectiveFileName)) effectiveFileName = "downloaded_sftp_file.bin";
        string localFilePath = Path.Combine(request.DestinationDirectory, effectiveFileName);
        string remoteUrl = $"sftp://{user}@{host}:{port}{remotePath}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría SFTP {remoteUrl} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = CreateDownloadResult(item, localFilePath, remoteUrl, remotePath, 1024);
            await context.EmitAsync("Out", dryItem);
            return;
        }

        try
        {
            ConnectionInfo connInfo;
            if (authMethod.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
                {
                    context.Log($"Archivo de clave privada SSH no encontrado: {keyPath}", LogLevel.Error, item.CurrentPath);
                    await context.EmitAsync("Error", item);
                    return;
                }
                var keyFile = string.IsNullOrEmpty(passphrase) ? new PrivateKeyFile(keyPath) : new PrivateKeyFile(keyPath, passphrase);
                connInfo = new ConnectionInfo(host, port, user, new PrivateKeyAuthenticationMethod(user, keyFile));
            }
            else
            {
                connInfo = new ConnectionInfo(host, port, user, new PasswordAuthenticationMethod(user, pass));
            }

            using var client = new SftpClient(connInfo);
            await Task.Run(() => client.Connect(), cancellationToken);

            if (!client.Exists(remotePath))
            {
                context.Log($"Archivo SFTP no existe: {remotePath}", LogLevel.Error, item.CurrentPath);
                client.Disconnect();
                await context.EmitAsync("Error", item);
                return;
            }

            if (!File.Exists(localFilePath) || request.Overwrite)
            {
                await using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await Task.Run(() => client.DownloadFile(remotePath, fileStream), cancellationToken);
            }

            if (request.DeleteAfterDownload)
            {
                try { client.DeleteFile(remotePath); } catch { }
            }

            client.Disconnect();
            var info = new FileInfo(localFilePath);
            context.Log($"Descarga SFTP completada: {remotePath} -> {localFilePath} ({info.Length} bytes)", LogLevel.Information, localFilePath);
            var result = CreateDownloadResult(item, localFilePath, remoteUrl, remotePath, info.Length);
            await context.EmitAsync("Out", result);
        }
        catch (Exception ex)
        {
            context.Log($"Error en descarga SFTP desde {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
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
        int port = request.Port > 0 ? request.Port : 22;
        string user = request.Username ?? string.Empty;
        string authMethod = request.AuthMethod ?? "Password";
        string pass = request.Password ?? string.Empty;
        string keyPath = request.PrivateKeyPath ?? string.Empty;
        string passphrase = request.PrivateKeyPassphrase ?? string.Empty;

        string remoteDir = NetworkTemplateHelper.ResolveRemotePath(request.RemoteDirectory, item).Replace('\\', '/');
        string remoteFilePath = string.IsNullOrWhiteSpace(remoteDir) || remoteDir == "/"
            ? $"/{Path.GetFileName(item.CurrentPath)}"
            : $"{remoteDir.TrimEnd('/')}/{Path.GetFileName(item.CurrentPath)}";

        string remoteUrl = $"sftp://{user}@{host}:{port}{remoteFilePath}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se subiría SFTP {item.CurrentPath} hacia {remoteUrl}", LogLevel.Information, item.CurrentPath);
            EnrichUploadMetadata(item, remoteUrl, remoteDir, item.FileSizeBytes);
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
            ConnectionInfo connInfo;
            if (authMethod.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
                {
                    context.Log($"Archivo de clave privada SSH no encontrado: {keyPath}", LogLevel.Error, item.CurrentPath);
                    await context.EmitAsync("Error", item);
                    return;
                }
                var keyFile = string.IsNullOrEmpty(passphrase) ? new PrivateKeyFile(keyPath) : new PrivateKeyFile(keyPath, passphrase);
                connInfo = new ConnectionInfo(host, port, user, new PrivateKeyAuthenticationMethod(user, keyFile));
            }
            else
            {
                connInfo = new ConnectionInfo(host, port, user, new PasswordAuthenticationMethod(user, pass));
            }

            using var client = new SftpClient(connInfo);
            await Task.Run(() => client.Connect(), cancellationToken);

            if (!string.IsNullOrWhiteSpace(remoteDir) && remoteDir != "/")
            {
                EnsureSftpDirectory(client, remoteDir);
            }

            await using (var fileStream = File.OpenRead(item.CurrentPath))
            {
                await Task.Run(() => client.UploadFile(fileStream, remoteFilePath, canOverride: true), cancellationToken);
            }

            client.Disconnect();
            context.Log($"Subida SFTP completada: {item.CurrentPath} -> {remoteUrl}", LogLevel.Information, item.CurrentPath);
            EnrichUploadMetadata(item, remoteUrl, remoteDir, item.FileSizeBytes);
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            context.Log($"Error en subida SFTP hacia {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private static void EnsureSftpDirectory(SftpClient client, string remoteDir)
    {
        string[] parts = remoteDir.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = "";
        foreach (var part in parts)
        {
            current += "/" + part;
            if (!client.Exists(current))
            {
                try { client.CreateDirectory(current); } catch { }
            }
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
        result.Metadata["Protocol"] = "SFTP";
        result.Metadata["Network:DownloadedFromUrl"] = remoteUrl;
        result.Metadata["Network:RemotePath"] = remotePath;
        result.Metadata["Network:Protocol"] = "SFTP";
        result.Metadata["Network:DownloadTimestamp"] = DateTime.UtcNow.ToString("O");
        result.ExecutionLog.Add($"Descarga SFTP completada desde {remoteUrl} a {localPath}");
        return result;
    }

    private static void EnrichUploadMetadata(FileItemContext item, string remoteUrl, string remoteDirectory, long sizeBytes)
    {
        item.Metadata["RemoteUrl"] = remoteUrl;
        item.Metadata["RemoteDirectory"] = remoteDirectory;
        item.Metadata["Protocol"] = "SFTP";
        item.Metadata["BytesTransferred"] = sizeBytes;
        item.Metadata["Network:UploadedToUrl"] = remoteUrl;
        item.Metadata["Network:RemoteDirectory"] = remoteDirectory;
        item.Metadata["Network:Protocol"] = "SFTP";
        item.Metadata["Network:UploadTimestamp"] = DateTime.UtcNow.ToString("O");
        item.Metadata["Network:BytesTransferred"] = sizeBytes;
        item.ExecutionLog.Add($"Subida SFTP exitosa a {remoteUrl}");
    }
}
