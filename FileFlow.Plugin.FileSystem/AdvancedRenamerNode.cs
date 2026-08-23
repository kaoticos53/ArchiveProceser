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
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(item.CurrentPath) || !File.Exists(item.CurrentPath))
        {
            context.Log($"[Renombrador] Archivo de origen no encontrado: '{item.CurrentPath}'", LogLevel.Warning, item);
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

            // Sanitizar caracteres inválidos en el nombre de archivo resultante
            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (resolvedName.IndexOfAny(invalidChars) >= 0)
            {
                foreach (char c in invalidChars)
                {
                    resolvedName = resolvedName.Replace(c, '_');
                }
            }

            string currentDir = Path.GetDirectoryName(item.CurrentPath) ?? string.Empty;
            string targetPath = Path.Combine(currentDir, resolvedName);

            if (string.Equals(item.CurrentPath, targetPath, StringComparison.Ordinal))
            {
                context.Log($"[Renombrador] Nombre idéntico al actual, sin cambios necesarios: '{resolvedName}'", LogLevel.Debug, item);
                await context.EmitAsync("Out", item);
                return;
            }

            bool isSameFileDifferentCasing = string.Equals(item.CurrentPath, targetPath, StringComparison.OrdinalIgnoreCase);

            if (!isSameFileDifferentCasing && File.Exists(targetPath))
            {
                switch (collisionStrategy.ToUpperInvariant())
                {
                    case "SKIP":
                        sw.Stop();
                        context.Log($"[Renombrador] Destino ya existente, omitiendo según estrategia 'Skip': '{targetPath}'", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds);
                        await context.EmitAsync("Skipped", item);
                        return;

                    case "FAIL":
                        throw new IOException($"Target file already exists: '{targetPath}'.");

                    case "AUTOINCREMENT":
                        targetPath = GetAutoIncrementPath(currentDir, resolvedName);
                        context.Log($"[Renombrador] Resuelta colisión con autoincremento: '{Path.GetFileName(targetPath)}'", LogLevel.Debug, item);
                        break;

                    case "OVERWRITE":
                    default:
                        context.Log($"[Renombrador] Sobrescribiendo archivo existente por política 'Overwrite': '{targetPath}'", LogLevel.Debug, item);
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

            sw.Stop();
            item.CurrentPath = targetPath;
            item.AddLog($"Renamed to: {targetPath}");

            string detailsJson = $"{{\"pattern\": \"{pattern.Replace("\"", "\\\"")}\", \"originalName\": \"{Path.GetFileName(originalCurrent).Replace("\"", "\\\"")}\", \"newName\": \"{Path.GetFileName(targetPath).Replace("\"", "\\\"")}\", \"collisionStrategy\": \"{collisionStrategy}\"}}";
            context.Log($"[Renombrador] Renombrado con éxito: '{Path.GetFileName(originalCurrent)}' -> '{Path.GetFileName(targetPath)}'", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"source\": \"{item.CurrentPath.Replace("\\", "\\\\")}\"}}";
            context.Log($"[Renombrador] Error en renombramiento: {ex.Message}", LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
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
