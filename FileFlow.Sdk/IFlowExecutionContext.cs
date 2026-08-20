namespace FileFlow.Sdk;

public interface IFlowExecutionContext
{
    Task EmitAsync(string outputPortName, FileItemContext item);
    void ReportProgress(double percentage, string statusMessage);
    void Log(string message, LogLevel level);
}
