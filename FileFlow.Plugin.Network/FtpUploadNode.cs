using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FluentFTP;

namespace FileFlow.Plugin.Network;

[NodeDefinition("FtpUploadNode_Name", "Network & Remote", "FtpUploadNode_Desc")]
public class FtpUploadNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("FtpUploadNode_Name", "Subir a FTP / FTPS (FTP Upload)");
    public string Category => "Network & Remote";
    public string Description => LocalizationManager.Instance.GetString("FtpUploadNode_Desc", "Sube archivos a un servidor FTP o FTPS con soporte para TLS/SSL y creación automática de directorios remotos.");

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
        ["RemoteDirectory"] = "/uploads/{Year}/{Month}",
        ["Encryption"] = "None",
        ["PassiveMode"] = true
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("Host", ParameterEditorType.Text, DefaultValue: "ftp.example.com", DisplayOrder: 1),
        new("Port", ParameterEditorType.Number, DefaultValue: 21, DisplayOrder: 2),
        new("Username", ParameterEditorType.Text, DefaultValue: "anonymous", DisplayOrder: 3),
        new("Password", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 4),
        new("RemoteDirectory", ParameterEditorType.Text, DefaultValue: "/uploads/{Year}/{Month}", DisplayOrder: 5),
        new("Encryption", ParameterEditorType.Dropdown, DefaultValue: "None", Options: ["None", "Explicit", "Implicit"], DisplayOrder: 6),
        new("PassiveMode", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 7)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            await context.EmitAsync("Error", item);
            return;
        }

        string host = Parameters.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
        int port = Parameters.TryGetValue("Port", out var p) && int.TryParse(p?.ToString(), out int parsedPort) ? parsedPort : 21;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;
        string remoteDirTemplate = Parameters.TryGetValue("RemoteDirectory", out var rd) ? rd?.ToString() ?? "/uploads" : "/uploads";
        string encryptionStr = Parameters.TryGetValue("Encryption", out var enc) ? enc?.ToString() ?? "None" : "None";
        bool passive = !Parameters.TryGetValue("PassiveMode", out var pas) || !bool.TryParse(pas?.ToString(), out bool isPas) || isPas;

        string resolvedDir = NetworkTemplateHelper.ResolveRemotePath(remoteDirTemplate, item);
        string remotePath = $"{resolvedDir.TrimEnd('/')}/{item.FileName}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Archivo {item.FileName} se subiría a ftp://{host}:{port}{remotePath}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = $"ftp://{host}:{port}{remotePath}";
            item.Metadata["RemotePath"] = remotePath;
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
                context.Log($"Archivo {item.FileName} subido correctamente a ftp://{host}:{port}{remotePath}", LogLevel.Information, item.CurrentPath);
                item.Metadata["RemoteUrl"] = $"ftp://{host}:{port}{remotePath}";
                item.Metadata["RemotePath"] = remotePath;
                item.Metadata["UploadedBytes"] = item.FileSizeBytes > 0 ? item.FileSizeBytes : new FileInfo(item.CurrentPath).Length;
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
}
