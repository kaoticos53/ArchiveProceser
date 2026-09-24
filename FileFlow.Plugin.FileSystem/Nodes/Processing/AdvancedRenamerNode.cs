using System.Collections.Concurrent;
using System.Text.Json;
using FileFlow.Plugin.FileSystem.UI.Views;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Renaming;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.FileSystem;

[NodeDefinition("AdvancedRenamerNode_Name", "Files", "AdvancedRenamerNode_Desc", PipelineRole.Transform,
    "renombrar", "nombre", "patron", "tokens", "exif", "fecha", "rename", "pattern", "batch")]
public sealed class AdvancedRenamerNode : FlowNodeBase, INodeCustomActionProvider
{
    private readonly IRenameTransformEngine _transformEngine = new RenameTransformEngine();
    private readonly RenameBatchContext _batchContext = new();
    private readonly ConcurrentDictionary<string, byte> _claimedTargetPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _stepsLock = new();

    /// <summary>
    /// Los pasos del pipeline, resueltos <b>una sola vez</b> por instancia.
    ///
    /// <para>La resolución migra parámetros legados —lee <c>Pattern</c>, lo retira y deja los pasos en
    /// <c>MethodSteps</c>—, y esa migración no es atómica: el motor entrega los ítems de un lote en paralelo y
    /// el nodo es el mismo objeto para todos. Con dos ítems a la vez, el segundo podía leer el <c>Pattern</c> ya
    /// retirado y los <c>MethodSteps</c> todavía sin escribir, y caía en la plantilla por omisión
    /// (<c>{ParentDir}_{CreationDate:yyyyMMdd}_{FileNameNoExt}.{Ext}</c>) — renombrando a un nombre que nadie
    /// configuró, en silencio—. Lo destapó la prueba del puerto <c>Skipped</c>, que con dos archivos veía uno
    /// omitido y el otro renombrado con la plantilla por omisión.</para>
    /// </summary>
    private IReadOnlyList<RenameMethodStep>? _resolvedSteps;

    public override string Name => LocalizationManager.Instance.GetString("AdvancedRenamerNode_Name", "Renombrador Inteligente");
    public override string Category => "Files";
    public override string Description => LocalizationManager.Instance.GetString("AdvancedRenamerNode_Desc", "Renombra archivos por lotes mediante transformaciones avanzadas, patrones basados en tokens, fechas y números secuenciales.");

    public AdvancedRenamerNode()
    {
        Inputs =
        [
            new("In", typeof(FileItemContext), PortDirection.Input, "In", "Flujo de archivos de entrada")
        ];

        Outputs =
        [
            new("Out", typeof(FileItemContext), PortDirection.Output, "Out", "Archivos renombrados"),
            new("Skipped", typeof(FileItemContext), PortDirection.Output, "Skipped", "Archivos que ya existían en el destino y se omitieron"),
            new("Error", typeof(FileItemContext), PortDirection.Output, "Error", "Archivos que no se pudieron renombrar")
        ];

        Parameters["PipelineName"] = "Pipeline Predeterminado";
        Parameters["RenameMode"] = "Virtual";
        Parameters["CollisionStrategy"] = "AutoIncrement";
        Parameters["MethodSteps"] = "";
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("PipelineName", ParameterEditorType.Dropdown, DefaultValue: "Pipeline Predeterminado", DisplayOrder: 1, Options: GetPresetOptions()),
        new("RenameMode", ParameterEditorType.Dropdown, DefaultValue: "Virtual", DisplayOrder: 2, Options: ["Virtual", "DirectInPlace"]),
        new("CollisionStrategy", ParameterEditorType.Dropdown, DefaultValue: "AutoIncrement", DisplayOrder: 3, Options: ["AutoIncrement", "Overwrite", "Skip", "Fail"])
    ];

    private static IReadOnlyList<string> GetPresetOptions()
    {
        var presets = RenamerPresetService.GetBuiltinPresets();
        var options = new List<string> { "Pipeline Predeterminado" };
        foreach (var p in presets)
        {
            if (!options.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            {
                options.Add(p.Name);
            }
        }
        return options;
    }

    public override IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("OpenRenamerPipeline", "🏷️ Pipeline de Métodos...", "🏷️", "Abrir el Estudio de Renombrado Avanzado (7 métodos, presets y vista previa)")
    ];

    public void ExecuteCustomAction(string actionId, object? context = null)
    {
        if (actionId.Equals("OpenRenamerPipeline", StringComparison.OrdinalIgnoreCase))
        {
            var window = new AdvancedRenamerEditorWindow(this);
            Action? onCompleted = null;
            object? parentWindow = context;

            if (context is NodeCustomActionContext customCtx)
            {
                parentWindow = customCtx.ParentWindow;
                onCompleted = customCtx.OnCompleted;
            }
            else if (context is Action callback)
            {
                onCompleted = callback;
            }

            if (onCompleted != null)
            {
                window.Closed += (_, _) => onCompleted();
            }

            if (parentWindow is Avalonia.Controls.Window ownerWindow)
            {
                window.ShowDialog(ownerWindow);
            }
            else if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                window.ShowDialog(desktop.MainWindow);
            }
            else
            {
                window.Show();
            }
        }
    }

    public override async Task ExecuteAsync(
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
            string renameMode = GetParameter("RenameMode", "Virtual");
            bool isVirtual = string.Equals(renameMode, "Virtual", StringComparison.OrdinalIgnoreCase);
            string collisionStrategy = GetParameter("CollisionStrategy", "AutoIncrement");
            var steps = ResolveSteps(context, item);

            string currentFileName = Path.GetFileName(item.CurrentPath);
            string currentDir = Path.GetDirectoryName(item.CurrentPath) ?? string.Empty;

            var transformResult = _transformEngine.Transform(currentFileName, item, steps, _batchContext, recordTraces: false);

            if (!string.IsNullOrEmpty(transformResult.ErrorMessage))
            {
                throw new InvalidOperationException($"Error en transformación de renombrado: {transformResult.ErrorMessage}");
            }

            string resolvedName = transformResult.ResultFileName;

            // Sanitización preventiva final de caracteres inválidos de Windows
            char[] invalidChars = CrossPlatformPath.InvalidFileNameChars;
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

    private IReadOnlyList<RenameMethodStep> ResolveSteps(IFlowExecutionContext context, FileItemContext item)
    {
        if (_resolvedSteps is { Count: > 0 } alreadyResolved)
        {
            return alreadyResolved;
        }

        lock (_stepsLock)
        {
            _resolvedSteps ??= MigrateAndResolveSteps(context, item);
            return _resolvedSteps;
        }
    }

    private IReadOnlyList<RenameMethodStep> MigrateAndResolveSteps(IFlowExecutionContext context, FileItemContext item)
    {
        if (string.IsNullOrWhiteSpace(GetParameter("PipelineName", string.Empty)))
        {
            Parameters["PipelineName"] = "Pipeline Predeterminado";
        }

        // Los pasos son una lista o su JSON: se leen sin convertir para conservar el tipo original.
        object? stepsObj = GetParameter<object?>("MethodSteps", null);
        if (stepsObj is not null)
        {
            if (stepsObj is IReadOnlyList<RenameMethodStep> stepList && stepList.Count > 0)
            {
                return stepList;
            }
            if (stepsObj is string jsonStr && !string.IsNullOrWhiteSpace(jsonStr))
            {
                try
                {
                    // La lectura es la del SDK —la misma que usa el editor de renombrado—, así que acepta lo que
                    // el producto escribe y lo que escribe quien retoca un flujo a mano (los pasos con los nombres
                    // de las enumeraciones).
                    var parsed = RenamerPresetService.DeserializeSteps(jsonStr);
                    if (parsed.Count > 0)
                    {
                        Parameters["MethodSteps"] = parsed;
                        return parsed;
                    }
                }
                catch (JsonException ex)
                {
                    // No se calla: seguir con la plantilla por omisión sería renombrar a un nombre que nadie
                    // configuró sin decirlo, que es el defecto que ya destapó la prueba del puerto Skipped.
                    context.Log(
                        LocalizationManager.Instance.GetFormattedString(
                            "Log_Renamer_StepsUnreadable",
                            "[Renombrador] No se pudieron leer los pasos del pipeline configurados: {0}. Se aplica la plantilla por omisión.",
                            ex.Message),
                        LogLevel.Warning,
                        item);
                }
            }
        }

        // Si no hay pasos explícitos configurados pero se especificó un PipelineName correspondiente a un preset incorporado
        string pName = GetParameter("PipelineName", string.Empty);
        if (!string.IsNullOrWhiteSpace(pName))
        {
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
        string legacyPattern = GetParameter("Pattern", string.Empty);
        if (!string.IsNullOrWhiteSpace(legacyPattern))
        {
            Parameters.Remove("Pattern");
        }
        else
        {
            legacyPattern = GetParameter("NameTemplate", string.Empty);
            if (!string.IsNullOrWhiteSpace(legacyPattern))
            {
                Parameters.Remove("NameTemplate");
            }
        }

        string legacyCase = GetParameter("CaseTransformation", string.Empty);
        if (Parameters.ContainsKey("CaseTransformation"))
        {
            // Se consume el parámetro legado para que la migración no se repita en cada ejecución.
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
