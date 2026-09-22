using FileFlow.Sdk;
using FileFlow.Sdk.Common;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

namespace FileFlow.Plugin.Subflows;

[NodeDefinition("SubflowNode_Name", "Subflows", "SubflowNode_Desc", PipelineRole.Control,
    "subflow", "subgrafo", "macro", "composite", "modular", "reutilizable", "anidado",
    SubCategory = "Subflows")]
public sealed class SubflowNode : FlowNodeBase, ISubflowNode
{
    /// <summary>
    /// Claves donde el contenedor recuerda los puertos que expone. No están en
    /// <see cref="ParameterDescriptors"/> a propósito: no son configuración del usuario, son estado de
    /// diseño que se guarda con el nodo para poder reconstruir las conexiones de un flujo cuyo subflujo ya
    /// no está al alcance. Es el mismo patrón que usa el nodo de switch con sus casos.
    /// </summary>
    private const string RememberedInputPortsKey = "RememberedInputPorts";
    private const string RememberedOutputPortsKey = "RememberedOutputPorts";

    private readonly List<NodePort> _inputPorts = [];
    private readonly List<NodePort> _outputPorts = [];
    private readonly Lock _portLock = new();

    public override string Name => LocalizationManager.Instance.GetString("SubflowNode_Name", "Subflujo Reutilizable");
    public override string Category => "Subflows";
    public override string Description => LocalizationManager.Instance.GetString("SubflowNode_Desc", "Encapsula un subgrafo DAG completo en un único nodo modular reutilizable con puertos dinámicos mapeados a sus entradas y salidas internas.");

    /// <summary>
    /// Puertos de entrada generados a partir del subgrafo incrustado, que el usuario redefine en tiempo
    /// de diseño.
    /// </summary>
    protected override IReadOnlyList<NodePort> BuildInputPorts()
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

    /// <summary>Puertos de salida generados a partir del subgrafo incrustado.</summary>
    protected override IReadOnlyList<NodePort> BuildOutputPorts()
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

    public string SubflowPath
    {
        get => GetParameter("SubflowPath", string.Empty);
        set => Parameters["SubflowPath"] = value;
    }

    public bool EmbedDefinition
    {
        get => GetParameter("EmbedDefinition", false);
        set => Parameters["EmbedDefinition"] = value;
    }

    public string SubflowDefinitionJson
    {
        get => GetParameter("SubflowDefinitionJson", string.Empty);
        set => Parameters["SubflowDefinitionJson"] = value;
    }

    public string SubflowName
    {
        get => GetParameter("SubflowName", "Subflujo");
        set => Parameters["SubflowName"] = value;
    }

    public override IReadOnlyList<NodeParameterDescriptor> ParameterDescriptors => [
        new("SubflowPath", ParameterEditorType.FilePath, DefaultValue: "", DisplayOrder: 1, HelpText: "Ruta al archivo .flow o .subflow reutilizable en disco."),
        new("EmbedDefinition", ParameterEditorType.Toggle, DefaultValue: false, DisplayOrder: 2, HelpText: "Si está activo, incrusta la definición completa del subgrafo dentro de este flujo para máxima portabilidad."),
        new("SubflowName", ParameterEditorType.Text, DefaultValue: "Subflujo", DisplayOrder: 3, HelpText: "Nombre descriptivo del subflujo.")
    ];

    public override IReadOnlyList<NodeActionDescriptor> CustomActions => [
        new("OpenSubflowEditor", "📂 Abrir Subflujo", "OpenInNew", "Abre el subgrafo en el editor visual para inspeccionar o modificar sus nodos.")
    ];

    public SubflowNode()
    {
        Parameters["SubflowPath"] = string.Empty;
        Parameters["EmbedDefinition"] = false;
        Parameters["SubflowDefinitionJson"] = string.Empty;
        Parameters["SubflowName"] = "Subflujo";
        Parameters[RememberedInputPortsKey] = string.Empty;
        Parameters[RememberedOutputPortsKey] = string.Empty;

        RefreshDynamicPorts([WellKnownPorts.In], [WellKnownPorts.Out]);
    }

    /// <summary>
    /// Puertos que expone el contenedor, recordados con el nodo. Ver <see cref="ISubflowNode.RememberedPorts"/>.
    /// </summary>
    public (IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs) RememberedPorts
    {
        get => (SplitPortNames(GetParameter(RememberedInputPortsKey, string.Empty)),
                SplitPortNames(GetParameter(RememberedOutputPortsKey, string.Empty)));

        // Sólo la escribe quien reconstruye un archivo guardado antes de que el formato la guardara: se
        // siembra con lo que ese archivo todavía dice de los puertos del contenedor, para que el respaldo
        // de la materialización los conserve en vez de caer a los genéricos. La materialización posterior
        // decide si esa siembra sobrevive —una definición que resuelve la sustituye—.
        set
        {
            Parameters[RememberedInputPortsKey] = string.Join(';', value.Inputs ?? []);
            Parameters[RememberedOutputPortsKey] = string.Join(';', value.Outputs ?? []);
        }
    }

    public void RefreshDynamicPorts(IEnumerable<string> inputPortNames, IEnumerable<string> outputPortNames)
    {
        string rememberedInputs;
        string rememberedOutputs;

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

            // Las listas de arriba son la verdad de lo que expone el contenedor, así que se recuerdan aquí,
            // en el único sitio que las cambia: es lo que quedará guardado en el flujo.
            rememberedInputs = string.Join(';', _inputPorts.Select(port => port.Name));
            rememberedOutputs = string.Join(';', _outputPorts.Select(port => port.Name));
        }

        Parameters[RememberedInputPortsKey] = rememberedInputs;
        Parameters[RememberedOutputPortsKey] = rememberedOutputs;

        // Los puertos de este nodo no salen de sus parámetros sino de las listas de arriba, así que la
        // reevaluación automática no los cubre: el anuncio se hace aquí, ya fuera del bloqueo y sólo si el
        // conjunto cambió (la base descarta las repeticiones).
        NotifyPortsChanged();
    }

    /// <summary>
    /// Nombres de puerto guardados como lista; el separador es el mismo que aceptan los nodos frontera,
    /// así que un nombre nunca puede contenerlo.
    /// </summary>
    private static List<string> SplitPortNames(string raw) =>
        [.. raw.Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    public override async Task ExecuteAsync(
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
