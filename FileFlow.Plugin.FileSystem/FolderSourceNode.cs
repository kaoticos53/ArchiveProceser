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
        int totalFiles = files.Length;

        if (totalFiles == 0)
        {
            context.ReportProgress(100.0, "0 archivos");
        }

        long lastReportTicks = 0;
        for (int i = 0; i < totalFiles; i++)
        {
            string filePath = files[i];
            count++;
            cancellationToken.ThrowIfCancellationRequested();

            long nowTicks = Environment.TickCount64;
            if (i == 0 || i == totalFiles - 1 || nowTicks - lastReportTicks > 60)
            {
                lastReportTicks = nowTicks;
                double pct = ((double)(i + 1) / totalFiles) * 100.0;
                context.ReportProgress(pct, $"{count}/{totalFiles} ({pct:F0}%)");
            }

            var fileItem = new FileItemContext(filePath, isDirectory: false);
            fileItem.Metadata["SourceRootPath"] = sourcePath;
            fileItem.Metadata["Counter"] = count;
            fileItem.AddLog($"Emitted by FolderSourceNode from {sourcePath}");
            await context.EmitAsync("Out", fileItem);
        }

        context.ReportProgress(100.0, $"{count}/{totalFiles} (100%)");
        context.Log($"FolderSourceNode scanned and emitted {count} items.", LogLevel.Information);
    }
}
