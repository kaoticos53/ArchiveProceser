using System.IO;
using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Storage;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("FileForkNode_Name", "Logic", "FileForkNode_Desc", PipelineRole.Control,
    tags: ["fork", "duplicar", "clonar", "bifurcar", "versiones", "original", "paralelo", "avanzado"],
    SubCategory = "Advanced")]
public sealed class FileForkNode : FlowNodeBase
{
    public override string Name => LocalizationManager.Instance.GetString("FileForkNode_Name", "Bifurcador de Flujo (Original vs Actual)");
    public override string Category => "Logic";
    public override string Description => LocalizationManager.Instance.GetString("FileForkNode_Desc", "Clona el contexto en ramas paralelas independientes para procesar simultáneamente el archivo original y la versión actual procesada (ej. archivar original en NAS y publicar versión optimizada).");

    public FileForkNode()
    {
        Inputs =
        [
            new NodePort(WellKnownPorts.In, typeof(FileItemContext), PortDirection.Input, WellKnownPorts.In)
        ];

        Outputs =
        [
            new NodePort("Original", typeof(FileItemContext), PortDirection.Output, "Original"),
            new NodePort("Current", typeof(FileItemContext), PortDirection.Output, "Current"),
            new NodePort("Version", typeof(FileItemContext), PortDirection.Output, "Version")
        ];

        Parameters["ForkOriginal"] = true;
        Parameters["ForkCurrent"] = true;
        Parameters["ForkAllVersions"] = false;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("ForkOriginal", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 1, HelpText: "Emite una copia independiente por el puerto 'Original' restableciendo el archivo activo al original intacto"),
        new("ForkCurrent", ParameterEditorType.Toggle, DefaultValue: true, DisplayOrder: 2, HelpText: "Emite una copia independiente por el puerto 'Current' con el archivo procesado actual"),
        new("ForkAllVersions", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 3, HelpText: "Emite copias adicionales por el puerto 'Version' para cada versión intermedia registrada en el flujo")
    ];

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var storage = context.GetStorage();

        bool forkOriginal = GetParameter("ForkOriginal", true);
        bool forkCurrent = GetParameter("ForkCurrent", true);
        bool forkAllVersions = GetParameter("ForkAllVersions", false);

        if (forkOriginal)
        {
            var origClone = item.DeepClone();
            origClone.CurrentPath = item.OriginalPath;
            origClone.PhysicalPath = item.OriginalPath;
            if (await storage.FileExistsAsync(item.OriginalPath, cancellationToken).ConfigureAwait(false))
            {
                origClone.FileSizeBytes = await storage.GetFileSizeAsync(item.OriginalPath, cancellationToken).ConfigureAwait(false);
            }
            origClone.Metadata["ForkBranch"] = "Original";
            origClone.AddLog("[FileForkNode] Emitted clone on 'Original' port");
            await context.EmitAsync("Original", origClone).ConfigureAwait(false);
        }

        if (forkCurrent)
        {
            var currClone = item.DeepClone();
            currClone.Metadata["ForkBranch"] = "Current";
            currClone.AddLog("[FileForkNode] Emitted clone on 'Current' port");
            await context.EmitAsync("Current", currClone).ConfigureAwait(false);
        }

        if (forkAllVersions)
        {
            foreach (var kvp in item.FileVersions)
            {
                if (string.Equals(kvp.Key, "Original", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var verClone = item.DeepClone();
                verClone.CurrentPath = kvp.Value;
                verClone.PhysicalPath = kvp.Value;
                if (await storage.FileExistsAsync(kvp.Value, cancellationToken).ConfigureAwait(false))
                {
                    verClone.FileSizeBytes = await storage.GetFileSizeAsync(kvp.Value, cancellationToken).ConfigureAwait(false);
                }
                verClone.Metadata["ForkBranch"] = kvp.Key;
                verClone.AddLog($"[FileForkNode] Emitted clone on 'Version' port for version tag '{kvp.Key}'");
                await context.EmitAsync("Version", verClone).ConfigureAwait(false);
            }
        }

        context.Log($"[FileForkNode] Dispatched parallel forks (Original: {forkOriginal}, Current: {forkCurrent}, AllVersions: {forkAllVersions})", LogLevel.Information, item);
    }
}
