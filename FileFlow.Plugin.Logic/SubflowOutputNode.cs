using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Localization;

namespace FileFlow.Plugin.Logic;

[NodeDefinition("SubflowOutputNode_Name", "Logic", "SubflowOutputNode_Desc", PipelineRole.Control,
    "subflow", "subgrafo", "output", "salida", "boundary", "macro", "reutilizable",
    SubCategory = "Subflows")]
public sealed class SubflowOutputNode : IFlowNode, ISubflowBoundaryNode
{
    public const string SubflowSinkKey = "__SubflowOutputSink__";

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SubflowOutputNode_Name", "Salida de Subflujo");
    public string Category => "Logic";
    public string Description => LocalizationManager.Instance.GetString("SubflowOutputNode_Desc", "Punto de salida frontera dentro de un subflujo. Los elementos procesados que llegan a este nodo se emiten hacia los puertos de salida del nodo subflujo exterior.");

    public IReadOnlyList<NodePort> Inputs
    {
        get
        {
            var portNames = GetConfiguredPorts();
            return portNames.Select(p => new NodePort(p, typeof(FileItemContext), PortDirection.Input, p)).ToList();
        }
    }

    public IReadOnlyList<NodePort> Outputs { get; } = Array.Empty<NodePort>();

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PortNames"] = "Out"
    };

    public string PortNames
    {
        get => Parameters.TryGetValue("PortNames", out var val) ? ParameterHelper.GetString(val, "Out") : "Out";
        set => Parameters["PortNames"] = value;
    }

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("PortNames", ParameterEditorType.Text, DefaultValue: "Out", DisplayOrder: 1, HelpText: "Nombres de los puertos de salida separados por punto y coma (ej. 'Out' o 'Out;Errors') que se expondrán en el nodo contenedor exterior.")
    ];

    public List<string> GetConfiguredPorts()
    {
        string raw = PortNames;
        if (string.IsNullOrWhiteSpace(raw)) return ["Out"];

        var list = raw.Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return list.Count > 0 ? list : ["Out"];
    }

    public async Task ExecuteAsync(
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
        }
    }
}
