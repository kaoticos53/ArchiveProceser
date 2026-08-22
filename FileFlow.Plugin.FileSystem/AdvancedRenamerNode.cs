using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("AdvancedRenamerNode_Name", "FileSystem", "AdvancedRenamerNode_Desc")]
public class AdvancedRenamerNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("AdvancedRenamerNode_Name", "Renombrador Avanzado con Tokens");
    public string Category => "FileSystem";
    public string Description => LocalizationManager.Instance.GetString("AdvancedRenamerNode_Desc", "Renombra archivos masivamente evaluando plantillas dinámicas con tokens ({ParentDir}, {CreationDate:yyyyMMdd}, {Hash:SHA256:8}), transformaciones de mayúsculas/minúsculas y resolución automática de colisiones.");


    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("Skipped", typeof(FileItemContext), PortDirection.Output, "Skipped"),
        new NodePort("Error", typeof(FileItemContext), PortDirection.Output, "Error")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pattern"] = "{ParentDir}_{CreationDate:yyyyMMdd}_{FileNameNoExt}.{Ext}",
        ["CollisionStrategy"] = "AutoIncrement", // Overwrite, Skip, AutoIncrement, Fail
        ["CaseTransformation"] = "None" // None, Lowercase, Uppercase, TitleCase
    };

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[AdvancedRenamerNode] File not found: '{item.CurrentPath}'", LogLevel.Warning);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            string pattern = Parameters.TryGetValue("Pattern", out var pVal) ? ParameterHelper.GetString(pVal, "{FileName}") : "{FileName}";
            string collisionStrategy = Parameters.TryGetValue("CollisionStrategy", out var cVal) ? ParameterHelper.GetString(cVal, "AutoIncrement") : "AutoIncrement";
            string caseTransform = Parameters.TryGetValue("CaseTransformation", out var csVal) ? ParameterHelper.GetString(csVal, "None") : "None";

            string resolvedName = VariableTemplateResolver.Resolve(pattern, item);

            switch (caseTransform.ToUpperInvariant())
            {
                case "LOWERCASE":
                    resolvedName = resolvedName.ToLowerInvariant();
                    break;
                case "UPPERCASE":
                    resolvedName = resolvedName.ToUpperInvariant();
                    break;
                case "TITLECASE":
                    resolvedName = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(resolvedName);
                    break;
            }

            string currentDir = Path.GetDirectoryName(item.CurrentPath) ?? string.Empty;
            string targetPath = Path.Combine(currentDir, resolvedName);

            if (string.Equals(item.CurrentPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                await context.EmitAsync("Out", item);
                return;
            }

            if (File.Exists(targetPath))
            {
                switch (collisionStrategy.ToUpperInvariant())
                {
                    case "SKIP":
                        context.Log($"[AdvancedRenamerNode] Target exists, skipping: {targetPath}", LogLevel.Information);
                        await context.EmitAsync("Skipped", item);
                        return;

                    case "FAIL":
                        throw new IOException($"Target file already exists: '{targetPath}'.");

                    case "AUTOINCREMENT":
                        targetPath = GetAutoIncrementPath(currentDir, resolvedName);
                        break;

                    case "OVERWRITE":
                    default:
                        break;
                }
            }

            if (context.IsDryRun)
            {
                context.RegisterPlannedAction(new PlannedAction(
                    Guid.NewGuid(),
                    Id,
                    Name,
                    PlannedOperationType.Rename,
                    item.CurrentPath,
                    targetPath,
                    $"Rename to {Path.GetFileName(targetPath)}",
                    item.FileSizeBytes
                ));
                item.AddLog($"[DryRun] Planned Rename: {item.CurrentPath} -> {targetPath}");
                item.CurrentPath = targetPath;
                await context.EmitAsync("Out", item);
                return;
            }

            string originalCurrent = item.CurrentPath;
            File.Move(item.CurrentPath, targetPath, overwrite: true);

            context.RecordJournalEntry(new JournalEntry(
                Guid.NewGuid(),
                Id,
                JournalOperationType.Renamed,
                originalCurrent,
                targetPath,
                UndoAction: async (ct) =>
                {
                    if (File.Exists(targetPath))
                    {
                        File.Move(targetPath, originalCurrent, true);
                        return await Task.FromResult(true);
                    }
                    return false;
                }
            ));

            item.CurrentPath = targetPath;
            item.AddLog($"Renamed to: {targetPath}");
            context.Log($"[AdvancedRenamerNode] Renamed '{originalCurrent}' -> '{targetPath}'", LogLevel.Information);
            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            context.Log($"[AdvancedRenamerNode] Error: {ex.Message}", LogLevel.Error);
            item.AddLog($"Rename failed: {ex.Message}");
            await context.EmitAsync("Error", item);
        }
    }

    private static string GetAutoIncrementPath(string folder, string fileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        int counter = 1;
        string targetPath;

        do
        {
            targetPath = Path.Combine(folder, $"{nameWithoutExt}_{counter}{ext}");
            counter++;
        } while (File.Exists(targetPath));

        return targetPath;
    }
}
