using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

namespace FileFlow.Plugin.Subflows;

[NodeDefinition("SubflowNode_Name", "Subflows", "SubflowNode_Desc", PipelineRole.Control,
    "subflow", "subgrafo", "macro", "composite", "modular", "reutilizable", "anidado",
    SubCategory = "Subflows")]
public sealed class SubflowNode : IFlowNode, ISubflowNode
{
    private readonly List<NodePort> _inputPorts = [];
    private readonly List<NodePort> _outputPorts = [];
    private readonly Lock _portLock = new();

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name => LocalizationManager.Instance.GetString("SubflowNode_Name", "Subflujo Reutilizable");
    public string Category => "Subflows";
    public string Description => LocalizationManager.Instance.GetString("SubflowNode_Desc", "Encapsula un subgrafo DAG completo en un único nodo modular reutilizable con puertos dinámicos mapeados a sus entradas y salidas internas.");

    public IReadOnlyList<NodePort> Inputs
    {
        get
        {
            lock (_portLock)
            {
                if (_inputPorts.Count == 0)
                {
                    return [new NodePort(WellKnownPorts.In, typeof(FileItemContext), PortDirection.Input, WellKnownPorts.In)];
                }
                return _inputPorts.ToList();
            }
        }
    }

    public IReadOnlyList<NodePort> Outputs
    {
        get
        {
            lock (_portLock)
            {
                if (_outputPorts.Count == 0)
                {
                    return [new NodePort(WellKnownPorts.Out, typeof(FileItemContext), PortDirection.Output, WellKnownPorts.Out)];
                }
                return _outputPorts.ToList();
            }
        }
    }

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SubflowPath"] = string.Empty,
        ["EmbedDefinition"] = false,
        ["SubflowDefinitionJson"] = string.Empty,
        ["SubflowName"] = "Subflujo"
    };

    public string SubflowPath
    {
        get => Parameters.TryGetValue("SubflowPath", out var val) ? ParameterHelper.GetString(val, string.Empty) : string.Empty;
        set => Parameters["SubflowPath"] = value;
    }

    public bool EmbedDefinition
    {
        get => Parameters.TryGetValue("EmbedDefinition", out var val) ? ParameterHelper.GetBoolean(val, false) : false;
        set => Parameters["EmbedDefinition"] = value;
    }

    public string SubflowDefinitionJson
    {
        get => Parameters.TryGetValue("SubflowDefinitionJson", out var val) ? ParameterHelper.GetString(val, string.Empty) : string.Empty;
        set => Parameters["SubflowDefinitionJson"] = value;
    }

    public string SubflowName
    {
        get => Parameters.TryGetValue("SubflowName", out var val) ? ParameterHelper.GetString(val, "Subflujo") : "Subflujo";
        set => Parameters["SubflowName"] = value;
    }

    public IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("SubflowPath", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 1, HelpText: "Ruta al archivo .flow o .subflow reutilizable en disco."),
        new("EmbedDefinition", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 2, HelpText: "Si está activo, incrusta la definición completa del subgrafo dentro de este flujo para máxima portabilidad."),
        new("SubflowName", ParameterEditorType.Text, DefaultValue: "Subflujo", DisplayOrder: 3, HelpText: "Nombre descriptivo del subflujo.")
    ];

    public IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("OpenSubflowEditor", "📂 Abrir Subflujo", "OpenInNew", "Abre el subgrafo en el editor visual para inspeccionar o modificar sus nodos.")
    ];

    public SubflowNode()
    {
        RefreshDynamicPorts(["In"], ["Out"]);
    }

    public void RefreshDynamicPorts(IEnumerable<string> inputPortNames, IEnumerable<string> outputPortNames)
    {
        lock (_portLock)
        {
            _inputPorts.Clear();
            var inList = inputPortNames.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (inList.Count == 0) inList.Add(WellKnownPorts.In);
            foreach (var name in inList)
            {
                _inputPorts.Add(new NodePort(name, typeof(FileItemContext), PortDirection.Input, name));
            }

            _outputPorts.Clear();
            var outList = outputPortNames.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (outList.Count == 0) outList.Add(WellKnownPorts.Out);
            foreach (var name in outList)
            {
                _outputPorts.Add(new NodePort(name, typeof(FileItemContext), PortDirection.Output, name));
            }
        }
    }

    public async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        context.Log($"[SubflowNode] Ejecutando subflujo '{SubflowName}' para el elemento '{item.FileName}' en puerto de entrada '{inputPortName}'", LogLevel.Information, item);

        ISubflowExecutionService service = ISubflowExecutionService.Instance;
        if (item.Metadata.TryGetValue("__SubflowExecutionService__", out var svcObj) && svcObj is ISubflowExecutionService itemService)
        {
            service = itemService;
        }

        await service.ExecuteSubflowAsync(
            this,
            inputPortName,
            item,
            context,
            cancellationToken).ConfigureAwait(false);
    }
}
