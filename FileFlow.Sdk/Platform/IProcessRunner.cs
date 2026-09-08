namespace FileFlow.Sdk.Platform;

/// <summary>
/// Parámetros de configuración para la ejecución controlada de un proceso del sistema operativo.
/// </summary>
public sealed record ProcessExecutionRequest
{
    public required string FileName { get; init; }
    public string Arguments { get; init; } = string.Empty;
    public string? WorkingDirectory { get; init; }
    public IDictionary<string, string>? EnvironmentVariables { get; init; }
    public TimeSpan? Timeout { get; init; }
    public bool RedirectStandardOutput { get; init; } = true;
    public bool RedirectStandardError { get; init; } = true;
    public Action<string>? OnStandardOutputLine { get; init; }
    public Action<string>? OnStandardErrorLine { get; init; }
}

/// <summary>
/// Resultado inmutable de la ejecución de un proceso del sistema operativo.
/// </summary>
public sealed record ProcessExecutionResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public bool TimedOut { get; init; }
    public bool Success => !TimedOut && ExitCode == 0;
}

/// <summary>
/// Contrato de puerto para ejecutar procesos del sistema operativo de manera asíncrona,
/// segura y testeable, con soporte para cancelación, timeouts y captura de streams.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Métodos de extensión para simplificar invocaciones comunes sobre <see cref="IProcessRunner"/>.
/// </summary>
public static class ProcessRunnerExtensions
{
    public static Task<ProcessExecutionResult> RunAsync(
        this IProcessRunner runner,
        string fileName,
        string arguments = "",
        TimeSpan? timeout = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runner);
        return runner.RunAsync(new ProcessExecutionRequest
        {
            FileName = fileName,
            Arguments = arguments,
            Timeout = timeout,
            WorkingDirectory = workingDirectory
        }, cancellationToken);
    }
}

/// <summary>
/// Implementación nula de <see cref="IProcessRunner"/> para contextos de prueba o inicialización sin plataforma.
/// </summary>
public sealed class NullProcessRunner : IProcessRunner
{
    public static NullProcessRunner Instance { get; } = new();

    public Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            StandardOutput = string.Empty,
            StandardError = string.Empty,
            Duration = TimeSpan.Zero,
            TimedOut = false
        });
}
