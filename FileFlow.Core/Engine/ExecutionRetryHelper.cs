using FileFlow.Sdk;

namespace FileFlow.Core.Engine;

/// <summary>
/// Helper de resiliencia y política de reintentos (Retry & Fallback Policy) para operaciones propensas a fallos temporales.
/// </summary>
public static class ExecutionRetryHelper
{
    public static async Task ExecuteWithRetryAsync(
        Func<Task> action,
        int maxRetries = 3,
        int initialBackoffMs = 1000,
        IFlowExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        int currentDelay = initialBackoffMs;

        while (true)
        {
            attempt++;
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (attempt <= maxRetries && !cancellationToken.IsCancellationRequested)
            {
                context?.Log($"ExecutionRetryHelper: Attempt {attempt}/{maxRetries} failed ({ex.Message}). Retrying in {currentDelay}ms...", LogLevel.Warning);
                await Task.Delay(currentDelay, cancellationToken);
                currentDelay *= 2; // Exponential backoff
            }
        }
    }

    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        int maxRetries = 3,
        int initialBackoffMs = 1000,
        IFlowExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        int currentDelay = initialBackoffMs;

        while (true)
        {
            attempt++;
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt <= maxRetries && !cancellationToken.IsCancellationRequested)
            {
                context?.Log($"ExecutionRetryHelper: Attempt {attempt}/{maxRetries} failed ({ex.Message}). Retrying in {currentDelay}ms...", LogLevel.Warning);
                await Task.Delay(currentDelay, cancellationToken);
                currentDelay *= 2;
            }
        }
    }
}
