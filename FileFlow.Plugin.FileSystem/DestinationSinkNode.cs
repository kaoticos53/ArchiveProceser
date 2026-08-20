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
        ["DestinationRoot"] = @"C:\FileFlowOutput",
        ["ConflictStrategy"] = "RenameIncremental"
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string destRoot = Parameters.TryGetValue("DestinationRoot", out var val) ? val?.ToString() ?? @"C:\FileFlowOutput" : @"C:\FileFlowOutput";
        string strategy = Parameters.TryGetValue("ConflictStrategy", out var sVal) ? sVal?.ToString() ?? "RenameIncremental" : "RenameIncremental";
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && Convert.ToBoolean(dryVal);

        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"DestinationSinkNode: Input file '{item.CurrentPath}' not found.", LogLevel.Warning);
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
                        context.Log($"[DestinationSinkNode] Skipping file due to conflict: {targetPath}", LogLevel.Information);
                        item.AddLog($"DestinationSinkNode skipped due to conflict: {targetPath}");
                        await context.EmitAsync("Done", item);
                        return;

                    case "RENAMEINCREMENTAL":
                        targetPath = GetIncrementalFileName(destRoot, fileName);
                        context.Log($"[DestinationSinkNode] Renamed target to avoid conflict: {targetPath}", LogLevel.Information);
                        break;

                    case "OVERWRITE":
                    default:
                        context.Log($"[DestinationSinkNode] Overwriting target file: {targetPath}", LogLevel.Information);
                        break;
                }
            }

            context.Log($"[DestinationSinkNode] Saving file to: {targetPath} (DryRun={isDryRun})", LogLevel.Information);

            if (!isDryRun)
            {
                File.Copy(item.CurrentPath, targetPath, overwrite: true);
                item.CurrentPath = targetPath;
            }

            item.AddLog($"DestinationSinkNode output saved to {targetPath}");
            await context.EmitAsync("Done", item);
        }
        catch (Exception ex)
        {
            context.Log($"DestinationSinkNode Error: {ex.Message}", LogLevel.Error);
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
