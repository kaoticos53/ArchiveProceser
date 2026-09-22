using System.IO;
using FileFlow.Plugin.Network.Transports;
using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.Network;

[NodeDefinition("NetworkDownloadNode_Name", "Network", "NetworkDownloadNode_Desc", PipelineRole.Source,
    "descargar", "download", "http", "https", "ftp", "ftps", "sftp", "ssh", "webdav", "smb", "red", "nube")]
public sealed class NetworkDownloadNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("NetworkDownloadNode_Name", "Descargar de Red / Nube (Network Download)");
    public override string Category => "Network";
    public override string Description => LocalizationManager.Instance.GetString("NetworkDownloadNode_Desc", "Descarga archivos desde servidores remotos HTTP/HTTPS, FTP/FTPS, SFTP/SSH, WebDAV o recursos SMB de red local hacia una carpeta de destino.");

    public NetworkDownloadNode()
    {
        Inputs =
        [
            new NodePort(WellKnownPorts.In, typeof(FileItemContext), PortDirection.Input, WellKnownPorts.In)
        ];

        Outputs =
        [
            new NodePort(WellKnownPorts.Out, typeof(FileItemContext), PortDirection.Output, WellKnownPorts.Out),
            new NodePort(WellKnownPorts.Error, typeof(FileItemContext), PortDirection.Output, WellKnownPorts.Error)
        ];

        Parameters["Protocol"] = "HTTP";

        // Parámetros HTTP
        Parameters["SourceUrl"] = "{RemoteUrl}";
        Parameters["TimeoutSeconds"] = 60;

        // Parámetros Servidor (FTP / SFTP / WebDAV)
        Parameters["Host"] = "ftp.example.com";
        Parameters["Port"] = 21;
        Parameters["Username"] = "anonymous";
        Parameters["Password"] = "";
        Parameters["RemoteFilePath"] = "/incoming/{FileName}";

        // Parámetros FTP
        Parameters["Encryption"] = "None";
        Parameters["PassiveMode"] = true;

        // Parámetros SFTP
        Parameters["AuthMethod"] = "Password";
        Parameters["PrivateKeyPath"] = "";
        Parameters["PrivateKeyPassphrase"] = "";

        // Parámetros WebDAV
        Parameters["ServerUrl"] = "https://nextcloud.example.com/remote.php/dav/files/user/{FileName}";

        // Parámetros SMB
        Parameters["UncPath"] = @"\\servidor\compartido\{FileName}";
        Parameters["Domain"] = "";

        // Comunes
        Parameters["DestinationFolder"] = "{GlobalOutputDir}";
        Parameters["FileName"] = "";
        Parameters["Overwrite"] = true;
        Parameters["DeleteAfterDownload"] = false;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
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

        // 6. Parámetros específicos WebDAV
        new("ServerUrl", ParameterEditorType.Text, DefaultValue: "https://nextcloud.example.com/remote.php/dav/files/user/{FileName}", DisplayOrder: 14,
            DependsOnKey: "Protocol", DependsOnValues: ["WebDAV"]),

        // 7. Parámetros específicos SMB
        new("UncPath", ParameterEditorType.Text, DefaultValue: @"\\servidor\compartido\{FileName}", DisplayOrder: 15,
            DependsOnKey: "Protocol", DependsOnValues: ["SMB"]),
        new("Domain", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 16,
            DependsOnKey: "Protocol", DependsOnValues: ["SMB"]),

        // 8. Parámetros comunes de destino
        new("DestinationFolder", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 17),
        new("FileName", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 18),
        new("Overwrite", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 19),
        new("DeleteAfterDownload", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 20)
    ];

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        string protocol = GetParameter("Protocol", "HTTP");
        string destFolder = GetParameter("DestinationFolder", "{GlobalOutputDir}");
        string fileNameOverride = GetParameter("FileName", string.Empty);
        bool overwrite = GetParameter("Overwrite", true);
        bool deleteAfter = GetParameter("DeleteAfterDownload", false);

        string resolvedDestDir = ParameterHelper.ResolveOutputPath(destFolder, item);
        if (string.IsNullOrWhiteSpace(resolvedDestDir))
        {
            resolvedDestDir = Directory.GetCurrentDirectory();
        }

        var storage = context.GetStorage();
        await storage.CreateDirectoryAsync(resolvedDestDir, cancellationToken).ConfigureAwait(false);

        var request = new NetworkDownloadRequest(
            DestinationDirectory: resolvedDestDir,
            FileNameOverride: fileNameOverride,
            Overwrite: overwrite,
            DeleteAfterDownload: deleteAfter,
            SourceUrl: GetParameter("SourceUrl", string.Empty),
            TimeoutSeconds: Math.Max(5, GetParameter("TimeoutSeconds", 60)),
            Host: GetParameter("Host", "localhost"),
            Port: GetParameter("Port", 21),
            Username: GetParameter("Username", string.Empty),
            Password: GetParameter("Password", string.Empty),
            RemoteFilePath: GetParameter("RemoteFilePath", string.Empty),
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
            await transport.DownloadAsync(request, item, context, cancellationToken);
        }
        catch (NotSupportedException ex)
        {
            context.Log(ex.Message, LogLevel.Error, item.CurrentPath);
            await context.EmitAsync(WellKnownPorts.Error, item);
        }
    }
}
