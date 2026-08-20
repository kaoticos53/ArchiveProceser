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
