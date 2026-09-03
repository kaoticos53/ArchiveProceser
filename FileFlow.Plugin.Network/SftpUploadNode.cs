using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using Renci.SshNet;

namespace FileFlow.Plugin.Network;

[NodeDefinition("SftpUploadNode_Name", "Network", "SftpUploadNode_Desc", PipelineRole.Sink,
    "sftp", "ssh", "subir", "seguro", "linux", "servidor", "upload")]
public class SftpUploadNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SftpUploadNode_Name", "Subir a SFTP / SSH (SFTP Upload)");
    public string Category => "Network";
    public string Description => LocalizationManager.Instance.GetString("SftpUploadNode_Desc", "Transfiere archivos de forma cifrada mediante SFTP (SSH) a servidores Linux/VPS con soporte para contraseñas y llaves privadas.");

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
        ["RemoteDirectory"] = "/var/www/uploads/{Year}/{Month}"
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
        new("RemoteDirectory", ParameterEditorType.Text, DefaultValue: "/var/www/uploads/{Year}/{Month}", DisplayOrder: 8)
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
        int port = Parameters.TryGetValue("Port", out var p) && int.TryParse(p?.ToString(), out int parsedPort) ? parsedPort : 22;
        string user = Parameters.TryGetValue("Username", out var u) ? u?.ToString() ?? string.Empty : string.Empty;
        string authMethod = Parameters.TryGetValue("AuthMethod", out var am) ? am?.ToString() ?? "Password" : "Password";
        string pass = Parameters.TryGetValue("Password", out var pwd) ? pwd?.ToString() ?? string.Empty : string.Empty;
        string keyPath = Parameters.TryGetValue("PrivateKeyPath", out var kp) ? kp?.ToString() ?? string.Empty : string.Empty;
        string keyPass = Parameters.TryGetValue("PrivateKeyPassphrase", out var kpp) ? kpp?.ToString() ?? string.Empty : string.Empty;
        string remoteDirTemplate = Parameters.TryGetValue("RemoteDirectory", out var rd) ? rd?.ToString() ?? "/uploads" : "/uploads";

        string resolvedDir = NetworkTemplateHelper.ResolveRemotePath(remoteDirTemplate, item);
        string remotePath = $"{resolvedDir.TrimEnd('/')}/{item.FileName}";

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Archivo {item.FileName} se subiría por SFTP a sftp://{host}:{port}{remotePath}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = $"sftp://{host}:{port}{remotePath}";
            item.Metadata["RemotePath"] = remotePath;
            await context.EmitAsync("Out", item);
            return;
        }

        try
        {
            ConnectionInfo connectionInfo;
            if (authMethod.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath))
            {
                var keyFile = string.IsNullOrWhiteSpace(keyPass)
                    ? new PrivateKeyFile(keyPath)
                    : new PrivateKeyFile(keyPath, keyPass);

                connectionInfo = new ConnectionInfo(host, port, user, new PrivateKeyAuthenticationMethod(user, keyFile));
            }
            else
            {
                connectionInfo = new ConnectionInfo(host, port, user, new PasswordAuthenticationMethod(user, pass));
            }

            using var sftp = new SftpClient(connectionInfo);
            await sftp.ConnectAsync(cancellationToken);

            // Crear directorios remotos recursivamente si no existen
            CreateRemoteDirectoryRecursive(sftp, resolvedDir);

            // Subir archivo
            await using (var fileStream = File.OpenRead(item.CurrentPath))
            {
                await Task.Run(() => sftp.UploadFile(fileStream, remotePath, true), cancellationToken);
            }

            sftp.Disconnect();

            context.Log($"Archivo {item.FileName} subido correctamente por SFTP a {host}:{remotePath}", LogLevel.Information, item.CurrentPath);
            item.Metadata["RemoteUrl"] = $"sftp://{host}:{port}{remotePath}";
            item.Metadata["RemotePath"] = remotePath;
            item.Metadata["UploadedBytes"] = item.FileSizeBytes > 0 ? item.FileSizeBytes : new FileInfo(item.CurrentPath).Length;

            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            context.Log($"Error en transferencia SFTP a {host}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }

    private static void CreateRemoteDirectoryRecursive(SftpClient client, string path)
    {
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = path.StartsWith('/') ? "/" : "";

        foreach (var part in parts)
        {
            current = current == "/" ? $"/{part}" : $"{current}/{part}";
            if (!client.Exists(current))
            {
                try
                {
                    client.CreateDirectory(current);
                }
                catch
                {
                    // Si ya existía o fallo no crítico, continuar
                }
            }
        }
    }
}
