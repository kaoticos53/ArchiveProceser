using FileFlow.Sdk;

namespace FileFlow.App.Models;

public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message
);

public record NodeToolboxItem(
    string Name,
    string Category,
    string Description,
    string TypeName
);

public record VariableItem(
    string Name,
    string Token,
    string Description
);

public class VariableGroupItem(string groupName)
{
    public string GroupName { get; set; } = groupName;
    public List<VariableItem> Variables { get; } = [];
}
