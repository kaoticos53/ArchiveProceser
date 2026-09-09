using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.Network.Transports;

/// <summary>
/// Estrategia de transporte para transferencias SMB / Red Local (rutas UNC \\servidor\recurso).
/// </summary>
public sealed class SmbTransportStrategy : INetworkTransportStrategy
{
    public string ProtocolName => "SMB";

    public async Task DownloadAsync(
        NetworkDownloadRequest request,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string uncPathRaw = request.UncPath ?? string.Empty;
        string resolvedUncPath = NetworkTemplateHelper.ResolveRemotePath(uncPathRaw, item);

        if (string.IsNullOrWhiteSpace(resolvedUncPath))
        {
            context.Log("Ruta UNC de red SMB no especificada.", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        string effectiveFileName = !string.IsNullOrWhiteSpace(request.FileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(request.FileNameOverride, item)
            : Path.GetFileName(resolvedUncPath);

        if (string.IsNullOrWhiteSpace(effectiveFileName)) effectiveFileName = "downloaded_smb_file.bin";
        string localFilePath = Path.Combine(request.DestinationDirectory, effectiveFileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se copiaría SMB {resolvedUncPath} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = CreateDownloadResult(item, localFilePath, resolvedUncPath, resolvedUncPath, 1024);
            await context.EmitAsync("Out", dryItem);
            return;
        }

        try
        {
            var storage = context.GetStorage();
            if (!await storage.FileExistsAsync(resolvedUncPath, cancellationToken).ConfigureAwait(false))
            {
                context.Log($"El archivo SMB remoto no existe o es inaccesible: '{resolvedUncPath}'", LogLevel.Error, item.CurrentPath);
                await context.EmitAsync("Error", item);
                return;
            }

            if (await storage.FileExistsAsync(localFilePath, cancellationToken).ConfigureAwait(false) && !request.Overwrite)
            {
                context.Log($"El archivo destino {localFilePath} ya existe y Overwrite=false.", LogLevel.Warning, localFilePath);
                await context.EmitAsync("Error", item);
                return;
            }

            var copyResult = await storage.CopyAsync(
                resolvedUncPath,
                localFilePath,
                request.Overwrite ? StorageCollisionStrategy.Overwrite : StorageCollisionStrategy.Skip,
                cancellationToken).ConfigureAwait(false);

            if (!copyResult.IsSuccess)
            {
                context.Log($"Error copiando archivo SMB: {copyResult.ErrorMessage}", LogLevel.Error, item.CurrentPath);
                await context.EmitAsync("Error", item);
                return;
            }

            if (request.DeleteAfterDownload)
            {
                try
                {
                    await storage.DeleteAsync(resolvedUncPath, permanent: true, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception delEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[SmbTransportStrategy] Remote file cleanup failed: {delEx.Message}");
                }
            }

            long sizeBytes = await storage.GetFileSizeAsync(localFilePath, cancellationToken).ConfigureAwait(false);
            context.Log($"Copia SMB completada: {resolvedUncPath} -> {localFilePath} ({sizeBytes} bytes)", LogLevel.Information, localFilePath);
            var result = CreateDownloadResult(item, localFilePath, resolvedUncPath, resolvedUncPath, sizeBytes);
            await context.EmitAsync("Out", result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"Error en acceso SMB a {resolvedUncPath}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    public async Task UploadAsync(
        NetworkUploadRequest request,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string uncPathRaw = request.UncPath ?? string.Empty;
        string resolvedUncDir = NetworkTemplateHelper.ResolveRemotePath(uncPathRaw, item);

        if (string.IsNullOrWhiteSpace(resolvedUncDir))
        {
            context.Log("Ruta UNC de destino SMB no especificada.", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        string targetFilePath = Path.Combine(resolvedUncDir, Path.GetFileName(item.CurrentPath));

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se copiaría SMB {item.CurrentPath} hacia {targetFilePath}", LogLevel.Information, item.CurrentPath);
            EnrichUploadMetadata(item, targetFilePath, resolvedUncDir, item.FileSizeBytes);
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
            var storage = context.GetStorage();
            if (!await storage.DirectoryExistsAsync(resolvedUncDir, cancellationToken).ConfigureAwait(false))
            {
                await storage.CreateDirectoryAsync(resolvedUncDir, cancellationToken).ConfigureAwait(false);
            }

            var copyResult = await storage.CopyAsync(
                item.CurrentPath,
                targetFilePath,
                StorageCollisionStrategy.Overwrite,
                cancellationToken).ConfigureAwait(false);

            if (!copyResult.IsSuccess)
            {
                context.Log($"Error en subida SMB hacia {targetFilePath}: {copyResult.ErrorMessage}", LogLevel.Error, item.CurrentPath);
                await context.EmitAsync("Error", item);
                return;
            }

            context.Log($"Copia SMB completada: {item.CurrentPath} -> {targetFilePath}", LogLevel.Information, item.CurrentPath);
            EnrichUploadMetadata(item, targetFilePath, resolvedUncDir, item.FileSizeBytes);
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Log($"Error en subida SMB hacia {targetFilePath}: {ex.Message}", LogLevel.Error, item.CurrentPath);
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
        result.Metadata["Protocol"] = "SMB";
        result.Metadata["Network:DownloadedFromUrl"] = remoteUrl;
        result.Metadata["Network:RemotePath"] = remotePath;
        result.Metadata["Network:Protocol"] = "SMB";
        result.Metadata["Network:DownloadTimestamp"] = DateTime.UtcNow.ToString("O");
        result.ExecutionLog.Add($"Copia SMB completada desde {remoteUrl} a {localPath}");
        return result;
    }

    private static void EnrichUploadMetadata(FileItemContext item, string remoteUrl, string remoteDirectory, long sizeBytes)
    {
        item.Metadata["RemoteUrl"] = remoteUrl;
        item.Metadata["RemoteDirectory"] = remoteDirectory;
        item.Metadata["Protocol"] = "SMB";
        item.Metadata["BytesTransferred"] = sizeBytes;
        item.Metadata["Network:UploadedToUrl"] = remoteUrl;
        item.Metadata["Network:RemoteDirectory"] = remoteDirectory;
        item.Metadata["Network:Protocol"] = "SMB";
        item.Metadata["Network:UploadTimestamp"] = DateTime.UtcNow.ToString("O");
        item.Metadata["Network:BytesTransferred"] = sizeBytes;
        item.ExecutionLog.Add($"Subida SMB exitosa a {remoteUrl}");
    }
}
