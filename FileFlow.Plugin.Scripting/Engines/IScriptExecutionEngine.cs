using FileFlow.Sdk;

namespace FileFlow.Plugin.Scripting.Engines;

public sealed class ScriptExecutionContext
{
    public required FileItemContext Item { get; init; }
    public required IFlowExecutionContext FlowContext { get; init; }
    public required string InputPortName { get; init; }
    public required CancellationToken CancellationToken { get; init; }
    public List<string> ExecutionLogs { get; } = [];

    public async Task EmitAsync(string portName, FileItemContext? item = null)
    {
        await FlowContext.EmitAsync(portName, item ?? Item).ConfigureAwait(false);
    }

    public void Log(string message, LogLevel level = LogLevel.Information)
    {
        ExecutionLogs.Add($"[{level}] {message}");
        FlowContext.Log(message, level, Item);
    }
}

public interface IScriptExecutionEngine
{
    Task ExecuteAsync(string code, ScriptExecutionContext context, CancellationToken cancellationToken);
}
