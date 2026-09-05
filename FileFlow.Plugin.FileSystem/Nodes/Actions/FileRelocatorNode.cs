using System.Security.Cryptography;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("FileRelocatorNode_Name", "Files", "FileRelocatorNode_Desc", PipelineRole.Transform,
    "reubicar", "mover", "copiar", "hardlink", "transferir", "relocate", "move", "copy")]
public class FileRelocatorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("FileRelocatorNode_Name", "File Relocator and Copier");
    public string Category => "Files";
    public string Description => LocalizationManager.Instance.GetString("FileRelocatorNode_Desc", "Moves or copies files to dynamically token-built destination trees, verifying binary integrity via SHA-256 hash calculation.");

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
        ["Operation"] = "Copy", // Copy, Move
        ["DestinationDirectory"] = @"{SourceDir}\{Year}\{Month}",
        ["VerifyIntegrity"] = true,
        ["CreateDirectories"] = true
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("Operation", ParameterEditorType.Dropdown, DefaultValue: "Copy", DisplayOrder: 1, Options: ["Copy", "Move"]),
        new("DestinationDirectory", ParameterEditorType.FolderPath, DefaultValue: @"{SourceDir}\{Year}\{Month}", DisplayOrder: 2),
        new("VerifyIntegrity", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 3),
        new("CreateDirectories", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 4)
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string sourcePath = item.GetExistingPhysicalPath();

        if (string.IsNullOrWhiteSpace(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
        {
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Relocator_NotFound", "[Relocator] Source file not found: '{0}'", item.CurrentPath), LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        string operation = "Move";
        try
        {
            operation = Parameters.TryGetValue("Operation", out var opVal) ? ParameterHelper.GetString(opVal, "Move") : "Move";
            string destDirTemplate = Parameters.TryGetValue("DestinationDirectory", out var dirVal) ? ParameterHelper.GetString(dirVal, @"{CurrentDir}") : @"{CurrentDir}";
            bool verifyIntegrity = Parameters.TryGetValue("VerifyIntegrity", out var vVal) && ParameterHelper.GetBoolean(vVal, true);
            bool createDirs = Parameters.TryGetValue("CreateDirectories", out var crVal) && ParameterHelper.GetBoolean(crVal, true);

            string targetDir = VariableTemplateResolver.Resolve(destDirTemplate, item);
            string fileName = Path.GetFileName(item.CurrentPath);
            string targetPath = Path.Combine(targetDir, fileName);

            string fullSource = Path.GetFullPath(sourcePath);
            string fullTarget = Path.GetFullPath(targetPath);
            bool isSamePath = string.Equals(fullSource, fullTarget, StringComparison.OrdinalIgnoreCase);

            if (context.IsDryRun)
            {
                var plannedType = operation.Equals("Copy", StringComparison.OrdinalIgnoreCase) ? PlannedOperationType.Copy : PlannedOperationType.Move;
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

            if (createDirs && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            string sourceHash = string.Empty;
            if (verifyIntegrity)
            {
                sourceHash = await CalculateSha256Async(sourcePath, cancellationToken).ConfigureAwait(false);
            }

            string originalCurrent = sourcePath;

            if (operation.Equals("Copy", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, targetPath, overwrite: true);

                if (verifyIntegrity)
                {
                    string destHash = await CalculateSha256Async(targetPath, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(sourceHash, destHash, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(targetPath); } catch { }
                        throw new IOException($"Integrity check failed: Source hash '{sourceHash}' does not match destination hash '{destHash}'.");
                    }
                }

                context.RecordJournalEntry(new JournalEntry(
                    Guid.NewGuid(),
                    Id,
                    JournalOperationType.Copied,
                    originalCurrent,
                    targetPath
                ));

                item.PhysicalPath = targetPath;
            }
            else
            {
                if (verifyIntegrity)
                {
                    // Safe Move: Copy -> Verify -> Delete Original
                    File.Copy(sourcePath, targetPath, overwrite: true);
                    string destHash = await CalculateSha256Async(targetPath, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(sourceHash, destHash, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(targetPath); } catch { }
                        throw new IOException($"Integrity check failed during Move: Source hash '{sourceHash}' does not match destination hash '{destHash}'.");
                    }
                    File.Delete(originalCurrent);
                }
                else
                {
                    File.Move(sourcePath, targetPath, overwrite: true);
                }

                context.RecordJournalEntry(new JournalEntry(
                    Guid.NewGuid(),
                    Id,
                    JournalOperationType.Moved,
                    originalCurrent,
                    targetPath
                ));

                item.PhysicalPath = targetPath;
            }

            sw.Stop();
            item.CurrentPath = targetPath;
            item.AddLog($"{operation} completed -> {targetPath}");

            string detailsJson = $"{{\"operation\": \"{operation}\", \"sourcePath\": \"{originalCurrent.Replace("\\", "\\\\")}\", \"targetPath\": \"{targetPath.Replace("\\", "\\\\")}\", \"integrityVerified\": {verifyIntegrity.ToString().ToLowerInvariant()}, \"sha256\": \"{sourceHash}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Relocator_Success", "[Relocator] Operation {0} completed successfully: '{1}' -> '{2}'", operation.ToUpperInvariant(), Path.GetFileName(originalCurrent), targetPath), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);
            
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"source\": \"{item.CurrentPath.Replace("\\", "\\\\")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Relocator_Error", "[Relocator] Error during operation {0}: {1}", operation, ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
            item.AddLog($"Relocation failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static async Task<string> CalculateSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        byte[] hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }
}
