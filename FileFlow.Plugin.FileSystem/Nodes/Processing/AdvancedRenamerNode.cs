using System.Collections.Concurrent;
using System.Text.Json;
using System.Windows;
using FileFlow.Plugin.FileSystem.UI.Views;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Renaming;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("AdvancedRenamerNode_Name", "Files", "AdvancedRenamerNode_Desc", PipelineRole.Transform,
    "renombrar", "nombre", "patron", "tokens", "exif", "fecha", "rename", "pattern", "batch")]
public sealed class AdvancedRenamerNode : IFlowNode, INodeCustomActionProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IRenameTransformEngine _transformEngine = new RenameTransformEngine();
    private readonly RenameBatchContext _batchContext = new();
    private readonly ConcurrentDictionary<string, byte> _claimedTargetPaths = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("AdvancedRenamerNode_Name", "Renombrador Avanzado con Tokens");
    public string Category => "Files";
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
        ["RenameMode"] = "Virtual",             // "Virtual" (no modifica el archivo original) o "DirectInPlace" (renombra en disco)
        ["CollisionStrategy"] = "AutoIncrement", // Overwrite, Skip, AutoIncrement, Fail
        ["MethodSteps"] = string.Empty          // JSON serializado de List<RenameMethodStep>
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("PipelineName", ParameterEditorType.Text, DefaultValue: "Pipeline Predeterminado", DisplayOrder: 1),
        new("RenameMode", ParameterEditorType.Dropdown, DefaultValue: "Virtual", DisplayOrder: 2, Options: ["Virtual", "DirectInPlace"]),
        new("CollisionStrategy", ParameterEditorType.Dropdown, DefaultValue: "AutoIncrement", DisplayOrder: 3, Options: ["AutoIncrement", "Overwrite", "Skip", "Fail"])
    ];

    public IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("OpenRenamerPipeline", "🏷️ Pipeline de Métodos...", "🏷️", "Abrir el Estudio de Renombrado Avanzado (7 métodos, presets y vista previa)")
    ];

    public void ExecuteCustomAction(string actionId, object? context = null)
    {
        if (actionId.Equals("OpenRenamerPipeline", StringComparison.OrdinalIgnoreCase))
        {
            var window = new AdvancedRenamerEditorWindow(this);
            if (context is Window ownerWindow)
            {
                window.Owner = ownerWindow;
            }
            else if (Application.Current?.MainWindow != null)
            {
                window.Owner = Application.Current.MainWindow;
            }
            window.ShowDialog();
        }
    }

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var storage = context.GetStorage();
        string existingSource = item.GetExistingPhysicalPath();

        if (!item.IsVirtual && (string.IsNullOrWhiteSpace(existingSource) || (!await storage.FileExistsAsync(existingSource, cancellationToken) && !await storage.DirectoryExistsAsync(existingSource, cancellationToken))))
        {
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Renamer_SourceNotFound", "[Renamer] Source file or folder not found: '{0}'", item.CurrentPath), LogLevel.Warning, item);
            await context.EmitAsync("Error", item);
            return;
        }

        try
        {
            string renameMode = Parameters.TryGetValue("RenameMode", out var rmVal) ? ParameterHelper.GetString(rmVal, "Virtual") : "Virtual";
            bool isVirtual = string.Equals(renameMode, "Virtual", StringComparison.OrdinalIgnoreCase);
            string collisionStrategy = Parameters.TryGetValue("CollisionStrategy", out var cVal) ? ParameterHelper.GetString(cVal, "AutoIncrement") : "AutoIncrement";
            var steps = ResolveSteps();

            string currentFileName = Path.GetFileName(item.CurrentPath);
            string currentDir = Path.GetDirectoryName(item.CurrentPath) ?? string.Empty;

            var transformResult = _transformEngine.Transform(currentFileName, item, steps, _batchContext, recordTraces: false);

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
                context.Log(LocalizationManager.Instance.GetFormattedString("Log_Renamer_NoChange", "[Renamer] Name is identical to current, no changes needed: '{0}'", resolvedName), LogLevel.Debug, item);
                await context.EmitAsync("Out", item);
                return;
            }

            bool isSameFileDifferentCasing = string.Equals(item.CurrentPath, targetPath, StringComparison.OrdinalIgnoreCase);

            var storageStrategy = collisionStrategy.ToUpperInvariant() switch
            {
                "SKIP" => StorageCollisionStrategy.Skip,
                "FAIL" => StorageCollisionStrategy.ThrowError,
                "AUTOINCREMENT" => StorageCollisionStrategy.RenameIncremental,
                _ => StorageCollisionStrategy.Overwrite
            };

            // Verificación y resolución atómica de colisiones (contra storage y contra el lote concurrente)
            bool shouldSkip = false;
            bool targetExists = !isSameFileDifferentCasing && await storage.FileExistsAsync(targetPath, cancellationToken);

            if (targetExists || _claimedTargetPaths.ContainsKey(targetPath))
            {
                switch (storageStrategy)
                {
                    case StorageCollisionStrategy.Skip:
                        shouldSkip = true;
                        break;

                    case StorageCollisionStrategy.ThrowError:
                        throw new IOException($"Target file already exists: '{targetPath}'.");

                    case StorageCollisionStrategy.RenameIncremental:
                        targetPath = await GetAutoIncrementPathAsync(currentDir, resolvedName, storage, cancellationToken);
                        context.Log(LocalizationManager.Instance.GetFormattedString("Log_Renamer_AutoIncrementCollision", "[Renamer] Auto-increment collision resolved: '{0}'", Path.GetFileName(targetPath)), LogLevel.Debug, item);
                        break;

                    case StorageCollisionStrategy.Overwrite:
                    default:
                        context.Log(LocalizationManager.Instance.GetFormattedString("Log_Renamer_OverwriteCollision", "[Renamer] Overwriting existing file per 'Overwrite' policy: '{0}'", targetPath), LogLevel.Debug, item);
                        break;
                }
            }

            if (!shouldSkip)
            {
                _claimedTargetPaths.TryAdd(targetPath, 0);
            }

            if (shouldSkip)
            {
                sw.Stop();
                context.Log(LocalizationManager.Instance.GetFormattedString("Log_Renamer_SkipExisting", "[Renamer] Target already exists, skipping per 'Skip' strategy: '{0}'", targetPath), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds);
                await context.EmitAsync("Skipped", item);
                return;
            }

            if (context.IsDryRun || isVirtual || item.IsVirtual)
            {
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
                }
                else
                {
                    item.AddLog($"Renamed (Virtual): {resolvedName}");
                }

                // Sincronizar con el sistema de archivos virtual si está activo
                context.VirtualFileSystem?.RenameFile(item.CurrentPath, targetPath, Name, Id);

                sw.Stop();
                string prevName = Path.GetFileName(item.CurrentPath);
                item.CurrentPath = targetPath;

                string detailsJson = $"{{\"originalName\": \"{prevName.Replace("\"", "\\\"")}\", \"newName\": \"{Path.GetFileName(targetPath).Replace("\"", "\\\"")}\", \"renameMode\": \"{(isVirtual ? "Virtual" : "DryRun")}\", \"collisionStrategy\": \"{collisionStrategy}\", \"stepsCount\": {steps.Count}}}";
                context.Log(LocalizationManager.Instance.GetFormattedString("Log_Renamer_VirtualTransformed", "[Renamer] Name transformed (Virtual Mode): '{0}' -> '{1}'", prevName, Path.GetFileName(targetPath)), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJson);

                await context.EmitAsync("Out", item);
                return;
            }

            string originalCurrent = item.GetExistingPhysicalPath();
            var moveResult = await storage.MoveAsync(originalCurrent, targetPath, StorageCollisionStrategy.Overwrite, cancellationToken);
            if (!moveResult.IsSuccess)
            {
                throw new IOException(moveResult.ErrorMessage ?? "Failed to move/rename file on storage.");
            }
            targetPath = moveResult.FinalPath;

            context.RecordJournalEntry(new JournalEntry(
                Guid.NewGuid(),
                Id,
                JournalOperationType.Renamed,
                originalCurrent,
                targetPath,
                UndoAction: async (ct) =>
                {
                    if (await storage.FileExistsAsync(targetPath, ct))
                    {
                        var undoResult = await storage.MoveAsync(targetPath, originalCurrent, StorageCollisionStrategy.Overwrite, ct);
                        return undoResult.IsSuccess;
                    }
                    return false;
                }
            ));

            sw.Stop();
            item.PhysicalPath = targetPath;
            item.CurrentPath = targetPath;
            item.AddLog($"Renamed (DirectInPlace): {targetPath}");

            string detailsJsonInPlace = $"{{\"originalName\": \"{Path.GetFileName(originalCurrent).Replace("\"", "\\\"")}\", \"newName\": \"{Path.GetFileName(targetPath).Replace("\"", "\\\"")}\", \"renameMode\": \"DirectInPlace\", \"collisionStrategy\": \"{collisionStrategy}\", \"stepsCount\": {steps.Count}}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Renamer_PhysicalRenamed", "[Renamer] Physical rename successful (In-Place): '{0}' -> '{1}'", Path.GetFileName(originalCurrent), Path.GetFileName(targetPath)), LogLevel.Information, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: detailsJsonInPlace);

            await context.EmitAsync("Out", item);
        }
        catch (Exception ex)
        {
            sw.Stop();
            string errJson = $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\", \"source\": \"{item.CurrentPath.Replace("\\", "\\\\")}\"}}";
            context.Log(LocalizationManager.Instance.GetFormattedString("Log_Renamer_Error", "[Renamer] Rename error: {0}", ex.Message), LogLevel.Error, item, durationMs: sw.Elapsed.TotalMilliseconds, detailsJson: errJson);
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
                        Parameters["MethodSteps"] = parsed;
                        return parsed;
                    }
                }
                catch { }
            }
        }

        // Si no hay pasos explícitos configurados pero se especificó un PipelineName correspondiente a un preset incorporado
        if (Parameters.TryGetValue("PipelineName", out var nameVal) && nameVal != null && !string.IsNullOrWhiteSpace(nameVal.ToString()))
        {
            string pName = nameVal.ToString()!;
            if (!string.Equals(pName, "Pipeline Predeterminado", StringComparison.OrdinalIgnoreCase))
            {
                var matchingPreset = RenamerPresetService.GetBuiltinPresets()
                    .FirstOrDefault(p => string.Equals(p.Name, pName, StringComparison.OrdinalIgnoreCase) ||
                                         p.Name.Contains(pName, StringComparison.OrdinalIgnoreCase));
                if (matchingPreset != null && matchingPreset.Steps.Count > 0)
                {
                    return matchingPreset.Steps;
                }
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

        Parameters["MethodSteps"] = defaultSteps;
        return defaultSteps;
    }

    private async ValueTask<string> GetAutoIncrementPathAsync(string folder, string fileName, IStorageService storage, CancellationToken ct)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        int counter = 1;
        string targetPath;

        do
        {
            targetPath = Path.Combine(folder, $"{nameWithoutExt}_{counter}{ext}");
            counter++;
        } while (_claimedTargetPaths.ContainsKey(targetPath) || await storage.FileExistsAsync(targetPath, ct));

        return targetPath;
    }
}
