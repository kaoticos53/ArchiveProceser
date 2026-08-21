namespace FileFlow.Sdk;

public enum NodeExecutionStatus
{
    Idle,
    Running,
    PausedAtBreakpoint,
    PausedOnError,
    Completed,
    Faulted
}
