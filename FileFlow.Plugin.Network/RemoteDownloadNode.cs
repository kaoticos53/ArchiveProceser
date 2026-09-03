using System.IO;
using System.Net.Http;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Network;

[NodeDefinition("RemoteDownloadNode_Name", "Network", "RemoteDownloadNode_Desc", PipelineRole.Source,
    "descargar", "download", "http", "https", "url", "web", "remoto", "ftp")]
public class RemoteDownloadNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("RemoteDownloadNode_Name", "Descargar Archivo Remoto (Remote Download)");
    public string Category => "Network";
    public string Description => LocalizationManager.Instance.GetString("RemoteDownloadNode_Desc", "Descarga archivos remotos desde URLs HTTP, HTTPS o WebDAV hacia una carpeta local para su procesamiento.");

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
        ["SourceUrl"] = "{RemoteUrl}",
        ["DestinationFolder"] = "{GlobalOutputDir}",
        ["FileName"] = "",
        ["Overwrite"] = true,
        ["TimeoutSeconds"] = 60
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("SourceUrl", ParameterEditorType.Text, DefaultValue: "{RemoteUrl}", DisplayOrder: 1),
        new("DestinationFolder", ParameterEditorType.FolderPath, DefaultValue: "{GlobalOutputDir}", DisplayOrder: 2),
        new("FileName", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 3),
        new("Overwrite", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4),
        new("TimeoutSeconds", ParameterEditorType.Number, DefaultValue: 60, DisplayOrder: 5)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        string rawUrl = Parameters.TryGetValue("SourceUrl", out var su) ? su?.ToString() ?? string.Empty : string.Empty;
        string destFolder = Parameters.TryGetValue("DestinationFolder", out var df) ? df?.ToString() ?? "{GlobalOutputDir}" : "{GlobalOutputDir}";
        string fileNameOverride = Parameters.TryGetValue("FileName", out var fn) ? fn?.ToString() ?? string.Empty : string.Empty;
        bool overwrite = !Parameters.TryGetValue("Overwrite", out var ow) || !bool.TryParse(ow?.ToString(), out bool isOw) || isOw;
        int timeoutSec = Parameters.TryGetValue("TimeoutSeconds", out var to) && int.TryParse(to?.ToString(), out int parsedTo) ? Math.Max(5, parsedTo) : 60;

        string targetUrl = NetworkTemplateHelper.ResolveRemotePath(rawUrl, item);
        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            context.Log($"La URL de descarga no es válida o está vacía: '{targetUrl}'", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
            return;
        }

        string resolvedDestDir = NetworkTemplateHelper.ResolveRemotePath(destFolder, item).Replace('/', '\\');
        string effectiveFileName = !string.IsNullOrWhiteSpace(fileNameOverride)
            ? NetworkTemplateHelper.ResolveRemotePath(fileNameOverride, item)
            : Path.GetFileName(uri.LocalPath);

        if (string.IsNullOrWhiteSpace(effectiveFileName))
        {
            effectiveFileName = $"download_{DateTime.Now:yyyyMMdd_HHmmss}.dat";
        }

        string localTargetPath = Path.Combine(resolvedDestDir, effectiveFileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Se descargaría {targetUrl} hacia {localTargetPath}", LogLevel.Information, item.CurrentPath);
            var simulatedContext = new FileItemContext(localTargetPath)
            {
                FileSizeBytes = item.FileSizeBytes > 0 ? item.FileSizeBytes : 1024,
                Metadata = new Dictionary<string, object?>(item.Metadata)
                {
                    ["SourceUrl"] = targetUrl,
                    ["DownloadedPath"] = localTargetPath
                }
            };
            await context.EmitAsync("Out", simulatedContext);
            return;
        }

        try
        {
            if (!Directory.Exists(resolvedDestDir))
            {
                Directory.CreateDirectory(resolvedDestDir);
            }

            if (File.Exists(localTargetPath) && !overwrite)
            {
                context.Log($"El archivo destino {localTargetPath} ya existe y Overwrite está deshabilitado.", LogLevel.Warning, item.CurrentPath);
                await context.EmitAsync("Error", item);
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSec) };
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var localStream = new FileStream(localTargetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await remoteStream.CopyToAsync(localStream, cancellationToken);
            }

            context.Log($"Descarga remota completada con éxito: {targetUrl} -> {localTargetPath}", LogLevel.Information, localTargetPath);

            var downloadedItem = new FileItemContext(localTargetPath)
            {
                OriginalPath = item.OriginalPath,
                FileSizeBytes = new FileInfo(localTargetPath).Length,
                Metadata = new Dictionary<string, object?>(item.Metadata)
                {
                    ["SourceUrl"] = targetUrl,
                    ["DownloadedPath"] = localTargetPath,
                    ["DownloadedBytes"] = new FileInfo(localTargetPath).Length
                }
            };

            await context.EmitAsync("Out", downloadedItem);
        }
        catch (Exception ex)
        {
            context.Log($"Error al descargar desde {targetUrl}: {ex.Message}", LogLevel.Error, item.CurrentPath);
            await context.EmitAsync("Error", item);
        }
    }
}
