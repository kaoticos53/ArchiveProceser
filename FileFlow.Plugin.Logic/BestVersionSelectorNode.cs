using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;
using FileFlow.Sdk.TemplateEngine;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("BestVersionSelectorNode_Name", "Logic", "BestVersionSelectorNode_Desc", PipelineRole.Filter,
    "comparar", "version", "mejor", "tamaño", "peso", "resolucion", "autopurga", "selector", "best", "version")]
public sealed class BestVersionSelectorNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("BestVersionSelectorNode_Name", "Selector de Mejor Versión");
    public override string Category => "Logic";
    public override string Description => LocalizationManager.Instance.GetString("BestVersionSelectorNode_Desc", "Compara dos candidatos de archivo (ej. versión optimizada vs original) según criterios como menor tamaño o porcentaje de ahorro, establece el ganador como archivo activo y puede autopurgar el archivo intermedio perdedor.");

    public BestVersionSelectorNode()
    {
        Inputs =
        [
            new NodePort(WellKnownPorts.In, typeof(FileItemContext), PortDirection.Input, WellKnownPorts.In)
        ];

        Outputs =
        [
            new NodePort(WellKnownPorts.Out, typeof(FileItemContext), PortDirection.Output, WellKnownPorts.Out),
            new NodePort("WonA", typeof(FileItemContext), PortDirection.Output, "WonA"),
            new NodePort("WonB", typeof(FileItemContext), PortDirection.Output, "WonB")
        ];

        Parameters["CandidateA"] = "{CurrentPath}";
        Parameters["CandidateB"] = "{OriginalPath}";
        Parameters["Criterion"] = "SmallestSize"; // SmallestSize, LargestSize, SavedPercentThreshold, CandidateA, CandidateB
        Parameters["Threshold"] = 0.0;
        Parameters["DiscardLoser"] = true;
        Parameters["SetWinnerAsCurrent"] = true;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("CandidateA", ParameterEditorType.FileVersionSelector, DefaultValue: "{CurrentPath}", DisplayOrder: 1, HelpText: "Primer archivo o versión candidato a comparar (por defecto la versión activa actual)"),
        new("CandidateB", ParameterEditorType.FileVersionSelector, DefaultValue: "{OriginalPath}", DisplayOrder: 2, HelpText: "Segundo archivo o versión candidato a comparar (por defecto el archivo original)"),
        new("Criterion", ParameterEditorType.Dropdown, DefaultValue: "SmallestSize", DisplayOrder: 3, Options: ["SmallestSize", "LargestSize", "SavedPercentThreshold", "CandidateA", "CandidateB"], HelpText: "Criterio de selección para determinar el ganador"),
        new("Threshold", ParameterEditorType.Number, DefaultValue: 0.0, DisplayOrder: 4, HelpText: "Umbral requerido (ej. porcentaje de ahorro para SavedPercentThreshold)", DependsOnKey: "Criterion", DependsOnValues: ["SavedPercentThreshold"]),
        new("DiscardLoser", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 5, HelpText: "Autopurga el archivo intermedio perdedor del disco si no es el archivo original"),
        new("SetWinnerAsCurrent", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 6, HelpText: "Establece el archivo ganador como archivo activo del contexto para los siguientes nodos")
    ];

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();

        string candAPattern = GetParameter("CandidateA", "{CurrentPath}");
        string candBPattern = GetParameter("CandidateB", "{OriginalPath}");
        string criterion = GetParameter("Criterion", "SmallestSize");
        double threshold = GetParameter("Threshold", 0.0);
        bool discardLoser = GetParameter("DiscardLoser", true);
        bool setWinnerAsCurrent = GetParameter("SetWinnerAsCurrent", true);

        string pathA = await ResolveCandidatePathAsync(storage, candAPattern, item, cancellationToken).ConfigureAwait(false);
        string pathB = await ResolveCandidatePathAsync(storage, candBPattern, item, cancellationToken).ConfigureAwait(false);

        bool existsA = !string.IsNullOrWhiteSpace(pathA) && await storage.FileExistsAsync(pathA, cancellationToken).ConfigureAwait(false);
        bool existsB = !string.IsNullOrWhiteSpace(pathB) && await storage.FileExistsAsync(pathB, cancellationToken).ConfigureAwait(false);

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
            long sizeA = await storage.GetFileSizeAsync(pathA, cancellationToken).ConfigureAwait(false);
            long sizeB = await storage.GetFileSizeAsync(pathB, cancellationToken).ConfigureAwait(false);

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
        if (discardLoser && !string.IsNullOrWhiteSpace(loserPath) && await storage.FileExistsAsync(loserPath, cancellationToken).ConfigureAwait(false))
        {
            if (!string.Equals(Path.GetFullPath(loserPath), Path.GetFullPath(item.OriginalPath), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await storage.DeleteAsync(loserPath, permanent: true, cancellationToken).ConfigureAwait(false);
                    context.Log($"[BestVersionSelector] Auto-purged losing candidate intermediate file: '{loserPath}'", LogLevel.Debug, item);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
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
            if (await storage.FileExistsAsync(winnerPath, cancellationToken).ConfigureAwait(false))
            {
                item.FileSizeBytes = await storage.GetFileSizeAsync(winnerPath, cancellationToken).ConfigureAwait(false);
            }
        }

        item.Metadata["SelectedVersion"] = isWinnerA ? "CandidateA" : "CandidateB";
        item.Metadata["SelectedVersionPath"] = winnerPath;
        item.Metadata["SelectionReason"] = reason;
        item.AddLog($"[BestVersionSelector] Selected: {(isWinnerA ? "CandidateA" : "CandidateB")} ('{winnerPath}') - {reason}");

        context.Log($"[BestVersionSelector] Selection: {(isWinnerA ? "CandidateA" : "CandidateB")} ('{Path.GetFileName(winnerPath)}') - {reason}", LogLevel.Information, item);

        await context.EmitAsync(WellKnownPorts.Out, item).ConfigureAwait(false);
        if (isWinnerA)
        {
            await context.EmitAsync("WonA", item).ConfigureAwait(false);
        }
        else
        {
            await context.EmitAsync("WonB", item).ConfigureAwait(false);
        }
    }

    private static async Task<string> ResolveCandidatePathAsync(IStorageService storage, string candidatePattern, FileItemContext item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(candidatePattern))
        {
            return item.CurrentPath;
        }

        string resolved = VariableTemplateResolver.Resolve(candidatePattern, item);
        if (await storage.FileExistsAsync(resolved, cancellationToken).ConfigureAwait(false))
        {
            return resolved;
        }

        string? versionPath = item.GetVersionPath(candidatePattern);
        if (!string.IsNullOrWhiteSpace(versionPath) && await storage.FileExistsAsync(versionPath, cancellationToken).ConfigureAwait(false))
        {
            return versionPath;
        }

        return resolved;
    }
}
