using System.IO;
using FileFlow.Plugin.Network.Transports;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

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
            DependsOnKey: "Protocol", DependsOnValues: ["FTP", "SFTP"]),

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

        // 6. Parámetros específicos WebDAV
        new("ServerUrl", ParameterEditorType.Text, DefaultValue: "https://nextcloud.example.com/remote.php/dav/files/user/{Year}/{Month}", DisplayOrder: 15,
            DependsOnKey: "Protocol", DependsOnValues: ["WebDAV"]),

        // 7. Parámetros específicos SMB
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
        if (!File.Exists(item.CurrentPath) && !context.IsDryRun)
        {
            context.Log($"El archivo a subir no existe en disco: {item.CurrentPath}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        string protocol = Parameters.TryGetValue("Protocol", out var pr) ? pr?.ToString() ?? "FTP" : "FTP";

        var request = new NetworkUploadRequest(
            TargetUrl: Parameters.TryGetValue("TargetUrl", out var tu) ? tu?.ToString() ?? string.Empty : string.Empty,
            HttpMethod: Parameters.TryGetValue("HttpMethod", out var hm) ? hm?.ToString() ?? "POST" : "POST",
            AuthHeader: Parameters.TryGetValue("AuthHeader", out var ah) ? ah?.ToString() ?? string.Empty : string.Empty,
            Host: Parameters.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost",
            Port: Parameters.TryGetValue("Port", out var p) && int.TryParse(p?.ToString(), out int parsedPort) ? parsedPort : 21,
            Username: Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty,
            Password: Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty,
            RemoteDirectory: Parameters.TryGetValue("RemoteDirectory", out var rd) ? rd?.ToString() ?? string.Empty : string.Empty,
            Encryption: Parameters.TryGetValue("Encryption", out var enc) ? enc?.ToString() ?? "None" : "None",
            PassiveMode: !Parameters.TryGetValue("PassiveMode", out var pas) || !bool.TryParse(pas?.ToString(), out bool isPas) || isPas,
            AuthMethod: Parameters.TryGetValue("AuthMethod", out var am) ? am?.ToString() ?? "Password" : "Password",
            PrivateKeyPath: Parameters.TryGetValue("PrivateKeyPath", out var kp) ? kp?.ToString() ?? string.Empty : string.Empty,
            PrivateKeyPassphrase: Parameters.TryGetValue("PrivateKeyPassphrase", out var pp) ? pp?.ToString() ?? string.Empty : string.Empty,
            ServerUrl: Parameters.TryGetValue("ServerUrl", out var su) ? su?.ToString() ?? string.Empty : string.Empty,
            UncPath: Parameters.TryGetValue("UncPath", out var unc) ? unc?.ToString() ?? string.Empty : string.Empty,
            Domain: Parameters.TryGetValue("Domain", out var dom) ? dom?.ToString() ?? string.Empty : string.Empty
        );

        try
        {
            var transport = NetworkTransportFactory.GetTransport(protocol);
            await transport.UploadAsync(request, item, context, cancellationToken);
        }
        catch (NotSupportedException ex)
        {
            context.Log(ex.Message, LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }
}
