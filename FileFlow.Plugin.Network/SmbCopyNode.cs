using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Network;

[NodeDefinition("SmbCopyNode_Name", "Network & Remote", "SmbCopyNode_Desc")]
public class SmbCopyNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SmbCopyNode_Name", "Copiar a Red / NAS (SMB Copy)");
    public string Category => "Network & Remote";
    public string Description => LocalizationManager.Instance.GetString("SmbCopyNode_Desc", "Copia archivos a carpetas compartidas de red local o servidores NAS (rutas UNC \\\\NAS\\Share) con reintentos automáticos y buffers asíncronos.");

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
        ["DestinationFolder"] = @"\\NAS-STORAGE\Backups\{Year}\{Month}",
        ["Overwrite"] = true,
        ["RetryCount"] = 3,
        ["RetryDelaySeconds"] = 2
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors =>
    [
        new("DestinationFolder", ParameterEditorType.FolderPath, DefaultValue: @"\\NAS-STORAGE\Backups\{Year}\{Month}", DisplayOrder: 1),
        new("Overwrite", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 2),
        new("RetryCount", ParameterEditorType.Number, DefaultValue: 3, DisplayOrder: 3),
        new("RetryDelaySeconds", ParameterEditorType.Number, DefaultValue: 2, DisplayOrder: 4)
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

        string destTemplate = Parameters.TryGetValue("DestinationFolder", out var df) ? df?.ToString() ?? @"\\NAS\Share" : @"\\NAS\Share";
        bool overwrite = !Parameters.TryGetValue("Overwrite", out var ow) || !bool.TryParse(ow?.ToString(), out bool isOw) || isOw;
        int maxRetries = Parameters.TryGetValue("RetryCount", out var rc) && int.TryParse(rc?.ToString(), out int parsedRc) ? Math.Max(1, parsedRc) : 3;
        int retryDelay = Parameters.TryGetValue("RetryDelaySeconds", out var rd) && int.TryParse(rd?.ToString(), out int parsedRd) ? Math.Max(1, parsedRd) : 2;

        string resolvedDestDir = NetworkTemplateHelper.ResolveRemotePath(destTemplate, item).Replace('/', '\\');
        string targetFilePath = Path.Combine(resolvedDestDir, item.FileName);

        if (context.IsDryRun)
        {
            context.Log($"[Simulación DryRun] Archivo {item.FileName} se copiaría a {targetFilePath}", LogLevel.Information, item.CurrentPath);
            item.Metadata["NetworkPath"] = targetFilePath;
            await context.EmitAsync("Out", item);
            return;
        }

        int attempt = 0;
        while (attempt < maxRetries)
        {
            attempt++;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(resolvedDestDir))
                {
                    Directory.CreateDirectory(resolvedDestDir);
                }

                if (File.Exists(targetFilePath) && !overwrite)
                {
                    context.Log($"El archivo de destino {targetFilePath} ya existe y Overwrite está deshabilitado.", LogLevel.Warning, item.CurrentPath);
                    await context.EmitAsync("Error", item);
                    return;
                }

                // Copia asíncrona de alto rendimiento con buffer de 80 KB
                const int bufferSize = 81920;
                await using (var sourceStream = new FileStream(item.CurrentPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
                await using (var destStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
                {
                    await sourceStream.CopyToAsync(destStream, bufferSize, cancellationToken);
                }

                context.Log($"Archivo copiado con éxito a ruta de red: {targetFilePath}", LogLevel.Information, item.CurrentPath);
                item.Metadata["NetworkPath"] = targetFilePath;
                item.Metadata["NetworkDirectory"] = resolvedDestDir;
                await context.EmitAsync("Out", item);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                context.Log($"Intento {attempt}/{maxRetries} fallido al copiar a {targetFilePath}: {ex.Message}. Reintentando en {retryDelay}s...", LogLevel.Warning, item.CurrentPath);
                await Task.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken);
            }
            catch (Exception ex)
            {
                context.Log($"Error definitivo tras {attempt} intentos al copiar a {targetFilePath}: {ex.Message}", LogLevel.Error, item.CurrentPath);
                await context.EmitAsync("Error", item);
                return;
            }
        }
    }
}
