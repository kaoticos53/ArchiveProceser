namespace FileFlow.Sdk;

public interface IFlowNode
{
    string Id { get; set; }
    string Name { get; }
    string Category { get; }
    string Description { get; }
    IReadOnlyList<NodePort> Inputs { get; }
    IReadOnlyList<NodePort> Outputs { get; }
    Dictionary<string, object?> Parameters { get; }

    Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken);
}
