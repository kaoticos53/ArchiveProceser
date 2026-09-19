namespace FileFlow.Sdk.Services;

/// <summary>
/// Servicio para la orquestación y ejecución desacoplada de subflujos jerárquicos.
/// Permite a nodos compuestos ejecutar subgrafos sin referenciar directamente el ensamblado Core.
/// </summary>
public interface ISubflowExecutionService
{
    private static ISubflowExecutionService? _instance;

    /// <summary>
    /// Instancia global activa del servicio de ejecución de subflujos.
    /// </summary>
    public static ISubflowExecutionService Instance
    {
        get => _instance ?? NullSubflowExecutionService.Instance;
        set => _instance = value;
    }

    /// <summary>
    /// Ejecuta el subgrafo referenciado o embebido en el nodo de subflujo.
    /// </summary>
    Task ExecuteSubflowAsync(
        ISubflowNode subflowNode,
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext parentContext,
        CancellationToken cancellationToken);

    /// <summary>
    /// Descubre e inspecciona los puertos de entrada y salida configurados en los nodos frontera del subflujo.
    /// </summary>
    (List<string> Inputs, List<string> Outputs) DiscoverSubflowPorts(ISubflowNode subflowNode);
}

