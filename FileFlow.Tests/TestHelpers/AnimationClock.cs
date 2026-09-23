using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Headless;
using Avalonia.Threading;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// El <b>reloj de animación</b> de la sesión headless, puesto bajo control del suite.
///
/// <para><b>El problema (medido, hito 172)</b>: Avalonia avanza sus animaciones con el tiempo <b>real</b>
/// transcurrido entre ticks del reloj de render, no con el número de ticks. Una <c>BrushTransition</c> de
/// 120 ms quedaba al 30 % con 12 fotogramas forzados, llegaba al 100 % con ~42 en solitario y se quedaba al
/// 80 % con 64 bajo la carga del suite. De ahí los fallos intermitentes: la única forma de afirmar «el color
/// final es el token» era <b>esperar</b> 180 ms reales por cada asentado, y una espera real no prueba la
/// animación, prueba que el tiempo pasa.</para>
///
/// <para><b>La costura</b>: <c>Avalonia.Animation.Clock</c> no guarda un reloj propio — lo resuelve del
/// <c>AvaloniaLocator</c> cada vez que se construye un reloj de animación
/// (<c>GetRequiredService&lt;IGlobalClock&gt;()</c>), y la sesión headless tiene ahí el suyo
/// (<c>MediaContext+MediaContextClock</c>). Como <c>IGlobalClock</c> está marcado <c>[PrivateApi]</c> —no
/// existe en los ensamblados de referencia, así que no se puede compilar contra él—, la inyección se hace por
/// reflexión: se implementa la interfaz interna con un <c>DispatchProxy</c> y se registra con la API pública
/// <c>Bind&lt;T&gt;().ToConstant()</c>.</para>
///
/// <para><b>Qué consigue</b>: las animaciones avanzan <b>sólo</b> cuando este reloj las pulsa, y cada pulsación
/// vale exactamente el tiempo que dice. Forzar 200 ticks del reloj de render ya no mueve una transición ni un
/// píxel; avanzar 192 ms virtuales la deja clavada en su token. El suite deja de depender del tiempo real, y
/// con ello de la carga de la máquina.</para>
///
/// <para><b>Semántica del primer pulso</b>: el reloj de cada animación toma su primer pulso como <b>base</b>
/// (tiempo interno cero) y sólo suma los incrementos siguientes, así que una animación que empieza tarde no
/// hereda el tiempo ya avanzado. De ahí que el presupuesto de asentado (<see cref="SettleFrames"/> fotogramas)
/// se elija con margen sobre la transición más larga del sistema de diseño: un pulso se consume como base.</para>
///
/// <para><b><see cref="Settle"/> es el único mecanismo de asentado del suite</b>: lo usan la entrada simulada
/// (<see cref="InputSimulator.Settle"/>) y las capturas visuales (<see cref="VisualSnapshot"/>). Una captura
/// que no asiente fotografía una transición en su valor de partida —medido en el hito 181: un fondo que iba de
/// negro a blanco se capturaba en negro, porque el reloj virtual nunca se pulsó— y la línea base congelaría un
/// estado que el usuario nunca ve, para compararlo después como si fuera el correcto.</para>
/// </summary>
public static class AnimationClock
{
    /// <summary>
    /// Milisegundos que representa un fotograma de asentado: es el paso del reloj virtual. Con el presupuesto
    /// de <see cref="SettleFrames"/> (12 fotogramas) supera con margen la transición más larga del sistema de
    /// diseño (120 ms), incluso descontando el pulso que cada animación consume como base.
    /// </summary>
    public const int FrameMilliseconds = 16;

    /// <summary>
    /// Fotogramas que avanza un asentado: 192 ms virtuales, por encima de la transición más larga del sistema de
    /// diseño (120 ms) incluso descontando el pulso que cada animación consume como base. El lint
    /// <c>AnimationClockTests</c> ata esa cuenta a las duraciones realmente declaradas en los estilos, y rige
    /// tanto para un asentado de interacción como para uno de captura.
    /// </summary>
    public const int SettleFrames = 12;

    /// <summary>
    /// Asienta la interfaz: bombea el dispatcher, avanza el reloj de <b>render</b> y pulsa este reloj un
    /// fotograma por vuelta, de modo que transiciones y animaciones quedan en su valor <b>final</b>.
    ///
    /// <para>No hay ninguna espera real: este reloj sólo avanza cuando se le pulsa. El orden —tick de render y,
    /// tras él, el pulso del reloj— reproduce el fotograma de la aplicación: el render entrega el estado
    /// aplicado y el reloj decide el tiempo. Se ejecuta en el hilo de UI de la sesión (es el único que puede
    /// tocar el reloj y el dispatcher).</para>
    /// </summary>
    public static void Settle(int frames = SettleFrames)
    {
        if (frames < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frames), frames, "No se puede asentar hacia atrás.");
        }

        for (int frame = 0; frame < frames; frame++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            AdvanceBy(TimeSpan.FromMilliseconds(FrameMilliseconds));
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static readonly Lock Gate = new();

    private static ClockFace? _face;
    private static object? _proxy;
    private static Type? _globalClockType;
    private static Type? _clockType;

    /// <summary>
    /// ¿Es <b>nuestro</b> reloj el que las animaciones van a usar ahora mismo? No se limita a recordar que se
    /// enlazó: vuelve a preguntar por el reloj global y comprueba que sigue siendo el proxy. Es la mitad
    /// fundamental de la guardia: si Avalonia deja de resolverlo por el locator, esto pasa a <c>false</c> y el
    /// fallo aparece aquí en vez de como un test intermitente tres hitos después.
    /// </summary>
    public static bool IsInEffect
    {
        get
        {
            lock (Gate)
            {
                return _proxy != null && ReferenceEquals(CurrentGlobalClock(), _proxy);
            }
        }
    }

    /// <summary>Tipo real del reloj global que Avalonia tiene en el locator, para el diagnóstico.</summary>
    public static string Implementation => CurrentGlobalClock()?.GetType().FullName ?? "(ninguno)";

    /// <summary>Relojes de animación suscritos ahora mismo: quién está animando de verdad.</summary>
    public static int Subscriptions
    {
        get
        {
            lock (Gate)
            {
                return _face?.Subscriptions ?? 0;
            }
        }
    }

    /// <summary>Tiempo virtual acumulado desde que el reloj se instaló.</summary>
    public static TimeSpan Now
    {
        get
        {
            lock (Gate)
            {
                return _face?.Now ?? TimeSpan.Zero;
            }
        }
    }

    /// <summary>
    /// Avanza el tiempo de animación el equivalente a <paramref name="frames"/> fotogramas y pulsa a todos los
    /// relojes suscritos. Es <b>el</b> mecanismo de asentado: lo que antes costaba 180 ms de espera real.
    /// </summary>
    public static void Advance(int frames)
    {
        if (frames < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frames), frames, "No se puede retroceder el tiempo.");
        }

        AdvanceBy(TimeSpan.FromMilliseconds((double)frames * FrameMilliseconds));
    }

    /// <summary>Igual que <see cref="Advance(int)"/>, con un incremento explícito.</summary>
    public static void AdvanceBy(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "No se puede retroceder el tiempo.");
        }

        AvaloniaTestHelper.RequireUIThread("AnimationClock.AdvanceBy");

        ClockFace face = Face();
        face.Advance(delta);
    }

    /// <summary>
    /// Instala el reloj si no lo está y devuelve su estado. Idempotente y verificado: tras enlazarlo comprueba
    /// que Avalonia lo usa de verdad, porque un enlace ignorado dejaría el suite midiendo el reloj real <b>en
    /// silencio</b> —que es justo el fallo que este helper existe para eliminar—.
    /// </summary>
    public static void Install()
    {
        lock (Gate)
        {
            if (_proxy != null && ReferenceEquals(CurrentGlobalClock(), _proxy))
            {
                return;
            }

            Type globalClock = RequireType("Avalonia.Animation.IGlobalClock");
            Type clock = RequireType("Avalonia.Animation.Clock");

            _globalClockType = globalClock;
            _clockType = clock;

            if (_proxy == null || !globalClock.IsInstanceOfType(_proxy))
            {
                var proxy = (Proxy)DispatchProxy.Create(globalClock, typeof(Proxy));
                proxy.Face = new ClockFace();
                _face = proxy.Face;
                _proxy = proxy;
            }

            Bind(globalClock, _proxy!);

            if (!ReferenceEquals(CurrentGlobalClock(), _proxy))
            {
                throw new InvalidOperationException(
                    "El reloj de animación no quedó instalado: 'Avalonia.Animation.Clock.GlobalClock' sigue " +
                    $"devolviendo '{Implementation}'. Avalonia dejó de resolver el reloj por el AvaloniaLocator, " +
                    "así que las animaciones volverían a avanzar con el tiempo real y las pruebas de animación " +
                    "serían intermitentes otra vez. Revisa AnimationClock antes de dar por buenos esos fallos.");
            }
        }
    }

    /// <summary>
    /// El valor del reloj global tal y como lo ve Avalonia (<c>Clock.GlobalClock</c>), leído por reflexión
    /// porque el tipo de retorno es una interfaz interna.
    /// </summary>
    private static object? CurrentGlobalClock()
    {
        try
        {
            var property = RequireType("Avalonia.Animation.Clock").GetProperty(
                "GlobalClock", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            return property?.GetValue(null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or TargetInvocationException or TypeLoadException)
        {
            // El diagnóstico nunca debe tumbar a quien pregunta: si el reloj no se puede leer, no está en efecto.
            return null;
        }
    }

    private static ClockFace Face()
    {
        Install();

        lock (Gate)
        {
            return _face
                ?? throw new InvalidOperationException("El reloj de animación no tiene estado interno: reinstala la sesión.");
        }
    }

    /// <summary>
    /// Registra el reloj en el locator con la API pública <c>Bind&lt;T&gt;().ToConstant()</c>. La única razón
    /// por la que hay reflexión aquí es que <c>T</c> es una interfaz interna; el resto es la API que Avalonia
    /// usa para enlazarlo.
    /// </summary>
    private static void Bind(Type service, object instance)
    {
        Type locator = RequireType("Avalonia.AvaloniaLocator");

        object mutable = Constant(locator, "CurrentMutable")!;

        var bind = locator.GetMethods()
            .FirstOrDefault(m => m.Name == "Bind" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
            ?? throw new InvalidOperationException(
                "El AvaloniaLocator ya no ofrece 'Bind<T>()': no hay forma de instalar el reloj de animación. " +
                "La inyección que describe AnimationClock necesita otro camino.");

        object helper = bind.MakeGenericMethod(service).Invoke(mutable, null)!;

        var toConstant = helper.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "ToConstant" && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException(
                "'Bind<T>()' devolvió un ayudante sin 'ToConstant(...)': no se puede registrar el reloj.");

        toConstant.MakeGenericMethod(service).Invoke(helper, new[] { instance });
    }

    private static object? Constant(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
        ?? throw new InvalidOperationException($"'{type.FullName}.{name}' no existe: la costura de Avalonia cambió.");

    private static Type RequireType(string fullName)
    {
        var type = typeof(Visual).Assembly.GetType(fullName, throwOnError: false)
            ?? Type.GetType($"{fullName}, Avalonia.Base", throwOnError: false, ignoreCase: false);

        return type
            ?? throw new InvalidOperationException(
                $"No existe el tipo '{fullName}' en Avalonia.Base: la costura que usa AnimationClock para " +
                "controlar el tiempo de las animaciones ya no está. Habrá que buscar otro punto de inyección.");
    }

    /// <summary>
    /// El reloj virtual: tiempo acumulado a mano y la lista de animaciones que están escuchando. Las
    /// pulsaciones se entregan <b>fuera</b> del candado (misma doctrina que <c>ManualTimeProvider</c>): un
    /// animador que aplica una propiedad puede reentrar en el helper, y entregar bajo candado sería un
    /// interbloqueo de los que se investigan una tarde.
    /// </summary>
    private sealed class ClockFace
    {
        private readonly Lock _gate = new();
        private readonly List<IObserver<TimeSpan>> _observers = new();
        private TimeSpan _now;

        /// <summary>
        /// Estado de reproducción. <c>PlayState</c> sí es un tipo público (a diferencia de la interfaz), así
        /// que no hay que reflejarlo: arranca en <c>Run</c>, que es lo que espera un reloj global.
        /// </summary>
        public PlayState PlayState { get; set; } = PlayState.Run;

        public int Subscriptions
        {
            get
            {
                lock (_gate)
                {
                    return _observers.Count;
                }
            }
        }

        public TimeSpan Now
        {
            get
            {
                lock (_gate)
                {
                    return _now;
                }
            }
        }

        public void Advance(TimeSpan delta)
        {
            IObserver<TimeSpan>[] targets;
            TimeSpan stamp;

            lock (_gate)
            {
                _now += delta;
                stamp = _now;
                targets = _observers.ToArray();
            }

            foreach (var observer in targets)
            {
                observer.OnNext(stamp);
            }
        }

        private IDisposable Subscribe(IObserver<TimeSpan> observer)
        {
            lock (_gate)
            {
                _observers.Add(observer);
            }

            return new Subscription(this, observer);
        }

        private void Unsubscribe(IObserver<TimeSpan> observer)
        {
            lock (_gate)
            {
                _observers.Remove(observer);
            }
        }

        public object? Invoke(MethodInfo target, object?[]? args)
        {
            switch (target.Name)
            {
                case "Subscribe":
                    return Subscribe((IObserver<TimeSpan>)args![0]!);

                case "get_PlayState":
                    return PlayState;

                case "set_PlayState":
                    PlayState = (PlayState)args![0]!;
                    return null;

                case "GetHashCode":
                    return GetHashCode();

                case "Equals":
                    return ReferenceEquals(this, args?[0]);

                case "ToString":
                    return "AnimationClock (reloj virtual del suite)";

                default:
                    return target.ReturnType.IsValueType ? Activator.CreateInstance(target.ReturnType) : null;
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly ClockFace _face;
            private IObserver<TimeSpan>? _observer;

            public Subscription(ClockFace face, IObserver<TimeSpan> observer)
            {
                _face = face;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_observer is { } observer)
                {
                    _observer = null;
                    _face.Unsubscribe(observer);
                }
            }
        }
    }

    /// <summary>
    /// Implementación de la interfaz interna: cada llamada va al reloj virtual. <b>No</b> puede ser
    /// <c>sealed</c>: <c>DispatchProxy</c> genera una subclase de este tipo, y sellarla es un error de
    /// argumento, no de compilación.
    /// </summary>
    private class Proxy : DispatchProxy
    {
        public ClockFace Face { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Face.Invoke(targetMethod!, args);
    }
}
