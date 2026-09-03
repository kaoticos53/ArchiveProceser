using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Renci.SshNet;

namespace FileFlow.Plugin.Network;

[NodeDefinition("SftpDownloadNode_Name", "Network", "SftpDownloadNode_Desc", PipelineRole.Source,
    "sftp", "ssh", "descargar", "seguro", "linux", "servidor", "download")]
public class SftpDownloadNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SftpDownloadNode_Name", "Descargar de SFTP / SSH (SFTP Download)");
    public string Category => "Network";
    public string Description => LocalizationManager.Instance.GetString("SftpDownloadNode_Desc", "Descarga archivos de forma cifrada mediante SFTP (SSH) desde servidores Linux/VPS hacia una carpeta local.");

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
        ["Host"] = "vps.example.com",
        ["Port"] = 22,
        ["Username"] = "root",
        ["AuthMethod"] = "Password",
        ["Password"] = "",
        ["PrivateKeyPath"] = "",
        ["PrivateKeyPassphrase"] = "",
        ["RemoteFilePath"] = "/var/www/uploads/{FileName}",
        ["DestinationFolder"] = "{GlobalOutputDir}",
        ["FileName"] = "",
        ["Overwrite"] = true,
        ["DeleteAfterDownload"] = false
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Host", ParameterEditorType.Text, DefaultValue: "vps.example.com", DisplayOrder: 1),
        new("Port", ParameterEditorType.Number, DefaultValue: 22, DisplayOrder: 2),
        new("Username", ParameterEditorType.Text, DefaultValue: "root", DisplayOrder: 3),
        new("AuthMethod", ParameterEditorType.Dropdown, DefaultValue: "Password", Options: ["Password", "PrivateKey"], DisplayOrder: 4),
        new("Password", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 5),
        new("PrivateKeyPath", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 6),
        new("PrivateKeyPassphrase", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 7),
        new("RemoteFilePath", ParameterEditorType.Text, DefaultValue: "/var/www/uploads/{FileName}", DisplayOrder: 8),
        new("DestinationFolder", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 9),
        new("FileName", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 10),
        new("Overwrite", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 11),
        new("DeleteAfterDownload", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 12)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        string host = Parameters.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
        int port = Parameters.TryGetValue("Port", out var p) && int.TryParse(p?.ToString(), out int parsedPort) ? parsedPort : 22;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string authMethod = Parameters.TryGetValue("AuthMethod", out var am) ? am?.ToString() ?? "Password" : "Password";
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;
        string keyPath = Parameters.TryGetValue("PrivateKeyPath", out var kp) ? kp?.ToString() ?? string.Empty : string.Empty;
        string passphrase = Parameters.TryGetValue("PrivateKeyPassphrase", out var pp) ? pp?.ToString() ?? string.Empty : string.Empty;
        string remotePathRaw = Parameters.TryGetValue("RemoteFilePath", out var rp) ? rp?.ToString() ?? string.Empty : string.Empty;
        string destFolder = Parameters.TryGetValue("DestinationFolder", out var df) ? df?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";
        string fileNameOverride = Parameters.TryGetValue("FileName", out var fn) ? fn?.ToString() ?? string.Empty : string.Empty;
        bool overwrite = !Parameters.TryGetValue("Overwrite", out var ow) || !bool.TryParse(ow?.ToString(), out bool isOw) || isOw;
        bool deleteAfterDownload = Parameters.TryGetValue("DeleteAfterDownload", out var del) && bool.TryParse(del?.ToString(), out bool isDel) && isDel;

        string remotePath = NetworkTemplateHelper.ResolveRemotePath(remotePathRaw, item);
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            context.Log("Ruta remota no especificada o inválida en SftpDownloadNode.", LogLevel.Error, item.CurrentPath);
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
            localFileName = "downloaded_sftp_file.bin";
        }

        string localFilePath = Path.Combine(resolvedDestDir, localFileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría sftp://{user}@{host}:{port}{remotePath} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = new FileItemContext(localFilePath)
            {
                FileSizeBytes = item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024,
                Metadata = new Dictionary<string, object?>(item.Metadata)
                {
                    ["RemoteUrl"] = $"sftp://{user}@{host}:{port}{remotePath}",
                    ["RemotePath"] = remotePath,
                    ["DownloadedPath"] = localFilePath
                }
            };
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

                var keyFile = string.IsNullOrEmpty(passphrase)
                    ? new PrivateKeyFile(keyPath)
                    : new PrivateKeyFile(keyPath, passphrase);

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
                context.Log($"El archivo remoto no existe en el servidor SFTP: {remotePath}", LogLevel.Error, item.CurrentPath);
                client.Disconnect();
                await context.EmitAsync("Error", item);
                return;
            }

            if (File.Exists(localFilePath) && !overwrite)
            {
                context.Log($"El archivo local {localFilePath} ya existe y no se ha configurado sobreescritura.", LogLevel.Information, localFilePath);
            }
            else
            {
                await using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await Task.Run(() => client.DownloadFile(remotePath, fileStream), cancellationToken);
            }

            if (deleteAfterDownload)
            {
                try
                {
                    client.DeleteFile(remotePath);
                    context.Log($"Archivo remoto eliminado del servidor SFTP: {remotePath}", LogLevel.Information, localFilePath);
                }
                catch (Exception exDel)
                {
                    context.Log($"No se pudo eliminar el archivo remoto tras la descarga SFTP: {exDel.Message}", LogLevel.Warning, localFilePath);
                }
            }

            client.Disconnect();

            var downloadedInfo = new FileInfo(localFilePath);
            context.Log($"Archivo descargado con éxito desde SFTP: {remotePath} -> {localFilePath} ({downloadedInfo.Length} bytes)", LogLevel.Information, localFilePath);

            var resultItem = new FileItemContext(localFilePath, isDirectory: false);
            resultItem.Metadata["RemoteUrl"] = $"sftp://{host}:{port}{remotePath}";
            resultItem.Metadata["RemotePath"] = remotePath;
            resultItem.Metadata["DownloadedBytes"] = downloadedInfo.Length;

            // Propagar metadatos
            foreach (var kvp in item.Metadata)
            {
                if (!resultItem.Metadata.ContainsKey(kvp.Key))
                {
                    resultItem.Metadata[kvp.Key] = kvp.Value;
                }
            }

            await context.EmitAsync("Out", resultItem);
        }
        catch (Exception ex)
        {
            context.Log($"Error al conectar o descargar por SFTP desde {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }
}
