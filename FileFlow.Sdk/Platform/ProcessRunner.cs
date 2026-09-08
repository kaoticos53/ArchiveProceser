using System.Diagnostics;
using System.Text;

namespace FileFlow.Sdk.Platform;

/// <summary>
/// Implementación estándar de <see cref="IProcessRunner"/> para .NET 9.
/// Maneja redirección asíncrona de streams, timeouts, cancelación determinista y árbol de procesos.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    private static readonly Lazy<ProcessRunner> _instance = new(() => new ProcessRunner());
    public static ProcessRunner Instance => _instance.Value;

    public async Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException("El nombre del archivo ejecutable no puede estar vacío.", nameof(request));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            Arguments = request.Arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = request.RedirectStandardOutput,
            RedirectStandardError = request.RedirectStandardError
        };

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        if (request.EnvironmentVariables != null)
        {
            foreach (var kvp in request.EnvironmentVariables)
            {
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        var sw = Stopwatch.StartNew();

        using var linkedCts = request.Timeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (linkedCts != null && request.Timeout.HasValue)
        {
            linkedCts.CancelAfter(request.Timeout.Value);
        }

        var effectiveToken = linkedCts?.Token ?? cancellationToken;

        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        var streamTasks = new List<Task>();

        bool useLineStreaming = request.OnStandardOutputLine != null || request.OnStandardErrorLine != null;

        if (useLineStreaming)
        {
            if (request.RedirectStandardOutput)
            {
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        sbOut.AppendLine(e.Data);
                        request.OnStandardOutputLine?.Invoke(e.Data);
                    }
                };
            }

            if (request.RedirectStandardError)
            {
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        sbErr.AppendLine(e.Data);
                        request.OnStandardErrorLine?.Invoke(e.Data);
                    }
                };
            }
        }

        try
        {
            process.Start();

            if (useLineStreaming)
            {
                if (request.RedirectStandardOutput) process.BeginOutputReadLine();
                if (request.RedirectStandardError) process.BeginErrorReadLine();
            }
            else
            {
                if (request.RedirectStandardOutput)
                {
                    streamTasks.Add(Task.Run(async () =>
                    {
                        string output = await process.StandardOutput.ReadToEndAsync(effectiveToken).ConfigureAwait(false);
                        sbOut.Append(output);
                    }, effectiveToken));
                }

                if (request.RedirectStandardError)
                {
                    streamTasks.Add(Task.Run(async () =>
                    {
                        string error = await process.StandardError.ReadToEndAsync(effectiveToken).ConfigureAwait(false);
                        sbErr.Append(error);
                    }, effectiveToken));
                }
            }

            await process.WaitForExitAsync(effectiveToken).ConfigureAwait(false);
            if (streamTasks.Count > 0)
            {
                await Task.WhenAll(streamTasks).ConfigureAwait(false);
            }

            sw.Stop();

            return new ProcessExecutionResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = sbOut.ToString(),
                StandardError = sbErr.ToString(),
                Duration = sw.Elapsed,
                TimedOut = false
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignorar excepciones al intentar matar el proceso
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("La ejecución del proceso fue cancelada por el usuario.", cancellationToken);
            }

            // Fue timeout
            return new ProcessExecutionResult
            {
                ExitCode = -1,
                StandardOutput = sbOut.ToString(),
                StandardError = sbErr.ToString(),
                Duration = sw.Elapsed,
                TimedOut = true
            };
        }
    }
}
