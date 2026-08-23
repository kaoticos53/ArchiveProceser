using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("EmptyDirectoryCleanerNode_Name", "FileSystem", "EmptyDirectoryCleanerNode_Desc")]
public class EmptyDirectoryCleanerNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("EmptyDirectoryCleanerNode_Name", "Limpiador de Carpetas Vacías");
    public string Category => "FileSystem";
    public string Description => LocalizationManager.Instance.GetString("EmptyDirectoryCleanerNode_Desc", "Recorre recursivamente un directorio objetivo tras procesar un lote y elimina todas las subcarpetas que hayan quedado completamente vacías (ignorando opcionalmente archivos de sistema como Thumbs.db y .DS_Store).");


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
                context.Log($"[Limpiador Carpetas] Directorio no encontrado: '{targetDir}'", LogLevel.Warning, item);
                await context.EmitAsync("Out", item);
                return;
            }

            int deletedCount = CleanEmptyDirectories(targetDir, recursive, ignoreHidden, context.IsDryRun, context, Id, Name);
            sw.Stop();

            string detailsJson = $"{{\"targetDirectory\": \"{targetDir.Replace("\\", "\\\\")}\", \"deletedCount\": {deletedCount}, \"recursive\": {recursive.ToString().ToLowerInvariant()}, \"isDryRun\": {context.IsDryRun.ToString().ToLowerInvariant()}}}";
            context.Log($"[Limpiador Carpetas] Eliminadas {deletedCount:N0} carpetas vacías en '{targetDir}' (DryRun={context.IsDryRun})", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            item.AddLog($"Cleaned {deletedCount} empty directories in {targetDir}");
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\"}}";
            context.Log($"[Limpiador Carpetas] Error al limpiar carpetas vacías: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
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
                    JournalOperationType.CreatedDirectory,
                    rootDir,
                    null
                ));
            }
            deleted++;
        }

        return deleted;
    }
}
