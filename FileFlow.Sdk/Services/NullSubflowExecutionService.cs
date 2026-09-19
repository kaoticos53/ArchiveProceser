namespace FileFlow.Sdk.Services;

/// <summary>
/// Implementación nula (fallback seguro) del servicio de subflujos.
/// </summary>
public sealed class NullSubflowExecutionService : ISubflowExecutionService
{
    public static readonly NullSubflowExecutionService Instance = new();

    private NullSubflowExecutionService() { }

    public Task ExecuteSubflowAsync(
        ISubflowNode subflowNode,
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext parentContext,
        CancellationToken cancellationToken)
    {
        parentContext.Log("[Subflow] El servicio de ejecución de subflujos no está registrado.", LogLevel.Warning, item);
        return Task.CompletedTask;
    }

    public (List<string> Inputs, List<string> Outputs) DiscoverSubflowPorts(ISubflowNode subflowNode) =>
        (["In"], ["Out"]);
}

