using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileFlow.Core.Engine;

public class WorkflowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string NodeTypeName { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public bool HasBreakpoint { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class WorkflowEdge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePortName { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPortName { get; set; } = string.Empty;
}

public class WorkflowGraph
{
    public string Name { get; set; } = "Untitled Workflow";
    public string GlobalOutputDir { get; set; } = string.Empty;
    public List<WorkflowNode> Nodes { get; set; } = [];
    public List<WorkflowEdge> Edges { get; set; } = [];
    public HashSet<string> BreakpointNodeIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static WorkflowGraph FromJson(string json) =>
        JsonSerializer.Deserialize<WorkflowGraph>(json, JsonOptions) ?? new WorkflowGraph();
}
