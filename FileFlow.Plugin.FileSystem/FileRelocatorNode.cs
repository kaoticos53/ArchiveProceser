using System.Security.Cryptography;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("FileRelocatorNode_Name", "FileSystem", "FileRelocatorNode_Desc")]
public class FileRelocatorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("FileRelocatorNode_Name", "Reubicador y Copiador de Archivos");
    public string Category => "FileSystem";
    public string Description => LocalizationManager.Instance.GetString("FileRelocatorNode_Desc", "Mueve o copia archivos hacia árboles de directorios destino construidos dinámicamente con tokens, verificando la integridad binaria de los datos mediante cálculo de hash SHA-256.");


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
        ["Operation"] = "Move", // Move, Copy
        ["DestinationDirectory"] = @"{SourceDir}\{Year}\{Month}",
        ["VerifyIntegrity"] = true,
        ["CreateDirectories"] = true
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[Reubicador] Archivo de origen no encontrado: '{item.CurrentPath}'", LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            string operation = Parameters.TryGetValue("Operation", out var opVal) ? ParameterHelper.GetString(opVal, "Move") : "Move";
            string destDirTemplate = Parameters.TryGetValue("DestinationDirectory", out var dirVal) ? ParameterHelper.GetString(dirVal, @"{CurrentDir}") : @"{CurrentDir}";
            bool verifyIntegrity = Parameters.TryGetValue("VerifyIntegrity", out var vVal) && ParameterHelper.GetBoolean(vVal, true);
            bool createDirs = Parameters.TryGetValue("CreateDirectories", out var crVal) && ParameterHelper.GetBoolean(crVal, true);

            string targetDir = VariableTemplateResolver.Resolve(destDirTemplate, item);
            string fileName = Path.GetFileName(item.CurrentPath);
            string targetPath = Path.Combine(targetDir, fileName);

            if (context.IsDryRun)
            {
                var plannedType = operation.Equals("Copy", StringComparison.OrdinalIgnoreCase) ? PlannedOperationType.Copy : PlannedOperationType.Move;
                context.RegisterPlannedAction(new PlannedAction(
                    Guid.NewGuid(),
                    Id,
                    Name,
                    plannedType,
                    item.CurrentPath,
                    targetPath,
                    $"{operation} file to {targetPath}",
                    item.FileSizeBytes
                ));
                item.AddLog($"[DryRun] Planned {operation}: {item.CurrentPath} -> {targetPath}");
                item.CurrentPath = targetPath;
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
                sourceHash = await CalculateSha256Async(item.CurrentPath, cancellationToken).ConfigureAwait(false);
            }

            string originalCurrent = item.CurrentPath;

            if (operation.Equals("Copy", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(item.CurrentPath, targetPath, overwrite: true);
                context.RecordJournalEntry(new JournalEntry(
                    Guid.NewGuid(),
                    Id,
                    JournalOperationType.Copied,
                    originalCurrent,
                    targetPath
                ));
            }
            else
            {
                File.Move(item.CurrentPath, targetPath, overwrite: true);
                context.RecordJournalEntry(new JournalEntry(
                    Guid.NewGuid(),
                    Id,
                    JournalOperationType.Moved,
                    originalCurrent,
                    targetPath
                ));
            }

            if (verifyIntegrity)
            {
                string destHash = await CalculateSha256Async(targetPath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(sourceHash, destHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Integrity check failed: Source hash '{sourceHash}' does not match destination hash '{destHash}'.");
                }
            }

            sw.Stop();
            item.CurrentPath = targetPath;
            item.AddLog($"{operation} completed -> {targetPath}");

            string detailsJson = $"{{\"operation\": \"{operation}\", \"sourcePath\": \"{originalCurrent.Replace("\\", "\\\\")}\", \"targetPath\": \"{targetPath.Replace("\\", "\\\\")}\", \"integrityVerified\": {verifyIntegrity.ToString().ToLowerInvariant()}, \"sha256\": \"{sourceHash}\"}}";
            context.Log($"[Reubicador] Operación {operation.ToUpperInvariant()} completada con éxito: '{Path.GetFileName(originalCurrent)}' -> '{targetPath}'", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);
            
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"source\": \"{item.CurrentPath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Reubicador] Error en reubicación: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
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
