namespace FileFlow.Sdk;

public record NodeDataSnapshot
{
    public Guid SnapshotId { get; init; } = Guid.NewGuid();
    public string NodeId { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public bool IsInput { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public FileItemContext ItemSnapshot { get; init; } = new();
    public bool HasError { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }

    public static NodeDataSnapshot CreateInput(string nodeId, string portName, FileItemContext item)
    {
        return new NodeDataSnapshot
        {
            NodeId = nodeId,
            PortName = portName,
            IsInput = true,
            ItemSnapshot = item.DeepClone()
        };
    }

    public static NodeDataSnapshot CreateOutput(string nodeId, string portName, FileItemContext item)
    {
        return new NodeDataSnapshot
        {
            NodeId = nodeId,
            PortName = portName,
            IsInput = false,
            ItemSnapshot = item.DeepClone()
        };
    }

    public static NodeDataSnapshot CreateError(string nodeId, string portName, FileItemContext item, Exception ex)
    {
        return new NodeDataSnapshot
        {
            NodeId = nodeId,
            PortName = portName,
            IsInput = true,
            ItemSnapshot = item.DeepClone(),
            HasError = true,
            ErrorMessage = ex.Message,
            StackTrace = ex.StackTrace
        };
    }
}
