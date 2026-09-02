using System.IO;
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
        ["ExtensionFilter"] = "",
        ["Recursive"] = true,
        ["EmitMode"] = "FilesOnly",
        ["MaxRecursionDepth"] = -1,
        ["WatchRealtime"] = false
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("SourcePath", ParameterEditorType.FolderPath, DefaultValue: @"{RelativeDir}\Input", DisplayOrder: 1),
        new("ExtensionFilter", ParameterEditorType.Text, DefaultValue: "", DisplayOrder: 2),
        new("Recursive", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3),
        new("EmitMode", ParameterEditorType.Dropdown, DefaultValue: "FilesOnly", DisplayOrder: 4, Options: ["FilesOnly", "DirectoriesOnly", "FilesAndDirectories"]),
        new("MaxRecursionDepth", ParameterEditorType.Number, DefaultValue: -1, DisplayOrder: 5, Min: -1, Max: 100),
        new("WatchRealtime", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 6)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string rawSourcePattern = Parameters.TryGetValue("SourcePath", out var val) ? ParameterHelper.GetString(val, @"{RelativeDir}\Input") : @"{RelativeDir}\Input";
        string sourcePath = ParameterHelper.ResolveOutputPath(rawSourcePattern, item);
        string rawExtFilter = Parameters.TryGetValue("ExtensionFilter", out var extVal) ? ParameterHelper.GetString(extVal, string.Empty) : string.Empty;
        var filterSet = ParseExtensionFilter(rawExtFilter);
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

        string filterDesc = filterSet.Count > 0 ? $"ExtensionFilter=[{string.Join(", ", filterSet)}]" : "ExtensionFilter=*";
        context.Log($"Scanning directory: {sourcePath} (EmitMode={emitMode}, {filterDesc}, MaxDepth={maxDepth}, Recursive={recursive})", LogLevel.Information);

        // Pre-conteo ultrarrápido nativo Win32 (0-15 ms) para que el total exacto esté disponible desde el milisegundo 0
        long fastTotal = FastCountSourceFiles(sourcePath, recursive, maxDepth, emitFiles, emitDirectories, filterSet);
        if (fastTotal > 0)
        {
            context.SetTotalExpectedItems(fastTotal);
        }
        else
        {
            // Fallback asíncrono en background si el directorio es remoto o bloqueado
            _ = Task.Run(() =>
            {
                try
                {
                    var opt = new EnumerationOptions
                    {
                        RecurseSubdirectories = recursive,
                        MaxRecursionDepth = maxDepth == -1 ? int.MaxValue : maxDepth,
                        IgnoreInaccessible = true,
                        ReturnSpecialDirectories = false,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    };
                    long totalFound = 0;
                    if (emitFiles && emitDirectories)
                    {
                        if (filterSet.Count > 0)
                        {
                            totalFound += Directory.EnumerateFiles(sourcePath, "*", opt).Where(f => filterSet.Contains(Path.GetExtension(f))).LongCount();
                            totalFound += Directory.EnumerateDirectories(sourcePath, "*", opt).LongCount();
                        }
                        else
                        {
                            totalFound += Directory.EnumerateFileSystemEntries(sourcePath, "*", opt).LongCount();
                        }
                    }
                    else if (emitFiles)
                    {
                        if (filterSet.Count > 0)
                        {
                            totalFound += Directory.EnumerateFiles(sourcePath, "*", opt).Where(f => filterSet.Contains(Path.GetExtension(f))).LongCount();
                        }
                        else
                        {
                            totalFound += Directory.EnumerateFiles(sourcePath, "*", opt).LongCount();
                        }
                    }
                    else if (emitDirectories)
                    {
                        totalFound += Directory.EnumerateDirectories(sourcePath, "*", opt).LongCount();
                    }

                    if (totalFound > 0)
                    {
                        context.SetTotalExpectedItems(totalFound);
                    }
                }
                catch
                {
                    // Fallback silencioso
                }
            }, cancellationToken);
        }

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

        var dirInfo = new DirectoryInfo(sourcePath);

        // Consumer task reading from channel and emitting downstream
        var consumerTask = Task.Run(async () =>
        {
            await foreach (var itemContext in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                long currentCount = Interlocked.Increment(ref emittedCount);
                Interlocked.Add(ref totalBytesEmitted, itemContext.FileSizeBytes);

                long nowTicks = Environment.TickCount64;
                if (currentCount == 1 || nowTicks - lastReportTicks > 150)
                {
                    lastReportTicks = nowTicks;
                    double mb = totalBytesEmitted / (1024.0 * 1024.0);
                    string unit = emitFiles && emitDirectories ? "elementos" : (emitDirectories ? "carpetas" : "archivos");
                    context.ReportProgress(0, $"⚡ Escaneando y emitiendo: {currentCount:N0} {unit} ({mb:F1} MB)...");
                }

                itemContext.Metadata["SourceRootPath"] = sourcePath;
                itemContext.Metadata["Counter"] = currentCount;
                itemContext.Metadata["TotalEmittedBytes"] = totalBytesEmitted;
                itemContext.AddLog($"Emitted by FolderSourceNode from {sourcePath}");
                await context.EmitAsync("Out", itemContext).ConfigureAwait(false);
            }
        }, cancellationToken);

        // Producer: Scan directory tree with 1-pass DirectoryInfo/FileInfo I/O and write to channel
        Exception? producerError = null;
        try
        {
            await StreamAndEmitDirAsync(
                dirInfo,
                0,
                maxDepth,
                emitFiles,
                emitDirectories,
                filterSet,
                channel.Writer,
                context,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            producerError = ex;
            throw;
        }
        finally
        {
            channel.Writer.Complete(producerError);
        }

        await consumerTask.ConfigureAwait(false);

        double totalMB = totalBytesEmitted / (1024.0 * 1024.0);
        string finalUnit = emitFiles && emitDirectories ? "elementos" : (emitDirectories ? "carpetas" : "archivos");
        string detailsJson = $"{{\"sourcePath\": \"{sourcePath.Replace("\\", "\\\\")}\", \"emittedCount\": {emittedCount}, \"totalSizeBytes\": {totalBytesEmitted}, \"totalMB\": {totalMB.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, \"unit\": \"{finalUnit}\"}}";
        context.Log($"[Origen Carpeta] Finalizado escaneo y emisión: {emittedCount:N0} {finalUnit} ({totalMB:F1} MB)", LogLevel.Information, null, durationMs: 0.0, detailsJson: detailsJson);
    }

    public static HashSet<string> ParseExtensionFilter(string? filter)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(filter))
        {
            return set;
        }

        char[] separators = [',', ';', '|', ' ', '\t', '\r', '\n'];
        string[] parts = filter.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string part in parts)
        {
            string p = part.Trim();
            if (string.IsNullOrEmpty(p) || p == "*" || p == "*.*")
            {
                continue;
            }

            if (p.StartsWith("*."))
            {
                p = p[1..];
            }
            else if (!p.StartsWith('.'))
            {
                p = "." + p;
            }

            set.Add(p);
        }

        return set;
    }

    private static async Task StreamAndEmitDirAsync(
        DirectoryInfo currentDir,
        int currentDepth,
        int maxDepth,
        bool emitFiles,
        bool emitDirectories,
        HashSet<string> filterSet,
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

                    if (filterSet.Count > 0 && !filterSet.Contains(file.Extension))
                    {
                        continue;
                    }

                    var itemContext = new FileItemContext(file);
                    await writer.WriteAsync(itemContext, cancellationToken).ConfigureAwait(false);

                    fileCounter++;
                    if (fileCounter % 100 == 0)
                    {
                        await Task.Yield();
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is DirectoryNotFoundException || ex is IOException)
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
                    await StreamAndEmitDirAsync(subDir, currentDepth + 1, maxDepth, emitFiles, emitDirectories, filterSet, writer, context, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is DirectoryNotFoundException || ex is IOException)
        {
            context.Log($"Skipping directories in '{currentDir.FullName}': {ex.Message}", LogLevel.Warning);
        }
    }

    private static long FastCountSourceFiles(string path, bool recursive, int maxDepth, bool emitFiles, bool emitDirectories, HashSet<string> filterSet)
    {
        try
        {
            var opt = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                MaxRecursionDepth = maxDepth == -1 ? int.MaxValue : maxDepth,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            long fileCount = 0;
            if (emitFiles)
            {
                if (filterSet.Count > 0)
                {
                    fileCount = Directory.EnumerateFiles(path, "*", opt)
                                         .Where(f => filterSet.Contains(Path.GetExtension(f)))
                                         .LongCount();
                }
                else
                {
                    fileCount = Directory.EnumerateFiles(path, "*", opt).LongCount();
                }
            }

            long dirCount = emitDirectories ? Directory.EnumerateDirectories(path, "*", opt).LongCount() : 0;
            return fileCount + dirCount;
        }
        catch
        {
            return 0;
        }
    }
}


