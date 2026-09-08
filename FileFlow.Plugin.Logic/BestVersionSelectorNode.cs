using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("BestVersionSelectorNode_Name", "Logic", "BestVersionSelectorNode_Desc", PipelineRole.Filter,
    "comparar", "version", "mejor", "tamaño", "peso", "resolucion", "autopurga", "selector", "best", "version")]
public sealed class BestVersionSelectorNode : IFlowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("BestVersionSelectorNode_Name", "Selector de Mejor Versión");
    public string Category => "Logic";
    public string Description => LocalizationManager.Instance.GetString("BestVersionSelectorNode_Desc", "Compara dos candidatos de archivo (ej. versión optimizada vs original) según criterios como menor tamaño o porcentaje de ahorro, establece el ganador como archivo activo y puede autopurgar el archivo intermedio perdedor.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")
    };

    public IReadOnlyList<NodePort> Outputs { get; } = new[]
    {
        new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out"),
        new NodePort("WonA", typeof(FileItemContext), PortDirection.Output, "WonA"),
        new NodePort("WonB", typeof(FileItemContext), PortDirection.Output, "WonB")
    };

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CandidateA"] = "{CurrentPath}",
        ["CandidateB"] = "{OriginalPath}",
        ["Criterion"] = "SmallestSize", // SmallestSize, LargestSize, SavedPercentThreshold, CandidateA, CandidateB
        ["Threshold"] = 0.0,
        ["DiscardLoser"] = true,
        ["SetWinnerAsCurrent"] = true
    };

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("CandidateA", ParameterEditorType.FileVersionSelector, DefaultValue: "{CurrentPath}", DisplayOrder: 1, HelpText: "Primer archivo o versión candidato a comparar (por defecto la versión activa actual)"),
        new("CandidateB", ParameterEditorType.FileVersionSelector, DefaultValue: "{OriginalPath}", DisplayOrder: 2, HelpText: "Segundo archivo o versión candidato a comparar (por defecto el archivo original)"),
        new("Criterion", ParameterEditorType.Dropdown, DefaultValue: "SmallestSize", DisplayOrder: 3, Options: ["SmallestSize", "LargestSize", "SavedPercentThreshold", "CandidateA", "CandidateB"], HelpText: "Criterio de selección para determinar el ganador"),
        new("Threshold", ParameterEditorType.Number, DefaultValue: 0.0, DisplayOrder: 4, HelpText: "Umbral requerido (ej. porcentaje de ahorro para SavedPercentThreshold)", DependsOnKey: "Criterion", DependsOnValues: ["SavedPercentThreshold"]),
        new("DiscardLoser", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 5, HelpText: "Autopurga el archivo intermedio perdedor del disco si no es el archivo original"),
        new("SetWinnerAsCurrent", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 6, HelpText: "Establece el archivo ganador como archivo activo del contexto para los siguientes nodos")
    ];

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        string candAPattern = Parameters.TryGetValue("CandidateA", out var ca) ? ParameterHelper.GetString(ca, "{CurrentPath}") : "{CurrentPath}";
        string candBPattern = Parameters.TryGetValue("CandidateB", out var cb) ? ParameterHelper.GetString(cb, "{OriginalPath}") : "{OriginalPath}";
        string criterion = Parameters.TryGetValue("Criterion", out var cr) ? ParameterHelper.GetString(cr, "SmallestSize") : "SmallestSize";
        double threshold = Parameters.TryGetValue("Threshold", out var th) ? ParameterHelper.GetDouble(th, 0.0) : 0.0;
        bool discardLoser = Parameters.TryGetValue("DiscardLoser", out var dl) ? ParameterHelper.GetBoolean(dl, true) : true;
        bool setWinnerAsCurrent = Parameters.TryGetValue("SetWinnerAsCurrent", out var sw) ? ParameterHelper.GetBoolean(sw, true) : true;

        string pathA = ResolveCandidatePath(candAPattern, item);
        string pathB = ResolveCandidatePath(candBPattern, item);

        bool existsA = !string.IsNullOrWhiteSpace(pathA) && File.Exists(pathA);
        bool existsB = !string.IsNullOrWhiteSpace(pathB) && File.Exists(pathB);

        bool isWinnerA;
        string reason;

        if (!existsA && !existsB)
        {
            isWinnerA = true;
            reason = "Neither candidate file exists physically; defaulting to Candidate A.";
        }
        else if (existsA && !existsB)
        {
            isWinnerA = true;
            reason = "Candidate B does not exist physically; Candidate A wins by default.";
        }
        else if (!existsA && existsB)
        {
            isWinnerA = false;
            reason = "Candidate A does not exist physically; Candidate B wins by default.";
        }
        else
        {
            long sizeA = new FileInfo(pathA).Length;
            long sizeB = new FileInfo(pathB).Length;

            switch (criterion.ToUpperInvariant())
            {
                case "LARGESTSIZE":
                    isWinnerA = sizeA >= sizeB;
                    reason = $"Candidate {(isWinnerA ? "A" : "B")} is larger or equal ({sizeA} vs {sizeB} bytes).";
                    break;

                case "SAVEDPERCENTTHRESHOLD":
                    // Baseline is Candidate B (typically Original)
                    double savedPct = sizeB > 0 ? ((double)(sizeB - sizeA) / sizeB) * 100.0 : 0.0;
                    isWinnerA = savedPct >= threshold;
                    reason = isWinnerA
                        ? $"Candidate A saved {savedPct:F2}%, reaching the threshold of {threshold:F2}%."
                        : $"Candidate A saved {savedPct:F2}%, below required threshold of {threshold:F2}%; Candidate B retained.";
                    break;

                case "CANDIDATEA":
                    isWinnerA = true;
                    reason = "Forced selection of Candidate A.";
                    break;

                case "CANDIDATEB":
                    isWinnerA = false;
                    reason = "Forced selection of Candidate B.";
                    break;

                case "SMALLESTSIZE":
                default:
                    isWinnerA = sizeA <= sizeB;
                    reason = $"Candidate {(isWinnerA ? "A" : "B")} is smaller or equal ({sizeA} vs {sizeB} bytes).";
                    break;
            }
        }

        string winnerPath = isWinnerA ? pathA : pathB;
        string loserPath = isWinnerA ? pathB : pathA;

        // Auto-purge loser if requested and not the immutable original file
        if (discardLoser && !string.IsNullOrWhiteSpace(loserPath) && File.Exists(loserPath))
        {
            if (!string.Equals(Path.GetFullPath(loserPath), Path.GetFullPath(item.OriginalPath), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(loserPath);
                    context.Log($"[BestVersionSelector] Auto-purged losing candidate intermediate file: '{loserPath}'", LogLevel.Debug, item);
                }
                catch (Exception ex)
                {
                    context.Log($"[BestVersionSelector] Could not auto-purge losing candidate '{loserPath}': {ex.Message}", LogLevel.Warning, item);
                }
            }
            else
            {
                context.Log($"[BestVersionSelector] Preserved losing candidate because it is the original file: '{loserPath}'", LogLevel.Debug, item);
            }
        }

        if (setWinnerAsCurrent && !string.IsNullOrWhiteSpace(winnerPath))
        {
            item.CurrentPath = winnerPath;
            item.PhysicalPath = winnerPath;
            if (File.Exists(winnerPath))
            {
                item.FileSizeBytes = new FileInfo(winnerPath).Length;
            }
        }

        item.Metadata["SelectedVersion"] = isWinnerA ? "CandidateA" : "CandidateB";
        item.Metadata["SelectedVersionPath"] = winnerPath;
        item.Metadata["SelectionReason"] = reason;
        item.AddLog($"[BestVersionSelector] Selected: {(isWinnerA ? "CandidateA" : "CandidateB")} ('{winnerPath}') - {reason}");

        context.Log($"[BestVersionSelector] Selection: {(isWinnerA ? "CandidateA" : "CandidateB")} ('{Path.GetFileName(winnerPath)}') - {reason}", LogLevel.Information, item);

        await context.EmitAsync("Out", item).ConfigureAwait(false);
        if (isWinnerA)
        {
            await context.EmitAsync("WonA", item).ConfigureAwait(false);
        }
        else
        {
            await context.EmitAsync("WonB", item).ConfigureAwait(false);
        }
    }

    private static string ResolveCandidatePath(string candidatePattern, FileItemContext item)
    {
        if (string.IsNullOrWhiteSpace(candidatePattern))
        {
            return item.CurrentPath;
        }

        string resolved = VariableTemplateResolver.Resolve(candidatePattern, item);
        if (File.Exists(resolved))
        {
            return resolved;
        }

        string? versionPath = item.GetVersionPath(candidatePattern);
        if (!string.IsNullOrWhiteSpace(versionPath) && File.Exists(versionPath))
        {
            return versionPath;
        }

        return resolved;
    }
}
