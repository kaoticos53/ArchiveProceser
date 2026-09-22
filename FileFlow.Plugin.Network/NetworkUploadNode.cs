using System.IO;
using FileFlow.Plugin.Network.Transports;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Network;

[NodeDefinition("NetworkUploadNode_Name", "Network", "NetworkUploadNode_Desc", PipelineRole.Sink,
    "subir", "upload", "transferir", "http", "https", "ftp", "ftps", "sftp", "ssh", "webdav", "smb", "red", "nube")]
public sealed class NetworkUploadNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("NetworkUploadNode_Name", "Subir a Red / Nube (Network Upload)");
    public override string Category => "Network";
    public override string Description => LocalizationManager.Instance.GetString("NetworkUploadNode_Desc", "Transfiere archivos hacia servidores remotos HTTP/HTTPS (POST/PUT), FTP/FTPS, SFTP/SSH, WebDAV o recursos SMB de red local.");

    public NetworkUploadNode()
    {
        Inputs =
        [
            new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
        ];

        Outputs =
        [
            new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
            new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
        ];

        Parameters["Protocol"] = "FTP";

        // Parámetros HTTP / API Webhook
        Parameters["TargetUrl"] = "https://api.example.com/upload";
        Parameters["HttpMethod"] = "POST";
        Parameters["AuthHeader"] = "";

        // Parámetros Servidor (FTP / SFTP)
        Parameters["Host"] = "ftp.example.com";
        Parameters["Port"] = 21;
        Parameters["Username"] = "anonymous";
        Parameters["Password"] = "";
        Parameters["RemoteDirectory"] = "/uploads/{Year}/{Month}";

        // Parámetros FTP
        Parameters["Encryption"] = "None";
        Parameters["PassiveMode"] = true;

        // Parámetros SFTP
        Parameters["AuthMethod"] = "Password";
        Parameters["PrivateKeyPath"] = "";
        Parameters["PrivateKeyPassphrase"] = "";

        // Parámetros WebDAV
        Parameters["ServerUrl"] = "https://nextcloud.example.com/remote.php/dav/files/user/{Year}/{Month}";

        // Parámetros SMB
        Parameters["UncPath"] = @"\\servidor\compartido\{Year}\{Month}";
        Parameters["Domain"] = "";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
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

    public override async Task ExecuteAsync(
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

        string protocol = GetParameter("Protocol", "FTP");

        var request = new NetworkUploadRequest(
            TargetUrl: GetParameter("TargetUrl", string.Empty),
            HttpMethod: GetParameter("HttpMethod", "POST"),
            AuthHeader: GetParameter("AuthHeader", string.Empty),
            Host: GetParameter("Host", "localhost"),
            Port: GetParameter("Port", 21),
            Username: GetParameter("Username", string.Empty),
            Password: GetParameter("Password", string.Empty),
            RemoteDirectory: GetParameter("RemoteDirectory", string.Empty),
            Encryption: GetParameter("Encryption", "None"),
            PassiveMode: GetParameter("PassiveMode", true),
            AuthMethod: GetParameter("AuthMethod", "Password"),
            PrivateKeyPath: GetParameter("PrivateKeyPath", string.Empty),
            PrivateKeyPassphrase: GetParameter("PrivateKeyPassphrase", string.Empty),
            ServerUrl: GetParameter("ServerUrl", string.Empty),
            UncPath: GetParameter("UncPath", string.Empty),
            Domain: GetParameter("Domain", string.Empty)
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
