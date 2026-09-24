using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// <b>Nodo de CPU pura, para medir la concurrencia real del motor.</b> No toca discos, ni codificadores, ni
/// almacenes: hace un bucle de trabajo del tiempo que se le pida y lleva la cuenta de cuántas ejecuciones
/// coinciden en el tiempo. Sin nada más en la balanza, lo que mida es el motor: cuántos nodos llega a tener
/// a la vez, y cuánto tarda en llegar.
///
/// <para>Existe porque la pregunta «¿se usan todos los hilos?» no se puede contestar mirando el código del
/// despacho: el <c>Task.Run</c> por ítem parece que lanza todo a la vez, y sin embargo hay ejecuciones que
/// tardan siete veces más que otras. Con esto se midió que la diferencia no está en la concurrencia —la
/// primera ejecución alcanza los mismos 25-27 nodos simultáneos que las siguientes— sino en cuánto tarda el
/// proceso en llegar al primer ítem. Ver <c>EngineFirstRunTests</c>, que es la prueba que sale de medir aquí.</para>
///
/// <para>Se registra explícitamente (<c>RegisterNodeType&lt;CpuBoundProbeNode&gt;()</c>): el cargador ignora a
/// propósito los ensamblados de prueba, para que un doble no acabe en el catálogo del producto.</para>
/// </summary>
public sealed class CpuBoundProbeNode : FlowNodeBase
{
    private static int _active;
    private static int _maxObserved;
    private static int _executions;

    /// <summary>Máximo de ejecuciones que coincidieron en el tiempo desde el último <see cref="Reset"/>.</summary>
    public static int MaxObserved => Volatile.Read(ref _maxObserved);

    /// <summary>Ejecuciones totales desde el último <see cref="Reset"/>.</summary>
    public static int Executions => Volatile.Read(ref _executions);

    /// <summary>Ejecuciones en marcha en este instante: la concurrencia que se está viendo ahora mismo.</summary>
    public static int Active => Volatile.Read(ref _active);

    private static long _clockOrigin;
    private static long _firstStart = -1;
    private static long _lastEnd = -1;

    /// <summary>
    /// Arranca el reloj del que se miden <see cref="FirstStartMilliseconds"/> y <see cref="LastEndMilliseconds"/>.
    /// Sirve para partir una ejecución en dos tramos: lo que tarda el motor en <b>llegar</b> al primer ítem y lo
    /// que tarda el trabajo en sí.
    /// </summary>
    public static void StartClock() => Volatile.Write(ref _clockOrigin, Stopwatch.GetTimestamp());

    /// <summary>Milisegundos desde <see cref="StartClock"/> hasta el primer ítem que entró al nodo (-1 si no entró).</summary>
    public static double FirstStartMilliseconds => Elapsed(_firstStart);

    /// <summary>Milisegundos desde <see cref="StartClock"/> hasta que salió el último ítem (-1 si no salió).</summary>
    public static double LastEndMilliseconds => Elapsed(_lastEnd);

    private static double Elapsed(long timestamp) =>
        timestamp < 0 ? -1 : Stopwatch.GetElapsedTime(Volatile.Read(ref _clockOrigin), timestamp).TotalMilliseconds;

    /// <summary>Olvida lo medido: cada medición empieza de cero.</summary>
    public static void Reset()
    {
        Volatile.Write(ref _active, 0);
        Volatile.Write(ref _maxObserved, 0);
        Volatile.Write(ref _executions, 0);
        Volatile.Write(ref _firstStart, -1);
        Volatile.Write(ref _lastEnd, -1);
    }

    public override string Name => "Nodo de CPU (sonda)";
    public override string Category => "Testing";
    public override string Description => "Trabajo de CPU pura con la duración de BusyMilliseconds, para medir la concurrencia del motor.";

    public CpuBoundProbeNode()
    {
        Inputs = [new NodePort("In", typeof(FileItemContext), PortDirection.Input, "In")];
        Outputs = [new NodePort("Out", typeof(FileItemContext), PortDirection.Output, "Out")];
        Parameters["BusyMilliseconds"] = 50;
    }

    public override async Task ExecuteAsync(
        string inputPortName,
        FileItemContext item,
        IFlowExecutionContext context,
        CancellationToken cancellationToken)
    {
        int active = Interlocked.Increment(ref _active);
        Interlocked.Increment(ref _executions);
        Interlocked.CompareExchange(ref _firstStart, Stopwatch.GetTimestamp(), -1);

        while (true)
        {
            int previous = Volatile.Read(ref _maxObserved);
            if (active <= previous || Interlocked.CompareExchange(ref _maxObserved, active, previous) == previous)
            {
                break;
            }
        }

        try
        {
            int busyMilliseconds = GetParameter("BusyMilliseconds", 50);
            var clock = Stopwatch.StartNew();
            double sink = 0;

            // Bucle de verdad: un Thread.Sleep cedería el hilo y mediría el temporizador, no la CPU.
            while (clock.Elapsed.TotalMilliseconds < busyMilliseconds)
            {
                sink += Math.Sqrt(sink + 1.0000001);
            }

            if (sink < 0)
            {
                throw new InvalidOperationException("Resultado imposible: el bucle de la sonda no se ejecutó.");
            }
        }
        finally
        {
            Interlocked.Decrement(ref _active);
            Volatile.Write(ref _lastEnd, Stopwatch.GetTimestamp());
        }

        await context.EmitAsync("Out", item);
    }
}
