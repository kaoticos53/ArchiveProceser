using FileFlow.Sdk;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("FolderSourceNode_Name", "FileSystem", "FolderSourceNode_Desc")]
public class FolderSourceNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("FolderSourceNode_Name", "Folder Source");
    public string Category => "FileSystem";
    public string Description => LocalizationManager.Instance.GetString("FolderSourceNode_Desc", "Scans directory tree and emits each file or folder found.");

    public IReadOnlyList<NodePort> Inputs { get; } = Array.Empty<NodePort>();

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SourcePath"] = @"C:\SampleFiles",
        ["Recursive"] = true,
        ["WatchRealtime"] = false
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string sourcePath = Parameters.TryGetValue("SourcePath", out var val) ? val?.ToString() ?? string.Empty : string.Empty;
        bool recursive = Parameters.TryGetValue("Recursive", out var recVal) && Convert.ToBoolean(recVal);
        bool watchRealtime = Parameters.TryGetValue("WatchRealtime", out var watchVal) && Convert.ToBoolean(watchVal);

        if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
        {
            context.Log($"Source directory '{sourcePath}' does not exist or is invalid.", LogLevel.Warning);
            return;
        }

        context.Log($"Scanning directory: {sourcePath} (Recursive={recursive})", LogLevel.Information);

        SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] files = Directory.GetFiles(sourcePath, "*.*", searchOption);

        int count = 0;
        foreach (string filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileItem = new FileItemContext(filePath, isDirectory: false);
            fileItem.AddLog($"Emitted by FolderSourceNode from {sourcePath}");
            await context.EmitAsync("Out", fileItem);
            count++;
        }

        context.Log($"FolderSourceNode scanned and emitted {count} items.", LogLevel.Information);
    }
}
