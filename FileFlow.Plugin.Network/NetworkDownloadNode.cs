using System.IO;
using FileFlow.Plugin.Network.Transports;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

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

        var request = new NetworkDownloadRequest(
            DestinationDirectory: resolvedDestDir,
            FileNameOverride: fileNameOverride,
            Overwrite: overwrite,
            DeleteAfterDownload: deleteAfter,
            SourceUrl: Parameters.TryGetValue("SourceUrl", out var su) ? su?.ToString() ?? string.Empty : string.Empty,
            TimeoutSeconds: Parameters.TryGetValue("TimeoutSeconds", out var to) && int.TryParse(to?.ToString(), out int parsedTo) ? Math.Max(5, parsedTo) : 60,
            Host: Parameters.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost",
            Port: Parameters.TryGetValue("Port", out var p) && int.TryParse(p?.ToString(), out int parsedPort) ? parsedPort : 21,
            Username: Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty,
            Password: Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty,
            RemoteFilePath: Parameters.TryGetValue("RemoteFilePath", out var rp) ? rp?.ToString() ?? string.Empty : string.Empty,
            Encryption: Parameters.TryGetValue("Encryption", out var enc) ? enc?.ToString() ?? "None" : "None",
            PassiveMode: !Parameters.TryGetValue("PassiveMode", out var pas) || !bool.TryParse(pas?.ToString(), out bool isPas) || isPas,
            AuthMethod: Parameters.TryGetValue("AuthMethod", out var am) ? am?.ToString() ?? "Password" : "Password",
            PrivateKeyPath: Parameters.TryGetValue("PrivateKeyPath", out var kp) ? kp?.ToString() ?? string.Empty : string.Empty,
            PrivateKeyPassphrase: Parameters.TryGetValue("PrivateKeyPassphrase", out var pp) ? pp?.ToString() ?? string.Empty : string.Empty,
            ServerUrl: Parameters.TryGetValue("ServerUrl", out var svu) ? svu?.ToString() ?? string.Empty : string.Empty,
            UncPath: Parameters.TryGetValue("UncPath", out var unc) ? unc?.ToString() ?? string.Empty : string.Empty,
            Domain: Parameters.TryGetValue("Domain", out var dom) ? dom?.ToString() ?? string.Empty : string.Empty
        );

        try
        {
            var transport = NetworkTransportFactory.GetTransport(protocol);
            await transport.DownloadAsync(request, item, context, cancellationToken);
        }
        catch (NotSupportedException ex)
        {
            context.Log(ex.Message, LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }
}
