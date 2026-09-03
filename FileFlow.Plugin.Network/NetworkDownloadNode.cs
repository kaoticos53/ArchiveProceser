using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FluentFTP;
using Renci.SshNet;

namespace FileFlow.Plugin.Network;

[NodeDefinition("NetworkDownloadNode_Name", "Network", "NetworkDownloadNode_Desc", PipelineRole.Source,
    "descargar", "download", "http", "https", "ftp", "ftps", "sftp", "ssh", "webdav", "smb", "red", "nube")]
public class NetworkDownloadNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("NetworkDownloadNode_Name", "Descargar de Red / Nube (Network Download)");
    public string Category => "Network";
    public string Description => LocalizationManager.Instance.GetString("NetworkDownloadNode_Desc", "Descarga archivos desde servidores remotos HTTP/HTTPS, FTP/FTPS, SFTP/SSH, WebDAV o recursos SMB de red local hacia una carpeta de destino.");

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
        ["Protocol"] = "HTTP",

        // Parámetros HTTP
        ["SourceUrl"] = "{RemoteUrl}",
        ["TimeoutSeconds"] = 60,

        // Parámetros Servidor (FTP / SFTP / WebDAV)
        ["Host"] = "ftp.example.com",
        ["Port"] = 21,
        ["Username"] = "anonymous",
        ["Password"] = "",
        ["RemoteFilePath"] = "/incoming/{FileName}",

        // Parámetros FTP
        ["Encryption"] = "None",
        ["PassiveMode"] = true,

        // Parámetros SFTP
        ["AuthMethod"] = "Password",
        ["PrivateKeyPath"] = "",
        ["PrivateKeyPassphrase"] = "",

        // Parámetros WebDAV
        ["ServerUrl"] = "https://nextcloud.example.com/remote.php/dav/files/user/{FileName}",

        // Parámetros SMB
        ["UncPath"] = @"\\servidor\compartido\{FileName}",
        ["Domain"] = "",

        // Comunes
        ["DestinationFolder"] = "{GlobalOutputDir}",
        ["FileName"] = "",
        ["Overwrite"] = true,
        ["DeleteAfterDownload"] = false
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        // 1. Selector de Protocolo
        new("Protocol", ParameterEditorType.Dropdown, DefaultValue: "HTTP",
            Options: ["HTTP", "FTP", "SFTP", "WebDAV", "SMB"], DisplayOrder: 1),

        // 2. Parámetros HTTP
        new("SourceUrl", ParameterEditorType.Text, DefaultValue: "{RemoteUrl}", DisplayOrder: 2,
            DependsOnKey: "Protocol", DependsOnValues: ["HTTP"]),
        new("TimeoutSeconds", ParameterEditorType.Number, DefaultValue: 60, DisplayOrder: 3,
            DependsOnKey: "Protocol", DependsOnValues: ["HTTP"]),

        // 3. Parámetros FTP y SFTP compartidos (Host, Port, User, Pass, RemoteFilePath)
        new("Host", ParameterEditorType.Text, DefaultValue: "ftp.example.com", DisplayOrder: 4,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP"]),
        new("Port", ParameterEditorType.Number, DefaultValue: 21, DisplayOrder: 5,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP"]),
        new("Username", ParameterEditorType.Text, DefaultValue: "anonymous", DisplayOrder: 6,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP", "WebDAV", "SMB"]),
        new("Password", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 7,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP", "WebDAV", "SMB"]),
        new("RemoteFilePath", ParameterEditorType.Text, DefaultValue: "/incoming/{FileName}", DisplayOrder: 8,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP"]),

        // 4. Parámetros específicos FTP
        new("Encryption", ParameterEditorType.Dropdown, DefaultValue: "None",
            Options: ["None", "Explicit", "Implicit"], DisplayOrder: 9,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP"]),
        new("PassiveMode", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 10,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP"]),

        // 5. Parámetros específicos SFTP
        new("AuthMethod", ParameterEditorType.Dropdown, DefaultValue: "Password",
            Options: ["Password", "PrivateKey"], DisplayOrder: 11,
            DependsOnKey: "Protocol", DependsOnValues: ["SFTP"]),
        new("PrivateKeyPath", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 12,
            DependsOnKey: "Protocol", DependsOnValues: ["SFTP"]),
        new("PrivateKeyPassphrase", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 13,
            DependsOnKey: "Protocol", DependsOnValues: ["SFTP"]),

        // 6. Parámetros WebDAV
        new("ServerUrl", ParameterEditorType.Text, DefaultValue: "https://nextcloud.example.com/remote.php/dav/files/user/{FileName}", DisplayOrder: 14,
            DependsOnKey: "Protocol", DependsOnValues: ["WebDAV"]),

        // 7. Parámetros SMB
        new("UncPath", ParameterEditorType.Text, DefaultValue: @"\\servidor\compartido\{FileName}", DisplayOrder: 15,
            DependsOnKey: "Protocol", DependsOnValues: ["SMB"]),
        new("Domain", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 16,
            DependsOnKey: "Protocol", DependsOnValues: ["SMB"]),

        // 8. Parámetros Comunes
        new("DestinationFolder", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 20),
        new("FileName", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 21),
        new("Overwrite", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 22),
        new("DeleteAfterDownload", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 23,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP", "WebDAV", "SMB"])
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        string protocol = Parameters.TryGetValue("Protocol", out var pr) ? pr?.ToString() ?? "HTTP" : "HTTP";
        string destFolder = Parameters.TryGetValue("DestinationFolder", out var df) ? df?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";
        string fileNameOverride = Parameters.TryGetValue("FileName", out var fn) ? fn?.ToString() ?? string.Empty : string.Empty;
        bool overwrite = !Parameters.TryGetValue("Overwrite", out var ow) || !bool.TryParse(ow?.ToString(), out bool isOw) || isOw;
        bool deleteAfter = Parameters.TryGetValue("DeleteAfterDownload", out var del) && bool.TryParse(del?.ToString(), out bool isDel) && isDel;

        string resolvedDestDir = NetworkTemplateHelper.ResolveRemotePath(destFolder, item).Replace('/', '\\');
        if (string.IsNullOrWhiteSpace(resolvedDestDir))
        {
            resolvedDestDir = Directory.GetCurrentDirectory();
        }

        Directory.CreateDirectory(resolvedDestDir);

        switch (protocol.ToUpperInvariant())
        {
            case "HTTP":
            case "HTTPS":
                await ExecuteHttpDownloadAsync(item, context, resolvedDestDir, fileNameOverride, overwrite, cancellationToken);
                break;

            case "FTP":
            case "FTPS":
                await ExecuteFtpDownloadAsync(item, context, resolvedDestDir, fileNameOverride, overwrite, deleteAfter, cancellationToken);
                break;

            case "SFTP":
            case "SSH":
                await ExecuteSftpDownloadAsync(item, context, resolvedDestDir, fileNameOverride, overwrite, deleteAfter, cancellationToken);
                break;

            case "WEBDAV":
                await ExecuteWebDavDownloadAsync(item, context, resolvedDestDir, fileNameOverride, overwrite, deleteAfter, cancellationToken);
                break;

            case "SMB":
                await ExecuteSmbDownloadAsync(item, context, resolvedDestDir, fileNameOverride, overwrite, deleteAfter, cancellationToken);
                break;

            default:
                context.Log($"Protocolo de descarga no soportado: '{protocol}'", LogLevel.Error, item.CurrentPath);
                await context.EmitAsync("Error", item);
                break;
        }
    }

    #region Protocolos de Descarga

    private async Task ExecuteHttpDownloadAsync(
        FileItemContext item,
        IFlowExecutionContext context,
        string destDir,
        string fileNameOverride,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        string rawUrl = Parameters.TryGetValue("SourceUrl", out var su) ? su?.ToString() ?? string.Empty : string.Empty;
        int timeoutSec = Parameters.TryGetValue("TimeoutSeconds", out var to) && int.TryParse(to?.ToString(), out int parsedTo) ? Math.Max(5, parsedTo) : 60;

        string targetUrl = NetworkTemplateHelper.ResolveRemotePath(rawUrl, item);
        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            context.Log($"URL de descarga HTTP inválida: '{targetUrl}'", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        string effectiveFileName = !string.IsNullOrWhiteSpace(fileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(fileNameOverride, item)
            : Path.GetFileName(uri.LocalPath);

        if (string.IsNullOrWhiteSpace(effectiveFileName))
        {
            effectiveFileName = $"download_{DateTime.Now:yyyyMMdd_HHmmss}.dat";
        }

        string localFilePath = Path.Combine(destDir, effectiveFileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría HTTP {targetUrl} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = CreateResultItem(item, localFilePath, targetUrl, targetUrl, 1024);
            await context.EmitAsync("Out", dryItem);
            return;
        }

        try
        {
            if (File.Exists(localFilePath) && !overwrite)
            {
                context.Log($"El archivo destino {localFilePath} ya existe y Overwrite=false.", LogLevel.Warning, localFilePath);
                await context.EmitAsync("Error", item);
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSec) };
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var localStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await remoteStream.CopyToAsync(localStream, cancellationToken);
            }

            var info = new FileInfo(localFilePath);
            context.Log($"Descarga HTTP completada: {targetUrl} -> {localFilePath} ({info.Length} bytes)", LogLevel.Information, localFilePath);
            var result = CreateResultItem(item, localFilePath, targetUrl, targetUrl, info.Length);
            await context.EmitAsync("Out", result);
        }
        catch (Exception ex)
        {
            context.Log($"Error en descarga HTTP desde {targetUrl}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private async Task ExecuteFtpDownloadAsync(
        FileItemContext item,
        IFlowExecutionContext context,
        string destDir,
        string fileNameOverride,
        bool overwrite,
        bool deleteAfter,
        CancellationToken cancellationToken)
    {
        string host = Parameters.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
        int port = Parameters.TryGetValue("Port", out var p) && int.TryParse(p?.ToString(), out int parsedPort) ? parsedPort : 21;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;
        string remotePathRaw = Parameters.TryGetValue("RemoteFilePath", out var rp) ? rp?.ToString() ?? string.Empty : string.Empty;
        string encryptionStr = Parameters.TryGetValue("Encryption", out var enc) ? enc?.ToString() ?? "None" : "None";
        bool passive = !Parameters.TryGetValue("PassiveMode", out var pas) || !bool.TryParse(pas?.ToString(), out bool isPas) || isPas;

        string remotePath = NetworkTemplateHelper.ResolveRemotePath(remotePathRaw, item);
        string effectiveFileName = !string.IsNullOrWhiteSpace(fileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(fileNameOverride, item)
            : Path.GetFileName(remotePath);

        if (string.IsNullOrWhiteSpace(effectiveFileName)) effectiveFileName = "downloaded_ftp_file.bin";
        string localFilePath = Path.Combine(destDir, effectiveFileName);
        string remoteUrl = $"ftp://{host}:{port}{remotePath}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría FTP {remoteUrl} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = CreateResultItem(item, localFilePath, remoteUrl, remotePath, 1024);
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
                overwrite ? FtpLocalExists.Overwrite : FtpLocalExists.Skip,
                FtpVerify.None,
                null,
                cancellationToken);

            if (status == FtpStatus.Success || (status == FtpStatus.Skipped && File.Exists(localFilePath)))
            {
                if (deleteAfter)
                {
                    try { await client.DeleteFile(remotePath, cancellationToken); } catch { }
                }
                await client.Disconnect(cancellationToken);

                var info = new FileInfo(localFilePath);
                context.Log($"Descarga FTP completada: {remotePath} -> {localFilePath} ({info.Length} bytes)", LogLevel.Information, localFilePath);
                var result = CreateResultItem(item, localFilePath, remoteUrl, remotePath, info.Length);
                await context.EmitAsync("Out", result);
            }
            else
            {
                await client.Disconnect(cancellationToken);
                context.Log($"Fallo al descargar FTP {remotePath}. Estado: {status}", LogLevel.Warning, localFilePath);
                await context.EmitAsync("Error", item);
            }
        }
        catch (Exception ex)
        {
            context.Log($"Error en descarga FTP desde {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private async Task ExecuteSftpDownloadAsync(
        FileItemContext item,
        IFlowExecutionContext context,
        string destDir,
        string fileNameOverride,
        bool overwrite,
        bool deleteAfter,
        CancellationToken cancellationToken)
    {
        string host = Parameters.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
        int port = Parameters.TryGetValue("Port", out var p) && int.TryParse(p?.ToString(), out int parsedPort) ? parsedPort : 22;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string authMethod = Parameters.TryGetValue("AuthMethod", out var am) ? am?.ToString() ?? "Password" : "Password";
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;
        string keyPath = Parameters.TryGetValue("PrivateKeyPath", out var kp) ? kp?.ToString() ?? string.Empty : string.Empty;
        string passphrase = Parameters.TryGetValue("PrivateKeyPassphrase", out var pp) ? pp?.ToString() ?? string.Empty : string.Empty;
        string remotePathRaw = Parameters.TryGetValue("RemoteFilePath", out var rp) ? rp?.ToString() ?? string.Empty : string.Empty;

        string remotePath = NetworkTemplateHelper.ResolveRemotePath(remotePathRaw, item);
        string effectiveFileName = !string.IsNullOrWhiteSpace(fileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(fileNameOverride, item)
            : Path.GetFileName(remotePath);

        if (string.IsNullOrWhiteSpace(effectiveFileName)) effectiveFileName = "downloaded_sftp_file.bin";
        string localFilePath = Path.Combine(destDir, effectiveFileName);
        string remoteUrl = $"sftp://{user}@{host}:{port}{remotePath}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría SFTP {remoteUrl} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = CreateResultItem(item, localFilePath, remoteUrl, remotePath, 1024);
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

            if (!File.Exists(localFilePath) || overwrite)
            {
                await using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await Task.Run(() => client.DownloadFile(remotePath, fileStream), cancellationToken);
            }

            if (deleteAfter)
            {
                try { client.DeleteFile(remotePath); } catch { }
            }

            client.Disconnect();
            var info = new FileInfo(localFilePath);
            context.Log($"Descarga SFTP completada: {remotePath} -> {localFilePath} ({info.Length} bytes)", LogLevel.Information, localFilePath);
            var result = CreateResultItem(item, localFilePath, remoteUrl, remotePath, info.Length);
            await context.EmitAsync("Out", result);
        }
        catch (Exception ex)
        {
            context.Log($"Error en descarga SFTP desde {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private async Task ExecuteWebDavDownloadAsync(
        FileItemContext item,
        IFlowExecutionContext context,
        string destDir,
        string fileNameOverride,
        bool overwrite,
        bool deleteAfter,
        CancellationToken cancellationToken)
    {
        string serverUrlRaw = Parameters.TryGetValue("ServerUrl", out var su) ? su?.ToString() ?? string.Empty : string.Empty;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;

        string targetUrl = NetworkTemplateHelper.ResolveRemotePath(serverUrlRaw, item);
        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            context.Log($"URL de WebDAV inválida: '{targetUrl}'", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        string effectiveFileName = !string.IsNullOrWhiteSpace(fileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(fileNameOverride, item)
            : Path.GetFileName(uri.LocalPath);

        if (string.IsNullOrWhiteSpace(effectiveFileName)) effectiveFileName = "downloaded_webdav_file.bin";
        string localFilePath = Path.Combine(destDir, effectiveFileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría WebDAV {targetUrl} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = CreateResultItem(item, localFilePath, targetUrl, uri.LocalPath, 1024);
            await context.EmitAsync("Out", dryItem);
            return;
        }

        try
        {
            using var http = new HttpClient();
            if (!string.IsNullOrWhiteSpace(user))
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

            if (deleteAfter)
            {
                try { await http.DeleteAsync(uri, cancellationToken); } catch { }
            }

            var info = new FileInfo(localFilePath);
            context.Log($"Descarga WebDAV completada: {targetUrl} -> {localFilePath} ({info.Length} bytes)", LogLevel.Information, localFilePath);
            var result = CreateResultItem(item, localFilePath, targetUrl, uri.LocalPath, info.Length);
            await context.EmitAsync("Out", result);
        }
        catch (Exception ex)
        {
            context.Log($"Error en descarga WebDAV desde {targetUrl}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private async Task ExecuteSmbDownloadAsync(
        FileItemContext item,
        IFlowExecutionContext context,
        string destDir,
        string fileNameOverride,
        bool overwrite,
        bool deleteAfter,
        CancellationToken cancellationToken)
    {
        string uncPathRaw = Parameters.TryGetValue("UncPath", out var up) ? up?.ToString() ?? string.Empty : string.Empty;
        string resolvedUnc = NetworkTemplateHelper.ResolveRemotePath(uncPathRaw, item).Replace('/', '\\');

        string effectiveFileName = !string.IsNullOrWhiteSpace(fileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(fileNameOverride, item)
            : Path.GetFileName(resolvedUnc);

        if (string.IsNullOrWhiteSpace(effectiveFileName)) effectiveFileName = "downloaded_smb_file.bin";
        string localFilePath = Path.Combine(destDir, effectiveFileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se copiaría SMB desde {resolvedUnc} hacia {localFilePath}", LogLevel.Information, localFilePath);
            var dryItem = CreateResultItem(item, localFilePath, resolvedUnc, resolvedUnc, 1024);
            await context.EmitAsync("Out", dryItem);
            return;
        }

        try
        {
            if (!File.Exists(resolvedUnc))
            {
                context.Log($"El archivo SMB/UNC no existe: {resolvedUnc}", LogLevel.Error, item.CurrentPath);
                await context.EmitAsync("Error", item);
                return;
            }

            await Task.Run(() => File.Copy(resolvedUnc, localFilePath, overwrite), cancellationToken);

            if (deleteAfter)
            {
                try { File.Delete(resolvedUnc); } catch { }
            }

            var info = new FileInfo(localFilePath);
            context.Log($"Copia SMB completada: {resolvedUnc} -> {localFilePath} ({info.Length} bytes)", LogLevel.Information, localFilePath);
            var result = CreateResultItem(item, localFilePath, resolvedUnc, resolvedUnc, info.Length);
            await context.EmitAsync("Out", result);
        }
        catch (Exception ex)
        {
            context.Log($"Error al copiar desde recurso SMB {resolvedUnc}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private static FileItemContext CreateResultItem(
        FileItemContext originalItem,
        string localPath,
        string remoteUrl,
        string remotePath,
        long bytes)
    {
        var result = new FileItemContext(localPath)
        {
            FileSizeBytes = bytes,
            Metadata = new Dictionary<string, object?>(originalItem.Metadata)
            {
                ["RemoteUrl"] = remoteUrl,
                ["RemotePath"] = remotePath,
                ["DownloadedPath"] = localPath,
                ["DownloadedBytes"] = bytes
            }
        };
        return result;
    }

    #endregion
}
