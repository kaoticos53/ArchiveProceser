using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Subflows;

[NodeDefinition("SubflowOutputNode_Name", "Subflows", "SubflowOutputNode_Desc", PipelineRole.Control,
    "subflow", "subgrafo", "output", "salida", "boundary", "macro", "reutilizable",
    SubCategory = "Subflows")]
public sealed class SubflowOutputNode : FlowNodeBase, ISubflowBoundaryNode
{
    public const string SubflowSinkKey = "__SubflowOutputSink__";

    public override string Name => LocalizationManager.Instance.GetString("SubflowOutputNode_Name", "Salida de Subflujo");
    public override string Category => "Subflows";
    public override string Description => LocalizationManager.Instance.GetString("SubflowOutputNode_Desc", "Punto de salida frontera dentro de un subflujo. Los elementos procesados que llegan a este nodo se emiten hacia los puertos de salida del nodo subflujo exterior.");

    public SubflowOutputNode()
    {
        Parameters["PortNames"] = "Out";

        Outputs =
        [
            new NodePort(WellKnownPorts.Out, typeof(FileItemContext), PortDirection.Output, WellKnownPorts.Out)
        ];
    }

    /// <summary>
    /// Puertos de entrada calculados a partir de los nombres configurados por el usuario, que cambian en
    /// tiempo de diseño desde la UI.
    /// </summary>
    protected override IReadOnlyList<NodePort> BuildInputPorts() =>
        GetConfiguredPorts().Select(p => new NodePort(p, typeof(FileItemContext), PortDirection.Input, p)).ToList();

    public string PortNames
    {
        get => GetParameter("PortNames", "Out");
        set
        {
            Parameters["PortNames"] = value;

            // Los puertos de entrada son estos nombres: al cambiarlos hay que anunciar la topología nueva.
            NotifyPortsChanged();
        }
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("PortNames", ParameterEditorType.Text, DefaultValue: "Out", DisplayOrder: 1, HelpText: "Nombres de los puertos de salida separados por punto y coma (ej. 'Out' o 'Out;Errors') que se expondrán en el nodo contenedor exterior.")
    ];

    public List<string> GetConfiguredPorts()
    {
        string raw = PortNames;
        if (string.IsNullOrWhiteSpace(raw)) return ["Out"];

        var list = raw.Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return list.Count > 0 ? list : ["Out"];
    }

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        context.Log($"[SubflowOutput] Elemento alcanzó la frontera de salida en puerto '{inputPortName}'", LogLevel.Debug, item);

        if (item.Metadata.TryGetValue(SubflowSinkKey, out var sinkObj) && sinkObj is Func<string, FileItemContext, Task> emitCallback)
        {
            await emitCallback(inputPortName, item).ConfigureAwait(false);
        }
        else
        {
            context.Log($"[SubflowOutput] Salida completada de forma autónoma (sin contenedor anfitrión)", LogLevel.Information, item);
            await context.EmitAsync(WellKnownPorts.Out, item).ConfigureAwait(false);
        }
    }
}
