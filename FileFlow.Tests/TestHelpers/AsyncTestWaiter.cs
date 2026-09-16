using System.Diagnostics;

namespace FileFlow.Tests.TestHelpers;

/// <summary>Deterministic polling primitives for asynchronous tests.</summary>
public static class AsyncTestWaiter
{
    /// <summary>
    /// Waits until <paramref name="condition"/> becomes true without relying on a fixed sleep.
    /// </summary>
    /// <param name="condition">A cheap, side-effect-free observation of the expected state.</param>
    /// <param name="timeout">Maximum time to wait before failing the test.</param>
    /// <param name="pollInterval">Delay between observations.</param>
    /// <param name="description">Description included in the timeout exception.</param>
    /// <param name="cancellationToken">Token used to cancel the wait.</param>
    /// <exception cref="TimeoutException">Thrown when the condition is still false at the deadline.</exception>
    public static async Task WaitForAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The timeout must be positive.");
        }

        TimeSpan interval = pollInterval ?? TimeSpan.FromMilliseconds(10);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "The poll interval must be positive.");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition())
            {
                return;
            }

            TimeSpan remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    $"Timed out after {timeout.TotalMilliseconds:F0} ms waiting for " +
                    $"{description ?? "the asynchronous condition"}.");
            }

            await Task.Delay(remaining < interval ? remaining : interval, cancellationToken);
        }
    }
}
