using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FluentFTP;

namespace FileFlow.Plugin.Network;

[NodeDefinition("FtpDownloadNode_Name", "Network", "FtpDownloadNode_Desc", PipelineRole.Source,
    "ftp", "ftps", "descargar", "servidor", "download", "red", "remoto")]
public class FtpDownloadNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("FtpDownloadNode_Name", "Descargar de FTP / FTPS (FTP Download)");
    public string Category => "Network";
    public string Description => LocalizationManager.Instance.GetString("FtpDownloadNode_Desc", "Descarga archivos desde un servidor FTP o FTPS hacia una carpeta local para su posterior procesamiento.");

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
        ["Host"] = "ftp.example.com",
        ["Port"] = 21,
        ["Username"] = "anonymous",
        ["Password"] = "",
        ["RemoteFilePath"] = "/incoming/{FileName}",
        ["DestinationFolder"] = "{GlobalOutputDir}",
        ["FileName"] = "",
        ["Encryption"] = "None",
        ["PassiveMode"] = true,
        ["Overwrite"] = true,
        ["DeleteAfterDownload"] = false
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Host", ParameterEditorType.Text, DefaultValue: "ftp.example.com", DisplayOrder: 1),
        new("Port", ParameterEditorType.Number, DefaultValue: 21, DisplayOrder: 2),
        new("Username", ParameterEditorType.Text, DefaultValue: "anonymous", DisplayOrder: 3),
        new("Password", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 4),
        new("RemoteFilePath", ParameterEditorType.Text, DefaultValue: "/incoming/{FileName}", DisplayOrder: 5),
        new("DestinationFolder", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 6),
        new("FileName", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 7),
        new("Encryption", ParameterEditorType.Dropdown, DefaultValue: "None", Options: ["None", "Explicit", "Implicit"], DisplayOrder: 8),
        new("PassiveMode", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 9),
        new("Overwrite", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 10),
        new("DeleteAfterDownload", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 11)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        string host = Parameters.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
        int port = Parameters.TryGetValue("Port", out var p) && int.TryParse(p?.ToString(), out int parsedPort) ? parsedPort : 21;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;
        string remotePathRaw = Parameters.TryGetValue("RemoteFilePath", out var rp) ? rp?.ToString() ?? string.Empty : string.Empty;
        string destFolder = Parameters.TryGetValue("DestinationFolder", out var df) ? df?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";
        string fileNameOverride = Parameters.TryGetValue("FileName", out var fn) ? fn?.ToString() ?? string.Empty : string.Empty;
        string encryptionStr = Parameters.TryGetValue("Encryption", out var enc) ? enc?.ToString() ?? "None" : "None";
        bool passive = !Parameters.TryGetValue("PassiveMode", out var pas) || !bool.TryParse(pas?.ToString(), out bool isPas) || isPas;
        bool overwrite = !Parameters.TryGetValue("Overwrite", out var ow) || !bool.TryParse(ow?.ToString(), out bool isOw) || isOw;
        bool deleteAfterDownload = Parameters.TryGetValue("DeleteAfterDownload", out var del) && bool.TryParse(del?.ToString(), out bool isDel) && isDel;

        string remotePath = NetworkTemplateHelper.ResolveRemotePath(remotePathRaw, item);
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            context.Log("Ruta remota no especificada o inválida en FtpDownloadNode.", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        string resolvedDestDir = NetworkTemplateHelper.ResolveRemotePath(destFolder, item).Replace('/', '\\');
        if (string.IsNullOrWhiteSpace(resolvedDestDir))
        {
            resolvedDestDir = Directory.GetCurrentDirectory();
        }

        Directory.CreateDirectory(resolvedDestDir);

        string localFileName = !string.IsNullOrWhiteSpace(fileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(fileNameOverride, item)
            : Path.GetFileName(remotePath);

        if (string.IsNullOrWhiteSpace(localFileName))
        {
            localFileName = "downloaded_file.bin";
        }

        string localFilePath = Path.Combine(resolvedDestDir, localFileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría ftp://{host}:{port}{remotePath} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = new FileItemContext(localFilePath)
            {
                FileSizeBytes = item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024,
                Metadata = new Dictionary<string, object?>(item.Metadata)
                {
                    ["RemoteUrl"] = $"ftp://{host}:{port}{remotePath}",
                    ["RemotePath"] = remotePath,
                    ["DownloadedPath"] = localFilePath
                }
            };
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

            var localExistsMode = overwrite ? FtpLocalExists.Overwrite : FtpLocalExists.Skip;
            var status = await client.DownloadFile(
                localFilePath,
                remotePath,
                localExistsMode,
                FtpVerify.None,
                null,
                cancellationToken);

            if (status == FtpStatus.Success || (status == FtpStatus.Skipped && File.Exists(localFilePath)))
            {
                if (deleteAfterDownload)
                {
                    try
                    {
                        await client.DeleteFile(remotePath, cancellationToken);
                        context.Log($"Archivo remoto eliminado del servidor FTP: {remotePath}", LogLevel.Information, localFilePath);
                    }
                    catch (Exception exDel)
                    {
                        context.Log($"No se pudo eliminar el archivo remoto tras la descarga: {exDel.Message}", LogLevel.Warning, localFilePath);
                    }
                }

                await client.Disconnect(cancellationToken);

                var downloadedInfo = new FileInfo(localFilePath);
                context.Log($"Archivo descargado con éxito desde FTP: {remotePath} -> {localFilePath} ({downloadedInfo.Length} bytes)", LogLevel.Information, localFilePath);

                var resultItem = new FileItemContext(localFilePath, isDirectory: false);
                resultItem.Metadata["RemoteUrl"] = $"ftp://{host}:{port}{remotePath}";
                resultItem.Metadata["RemotePath"] = remotePath;
                resultItem.Metadata["DownloadedBytes"] = downloadedInfo.Length;

                // Propagar metadatos previos
                foreach (var kvp in item.Metadata)
                {
                    if (!resultItem.Metadata.ContainsKey(kvp.Key))
                    {
                        resultItem.Metadata[kvp.Key] = kvp.Value;
                    }
                }

                await context.EmitAsync("Out", resultItem);
            }
            else
            {
                context.Log($"Fallo al descargar archivo desde FTP: {remotePath}. Estado: {status}", LogLevel.Warning, localFilePath);
                await client.Disconnect(cancellationToken);
                await context.EmitAsync("Error", item);
            }
        }
        catch (Exception ex)
        {
            context.Log($"Error al conectar o descargar por FTP desde {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }
}
