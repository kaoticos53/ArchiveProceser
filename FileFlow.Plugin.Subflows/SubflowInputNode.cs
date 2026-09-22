using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Subflows;

[NodeDefinition("SubflowInputNode_Name", "Subflows", "SubflowInputNode_Desc", PipelineRole.Control,
    "subflow", "subgrafo", "input", "entrada", "boundary", "macro", "reutilizable",
    SubCategory = "Subflows")]
public sealed class SubflowInputNode : FlowNodeBase, ISubflowBoundaryNode
{
    public override string Name => LocalizationManager.Instance.GetString("SubflowInputNode_Name", "Entrada de Subflujo");
    public override string Category => "Subflows";
    public override string Description => LocalizationManager.Instance.GetString("SubflowInputNode_Desc", "Punto de entrada frontera dentro de un subflujo. Los datos inyectados en los puertos de entrada del nodo subflujo exterior se emiten a través de los puertos de salida de este nodo.");

    public SubflowInputNode()
    {
        Inputs =
        [
            new NodePort(WellKnownPorts.In, typeof(FileItemContext), PortDirection.Input, WellKnownPorts.In)
        ];

        Parameters["PortNames"] = "In";
    }

    /// <summary>
    /// Puertos de salida calculados a partir de los nombres configurados por el usuario, que cambian en
    /// tiempo de diseño desde la UI.
    /// </summary>
    protected override IReadOnlyList<NodePort> BuildOutputPorts() =>
        GetConfiguredPorts().Select(p => new NodePort(p, typeof(FileItemContext), PortDirection.Output, p)).ToList();

    public string PortNames
    {
        get => GetParameter("PortNames", "In");
        set
        {
            Parameters["PortNames"] = value;

            // Los puertos de salida son estos nombres: al cambiarlos hay que anunciar la topología nueva.
            NotifyPortsChanged();
        }
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("PortNames", ParameterEditorType.Text, DefaultValue: "In", DisplayOrder: 1, HelpText: "Nombres de los puertos de entrada separados por punto y coma (ej. 'In' o 'In;Alternate') que se expondrán en el nodo contenedor exterior.")
    ];

    public List<string> GetConfiguredPorts()
    {
        string raw = PortNames;
        if (string.IsNullOrWhiteSpace(raw)) return ["In"];

        var list = raw.Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return list.Count > 0 ? list : ["In"];
    }

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var ports = GetConfiguredPorts();
        string targetPort = ports.Contains(inputPortName, StringComparer.OrdinalIgnoreCase)
            ? inputPortName
            : ports[0];

        context.Log($"[SubflowInput] Emitiendo elemento hacia el subgrafo por el puerto '{targetPort}'", LogLevel.Debug, item);
        await context.EmitAsync(targetPort, item).ConfigureAwait(false);
    }
}
