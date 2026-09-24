using System;
using System.Collections.Generic;
using System.Threading;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Reloj manual: el tiempo avanza cuando la prueba lo dice, no cuando pasa de verdad.
///
/// <para>Existe para los relojes cuya <b>duración es semántica</b> —cuánto se queda encendido el pulso de
/// energía de un cable, cuánto dura la confirmación de «copiado»— y cuyo vencimiento no se podía probar sin
/// esperar ese tiempo real. Con la fuente de tiempo inyectada, el vencimiento se mide avanzando el reloj: el
/// paso de tiempo <i>es</i> el paso de la prueba.</para>
///
/// <para>Sólo cubre lo que la producción usa: la hora, la marca de tiempo y temporizadores de un disparo o
/// periódicos sobre <see cref="TimeProvider.CreateTimer(TimerCallback, object?, TimeSpan, TimeSpan)"/>, que es
/// lo que hay detrás de <c>Task.Delay(TimeSpan, TimeProvider)</c>. Los callbacks se disparan <b>fuera</b> del
/// candado y en el orden en que vencen, para que un callback pueda volver a programar sin bloquearse.</para>
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ScheduledTimer> _scheduled = [];
    private DateTimeOffset _utcNow;
    private long _timestamp;

    public ManualTimeProvider(DateTimeOffset? start = null)
    {
        _utcNow = start ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _timestamp = _utcNow.UtcTicks;
    }

    /// <summary>Las marcas van en ticks de .NET: una marca es un tick, sin conversiones que confundan.</summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>
    /// Temporizadores vivos en este reloj.
    ///
    /// Permite comprobar que un vencimiento quedó programado <b>aquí</b> y no en el reloj del sistema, sin
    /// esperar el tiempo real para descubrirlo: un test que sólo mida el efecto final puede pasar (despacio)
    /// con el reloj equivocado, y entonces no está midiendo la inyección sino la paciencia.
    /// </summary>
    public int PendingTimerCount
    {
        get
        {
            lock (_gate)
            {
                return _scheduled.Count;
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _utcNow;
        }
    }

    public override long GetTimestamp()
    {
        lock (_gate)
        {
            return _timestamp;
        }
    }

    /// <summary>
    /// Avanza el reloj y dispara los temporizadores vencidos. Debe llamarse desde el hilo que se quiera que
    /// ejecute los callbacks: aquí se ejecutan en línea, en quien avanza.
    /// </summary>
    public void AdvanceBy(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "El reloj manual no va hacia atrás.");
        }

        List<(TimerCallback Callback, object? State)> due = [];

        lock (_gate)
        {
            _utcNow += delta;
            _timestamp += delta.Ticks;

            for (int i = _scheduled.Count - 1; i >= 0; i--)
            {
                ScheduledTimer timer = _scheduled[i];
                if (timer.DueAt > _timestamp)
                {
                    continue;
                }

                due.Add((timer.Callback, timer.State));

                if (timer.PeriodTicks > 0)
                {
                    timer.DueAt = _timestamp + timer.PeriodTicks;
                }
                else
                {
                    _scheduled.RemoveAt(i);
                }
            }
        }

        foreach (var (callback, state) in due)
        {
            callback(state);
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (_gate)
        {
            long dueAt = dueTime == Timeout.InfiniteTimeSpan
                ? long.MaxValue
                : _timestamp + Math.Max(0, dueTime.Ticks);

            var timer = new ScheduledTimer(this, callback, state, dueAt, period);
            _scheduled.Add(timer);
            return timer;
        }
    }

    private void Reschedule(ScheduledTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            timer.DueAt = dueTime == Timeout.InfiniteTimeSpan
                ? long.MaxValue
                : _timestamp + Math.Max(0, dueTime.Ticks);
            timer.SetPeriod(period);

            if (!_scheduled.Contains(timer))
            {
                _scheduled.Add(timer);
            }
        }
    }

    private void Forget(ScheduledTimer timer)
    {
        lock (_gate)
        {
            _scheduled.Remove(timer);
        }
    }

    private sealed class ScheduledTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private bool _disposed;

        public ScheduledTimer(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state,
            long dueAt,
            TimeSpan period)
        {
            _owner = owner;
            Callback = callback;
            State = state;
            DueAt = dueAt;
            PeriodTicks = period == Timeout.InfiniteTimeSpan ? 0 : Math.Max(0, period.Ticks);
        }

        public TimerCallback Callback { get; }

        public object? State { get; }

        public long DueAt { get; set; }

        public long PeriodTicks { get; private set; }

        public void SetPeriod(TimeSpan period) =>
            PeriodTicks = period == Timeout.InfiniteTimeSpan ? 0 : Math.Max(0, period.Ticks);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
            {
                return false;
            }

            _owner.Reschedule(this, dueTime, period);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.Forget(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
