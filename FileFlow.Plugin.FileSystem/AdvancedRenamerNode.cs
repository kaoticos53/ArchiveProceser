using System.Collections.Concurrent;
using System.Text.Json;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Renaming;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("AdvancedRenamerNode_Name", "FileSystem", "AdvancedRenamerNode_Desc")]
public class AdvancedRenamerNode : IFlowNode
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IRenameTransformEngine _transformEngine = new RenameTransformEngine();
    private readonly RenameBatchContext _batchContext = new();
    private readonly ConcurrentDictionary<string, byte> _claimedTargetPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _collisionLock = new();

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("AdvancedRenamerNode_Name", "Renombrador Avanzado con Tokens");
    public string Category => "FileSystem";
    public string Description => LocalizationManager.Instance.GetString("AdvancedRenamerNode_Desc", "Renombra archivos y carpetas masivamente aplicando un pipeline acumulativo de métodos secuenciales (plantillas, regex, mayúsculas, numeración, sustitución y normalización) con resolución de colisiones.");

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
        ["PipelineName"] = "Pipeline Predeterminado",
        ["CollisionStrategy"] = "AutoIncrement", // Overwrite, Skip, AutoIncrement, Fail
        ["MethodSteps"] = string.Empty          // JSON serializado de List<RenameMethodStep>
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
            string collisionStrategy = Parameters.TryGetValue("CollisionStrategy", out var cVal) ? ParameterHelper.GetString(cVal, "AutoIncrement") : "AutoIncrement";
            var steps = ResolveSteps();

            string currentFileName = Path.GetFileName(item.CurrentPath);
            string currentDir = Path.GetDirectoryName(item.CurrentPath) ?? string.Empty;

            var transformResult = _transformEngine.Transform(currentFileName, item, steps, _batchContext);

            if (!string.IsNullOrEmpty(transformResult.ErrorMessage))
            {
                throw new InvalidOperationException($"Error en transformación de renombrado: {transformResult.ErrorMessage}");
            }

            string resolvedName = transformResult.ResultFileName;

            // Sanitización preventiva final de caracteres inválidos de Windows
            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (resolvedName.IndexOfAny(invalidChars) >= 0)
            {
                foreach (char c in invalidChars)
                {
                    resolvedName = resolvedName.Replace(c, '_');
                }
            }

            string targetPath = Path.Combine(currentDir, resolvedName);

            if (string.Equals(item.CurrentPath, targetPath, StringComparison.Ordinal))
            {
                context.Log($"[Renombrador] Nombre idéntico al actual, sin cambios necesarios: '{resolvedName}'", LogLevel.Debug, item);
                await context.EmitAsync("Out", item);
                return;
            }

            bool isSameFileDifferentCasing = string.Equals(item.CurrentPath, targetPath, StringComparison.OrdinalIgnoreCase);

            // Verificación y resolución atómica de colisiones (contra disco y contra el lote concurrente)
            bool shouldSkip = false;
            lock (_collisionLock)
            {
                bool hasDiskCollision = !isSameFileDifferentCasing && File.Exists(targetPath);
                bool hasBatchCollision = _claimedTargetPaths.ContainsKey(targetPath);

                if (hasDiskCollision || hasBatchCollision)
                {
                    switch (collisionStrategy.ToUpperInvariant())
                    {
                        case "SKIP":
                            shouldSkip = true;
                            break;

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

                if (!shouldSkip)
                {
                    _claimedTargetPaths.TryAdd(targetPath, 0);
                }
            }

            if (shouldSkip)
            {
                sw.Stop();
                context.Log($"[Renombrador] Destino ya existente, omitiendo según estrategia 'Skip': '{targetPath}'", LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds);
                await context.EmitAsync("Skipped", item);
                return;
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

            string detailsJson = $"{{\"originalName\": \"{Path.GetFileName(originalCurrent).Replace("\"", "\\\"")}\", \"newName\": \"{Path.GetFileName(targetPath).Replace("\"", "\\\"")}\", \"collisionStrategy\": \"{collisionStrategy}\", \"stepsCount\": {steps.Count}}}";
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

    private IReadOnlyList<RenameMethodStep> ResolveSteps()
    {
        if (!Parameters.TryGetValue("PipelineName", out var pnVal) || pnVal == null || string.IsNullOrWhiteSpace(pnVal.ToString()))
        {
            Parameters["PipelineName"] = "Pipeline Predeterminado";
        }

        if (Parameters.TryGetValue("MethodSteps", out var stepsObj) && stepsObj != null)
        {
            if (stepsObj is IReadOnlyList<RenameMethodStep> stepList && stepList.Count > 0)
            {
                return stepList;
            }
            if (stepsObj is string jsonStr && !string.IsNullOrWhiteSpace(jsonStr))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<RenameMethodStep>>(jsonStr, JsonOptions);
                    if (parsed != null && parsed.Count > 0)
                    {
                        return parsed;
                    }
                }
                catch { }
            }
        }

        // Migrar y limpiar parámetros legados (Pattern, NameTemplate, CaseTransformation)
        string legacyPattern = string.Empty;
        if (Parameters.TryGetValue("Pattern", out var pVal) && pVal != null && !string.IsNullOrWhiteSpace(pVal.ToString()))
        {
            legacyPattern = pVal.ToString()!;
            Parameters.Remove("Pattern");
        }
        else if (Parameters.TryGetValue("NameTemplate", out var ntVal) && ntVal != null && !string.IsNullOrWhiteSpace(ntVal.ToString()))
        {
            legacyPattern = ntVal.ToString()!;
            Parameters.Remove("NameTemplate");
        }

        string legacyCase = string.Empty;
        if (Parameters.TryGetValue("CaseTransformation", out var csVal) && csVal != null)
        {
            legacyCase = csVal.ToString()!;
            Parameters.Remove("CaseTransformation");
        }

        string pattern = !string.IsNullOrWhiteSpace(legacyPattern) ? legacyPattern : "{ParentDir}_{CreationDate:yyyyMMdd}_{FileNameNoExt}.{Ext}";

        var defaultSteps = new List<RenameMethodStep>
        {
            new()
            {
                MethodType = RenameMethodType.NewName,
                ApplyTo = ApplyToTarget.FullName,
                Pattern = pattern,
                IsEnabled = true,
                Name = "Plantilla Inicial"
            }
        };

        if (!string.IsNullOrWhiteSpace(legacyCase) && !string.Equals(legacyCase, "None", StringComparison.OrdinalIgnoreCase))
        {
            var caseType = legacyCase.ToUpperInvariant() switch
            {
                "LOWERCASE" => CaseTransformType.Lowercase,
                "UPPERCASE" => CaseTransformType.Uppercase,
                "TITLECASE" => CaseTransformType.TitleCase,
                "SENTENCECASE" => CaseTransformType.SentenceCase,
                _ => CaseTransformType.Lowercase
            };

            defaultSteps.Add(new RenameMethodStep
            {
                MethodType = RenameMethodType.CaseConversion,
                ApplyTo = ApplyToTarget.FullName,
                CaseType = caseType,
                IsEnabled = true,
                Name = $"Conversión: {legacyCase}"
            });
        }

        Parameters["MethodSteps"] = RenamerPresetService.SerializeSteps(defaultSteps);
        return defaultSteps;
    }

    private string GetAutoIncrementPath(string folder, string fileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        int counter = 1;
        string targetPath;

        do
        {
            targetPath = Path.Combine(folder, $"{nameWithoutExt}_{counter}{ext}");
            counter++;
        } while (File.Exists(targetPath) || _claimedTargetPaths.ContainsKey(targetPath));

        return targetPath;
    }
}
