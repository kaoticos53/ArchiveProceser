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
        ["EmitMode"] = "FilesOnly",
        ["MaxRecursionDepth"] = -1,
        ["WatchRealtime"] = false
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string sourcePath = Parameters.TryGetValue("SourcePath", out var val) ? ParameterHelper.GetString(val) : string.Empty;
        bool recursive = !Parameters.TryGetValue("Recursive", out var recVal) || ParameterHelper.GetBoolean(recVal, true);
        string emitMode = Parameters.TryGetValue("EmitMode", out var modeVal) ? ParameterHelper.GetString(modeVal, "FilesOnly") : "FilesOnly";
        
        int maxDepth = Parameters.TryGetValue("MaxRecursionDepth", out var depthVal) ? ParameterHelper.GetInt32(depthVal, -1) : -1;

        if (!recursive)
        {
            maxDepth = 0;
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
        {
            context.Log($"Source directory '{sourcePath}' does not exist or is invalid.", LogLevel.Warning);
            return;
        }

        bool emitFiles = true;
        bool emitDirectories = false;

        switch (emitMode.Trim().ToLowerInvariant())
        {
            case "directoriesonly":
            case "directories":
                emitFiles = false;
                emitDirectories = true;
                break;
            case "filesanddirectories":
            case "both":
            case "all":
                emitFiles = true;
                emitDirectories = true;
                break;
            case "filesonly":
            case "files":
            default:
                emitFiles = true;
                emitDirectories = false;
                break;
        }

        context.Log($"Scanning directory: {sourcePath} (EmitMode={emitMode}, MaxDepth={maxDepth}, Recursive={recursive})", LogLevel.Information);

        var itemsToEmit = new List<FileItemContext>();
        CollectItems(sourcePath, 0, maxDepth, emitFiles, emitDirectories, itemsToEmit, context, cancellationToken);

        int totalItems = itemsToEmit.Count;
        if (totalItems == 0)
        {
            context.ReportProgress(100.0, "0 elementos");
        }

        long lastReportTicks = 0;
        for (int i = 0; i < totalItems; i++)
        {
            var itemContext = itemsToEmit[i];
            cancellationToken.ThrowIfCancellationRequested();

            long nowTicks = Environment.TickCount64;
            if (i == 0 || i == totalItems - 1 || nowTicks - lastReportTicks > 60)
            {
                lastReportTicks = nowTicks;
                double pct = ((double)(i + 1) / totalItems) * 100.0;
                context.ReportProgress(pct, $"{i + 1}/{totalItems} ({pct:F0}%)");
            }

            itemContext.Metadata["SourceRootPath"] = sourcePath;
            itemContext.Metadata["Counter"] = i + 1;
            itemContext.AddLog($"Emitted by FolderSourceNode from {sourcePath}");
            await context.EmitAsync("Out", itemContext);
        }

        context.ReportProgress(100.0, $"{totalItems}/{totalItems} (100%)");
        context.Log($"FolderSourceNode scanned and emitted {totalItems} items.", LogLevel.Information);
    }

    private static void CollectItems(
        string currentDir,
        int currentDepth,
        int maxDepth,
        bool emitFiles,
        bool emitDirectories,
        List<FileItemContext> result,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (emitFiles)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(currentDir))
                {
                    result.Add(new FileItemContext(file, isDirectory: false));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is DirectoryNotFoundException)
            {
                context.Log($"Skipping files in '{currentDir}': {ex.Message}", LogLevel.Warning);
            }
        }

        try
        {
            foreach (string subDir in Directory.EnumerateDirectories(currentDir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (emitDirectories)
                {
                    result.Add(new FileItemContext(subDir, isDirectory: true));
                }

                if (maxDepth == -1 || currentDepth < maxDepth)
                {
                    CollectItems(subDir, currentDepth + 1, maxDepth, emitFiles, emitDirectories, result, context, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is DirectoryNotFoundException)
        {
            context.Log($"Skipping directories in '{currentDir}': {ex.Message}", LogLevel.Warning);
        }
    }
}

