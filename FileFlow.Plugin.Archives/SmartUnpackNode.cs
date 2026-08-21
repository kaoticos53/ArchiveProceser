using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

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
        ["AutoDeleteAfterExtraction"] = false,
        ["RecursiveUnpack"] = true
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string archivePath = item.CurrentPath;
        string destFolder = Parameters.TryGetValue("DestinationFolder", out var val) ? ParameterHelper.GetString(val, @"C:\FileFlowUnpacked") : @"C:\FileFlowUnpacked";
        destFolder = FileFlow.Sdk.TemplateEngine.VariableTemplateResolver.Resolve(destFolder, item);
        bool cleanWrapper = Parameters.TryGetValue("CleanWrapper", out var cwVal) ? ParameterHelper.GetBoolean(cwVal, true) : true;
        bool autoDelete = Parameters.TryGetValue("AutoDeleteAfterExtraction", out var adVal) && ParameterHelper.GetBoolean(adVal, false);
        bool recursiveUnpack = !Parameters.TryGetValue("RecursiveUnpack", out var ruVal) || ParameterHelper.GetBoolean(ruVal, true);
        bool isDryRun = item.Metadata.TryGetValue("DryRun", out var dryVal) && ParameterHelper.GetBoolean(dryVal, false);

        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            context.Log($"SmartUnpackNode: Archive file '{archivePath}' not found.", LogLevel.Warning);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            context.Log($"SmartUnpackNode inspecting archive: {archivePath}", LogLevel.Information);

            using var archive = ArchiveFactory.OpenArchive(new FileInfo(archivePath), new ReaderOptions());

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
                finalExtractDir = destFolder;
                context.Log($"SmartUnpackNode: Single wrapper detected ('{commonRoot}'). Cleaning redundant wrapper level and extracting directly to: {finalExtractDir}", LogLevel.Information);
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

                string fullTargetDir = Path.GetFullPath(finalExtractDir);

                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string entryPath = entry.Key ?? string.Empty;
                    string destinationPath = Path.GetFullPath(Path.Combine(finalExtractDir, entryPath));

                    if (!destinationPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new System.Security.SecurityException($"Zip Slip attempt detected! Entry '{entryPath}' targets outside of extraction directory.");
                    }

                    entry.WriteToDirectory(finalExtractDir, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }

                if (recursiveUnpack)
                {
                    ExtractNestedArchives(finalExtractDir, context, cancellationToken);
                }

                if (autoDelete)
                {
                    File.Delete(archivePath);
                    context.Log($"SmartUnpackNode: Auto-deleted original archive file '{archivePath}'.", LogLevel.Information);
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

    private static void ExtractNestedArchives(string targetDir, IFlowExecutionContext context, CancellationToken cancellationToken)
    {
        const int maxDepth = 5;

        for (int depth = 0; depth < maxDepth; depth++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var allFiles = Directory.EnumerateFiles(targetDir, "*.*", SearchOption.AllDirectories).ToList();

            var nestedPrimaryArchives = allFiles
                .Where(f => IsPrimaryArchiveFile(f))
                .ToList();

            if (nestedPrimaryArchives.Count == 0)
            {
                break;
            }

            foreach (var nestedArchive in nestedPrimaryArchives)
            {
                cancellationToken.ThrowIfCancellationRequested();
                context.Log($"SmartUnpackNode: Found nested archive '{Path.GetFileName(nestedArchive)}'. Extracting recursively...", LogLevel.Information);

                try
                {
                    using var archive = ArchiveFactory.OpenArchive(new FileInfo(nestedArchive), new ReaderOptions());
                    string nestedExtractDir = Path.GetDirectoryName(nestedArchive) ?? targetDir;

                    foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string entryPath = entry.Key ?? string.Empty;
                        string destPath = Path.GetFullPath(Path.Combine(nestedExtractDir, entryPath));

                        if (!destPath.StartsWith(Path.GetFullPath(nestedExtractDir), StringComparison.OrdinalIgnoreCase))
                        {
                            throw new System.Security.SecurityException($"Zip Slip attempt detected in nested archive '{entryPath}'");
                        }

                        entry.WriteToDirectory(nestedExtractDir, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }

                    // Delete intermediate nested primary archive
                    File.Delete(nestedArchive);
                    context.Log($"SmartUnpackNode: Deleted intermediate nested archive '{Path.GetFileName(nestedArchive)}'.", LogLevel.Information);
                }
                catch (Exception ex)
                {
                    context.Log($"SmartUnpackNode: Failed to unpack nested archive '{nestedArchive}': {ex.Message}", LogLevel.Warning);
                }
            }

            // Clean up any intermediate secondary volume files (.r01, .r02, .z01, .part02.rar) in targetDir
            var secondaryVolumes = Directory.EnumerateFiles(targetDir, "*.*", SearchOption.AllDirectories)
                .Where(f => IsSecondaryVolumeFile(f))
                .ToList();

            foreach (var secVol in secondaryVolumes)
            {
                try
                {
                    File.Delete(secVol);
                    context.Log($"SmartUnpackNode: Deleted intermediate secondary volume '{Path.GetFileName(secVol)}'.", LogLevel.Information);
                }
                catch { }
            }
        }
    }

    private static bool IsPrimaryArchiveFile(string filePath)
    {
        string fileName = Path.GetFileName(filePath).ToLowerInvariant();
        if (fileName.EndsWith(".part01.rar") || fileName.EndsWith(".part1.rar")) return true;
        if (System.Text.RegularExpressions.Regex.IsMatch(fileName, @"\.part(?!0*1\.)\d+\.rar$")) return false;
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".tgz" or ".bz2";
    }

    private static bool IsSecondaryVolumeFile(string filePath)
    {
        string fileName = Path.GetFileName(filePath).ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(fileName, @"\.(r\d{2,3}|z\d{2,3}|part(?!0*1\.)\d+\.rar)$");
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
