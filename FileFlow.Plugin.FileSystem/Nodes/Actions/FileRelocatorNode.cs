using System.IO;
using System.Security.Cryptography;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("FileRelocatorNode_Name", "Files", "FileRelocatorNode_Desc", PipelineRole.Sink,
    "mover", "copiar", "relocate", "move", "copy", "folder", "reubicar")]
public class FileRelocatorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("FileRelocatorNode_Name", "File Relocator");
    public string Category => "Files";
    public string Description => LocalizationManager.Instance.GetString("FileRelocatorNode_Desc", "Safely moves or copies files with SHA-256 integrity verification, automatic folder creation, and rollback support.");

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
        ["SourcePath"] = "{CurrentPath}",
        ["Operation"] = "Copy",
        ["DestinationDirectory"] = @"{SourceDir}\{Year}\{Month}",
        ["VerifyIntegrity"] = true,
        ["CreateDirectories"] = true,
        ["CleanupSource"] = false
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("SourcePath", ParameterEditorType.FileVersionSelector, DefaultValue: "{CurrentPath}", DisplayOrder: 1),
        new("Operation", ParameterEditorType.Dropdown, DefaultValue: "Copy", DisplayOrder: 2, Options: ["Copy", "Move"]),
        new("DestinationDirectory", ParameterEditorType.FolderPath, DefaultValue: @"{SourceDir}\{Year}\{Month}", DisplayOrder: 3),
        new("VerifyIntegrity", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4),
        new("CreateDirectories", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 5),
        new("CleanupSource", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 6)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        string sourcePathPattern = Parameters.TryGetValue("SourcePath", out var spVal) ? ParameterHelper.GetString(spVal, "{CurrentPath}") : "{CurrentPath}";
        string resolvedSource = VariableTemplateResolver.Resolve(sourcePathPattern, item);
        string? versionPath = item.GetVersionPath(sourcePathPattern);
        if (!string.IsNullOrWhiteSpace(versionPath))
        {
            resolvedSource = versionPath;
        }

        var storage = context.GetStorage();
        string sourcePath = !string.IsNullOrWhiteSpace(resolvedSource) ? resolvedSource : item.GetExistingPhysicalPath();
        if (string.IsNullOrWhiteSpace(sourcePath)) sourcePath = item.CurrentPath;

        bool hasSource = await storage.FileExistsAsync(sourcePath, cancellationToken).ConfigureAwait(false)
            || await storage.DirectoryExistsAsync(sourcePath, cancellationToken).ConfigureAwait(false);

        if (!hasSource && !item.IsVirtual && !context.IsVirtualFileSystemEnabled)
        {
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Relocator_NotFound", "[Relocator] Source file not found: '{0}'", sourcePath), LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        string operation = "Move";
        try
        {
            operation = Parameters.TryGetValue("Operation", out var opVal) ? ParameterHelper.GetString(opVal, "Move") : "Move";
            string destDirTemplate = Parameters.TryGetValue("DestinationDirectory", out var dirVal)
                ? ParameterHelper.GetString(dirVal, @"{CurrentDir}")
                : (Parameters.TryGetValue("DestinationFolder", out var dfVal) ? ParameterHelper.GetString(dfVal, @"{CurrentDir}") : @"{CurrentDir}");
            bool verifyIntegrity = Parameters.TryGetValue("VerifyIntegrity", out var vVal) && ParameterHelper.GetBoolean(vVal, true);
            bool createDirs = Parameters.TryGetValue("CreateDirectories", out var crVal) && ParameterHelper.GetBoolean(crVal, true);
            bool cleanupSource = Parameters.TryGetValue("CleanupSource", out var csVal) && ParameterHelper.GetBoolean(csVal, false);

            string targetDir = VariableTemplateResolver.Resolve(destDirTemplate, item);
            string fileName = Path.GetFileName(sourcePath);
            string targetPath = Path.Combine(targetDir, fileName);

            if (createDirs)
            {
                await storage.CreateDirectoryAsync(targetDir, cancellationToken).ConfigureAwait(false);
            }

            bool isCopy = operation.Equals("Copy", StringComparison.OrdinalIgnoreCase);
            bool isSamePath = string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase);

            if (context.IsDryRun)
            {
                var plannedType = isCopy ? PlannedOperationType.Copy : PlannedOperationType.Move;
                context.RegisterPlannedAction(new PlannedAction(
                    Guid.NewGuid(),
                    Id,
                    Name,
                    plannedType,
                    sourcePath,
                    targetPath,
                    $"{operation} file to {targetPath}",
                    item.FileSizeBytes
                ));
                item.AddLog($"[DryRun] Planned {operation}: {sourcePath} -> {targetPath}");
                item.CurrentPath = targetPath;
                await context.EmitAsync("Out", item);
                return;
            }

            if (isSamePath)
            {
                context.Log(LocalizationManager.Instance.GetFormattedString("Log_Relocator_SamePath", "[Relocator] Source and target are identical. Skipping physical operation: '{0}'", targetPath), LogLevel.Debug, item);
                await context.EmitAsync("Out", item);
                return;
            }

            string sourceHash = string.Empty;
            if (verifyIntegrity && !item.IsVirtual && !context.IsVirtualFileSystemEnabled)
            {
                sourceHash = await CalculateSha256Async(storage, sourcePath, cancellationToken).ConfigureAwait(false);
            }

            StorageOperationResult result;
            if (isCopy)
            {
                result = await storage.CopyAsync(sourcePath, targetPath, StorageCollisionStrategy.Overwrite, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                result = await storage.MoveAsync(sourcePath, targetPath, StorageCollisionStrategy.Overwrite, cancellationToken).ConfigureAwait(false);
            }

            if (!result.IsSuccess)
            {
                throw new IOException(result.ErrorMessage ?? $"Failed to {operation} '{sourcePath}' to '{targetPath}'.");
            }

            if (verifyIntegrity && !item.IsVirtual && !context.IsVirtualFileSystemEnabled && !string.IsNullOrEmpty(sourceHash))
            {
                string destHash = await CalculateSha256Async(storage, result.FinalPath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(sourceHash, destHash, StringComparison.OrdinalIgnoreCase))
                {
                    await storage.DeleteAsync(result.FinalPath, permanent: true, cancellationToken).ConfigureAwait(false);
                    throw new IOException($"Integrity check failed: Source hash '{sourceHash}' does not match destination hash '{destHash}'.");
                }
            }

            if (!item.IsVirtual && !context.IsVirtualFileSystemEnabled)
            {
                context.RecordJournalEntry(new JournalEntry(
                    Guid.NewGuid(),
                    Id,
                    isCopy ? JournalOperationType.Copied : JournalOperationType.Moved,
                    sourcePath,
                    result.FinalPath
                ));
            }

            if (cleanupSource && !string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(item.OriginalPath), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await storage.DeleteAsync(sourcePath, permanent: true, cancellationToken).ConfigureAwait(false);
                    context.Log($"[Relocator] Cleaned up intermediate source file: '{sourcePath}'", LogLevel.Debug, item);
                }
                catch (Exception cleanupEx)
                {
                    context.Log($"[Relocator] Could not clean up intermediate source file '{sourcePath}': {cleanupEx.Message}", LogLevel.Warning, item);
                }
            }

            item.CurrentPath = result.FinalPath;
            item.PhysicalPath = result.FinalPath;
            item.AddLog($"FileRelocatorNode {operation}: {result.FinalPath}");
            sw.Stop();

            string detailsJson = $"{{\"operation\": \"{operation}\", \"source\": \"{sourcePath.Replace("\\", "\\\\")}\", \"target\": \"{result.FinalPath.Replace("\\", "\\\\")}\", \"verified\": {verifyIntegrity.ToString().ToLowerInvariant()}, \"sizeBytes\": {item.FileSizeBytes}}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Relocator_Success", "[Relocator] Successfully executed {0}: '{1}' -> '{2}'", operation, Path.GetFileName(sourcePath), result.FinalPath), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            await context.EmitAsync("Out", item);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            string errDetails = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"operation\": \"{operation}\", \"source\": \"{sourcePath.Replace("\\", "\\\\")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Relocator_Error", "[Relocator] Error during {0}: {1}", operation, ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errDetails);
            item.AddLog($"Relocator failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static async Task<string> CalculateSha256Async(IStorageService storage, string path, CancellationToken ct)
    {
        await using var stream = await storage.OpenReadAsync(path, ct).ConfigureAwait(false);
        byte[] hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }
}
