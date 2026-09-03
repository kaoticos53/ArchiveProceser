using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FluentFTP;
using Renci.SshNet;

namespace FileFlow.Plugin.Network;

[NodeDefinition("NetworkUploadNode_Name", "Network", "NetworkUploadNode_Desc", PipelineRole.Sink,
    "subir", "upload", "transferir", "http", "https", "ftp", "ftps", "sftp", "ssh", "webdav", "smb", "red", "nube")]
public class NetworkUploadNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("NetworkUploadNode_Name", "Subir a Red / Nube (Network Upload)");
    public string Category => "Network";
    public string Description => LocalizationManager.Instance.GetString("NetworkUploadNode_Desc", "Transfiere archivos hacia servidores remotos HTTP/HTTPS (POST/PUT), FTP/FTPS, SFTP/SSH, WebDAV o recursos SMB de red local.");

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
        ["Protocol"] = "FTP",

        // Parámetros HTTP / API Webhook
        ["TargetUrl"] = "https://api.example.com/upload",
        ["HttpMethod"] = "POST",
        ["AuthHeader"] = "",

        // Parámetros Servidor (FTP / SFTP)
        ["Host"] = "ftp.example.com",
        ["Port"] = 21,
        ["Username"] = "anonymous",
        ["Password"] = "",
        ["RemoteDirectory"] = "/uploads/{Year}/{Month}",

        // Parámetros FTP
        ["Encryption"] = "None",
        ["PassiveMode"] = true,

        // Parámetros SFTP
        ["AuthMethod"] = "Password",
        ["PrivateKeyPath"] = "",
        ["PrivateKeyPassphrase"] = "",

        // Parámetros WebDAV
        ["ServerUrl"] = "https://nextcloud.example.com/remote.php/dav/files/user/{Year}/{Month}",

        // Parámetros SMB
        ["UncPath"] = @"\\servidor\compartido\{Year}\{Month}",
        ["Domain"] = ""
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        // 1. Selector de Protocolo
        new("Protocol", ParameterEditorType.Dropdown, DefaultValue: "FTP",
            Options: ["HTTP", "FTP", "SFTP", "WebDAV", "SMB"], DisplayOrder: 1),

        // 2. Parámetros HTTP
        new("TargetUrl", ParameterEditorType.Text, DefaultValue: "https://api.example.com/upload", DisplayOrder: 2,
            DependsOnKey: "Protocol", DependsOnValues: ["HTTP"]),
        new("HttpMethod", ParameterEditorType.Dropdown, DefaultValue: "POST", Options: ["POST", "PUT"], DisplayOrder: 3,
            DependsOnKey: "Protocol", DependsOnValues: ["HTTP"]),
        new("AuthHeader", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 4,
            DependsOnKey: "Protocol", DependsOnValues: ["HTTP"]),

        // 3. Parámetros FTP y SFTP compartidos
        new("Host", ParameterEditorType.Text, DefaultValue: "ftp.example.com", DisplayOrder: 5,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP"]),
        new("Port", ParameterEditorType.Number, DefaultValue: 21, DisplayOrder: 6,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP"]),
        new("Username", ParameterEditorType.Text, DefaultValue: "anonymous", DisplayOrder: 7,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP", "WebDAV", "SMB"]),
        new("Password", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 8,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP", "WebDAV", "SMB"]),
        new("RemoteDirectory", ParameterEditorType.Text, DefaultValue: "/uploads/{Year}/{Month}", DisplayOrder: 9,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP", "WebDAV"]),

        // 4. Parámetros específicos FTP
        new("Encryption", ParameterEditorType.Dropdown, DefaultValue: "None",
            Options: ["None", "Explicit", "Implicit"], DisplayOrder: 10,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP"]),
        new("PassiveMode", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 11,
            DependsOnKey: "Protocol", DependsOnValues: ["FTP"]),

        // 5. Parámetros específicos SFTP
        new("AuthMethod", ParameterEditorType.Dropdown, DefaultValue: "Password",
            Options: ["Password", "PrivateKey"], DisplayOrder: 12,
            DependsOnKey: "Protocol", DependsOnValues: ["SFTP"]),
        new("PrivateKeyPath", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 13,
            DependsOnKey: "Protocol", DependsOnValues: ["SFTP"]),
        new("PrivateKeyPassphrase", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 14,
            DependsOnKey: "Protocol", DependsOnValues: ["SFTP"]),

        // 6. Parámetros WebDAV
        new("ServerUrl", ParameterEditorType.Text, DefaultValue: "https://nextcloud.example.com/remote.php/dav/files/user/{Year}/{Month}", DisplayOrder: 15,
            DependsOnKey: "Protocol", DependsOnValues: ["WebDAV"]),

        // 7. Parámetros SMB
        new("UncPath", ParameterEditorType.Text, DefaultValue: @"\\servidor\compartido\{Year}\{Month}", DisplayOrder: 16,
            DependsOnKey: "Protocol", DependsOnValues: ["SMB"]),
        new("Domain", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 17,
            DependsOnKey: "Protocol", DependsOnValues: ["SMB"])
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log("El archivo local no existe o la ruta está vacía.", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        string protocol = Parameters.TryGetValue("Protocol", out var pr) ? pr?.ToString() ?? "FTP" : "FTP";

        switch (protocol.ToUpperInvariant())
        {
            case "HTTP":
            case "HTTPS":
                await ExecuteHttpUploadAsync(item, context, cancellationToken);
                break;

            case "FTP":
            case "FTPS":
                await ExecuteFtpUploadAsync(item, context, cancellationToken);
                break;

            case "SFTP":
            case "SSH":
                await ExecuteSftpUploadAsync(item, context, cancellationToken);
                break;

            case "WEBDAV":
                await ExecuteWebDavUploadAsync(item, context, cancellationToken);
                break;

            case "SMB":
                await ExecuteSmbUploadAsync(item, context, cancellationToken);
                break;

            default:
                context.Log($"Protocolo de subida no soportado: '{protocol}'", LogLevel.Error, item.CurrentPath);
                await context.EmitAsync("Error", item);
                break;
        }
    }

    #region Protocolos de Subida

    private async Task ExecuteHttpUploadAsync(
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string rawUrl = Parameters.TryGetValue("TargetUrl", out var tu) ? tu?.ToString() ?? string.Empty : string.Empty;
        string httpMethod = Parameters.TryGetValue("HttpMethod", out var hm) ? hm?.ToString() ?? "POST" : "POST";
        string authHeader = Parameters.TryGetValue("AuthHeader", out var ah) ? ah?.ToString() ?? string.Empty : string.Empty;

        string targetUrl = NetworkTemplateHelper.ResolveRemotePath(rawUrl, item);
        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            context.Log($"URL de subida HTTP inválida: '{targetUrl}'", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Archivo {item.FileName} se enviaría vía HTTP {httpMethod} a {targetUrl}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = targetUrl;
            item.Metadata["UploadedBytes"] = item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024;
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
            using var http = new HttpClient();
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
            }

            using var form = new MultipartFormDataContent();
            await using var fileStream = File.OpenRead(item.CurrentPath);
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(streamContent, "file", item.FileName);

            HttpResponseMessage response = httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase)
                ? await http.PutAsync(uri, form, cancellationToken)
                : await http.PostAsync(uri, form, cancellationToken);

            response.EnsureSuccessStatusCode();

            long fileSize = item.FileSizeBytes > 0 ? item.FileSizeBytes : new FileInfo(item.CurrentPath).Length;
            context.Log($"Archivo {item.FileName} enviado con éxito vía HTTP {httpMethod} a {targetUrl}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = targetUrl;
            item.Metadata["UploadedBytes"] = fileSize;
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            context.Log($"Error al enviar archivo vía HTTP a {targetUrl}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private async Task ExecuteFtpUploadAsync(
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string host = Parameters.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
        int port = Parameters.TryGetValue("Port", out var p) && int.TryParse(p?.ToString(), out int parsedPort) ? parsedPort : 21;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;
        string remoteDirTemplate = Parameters.TryGetValue("RemoteDirectory", out var rd) ? rd?.ToString() ?? "/uploads" : "/uploads";
        string encryptionStr = Parameters.TryGetValue("Encryption", out var enc) ? enc?.ToString() ?? "None" : "None";
        bool passive = !Parameters.TryGetValue("PassiveMode", out var pas) || !bool.TryParse(pas?.ToString(), out bool isPas) || isPas;

        string resolvedDir = NetworkTemplateHelper.ResolveRemotePath(remoteDirTemplate, item);
        string remotePath = $"{resolvedDir.TrimEnd('/')}/{item.FileName}";
        string remoteUrl = $"ftp://{host}:{port}{remotePath}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Archivo {item.FileName} se subiría a {remoteUrl}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = remoteUrl;
            item.Metadata["RemotePath"] = remotePath;
            item.Metadata["UploadedBytes"] = item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024;
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

            var status = await client.UploadFile(
                item.CurrentPath,
                remotePath,
                FtpRemoteExists.Overwrite,
                createRemoteDir: true,
                token: cancellationToken);

            if (status == FtpStatus.Success)
            {
                long fileSize = item.FileSizeBytes > 0 ? item.FileSizeBytes : new FileInfo(item.CurrentPath).Length;
                context.Log($"Archivo {item.FileName} subido correctamente a {remoteUrl}", LogLevel.Information, item.CurrentPath);
                item.Metadata["RemoteUrl"] = remoteUrl;
                item.Metadata["RemotePath"] = remotePath;
                item.Metadata["UploadedBytes"] = fileSize;
                await client.Disconnect(cancellationToken);
                await context.EmitAsync("Out", item);
            }
            else
            {
                context.Log($"Fallo en la subida FTP de {item.FileName}. Estado: {status}", LogLevel.Warning, item.CurrentPath);
                await client.Disconnect(cancellationToken);
                await context.EmitAsync("Error", item);
            }
        }
        catch (Exception ex)
        {
            context.Log($"Error al conectar o transferir por FTP a {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private async Task ExecuteSftpUploadAsync(
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string host = Parameters.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
        int port = Parameters.TryGetValue("Port", out var p) && int.TryParse(p?.ToString(), out int parsedPort) ? parsedPort : 22;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string authMethod = Parameters.TryGetValue("AuthMethod", out var am) ? am?.ToString() ?? "Password" : "Password";
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;
        string keyPath = Parameters.TryGetValue("PrivateKeyPath", out var kp) ? kp?.ToString() ?? string.Empty : string.Empty;
        string passphrase = Parameters.TryGetValue("PrivateKeyPassphrase", out var pp) ? pp?.ToString() ?? string.Empty : string.Empty;
        string remoteDirTemplate = Parameters.TryGetValue("RemoteDirectory", out var rd) ? rd?.ToString() ?? "/uploads" : "/uploads";

        string resolvedDir = NetworkTemplateHelper.ResolveRemotePath(remoteDirTemplate, item);
        string remotePath = $"{resolvedDir.TrimEnd('/')}/{item.FileName}";
        string remoteUrl = $"sftp://{user}@{host}:{port}{remotePath}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Archivo {item.FileName} se subiría a {remoteUrl}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = remoteUrl;
            item.Metadata["RemotePath"] = remotePath;
            item.Metadata["UploadedBytes"] = item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024;
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

            CreateSftpDirectoriesRecursively(client, resolvedDir);

            await using (var fileStream = File.OpenRead(item.CurrentPath))
            {
                await Task.Run(() => client.UploadFile(fileStream, remotePath, canOverride: true), cancellationToken);
            }

            client.Disconnect();

            long fileSize = item.FileSizeBytes > 0 ? item.FileSizeBytes : new FileInfo(item.CurrentPath).Length;
            context.Log($"Archivo {item.FileName} subido correctamente a {remoteUrl}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = remoteUrl;
            item.Metadata["RemotePath"] = remotePath;
            item.Metadata["UploadedBytes"] = fileSize;
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            context.Log($"Error al conectar o transferir por SFTP a {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private async Task ExecuteWebDavUploadAsync(
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string serverUrlRaw = Parameters.TryGetValue("ServerUrl", out var su) ? su?.ToString() ?? string.Empty : string.Empty;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;

        string resolvedBaseUrl = NetworkTemplateHelper.ResolveRemotePath(serverUrlRaw, item).TrimEnd('/');
        string targetUrl = $"{resolvedBaseUrl}/{item.FileName}";

        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            context.Log($"URL de WebDAV inválida: '{targetUrl}'", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Archivo {item.FileName} se subiría a WebDAV {targetUrl}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = targetUrl;
            item.Metadata["UploadedBytes"] = item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024;
            await context.EmitAsync("Out", item);
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

            await using var fileStream = File.OpenRead(item.CurrentPath);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await http.PutAsync(uri, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            long fileSize = item.FileSizeBytes > 0 ? item.FileSizeBytes : new FileInfo(item.CurrentPath).Length;
            context.Log($"Archivo {item.FileName} subido correctamente a WebDAV {targetUrl}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = targetUrl;
            item.Metadata["UploadedBytes"] = fileSize;
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            context.Log($"Error al transferir a WebDAV {targetUrl}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private async Task ExecuteSmbUploadAsync(
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string uncPathRaw = Parameters.TryGetValue("UncPath", out var up) ? up?.ToString() ?? string.Empty : string.Empty;
        string resolvedUncDir = NetworkTemplateHelper.ResolveRemotePath(uncPathRaw, item).Replace('/', '\\');
        string targetFilePath = Path.Combine(resolvedUncDir, item.FileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Archivo {item.FileName} se copiaría a recurso SMB {targetFilePath}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = targetFilePath;
            item.Metadata["RemotePath"] = targetFilePath;
            item.Metadata["UploadedBytes"] = item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024;
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
            if (!Directory.Exists(resolvedUncDir))
            {
                Directory.CreateDirectory(resolvedUncDir);
            }

            await Task.Run(() => File.Copy(item.CurrentPath, targetFilePath, overwrite: true), cancellationToken);

            long fileSize = item.FileSizeBytes > 0 ? item.FileSizeBytes : new FileInfo(item.CurrentPath).Length;
            context.Log($"Archivo {item.FileName} copiado con éxito a SMB {targetFilePath}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = targetFilePath;
            item.Metadata["RemotePath"] = targetFilePath;
            item.Metadata["UploadedBytes"] = fileSize;
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            context.Log($"Error al copiar archivo a recurso SMB {targetFilePath}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private static void CreateSftpDirectoriesRecursively(SftpClient client, string remoteDirectory)
    {
        string current = "";
        var parts = remoteDirectory.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            current += "/" + part;
            if (!client.Exists(current))
            {
                try { client.CreateDirectory(current); } catch { }
            }
        }
    }

    #endregion
}
