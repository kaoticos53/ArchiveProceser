using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("IntermediateCleanupNode_Name", "Logic", "IntermediateCleanupNode_Desc", PipelineRole.Control,
    "cleanup", "limpiar", "temporales", "borrar", "purgar", "intermedios", "purge")]
public sealed class IntermediateCleanupNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("IntermediateCleanupNode_Name", "Limpieza de Archivos Intermedios");
    public override string Category => "Logic";
    public override string Description => LocalizationManager.Instance.GetString("IntermediateCleanupNode_Desc", "Elimina del disco los archivos temporales e intermedios generados por transformadores previos, preservando siempre el archivo original y opcionalmente el archivo activo actual.");

    public IntermediateCleanupNode()
    {
        Inputs =
        [
            new NodePort(WellKnownPorts.In, typeof(FileItemContext), PortDirection.Input, WellKnownPorts.In)
        ];

        Outputs =
        [
            new NodePort(WellKnownPorts.Out, typeof(FileItemContext), PortDirection.Output, WellKnownPorts.Out)
        ];

        Parameters["KeepOriginal"] = true;
        Parameters["KeepCurrent"] = true;
        Parameters["TargetTags"] = ""; // Empty = all registered intermediate versions
    }

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();

        bool keepCurrent = GetParameter("KeepCurrent", true);
        string targetTags = GetParameter("TargetTags", "");

        var tagsToClean = string.IsNullOrWhiteSpace(targetTags)
            ? item.FileVersions.Keys.ToList()
            : targetTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        int deletedCount = 0;

        foreach (string tag in tagsToClean)
        {
            if (string.Equals(tag, "Original", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!item.FileVersions.TryGetValue(tag, out string? filePath) || string.IsNullOrWhiteSpace(filePath))
            {
                continue;
            }

            // Strictly prevent deletion of OriginalPath
            if (string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(item.OriginalPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check if we should keep current active path
            if (keepCurrent && string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(item.CurrentPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (await storage.FileExistsAsync(filePath, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await storage.DeleteAsync(filePath, permanent: true, cancellationToken).ConfigureAwait(false);
                    deletedCount++;
                    context.Log($"[IntermediateCleanup] Purged intermediate file for version '{tag}': '{filePath}'", LogLevel.Debug, item);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    context.Log($"[IntermediateCleanup] Failed to delete intermediate file '{filePath}': {ex.Message}", LogLevel.Warning, item);
                }
            }
        }

        item.AddLog($"[IntermediateCleanup] Purged {deletedCount} intermediate file(s)");
        context.Log($"[IntermediateCleanup] Successfully purged {deletedCount} intermediate file(s)", LogLevel.Information, item);

        await context.EmitAsync(WellKnownPorts.Out, item).ConfigureAwait(false);
    }
}
