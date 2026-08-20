using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace FileFlow.Plugin.Archives;

[NodeDefinition("SmartUnpackNode_Name", "Archives", "SmartUnpackNode_Desc")]
public class SmartUnpackNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SmartUnpackNode_Name", "Smart Unpack");
    public string Category => "Archives";
    public string Description => LocalizationManager.Instance.GetString("SmartUnpackNode_Desc", "Inspects archive structure and extracts intelligently.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DestinationFolder"] = @"C:\FileFlowUnpacked",
        ["CleanWrapper"] = true,
        ["AutoDeleteAfterExtraction"] = false
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string archivePath = item.CurrentPath;
        string destFolder = Parameters.TryGetValue("DestinationFolder", out var val) ? val?.ToString() ?? @"C:\FileFlowUnpacked" : @"C:\FileFlowUnpacked";
        destFolder = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(destFolder, item);
        bool cleanWrapper = Parameters.TryGetValue("CleanWrapper", out var cwVal) && Convert.ToBoolean(cwVal);
        bool autoDelete = Parameters.TryGetValue("AutoDeleteAfterExtraction", out var adVal) && Convert.ToBoolean(adVal);
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && Convert.ToBoolean(dryVal);

        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            context.Log($"SmartUnpackNode: Archive file '{archivePath}' not found.", LogLevel.Warning);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            context.Log($"SmartUnpackNode inspecting archive: {archivePath}", LogLevel.Information);

            using var archive = ArchiveFactory.Open(archivePath);

            var entryKeys = archive.Entries
                .Where(e => !e.IsDirectory)
                .Select(e => e.Key?.Replace('\\', '/') ?? string.Empty)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToList();

            string? commonRoot = GetCommonRootFolder(entryKeys);
            bool hasSingleWrapper = !string.IsNullOrEmpty(commonRoot);

            string archiveNameNoExt = Path.GetFileNameWithoutExtension(archivePath);
            string finalExtractDir;

            if (hasSingleWrapper && cleanWrapper)
            {
                finalExtractDir = Path.Combine(destFolder, archiveNameNoExt);
                context.Log($"SmartUnpackNode: Single wrapper detected ('{commonRoot}'). Extracting directly to: {finalExtractDir}", LogLevel.Information);
            }
            else
            {
                finalExtractDir = Path.Combine(destFolder, archiveNameNoExt);
                context.Log($"SmartUnpackNode: Multiple root entries detected. Extracting into subfolder: {finalExtractDir}", LogLevel.Information);
            }

            if (!isDryRun)
            {
                if (!Directory.Exists(finalExtractDir))
                {
                    Directory.CreateDirectory(finalExtractDir);
                }

                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    entry.WriteToDirectory(finalExtractDir, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }

                if (autoDelete)
                {
                    File.Delete(archivePath);
                    context.Log($"SmartUnpackNode: Auto-deleted archive file '{archivePath}'.", LogLevel.Information);
                }
            }

            var outputItem = new FileItemContext(finalExtractDir, isDirectory: true);
            outputItem.Metadata["UnpackedFrom"] = archivePath;
            outputItem.Metadata["HasSingleWrapper"] = hasSingleWrapper;
            outputItem.Metadata["ArchiveFormat"] = Path.GetExtension(archivePath).TrimStart('.').ToUpperInvariant();
            outputItem.Metadata["UnpackedFileCount"] = entryKeys.Count;
            outputItem.AddLog($"SmartUnpackNode extracted to {finalExtractDir}");

            await context.EmitAsync("Out", outputItem);
        }
        catch (Exception ex)
        {
            context.Log($"SmartUnpackNode Extraction Failed for '{archivePath}': {ex.Message}", LogLevel.Error);
            item.AddLog($"SmartUnpackNode error: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static string? GetCommonRootFolder(List<string> entryKeys)
    {
        if (entryKeys.Count == 0) return null;

        string firstKey = entryKeys[0];
        int slashIndex = firstKey.IndexOf('/');
        if (slashIndex <= 0) return null;

        string root = firstKey[..slashIndex];

        foreach (string key in entryKeys)
        {
            if (!key.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return root;
    }
}
