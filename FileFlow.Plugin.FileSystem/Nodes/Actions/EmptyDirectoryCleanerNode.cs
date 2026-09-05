using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("EmptyDirectoryCleanerNode_Name", "Files", "EmptyDirectoryCleanerNode_Desc", PipelineRole.Transform,
    "limpiar", "carpetas vacias", "directorios vacios", "purgar", "cleaner", "empty")]
public class EmptyDirectoryCleanerNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("EmptyDirectoryCleanerNode_Name", "Empty Directory Cleaner");
    public string Category => "Files";
    public string Description => LocalizationManager.Instance.GetString("EmptyDirectoryCleanerNode_Desc", "Recursively scans a target directory after batch processing and removes all empty subdirectories.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("TriggerIn", typeof(FileItemContext), PortDirection.Input, "TriggerIn")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TargetDirectory"] = @"{SourceDir}",
        ["Recursive"] = true,
        ["IgnoreHiddenSystemFiles"] = true
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            string dirTemplate = Parameters.TryGetValue("TargetDirectory", out var dVal) ? ParameterHelper.GetString(dVal, @"{CurrentDir}") : @"{CurrentDir}";
            bool recursive = Parameters.TryGetValue("Recursive", out var rVal) && ParameterHelper.GetBoolean(rVal, true);
            bool ignoreHidden = Parameters.TryGetValue("IgnoreHiddenSystemFiles", out var hVal) && ParameterHelper.GetBoolean(hVal, true);

            string targetDir = VariableTemplateResolver.Resolve(dirTemplate, item);

            if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
            {
                context.Log(LocalizationManager.Instance.GetFormattedString("Log_EmptyCleaner_NotFound", "[Empty Directory Cleaner] Directory not found: '{0}'", targetDir), LogLevel.Warning, item);
                await context.EmitAsync("Out", item);
                return;
            }

            int deletedCount = CleanEmptyDirectories(targetDir, recursive, ignoreHidden, context.IsDryRun, context, Id, Name);
            sw.Stop();

            string detailsJson = $"{{\"targetDirectory\": \"{targetDir.Replace("\\", "\\\\")}\", \"deletedCount\": {deletedCount}, \"recursive\": {recursive.ToString().ToLowerInvariant()}, \"isDryRun\": {context.IsDryRun.ToString().ToLowerInvariant()}}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_EmptyCleaner_Deleted", "[Empty Directory Cleaner] Deleted {0:N0} empty folders in '{1}' (DryRun={2})", deletedCount, targetDir, context.IsDryRun), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            item.AddLog($"Cleaned {deletedCount} empty directories in {targetDir}");
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_EmptyCleaner_Error", "[Empty Directory Cleaner] Error cleaning empty folders: {0}", ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"Empty directory cleaner failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static int CleanEmptyDirectories(string rootDir, bool recursive, bool ignoreHidden, bool isDryRun, IFlowExecutionContext context, string nodeId, string nodeName)
    {
        int deleted = 0;

        if (recursive)
        {
            foreach (var subDir in Directory.EnumerateDirectories(rootDir))
            {
                deleted += CleanEmptyDirectories(subDir, recursive, ignoreHidden, isDryRun, context, nodeId, nodeName);
            }
        }

        var entries = Directory.EnumerateFileSystemEntries(rootDir).ToList();
        if (ignoreHidden)
        {
            entries = entries.Where(e =>
            {
                string name = Path.GetFileName(e);
                return !name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase) &&
                       !name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) &&
                       !name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        if (entries.Count == 0)
        {
            if (isDryRun)
            {
                context.RegisterPlannedAction(new PlannedAction(
                    Guid.NewGuid(),
                    nodeId,
                    nodeName,
                    PlannedOperationType.Delete,
                    rootDir,
                    null,
                    "Delete empty directory"
                ));
            }
            else
            {
                Directory.Delete(rootDir, recursive: true);
                context.RecordJournalEntry(new JournalEntry(
                    Guid.NewGuid(),
                    nodeId,
                    JournalOperationType.DeletedPermanently,
                    rootDir,
                    null
                ));
            }
            deleted++;
        }

        return deleted;
    }
}
