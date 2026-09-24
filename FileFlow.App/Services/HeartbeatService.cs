using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.Sdk.Services;

namespace FileFlow.App.Services;

/// <summary>
/// Un <b>latido</b> declarado: su nombre en el registro, su periodo y si está latiendo ahora mismo. Es lo único
/// que un componente necesita para arrancarlo, pararlo y desecharlo.
/// </summary>
public interface IHeartbeat : IDisposable
{
    /// <summary>Nombre del latido dentro del servicio. Único: dos latidos con el mismo nombre se taparían.</summary>
    string Name { get; }

    /// <summary>El tiempo entre dos entregas consecutivas.</summary>
    TimeSpan Period { get; }

    /// <summary>¿Está latiendo?</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Arranca el latido (idempotente) y se devuelve a sí mismo, de modo que declarar y arrancar cabe en una
    /// línea. Tras <see cref="Stop"/> vuelve a arrancar con un vencimiento desde cero.
    /// </summary>
    IHeartbeat Start();

    /// <summary>Detiene el latido y desecha su temporizador. Un latido parado no entrega nada más.</summary>
    void Stop();
}

/// <summary>
/// El <b>registro de latidos</b> de la aplicación: cada latido se <b>declara</b> aquí y el servicio pone la
/// fontanería (el reloj inyectable, el despacho al hilo de la interfaz y la entrega protegida).
/// </summary>
public interface IHeartbeatService
{
    /// <summary>
    /// Declara un latido con su nombre, su periodo y el paso que entrega cada vencimiento. No lo arranca: eso lo
    /// decide quien lo declara (<see cref="IHeartbeat.Start"/>), porque un latido que sólo corre durante una
    /// ejecución no debe latir al declararse.
    /// </summary>
    IHeartbeat Declare(string name, TimeSpan period, Action step);

    /// <summary>Nombres declarados, en orden de declaración: es el registro que leen las guardias.</summary>
    IReadOnlyList<string> DeclaredNames { get; }

    /// <summary>El latido declarado con ese nombre, o <c>null</c>.</summary>
    IHeartbeat? Find(string name);
}

/// <summary>
/// La <b>única</b> fontanería de un latido en el producto: el reloj, el despacho y la entrega protegida viven
/// aquí, y cada latido es una declaración (<see cref="Declare"/>).
///
/// <para><b>Por qué</b>: antes cada uno de los cuatro latidos —vigilante de subflujos, vaciado de la consola,
/// muestreo de rendimiento y fotograma visual de la ejecución— copiaba la misma veintena de líneas: su campo
/// <c>ITimer</c>, su <c>CreateTimer</c> con el periodo por vencimiento y por intervalo, su <c>Heartbeat.Post</c>
/// y su desecho. Cuatro copias de lo mismo no son cuatro decisiones, son cuatro sitios donde equivocarse (y
/// cuatro sitios que el inventario de trabajo aplazado tenía que vigilar por separado). Añadir un latido era
/// aprender el ritual; ahora es <see cref="Declare"/> y arrancar.</para>
///
/// <para><b>Lo que sigue siendo de cada latido</b>: su periodo (constante pública en su componente, para que la
/// prueba de cadencia avance el reloj contra <i>ese</i> número) y su paso (público, alcanzable desde el suite: es
/// el patrón del hito 173 y no se toca). Lo que deja de serlo es el mecanismo.</para>
///
/// <para><b>Reloj y despacho</b>: el latido cuelga del <see cref="TimeProvider"/> <b>inyectado</b> —no de un
/// <c>DispatcherTimer</c>, cuya cadencia no se puede medir sin esperarla— y su tick llega en un hilo del grupo de
/// hilos, así que la entrega se publica al hilo de la interfaz con <see cref="Heartbeat.Post"/>, que además no
/// deja escapar la excepción: un latido no es una tarea de la que dependa nada.</para>
///
/// <para><b>Propiedad</b>: el registro posee los latidos que declara —desecharlo los para todos— y cada componente
/// posee el suyo (lo desecha al desecharse). Parar dos veces es idempotente.</para>
/// </summary>
public sealed class HeartbeatService : IHeartbeatService, IDisposable
{
    private readonly TimeProvider _clock;
    private readonly IUiDispatcher _ui;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Beat> _declared = new(StringComparer.Ordinal);

    /// <summary>
    /// Servicio compartido de producción cuando nadie inyecta otro. Es sólo reloj y despacho —los latidos los
    /// posee quien los declara—, así que compartirlo no reparte estado.
    /// </summary>
    public static HeartbeatService Shared { get; } = new();

    public HeartbeatService(TimeProvider? timeProvider = null, IUiDispatcher? uiDispatcher = null)
    {
        _clock = timeProvider ?? TimeProvider.System;
        _ui = uiDispatcher ?? AvaloniaUiDispatcher.Instance;
    }

    public IHeartbeat Declare(string name, TimeSpan period, Action step)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(step);

        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period), period, $"El latido '{name}' necesita un periodo positivo.");
        }

        lock (_gate)
        {
            if (_declared.TryGetValue(name, out var existing))
            {
                throw new InvalidOperationException(
                    $"Ya hay un latido declarado con el nombre '{name}' (periodo {existing.Period.TotalMilliseconds:F0} ms). " +
                    "Dos latidos con el mismo nombre se taparían en el registro, y el que no se viera sería el que " +
                    "nadie echa de menos: usa un nombre propio.");
            }

            var beat = new Beat(this, name, period, step);
            _declared[name] = beat;

            return beat;
        }
    }

    public IReadOnlyList<string> DeclaredNames
    {
        get
        {
            lock (_gate)
            {
                return _declared.Keys.ToList();
            }
        }
    }

    public IHeartbeat? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_gate)
        {
            return _declared.TryGetValue(name, out var beat) ? beat : null;
        }
    }

    /// <summary>
    /// Para todos los latidos declarados. El registro <b>posee</b> lo que declara, así que desecharlo es el cierre
    /// ordenado del conjunto; que un componente desee además el suyo (lo normal, es su latido) es idempotente.
    /// </summary>
    public void Dispose()
    {
        IHeartbeat[] beats;

        lock (_gate)
        {
            beats = _declared.Values.Cast<IHeartbeat>().ToArray();
            _declared.Clear();
        }

        foreach (var beat in beats)
        {
            beat.Dispose();
        }
    }

    private sealed class Beat : IHeartbeat
    {
        private readonly HeartbeatService _owner;
        private readonly Action _step;
        private ITimer? _timer;

        public Beat(HeartbeatService owner, string name, TimeSpan period, Action step)
        {
            _owner = owner;
            _step = step;
            Name = name;
            Period = period;
        }

        public string Name { get; }

        public TimeSpan Period { get; }

        public bool IsRunning => _timer is not null;

        public IHeartbeat Start()
        {
            // El único sitio del producto que programa un latido: mismo número por vencimiento y por periodo, el
            // reloj inyectado y la entrega protegida con el nombre del latido para que el aviso diga cuál falló.
            _timer ??= _owner._clock.CreateTimer(
                _ => Heartbeat.Post(_owner._ui, Name, _step),
                null,
                Period,
                Period);

            return this;
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose() => Stop();

        public override string ToString() => $"Latido '{Name}' cada {Period.TotalMilliseconds:F0} ms";
    }
}
