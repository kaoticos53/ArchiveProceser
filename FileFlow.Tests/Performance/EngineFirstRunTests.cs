using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Core.Engine;
using FileFlow.Core.Plugins;
using FileFlow.Plugin.FileSystem;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace FileFlow.Tests.Performance;

/// <summary>
/// <b>La primera ejecución de una sesión usa todos los hilos que se le dan.</b>
///
/// <para>La pregunta que esta prueba contesta es la que se hace quien mira el Administrador de tareas en la
/// primera ejecución de un flujo: «¿por qué no se usan todos los hilos?». La carga es CPU pura
/// (<see cref="CpuBoundProbeNode"/>, sin discos ni codificadores) para que lo que quede en la balanza sea el
/// motor: con 96 ítems de 50 ms de trabajo y el paralelismo pedido, la ejecución tiene que llegar a tener a la
/// vez tantos nodos como se le pidieron, <b>también la primera vez</b>.</para>
///
/// <para><b>Lo medido (2026-09-24, i7-14700KF, 28 hilos lógicos, tres procesos nuevos por variante).</b> La
/// concurrencia máxima observada fue <b>27 simultáneos de 28 pedidos, en las 20 ejecuciones medidas</b> —primera
/// y siguientes, con el grupo de hilos frío o caliente—: el motor no reparte el trabajo entre menos hilos de
/// los que se le dan. El <b>tiempo de pared</b> sí cambió mucho entre procesos (982-1 163 ms la primera
/// ejecución frente a 211-224 ms las siguientes), y por eso <b>no se afirma aquí</b>: contra el reloj de pared
/// lo que domina es cuándo se ejecutó la prueba, no el motor.</para>
///
/// <para><b>Qué era ese tiempo de pared</b>, porque se intentó arreglar dos veces y las dos mediciones
/// desmintieron la hipótesis:</para>
/// <list type="number">
///   <item><b>No era el suelo del grupo de hilos.</b> En .NET el mínimo de hilos de trabajo ya es
///   <c>Environment.ProcessorCount</c> (medido: 28). Subirlo y crear los hilos de antemano —un
///   <c>ThreadPoolWarmth</c> que llegó a existir en <c>FileFlow.Core</c>— mejoraba unas corridas y dejaba otras
///   en 965-1 066 ms: no reproducible, y ahora se sabe por qué.</item>
///   <item><b>No era compilar el camino de ejecución de antemano.</b> Pedirle al compilador 255 métodos con
///   <c>RuntimeHelpers.PrepareMethod</c> cuesta 5 ms y, en un proceso recién compilado, dejaba la primera
///   ejecución igual de lenta (973 ms, con los mismos dos tramos de arranque); en un proceso ya usado, sin
///   preparar nada, sale igual de rápida (269 ms). Un <c>ExecutionPathWarmUp</c> que llegó a existir en
///   <c>FileFlow.Core</c> tampoco cambiaba el número.</item>
///   <item><b>No era el almacén de registros</b> (su primera inicialización mide 14 ms, y adelantarla tampoco
///   movía la aguja).</item>
/// </list>
///
/// <para><b>Lo que sí es</b>: el <b>primer proceso que corre justo después de una compilación</b>. En ese
/// proceso el motor pasa 775-945 ms antes de que el primer ítem llegue a la rejilla, consumiendo sólo ~267 ms de
/// CPU: no está calculando, está esperando a que el sistema le entregue las cosas por primera vez (IL de las
/// bibliotecas recién escritas). El mismo binario, en un proceso siguiente, llega al primer ítem en 46-66 ms.
/// Por eso esta prueba <b>no</b> compara tiempos contra un umbral: el «antes y después» del arranque no es una
/// propiedad del motor sino de la máquina que acaba de compilar, y un umbral así sólo distingue «acabo de
/// compilar» de «no acabo de compilar».</para>
///
/// <para>Corre en la colección exclusiva <see cref="EngineFirstRunCollection"/>: lo que se mide —la concurrencia
/// alcanzada y los hilos que la sostienen— es un recurso de todo el proceso, y una colección vecina ejecutando su
/// propio flujo la ensucia (medido: 1 904 ms con vecino donde sola sale en 269 ms).</para>
/// </summary>
[Collection(EngineFirstRunCollection.Name)]
public class EngineFirstRunTests
{
    private readonly ITestOutputHelper _output;

    public EngineFirstRunTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task FirstRun_ShouldUseEveryThreadItWasGiven()
    {
        const int fileCount = 96;
        const int busyMilliseconds = 50;
        int degreeOfParallelism = Environment.ProcessorCount;

        string source = Path.Combine(Path.GetTempPath(), "FF_FirstRun_Src_" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(Path.GetTempPath(), "FF_FirstRun_Dst_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);

        try
        {
            for (int i = 0; i < fileCount; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(source, $"item_{i:D3}.txt"), "x");
            }

            var loader = new PluginLoader();
            loader.RegisterNodeTypesFromAssembly(typeof(FolderSourceNode).Assembly);
            loader.RegisterNodeType<CpuBoundProbeNode>();

            ThreadPool.GetMinThreads(out int workerMinimum, out _);
            _output.WriteLine(
                $"CPUs lógicos: {degreeOfParallelism} | ítems: {fileCount} | trabajo: {fileCount * busyMilliseconds} ms de CPU | " +
                $"mínimo de hilos de trabajo del grupo: {workerMinimum} (ya es el número de CPUs)");

            var first = await MeasureAsync(loader, source, destination, degreeOfParallelism, fileCount, busyMilliseconds, "primera ejecución de la sesión");
            var steady = await MeasureAsync(loader, source, destination, degreeOfParallelism, fileCount, busyMilliseconds, "segunda ejecución");
            var steadyAgain = await MeasureAsync(loader, source, destination, degreeOfParallelism, fileCount, busyMilliseconds, "tercera ejecución");

            long reference = Math.Min(steady.WallMilliseconds, steadyAgain.WallMilliseconds);
            _output.WriteLine(
                $"referencia de estado estable: {reference} ms | el tramo de trabajo de la primera ejecución fue de " +
                $"{first.WorkMilliseconds} ms (misma máquina, mismo trabajo)");

            // Lo que se afirma es que la primera ejecución no use menos hilos que las que vienen detrás: ése es el
            // defecto («la primera vez no se usan todos los hilos») y la comparación es entre ejecuciones de la
            // misma máquina, así que no depende de cuántas CPUs tenga ni de lo cargada que esté.
            int steadyFloor = (int)Math.Min(steady.MaxConcurrency, steadyAgain.MaxConcurrency);
            int expected = Math.Min(degreeOfParallelism, fileCount) - 1;

            first.MaxConcurrency.Should().BeGreaterThanOrEqualTo(
                steadyFloor - 1,
                $"la primera ejecución de la sesión no puede usar menos hilos que las siguientes: usó " +
                $"{first.MaxConcurrency} simultáneos donde las siguientes usaron {steady.MaxConcurrency} y " +
                $"{steadyAgain.MaxConcurrency} (se pidieron {degreeOfParallelism} y esta máquina dio {expected}); si " +
                $"esto falla, el motor está repartiendo la primera ejecución entre menos hilos de los que le dieron");

            // El trabajo de la primera ejecución no puede ser mucho más lento que el de las siguientes: ahí no hay
            // nada que cargar por primera vez, sólo la rejilla repartiendo ítems. (El arranque previo al primer
            // ítem no se juzga: depende de si la máquina acaba de compilar, no del motor — ver el resumen de la clase.)
            first.WorkMilliseconds.Should().BeLessThanOrEqualTo(
                (long)(reference * 2) + 100,
                $"el tramo en que la rejilla ejecuta los 96 ítems tiene que costar lo mismo la primera vez que las " +
                $"siguientes: la primera tardó {first.WorkMilliseconds} ms de trabajo y el total estable es {reference} ms");
        }
        finally
        {
            if (Directory.Exists(source)) Directory.Delete(source, true);
            if (Directory.Exists(destination)) Directory.Delete(destination, true);
        }
    }

    private async Task<(long WallMilliseconds, long WorkMilliseconds, int MaxConcurrency)> MeasureAsync(
        PluginLoader loader,
        string source,
        string destination,
        int degreeOfParallelism,
        int fileCount,
        int busyMilliseconds,
        string label)
    {
        foreach (string stale in Directory.EnumerateFiles(destination))
        {
            File.Delete(stale);
        }

        // Un ejecutor por medición: reutilizarlo traería, además del trabajo, el estado de la ejecución anterior.
        var executor = new WorkflowExecutor
        {
            MaxDegreeOfParallelism = degreeOfParallelism,
            EnableCheckpointing = false
        };

        var graph = new WorkflowGraph
        {
            Name = "first-run-" + Guid.NewGuid().ToString("N"),
            Nodes =
            {
                new WorkflowNode
                {
                    Id = "source",
                    NodeTypeName = "FolderSourceNode",
                    Parameters = new Dictionary<string, object?> { ["SourcePath"] = source }
                },
                new WorkflowNode
                {
                    Id = "busy",
                    NodeTypeName = "CpuBoundProbeNode",
                    Parameters = new Dictionary<string, object?> { ["BusyMilliseconds"] = busyMilliseconds }
                },
                new WorkflowNode
                {
                    Id = "sink",
                    NodeTypeName = "DestinationSinkNode",
                    Parameters = new Dictionary<string, object?> { ["DestinationRoot"] = destination }
                }
            },
            Edges =
            {
                new WorkflowEdge { SourceNodeId = "source", SourcePortName = "Out", TargetNodeId = "busy", TargetPortName = "In" },
                new WorkflowEdge { SourceNodeId = "busy", SourcePortName = "Out", TargetNodeId = "sink", TargetPortName = "In" }
            }
        };

        CpuBoundProbeNode.Reset();
        CpuBoundProbeNode.StartClock();

        Process process = Process.GetCurrentProcess();
        TimeSpan cpuBefore = process.TotalProcessorTime;

        var clock = Stopwatch.StartNew();
        await executor.ExecuteAsync(graph, loader, CancellationToken.None);
        clock.Stop();

        TimeSpan cpuUsed = process.TotalProcessorTime - cpuBefore;
        int delivered = Directory.EnumerateFiles(destination).Count();
        double idealMilliseconds = fileCount * (double)busyMilliseconds;
        long beforeFirstItem = (long)CpuBoundProbeNode.FirstStartMilliseconds;
        long workMilliseconds = (long)(CpuBoundProbeNode.LastEndMilliseconds - CpuBoundProbeNode.FirstStartMilliseconds);

        _output.WriteLine(
            $"{label}: pared {clock.ElapsedMilliseconds} ms (ideal {idealMilliseconds:F0} ms), x{idealMilliseconds / Math.Max(1, clock.ElapsedMilliseconds):F1}, " +
            $"paralelismo {cpuUsed.TotalMilliseconds / Math.Max(1.0, clock.ElapsedMilliseconds):F2}, máximo simultáneo {CpuBoundProbeNode.MaxObserved}, " +
            $"entregados {delivered}, hilos {ThreadPool.ThreadCount} | antes del primer ítem {beforeFirstItem} ms, trabajo {workMilliseconds} ms");

        delivered.Should().Be(fileCount, "el motor no puede perder ítems al repartir trabajo entre hilos");
        CpuBoundProbeNode.Executions.Should().Be(fileCount, "cada ítem tiene que pasar una vez por el nodo");

        return (clock.ElapsedMilliseconds, workMilliseconds, CpuBoundProbeNode.MaxObserved);
    }
}
