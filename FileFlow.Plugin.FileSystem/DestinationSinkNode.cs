using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("DestinationSinkNode_Name", "FileSystem", "DestinationSinkNode_Desc")]
public class DestinationSinkNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("DestinationSinkNode_Name", "Destination Sink");
    public string Category => "FileSystem";
    public string Description => LocalizationManager.Instance.GetString("DestinationSinkNode_Desc", "Writes or moves final processed file to projected target path.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Done", typeof(FileItemContext), PortDirection.Output, "Done"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DestinationRoot"] = @"{RelativeDir}\Output",
        ["ConflictStrategy"] = "RenameIncremental"
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("DestinationRoot", ParameterEditorType.FolderPath, DefaultValue: @"{RelativeDir}\Output", DisplayOrder: 1),
        new("ConflictStrategy", ParameterEditorType.Dropdown, DefaultValue: "RenameIncremental", DisplayOrder: 2, Options: ["RenameIncremental", "Overwrite", "Skip", "ThrowError"])
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string destPattern = Parameters.TryGetValue("DestinationRoot", out var val) ? ParameterHelper.GetString(val, string.Empty) : string.Empty;
        string destRoot = ParameterHelper.ResolveOutputPath(destPattern, item);
        string strategy = Parameters.TryGetValue("ConflictStrategy", out var sVal) ? ParameterHelper.GetString(sVal, "RenameIncremental") : "RenameIncremental";
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        string sourcePath = item.GetExistingPhysicalPath();

        if (string.IsNullOrWhiteSpace(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
        {
            context.Log($"[Destino Final] Archivo de entrada no encontrado: '{item.CurrentPath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            if (!Directory.Exists(destRoot) && !isDryRun)
            {
                Directory.CreateDirectory(destRoot);
            }

            string fileName = Path.GetFileName(item.CurrentPath);
            string targetPath = Path.Combine(destRoot, fileName);

            if (File.Exists(targetPath))
            {
                switch (strategy.ToUpperInvariant())
                {
                    case "SKIP":
                        context.Log($"[Destino Final] Omitido por colisión existente (Estrategia: Skip): '{targetPath}'", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds);
                        item.AddLog($"DestinationSinkNode skipped due to conflict: {targetPath}");
                        await context.EmitAsync("Done", item);
                        return;

                    case "RENAMEINCREMENTAL":
                        string originalTarget = targetPath;
                        targetPath = GetIncrementalFileName(destRoot, fileName);
                        context.Log($"[Destino Final] Renombrado incremental para evitar colisión: '{Path.GetFileName(targetPath)}'", LogLevel.Debug, item);
                        break;

                    case "OVERWRITE":
                    default:
                        context.Log($"[Destino Final] Sobrescribiendo archivo destino existente: '{targetPath}'", LogLevel.Debug, item);
                        break;
                }
            }

            if (!isDryRun)
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
                item.PhysicalPath = targetPath;
                item.CurrentPath = targetPath;
            }

            sw.Stop();
            string detailsJson = $"{{\"destinationRoot\": \"{destRoot.Replace("\\", "\\\\")}\", \"targetPath\": \"{targetPath.Replace("\\", "\\\\")}\", \"strategy\": \"{strategy}\", \"isDryRun\": {isDryRun.ToString().ToLowerInvariant()}, \"sizeBytes\": {item.FileSizeBytes}}}";
            context.Log($"[Destino Final] Guardado con éxito en '{targetPath}' (Estrategia: {strategy}, DryRun={isDryRun})", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            item.AddLog($"DestinationSinkNode output saved to {targetPath}");
            await context.EmitAsync("Done", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errDetails = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"destinationRoot\": \"{destRoot.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Destino Final] Error al guardar archivo: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errDetails);
            item.AddLog($"DestinationSinkNode failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static string GetIncrementalFileName(string folder, string fileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        int counter = 1;
        string targetPath;

        do
        {
            targetPath = Path.Combine(folder, $"{nameWithoutExt}_{counter}{ext}");
            counter++;
        } while (File.Exists(targetPath));

        return targetPath;
    }
}
