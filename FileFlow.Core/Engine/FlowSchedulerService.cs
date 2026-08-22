using System.Threading.Channels;

namespace FileFlow.Core.Engine;

/// <summary>
/// Servicio de planificación y ejecución programada de flujos desatendidos (Scheduler).
/// Soporta ejecución por intervalo en segundos/minutos o disparos periódicos.
/// </summary>
public class FlowSchedulerService : IDisposable
{
    private readonly Channel<DateTime> _triggerChannel = Channel.CreateUnbounded<DateTime>();
    private CancellationTokenSource? _cts;
    private Task? _timerTask;

    public bool IsRunning => _timerTask != null && !_timerTask.IsCompleted;
    public ChannelReader<DateTime> TriggerReader => _triggerChannel.Reader;

    public void StartInterval(TimeSpan interval)
    {
        Stop();

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
        }

        _cts = new CancellationTokenSource();
        _timerTask = Task.Run(() => RunTimerAsync(interval, _cts.Token));
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        _timerTask = null;
    }

    private async Task RunTimerAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await _triggerChannel.Writer.WriteAsync(DateTime.UtcNow, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
