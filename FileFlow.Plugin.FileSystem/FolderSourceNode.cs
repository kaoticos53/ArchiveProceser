using System.Threading.Channels;
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
        ["SourcePath"] = @"{RelativeDir}\Input",
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
        string rawSourcePattern = Parameters.TryGetValue("SourcePath", out var val) ? ParameterHelper.GetString(val, @"{RelativeDir}\Input") : @"{RelativeDir}\Input";
        string sourcePath = ParameterHelper.ResolveOutputPath(rawSourcePattern, item);
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

        // Bounded channel with 1000 items capacity to provide backpressure control
        var channel = Channel.CreateBounded<FileItemContext>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        long emittedCount = 0;
        long totalBytesEmitted = 0;
        long lastReportTicks = Environment.TickCount64;

        context.ReportProgress(0, "⚡ Escaneando y emitiendo elementos...");

        var dirInfo = new DirectoryInfo(sourcePath);

        // Consumer task reading from channel and emitting downstream
        var consumerTask = Task.Run(async () =>
        {
            await foreach (var itemContext in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                long currentCount = Interlocked.Increment(ref emittedCount);
                Interlocked.Add(ref totalBytesEmitted, itemContext.FileSizeBytes);

                long nowTicks = Environment.TickCount64;
                if (currentCount == 1 || nowTicks - lastReportTicks > 100)
                {
                    lastReportTicks = nowTicks;
                    double mb = totalBytesEmitted / (1024.0 * 1024.0);
                    context.ReportProgress(0, $"⚡ Escaneando y emitiendo: {currentCount:N0} archivos ({mb:F1} MB)...");
                }

                itemContext.Metadata["SourceRootPath"] = sourcePath;
                itemContext.Metadata["Counter"] = currentCount;
                itemContext.Metadata["TotalEmittedBytes"] = totalBytesEmitted;
                itemContext.AddLog($"Emitted by FolderSourceNode from {sourcePath}");
                await context.EmitAsync("Out", itemContext).ConfigureAwait(false);
            }
        }, cancellationToken);

        // Producer: Scan directory tree with 1-pass DirectoryInfo/FileInfo I/O and write to channel
        try
        {
            await StreamAndEmitDirAsync(
                dirInfo,
                0,
                maxDepth,
                emitFiles,
                emitDirectories,
                channel.Writer,
                context,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            channel.Writer.Complete();
        }

        await consumerTask.ConfigureAwait(false);

        double totalMB = totalBytesEmitted / (1024.0 * 1024.0);
        context.ReportProgress(100.0, $"{emittedCount:N0} elementos emmitidos ({totalMB:F1} MB - 100%)");
        context.Log($"FolderSourceNode scanned and emitted {emittedCount:N0} items ({totalMB:F1} MB).", LogLevel.Information);
    }

    private static async Task StreamAndEmitDirAsync(
        DirectoryInfo currentDir,
        int currentDepth,
        int maxDepth,
        bool emitFiles,
        bool emitDirectories,
        ChannelWriter<FileItemContext> writer,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (emitFiles)
        {
            try
            {
                int fileCounter = 0;
                foreach (FileInfo file in currentDir.EnumerateFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var itemContext = new FileItemContext(file);
                    await writer.WriteAsync(itemContext, cancellationToken).ConfigureAwait(false);

                    fileCounter++;
                    if (fileCounter % 100 == 0)
                    {
                        await Task.Yield();
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is DirectoryNotFoundException)
            {
                context.Log($"Skipping files in '{currentDir.FullName}': {ex.Message}", LogLevel.Warning);
            }
        }

        try
        {
            foreach (DirectoryInfo subDir in currentDir.EnumerateDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (emitDirectories)
                {
                    var dirContext = new FileItemContext(subDir);
                    await writer.WriteAsync(dirContext, cancellationToken).ConfigureAwait(false);
                }

                if (maxDepth == -1 || currentDepth < maxDepth)
                {
                    await StreamAndEmitDirAsync(subDir, currentDepth + 1, maxDepth, emitFiles, emitDirectories, writer, context, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is DirectoryNotFoundException)
        {
            context.Log($"Skipping directories in '{currentDir.FullName}': {ex.Message}", LogLevel.Warning);
        }
    }
}

