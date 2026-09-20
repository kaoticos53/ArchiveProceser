using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Subflows;

[NodeDefinition("SubflowInputNode_Name", "Subflows", "SubflowInputNode_Desc", PipelineRole.Control,
    "subflow", "subgrafo", "input", "entrada", "boundary", "macro", "reutilizable",
    SubCategory = "Subflows")]
public sealed class SubflowInputNode : IFlowNode, ISubflowBoundaryNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SubflowInputNode_Name", "Entrada de Subflujo");
    public string Category => "Subflows";
    public string Description => LocalizationManager.Instance.GetString("SubflowInputNode_Desc", "Punto de entrada frontera dentro de un subflujo. Los datos inyectados en los puertos de entrada del nodo subflujo exterior se emiten a través de los puertos de salida de este nodo.");

    public IReadOnlyList<NodePort> Inputs { get; } = new[]
    {
        new NodePort(WellKnownPorts.In, typeof(FileItemContext), PortDirection.Input, WellKnownPorts.In)
    };

    public IReadOnlyList<NodePort> Outputs
    {
        get
        {
            var portNames = GetConfiguredPorts();
            return portNames.Select(p => new NodePort(p, typeof(FileItemContext), PortDirection.Output, p)).ToList();
        }
    }

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PortNames"] = "In"
    };

    public string PortNames
    {
        get => Parameters.TryGetValue("PortNames", out var val) ? ParameterHelper.GetString(val, "In") : "In";
        set => Parameters["PortNames"] = value;
    }

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("PortNames", ParameterEditorType.Text, DefaultValue: "In", DisplayOrder: 1, HelpText: "Nombres de los puertos de entrada separados por punto y coma (ej. 'In' o 'In;Alternate') que se expondrán en el nodo contenedor exterior.")
    ];

    public List<string> GetConfiguredPorts()
    {
        string raw = PortNames;
        if (string.IsNullOrWhiteSpace(raw)) return ["In"];

        var list = raw.Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return list.Count > 0 ? list : ["In"];
    }

    public async Task ExecuteAsync(
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
