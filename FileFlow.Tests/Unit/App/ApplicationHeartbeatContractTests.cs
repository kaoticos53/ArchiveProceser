using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// El contrato de los cuatro <b>latidos</b> de la aplicación: el vigilante de subflujos (1 s), el vaciado de la
/// consola (40 ms), el muestreo de rendimiento (1 s) y el volcado visual de la ejecución (33 ms).
///
/// <para>Sólo corren en la aplicación —el suite no bombea el bucle de mensajes—, así que hasta el hito 173 su
/// camino no se ejecutaba en ninguna prueba: lo que hacían era invisible para el suite y una regresión ahí sólo se
/// veía usando el producto (el barrido de la splash murió así, en silencio, durante varios hitos).</para>
///
/// <para>Este lint cierra las formas de perder esa garantía: que el paso deje de ser alcanzable, que el latido
/// deje de declararse en el registro, que la fontanería vuelva a copiarse en un componente, que el temporizador no
/// se deseche, o que la entrega deje de ser la protegida. Y comprueba el <b>registro de la aplicación</b>: que el
/// contenedor enlaza los cuatro latidos al mismo servicio, de modo que «los latidos del producto» sea una lista
/// que alguien puede leer.</para>
///
/// <para>La otra mitad del contrato —que el periodo sea <i>de verdad</i> el declarado— la mide
/// <c>HeartbeatCadenceTests</c> con un reloj manual: un latido que nadie puede medir es un latido que nadie nota
/// si se apaga.</para>
/// </summary>
public class ApplicationHeartbeatContractTests
{
    private const string Subflows = "FileFlow.App/ViewModels/EditorViewModel.cs";
    private const string Console = "FileFlow.App/ViewModels/LogViewModel.cs";
    private const string Performance = "FileFlow.App/Services/SystemPerformanceMonitor.cs";
    private const string Visual = "FileFlow.App/Services/WorkflowExecutionCoordinator.cs";
    private const string Registry = "FileFlow.App/Services/HeartbeatService.cs";
    private const string Delivery = "FileFlow.App/Services/Heartbeat.cs";

    [Fact]
    public void TheSubflowHeartbeat_ShouldBeADeclarationOfItsPublicStep()
    {
        string code = SourceText.CodeWithoutComments(Subflows);

        code.Should().Contain("public void RunSubflowWatchTick()",
            "el paso del latido debe ser alcanzable desde las pruebas: es lo que el registro entrega");

        code.Should().Contain("public static readonly TimeSpan SubflowWatchInterval",
            "el periodo tiene que ser público: la prueba de cadencia avanza el reloj contra ese número, no contra una copia");

        ShouldBeADeclaration(code, field: "_subflowWatchBeat",
            declaration: "Declare(SubflowWatchBeat, SubflowWatchInterval, RunSubflowWatchTick)");
    }

    [Fact]
    public void TheConsoleHeartbeat_ShouldBeADeclarationOfItsPublicStep()
    {
        string code = SourceText.CodeWithoutComments(Console);

        code.Should().Contain("public void FlushAllPendingLogs()",
            "el paso del latido debe ser alcanzable: es lo que hace que los registros aparezcan solos");

        code.Should().Contain("public static readonly TimeSpan FlushInterval",
            "el periodo tiene que ser público: la prueba de cadencia avanza el reloj contra ese número");

        ShouldBeADeclaration(code, field: "_flushBeat",
            declaration: "Declare(ConsoleFlushBeat, FlushInterval, FlushAllPendingLogs)");
    }

    [Fact]
    public void ThePerformanceHeartbeat_ShouldBeADeclarationOfItsPublicStep()
    {
        string code = SourceText.CodeWithoutComments(Performance);

        code.Should().Contain("public async Task SampleNowAsync()",
            "el paso del latido debe ser alcanzable, y con Task para poder esperarlo desde una prueba");

        code.Should().Contain("public static readonly TimeSpan SampleInterval",
            "el periodo tiene que ser público: la prueba de cadencia avanza el reloj contra ese número");

        ShouldBeADeclaration(code, field: "_sampleBeat",
            declaration: "Declare(SampleBeat, SampleInterval, () => _ = SampleNowAsync())");
    }

    [Fact]
    public void TheVisualHeartbeat_ShouldBeDeclaredWithoutLating_AndStartedByTheExecution()
    {
        string code = SourceText.CodeWithoutComments(Visual);

        code.Should().Contain("public void FlushVisualFrame()",
            "el fotograma periódico debe ser alcanzable: es lo que mueve el lienzo durante la ejecución");

        code.Should().Contain("public static readonly TimeSpan VisualFlushInterval",
            "el periodo tiene que ser público: la prueba de cadencia avanza el reloj contra ese número");

        // El latido visual no lo desecha su dueño (el coordinador vive tanto como la aplicación): lo desecha cada
        // ejecución, que es quien lo arranca y lo para en su cierre.
        ShouldBeADeclaration(code, field: "_visualFrameBeat",
            declaration: "Declare(VisualFrameBeat, VisualFlushInterval, FlushVisualFrame)",
            disposal: "visualFlushTimer.Dispose();");

        // Declarar no es arrancar: este latido no puede latir mientras no haya ejecución, y su arranque es un paso
        // con nombre propio para que la prueba de cadencia lo mida sin poner una ejecución en marcha.
        code.Should().Contain("public IHeartbeat StartVisualHeartbeat() => _visualFrameBeat.Start();",
            "la ejecución arranca el latido declarado, y ese arranque es el que se mide en HeartbeatCadenceTests");

        code.Should().Contain("IHeartbeat visualFlushTimer = StartVisualHeartbeat();",
            "la ejecución usa el mismo latido que se mide, y lo desecha al terminar");

        // Las colas viven en el coordinador (no en locales de RunAsync) para que el paso sea ejercitable; al
        // arrancar cada ejecución se vacían, porque un fotograma no puede pintar lo que quedó de la anterior.
        code.Should().Contain("_pendingEdgeUpdates.Clear();",
            "cada ejecución empieza con el lienzo limpio de avisos pendientes");

        // Y los manejadores del motor encolan por los mismos métodos públicos que usa la prueba.
        code.Should().Contain("QueueNodeStatus(nodeId, status);");
        code.Should().Contain("QueueNodeProgress(nodeId, pct, message);");
        code.Should().Contain("QueueEdgeDispatch(src, port, count);");
    }

    /// <summary>
    /// La fontanería de un latido existe <b>una sola vez</b>. Es el contrato que el hito 178 vino a poner: si un
    /// quinto latido copia el ritual —su <c>CreateTimer</c>, su <c>Heartbeat.Post</c> y su campo <c>ITimer</c>—,
    /// esta prueba falla y dice que lo que toca es declararlo en el registro.
    /// </summary>
    [Fact]
    public void ThePlumbing_ShouldLiveOnlyInTheHeartbeatRegistry()
    {
        var offenders = new List<string>();
        var scanned = new List<string>();

        foreach (string file in AppSourceFiles())
        {
            if (file is Registry or Delivery)
            {
                continue;
            }

            scanned.Add(file);
            string code = SourceText.CodeWithoutComments(file);

            if (code.Contains("CreateTimer", StringComparison.Ordinal))
            {
                offenders.Add($"{file}: programa un temporizador de latido por su cuenta");
            }

            if (code.Contains("Heartbeat.Post(", StringComparison.Ordinal))
            {
                offenders.Add($"{file}: entrega el tick por su cuenta en lugar de declarar el latido");
            }
        }

        scanned.Should().NotBeEmpty("un barrido vacío haría pasar esta prueba sin mirar nada");
        scanned.Should().Contain(Subflows).And.Contain(Console).And.Contain(Performance).And.Contain(Visual);

        offenders.Should().BeEmpty(
            "un latido nuevo se declara en el registro (HeartbeatService.Declare), que ya pone el reloj inyectable, " +
            "el despacho al hilo de la interfaz y la entrega protegida. Copiar la fontanería son cuatro sitios " +
            "donde equivocarse en lugar de uno: " + string.Join(" | ", offenders));
    }

    /// <summary>
    /// El registro, que es el único que programa latidos: reloj <b>inyectado</b> (un <c>DispatcherTimer</c> no se
    /// puede medir sin esperar su periodo), el mismo número por vencimiento y por periodo, la entrega protegida con
    /// el nombre del latido, y el temporizador desechado al pararlo.
    /// </summary>
    [Fact]
    public void TheRegistry_ShouldScheduleWithTheInjectedClock_AndDeliverProtected()
    {
        string code = SourceText.CodeWithoutComments(Registry);

        code.Should().Contain("public IHeartbeat Declare(string name, TimeSpan period, Action step)",
            "declarar es la única puerta de entrada de un latido: es lo que hace que la fontanería no haya que copiarla");

        code.Should().Contain("_owner._clock.CreateTimer(",
            "el latido tiene que colgar del reloj inyectable: un DispatcherTimer no se puede medir sin esperar su periodo");

        code.Should().Contain("Heartbeat.Post(_owner._ui, Name, _step)",
            "la entrega es la protegida y con el nombre del latido, para que el aviso diga cuál falló");

        code.Should().Contain("_timer?.Dispose();",
            "un latido sin fin que nadie desecha sigue latiendo sobre objetos desmontados");

        code.Should().NotContain("new DispatcherTimer");

        TimerProgramming(code).Split("Period").Length.Should().BeGreaterThanOrEqualTo(3,
            "el latido tiene que usar 'Period' como vencimiento y como periodo, no un número suelto");
    }

    /// <summary>
    /// El <b>registro de la aplicación</b>: el contenedor tiene que enlazar los cuatro latidos al mismo servicio,
    /// de modo que se puedan enumerar. Antes cada componente programaba el suyo y esa lista no existía.
    /// </summary>
    [Fact]
    public void TheApplicationRegistry_ShouldDeclareTheFourBeats()
    {
        var services = new ServiceCollection();
        services.AddFileFlowServices();
        using var provider = services.BuildServiceProvider();

        // Se resuelven los dueños de los latidos: cada uno declara el suyo al construirse.
        provider.GetRequiredService<EditorViewModel>();
        provider.GetRequiredService<LogViewModel>();
        provider.GetRequiredService<ISystemPerformanceMonitor>();
        provider.GetRequiredService<ControlBarViewModel>();

        IHeartbeatService heartbeats = provider.GetRequiredService<IHeartbeatService>();

        heartbeats.DeclaredNames.Should().BeEquivalentTo(
            [
                EditorViewModel.SubflowWatchBeat,
                LogViewModel.ConsoleFlushBeat,
                SystemPerformanceMonitor.SampleBeat,
                WorkflowExecutionCoordinator.VisualFrameBeat
            ],
            "el registro es la lista de latidos del producto: un latido que no se declara en él no lo ve nadie");

        heartbeats.Find(EditorViewModel.SubflowWatchBeat)!.Period.Should().Be(EditorViewModel.SubflowWatchInterval);
        heartbeats.Find(LogViewModel.ConsoleFlushBeat)!.Period.Should().Be(LogViewModel.FlushInterval);
        heartbeats.Find(SystemPerformanceMonitor.SampleBeat)!.Period.Should().Be(SystemPerformanceMonitor.SampleInterval);

        heartbeats.Find(WorkflowExecutionCoordinator.VisualFrameBeat)!.IsRunning.Should().BeFalse(
            "el fotograma visual se declara en el arranque pero sólo late durante una ejecución");
    }

    /// <summary>
    /// La forma de un latido desde el hito 178, común a los cuatro: se declara en el registro con el paso
    /// <b>público</b> y su nombre y periodo declarados (nada de copias que puedan divergir), se arranca, y se
    /// desecha quien lo posee.
    /// </summary>
    private static void ShouldBeADeclaration(string code, string field, string declaration, string? disposal = null)
    {
        code.Should().Contain(declaration,
            "el latido se declara en el registro con su nombre, su periodo y el paso probado");

        code.Should().Contain($"IHeartbeat {field};",
            "lo que un latido guarda es su declaración del registro, no un temporizador propio");

        code.Should().Contain(".Start()",
            "declarar no arranca: quien posee el latido decide cuándo late");

        code.Should().Contain(disposal ?? $"{field}.Dispose();",
            "un latido sin fin que nadie desecha sigue latiendo sobre objetos desmontados");

        code.Should().NotContain("CreateTimer",
            "programar el temporizador a mano es la fontanería que el registro vino a quitar");

        code.Should().NotContain("Heartbeat.Post(",
            "la entrega protegida vive en el servicio, no en cada latido");
    }

    /// <summary>El texto de la programación del latido, para poder mirar dentro y no sólo ver que existe.</summary>
    private static string TimerProgramming(string code)
    {
        const string Marker = ".CreateTimer(";
        int start = code.IndexOf(Marker, StringComparison.Ordinal);

        start.Should().BeGreaterThanOrEqualTo(0, $"el latido se programa con '{Marker}'");

        int end = code.IndexOf(");", start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, "la programación del latido tiene que cerrar su llamada");

        return code[start..end];
    }

    /// <summary>Todo el código del host: es donde vive la fontanería de los latidos de la interfaz.</summary>
    private static IEnumerable<string> AppSourceFiles()
    {
        string root = TestRepositoryLocator.RepositoryRoot();

        return Directory.EnumerateFiles(Path.Combine(root, "FileFlow.App"), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal);
    }
}
