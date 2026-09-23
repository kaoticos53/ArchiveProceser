using System;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.Core.Plugins;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// El <b>registro de latidos</b>: qué latidos declara cada componente del producto, con qué periodo, y cuántas
/// veces entregan su trabajo por periodo.
///
/// <para><b>Por qué esto no existía</b>: medir la cadencia exigía esperar el periodo de verdad (un segundo por
/// caso, cuatro latidos) y, peor, una espera real no prueba la cadencia —prueba que el tiempo pasa—. Desde el
/// hito 176 los latidos cuelgan de un <c>TimeProvider</c> inyectable y desde el 178 de un único servicio, así que
/// aquí el tiempo lo avanza la prueba: <b>ni un tick antes del periodo, exactamente uno por periodo</b>, y ninguno
/// después de pararlo.</para>
///
/// <para><b>Qué se mide y qué no</b>: la cadencia es ahora una propiedad del <b>servicio</b>, no de cada
/// componente, así que se mide una vez por latido declarado y lo que se afirma de cada componente es que lo
/// declara <b>con su nombre y su periodo</b> (y que su paso es el que se entrega). El instrumento es
/// <see cref="RecordingUiDispatcher"/>: cada vencimiento entrega exactamente un despacho, así que contarlos por el
/// nombre del método es contar los latidos; contar el <i>efecto</i> no serviría para todos, porque el vigilante de
/// subflujos sin nada que refrescar no deja rastro por diseño.</para>
/// </summary>
public class HeartbeatCadenceTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Los cuatro latidos del producto: declarados y latiendo una vez por periodo
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheSubflowHeartbeat_ShouldBeDeclaredWithItsPeriod_AndFireOncePerInterval()
    {
        var clock = new ManualTimeProvider();
        var ui = new RecordingUiDispatcher();
        using var heartbeats = new HeartbeatService(clock, ui);

        using var editor = new EditorViewModel(
            new PluginLoader(),
            userPreferencesService: new InMemoryUserPreferencesService(),
            heartbeats: heartbeats);

        IHeartbeat beat = DeclaredAndRunning(heartbeats, EditorViewModel.SubflowWatchBeat, EditorViewModel.SubflowWatchInterval);

        MeasureHeartbeat(clock, ui, beat, nameof(EditorViewModel.RunSubflowWatchTick), periods: 4);
    }

    [Fact]
    public void TheConsoleHeartbeat_ShouldBeDeclaredWithItsPeriod_AndFireOncePerInterval()
    {
        var clock = new ManualTimeProvider();
        var ui = new RecordingUiDispatcher();
        using var heartbeats = new HeartbeatService(clock, ui);

        using var console = new LogViewModel(logStore: new InMemoryLogStore(), heartbeats: heartbeats);

        IHeartbeat beat = DeclaredAndRunning(heartbeats, LogViewModel.ConsoleFlushBeat, LogViewModel.FlushInterval);

        MeasureHeartbeat(clock, ui, beat, nameof(LogViewModel.FlushAllPendingLogs), periods: 9);
    }

    [Fact]
    public void ThePerformanceHeartbeat_ShouldBeDeclaredWithItsPeriod_AndFireOncePerInterval()
    {
        var clock = new ManualTimeProvider();
        var ui = new RecordingUiDispatcher();
        using var heartbeats = new HeartbeatService(clock, ui);

        using var monitor = new SystemPerformanceMonitor(heartbeats: heartbeats);

        IHeartbeat beat = DeclaredAndRunning(heartbeats, SystemPerformanceMonitor.SampleBeat, SystemPerformanceMonitor.SampleInterval);

        // El paso del muestreo se declara como una lambda (SampleNowAsync devuelve Task y el latido entrega una
        // acción), así que aquí se cuentan todos los despachos: este componente no despacha nada más. Que el paso
        // sea el suyo lo dice el muestreo de punta a punta, y que el latido sea éste lo dice su nombre en el
        // registro —que es justo lo que el nombre viene a resolver—.
        MeasureHeartbeat(clock, ui, beat, methodName: null, periods: 3);
    }

    [Fact]
    public void TheVisualHeartbeat_ShouldBeDeclaredWithoutLating_UntilTheExecutionStarts()
    {
        var clock = new ManualTimeProvider();
        var ui = new RecordingUiDispatcher();

        // Los demás latidos del montaje no se avanzan nunca: existen para poder construir el coordinador, no para
        // latir en esta prueba.
        using var dormant = new HeartbeatService(new ManualTimeProvider(), ui);
        using var heartbeats = new HeartbeatService(clock, ui);

        var loader = new PluginLoader();
        using var editor = new EditorViewModel(
            loader,
            userPreferencesService: new InMemoryUserPreferencesService(),
            heartbeats: dormant);

        using var log = new LogViewModel(new InMemoryLogStore(), heartbeats: dormant);

        var coordinator = new WorkflowExecutionCoordinator(
            editor,
            loader,
            log,
            new NodeInspectorViewModel(editor, new NullFileDialogService(), log),
            uiDispatcher: ui,
            userPreferencesService: new InMemoryUserPreferencesService(),
            heartbeats: heartbeats);

        // El fotograma visual es el único latido que no late siempre: declararlo no puede ponerlo en marcha, o
        // cada lienzo abierto pintaría fotogramas sin ejecución en marcha.
        IHeartbeat declared = Declared(heartbeats, WorkflowExecutionCoordinator.VisualFrameBeat, WorkflowExecutionCoordinator.VisualFlushInterval);
        declared.IsRunning.Should().BeFalse("declarar un latido no es arrancarlo: éste sólo late durante una ejecución");
        clock.PendingTimerCount.Should().Be(0, "mientras no haya ejecución no puede haber temporizador");

        using IHeartbeat beat = coordinator.StartVisualHeartbeat();
        ReferenceEquals(beat, declared).Should().BeTrue("arrancar el latido de la ejecución no declara otro: es el mismo del registro");

        MeasureHeartbeat(clock, ui, beat, nameof(WorkflowExecutionCoordinator.FlushVisualFrame), periods: 4);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // El servicio: lo que antes copiaba cada latido, y que ahora se prueba una vez
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheRegistry_ShouldNotAdmitTwoBeatsWithTheSameName()
    {
        using var heartbeats = new HeartbeatService(new ManualTimeProvider(), new RecordingUiDispatcher());

        heartbeats.Declare("vaciado", TimeSpan.FromMilliseconds(40), () => { });

        var duplicate = () => heartbeats.Declare("vaciado", TimeSpan.FromSeconds(1), () => { });

        duplicate.Should().Throw<InvalidOperationException>(
                "dos latidos con el mismo nombre se taparían en el registro, y el que no se viera sería el que " +
                "nadie echa de menos")
            .WithMessage("*'vaciado'*");
    }

    [Fact]
    public void TheRegistry_ShouldRejectAPeriodOrANameThatCannotBeALatido()
    {
        using var heartbeats = new HeartbeatService(new ManualTimeProvider(), new RecordingUiDispatcher());

        var noName = () => heartbeats.Declare("  ", TimeSpan.FromSeconds(1), () => { });
        var noPeriod = () => heartbeats.Declare("sin-periodo", TimeSpan.Zero, () => { });
        var noStep = () => heartbeats.Declare("sin-paso", TimeSpan.FromSeconds(1), null!);

        noName.Should().Throw<ArgumentException>("un latido sin nombre no se puede encontrar ni nombrar en un aviso");
        noPeriod.Should().Throw<ArgumentOutOfRangeException>("un periodo no positivo no es un latido, es un bucle cerrado");
        noStep.Should().Throw<ArgumentNullException>("un latido sin paso no tiene nada que entregar");
    }

    [Fact]
    public void AStoppedBeat_ShouldStopDelivering_AndResumeWhenStartedAgain()
    {
        var clock = new ManualTimeProvider();
        var ui = new RecordingUiDispatcher();
        using var heartbeats = new HeartbeatService(clock, ui);

        IHeartbeat beat = heartbeats.Declare("efimero", TimeSpan.FromMilliseconds(50), () => { }).Start();

        clock.AdvanceBy(TimeSpan.FromMilliseconds(50));
        ui.PostCount.Should().Be(1);
        beat.IsRunning.Should().BeTrue();

        beat.Stop();

        beat.IsRunning.Should().BeFalse("parado, el latido no tiene temporizador");
        clock.PendingTimerCount.Should().Be(0, "el temporizador se desecha al pararlo, no se deja corriendo");

        clock.AdvanceBy(TimeSpan.FromMilliseconds(500));
        ui.PostCount.Should().Be(1, "un latido parado no entrega nada: si entregara, seguiría latiendo sobre objetos desmontados");

        beat.Start();
        clock.AdvanceBy(TimeSpan.FromMilliseconds(50));

        ui.PostCount.Should().Be(2, "y vuelve a latir cuando se le arranca otra vez");
    }

    [Fact]
    public void TheRegistry_ShouldListAndFindTheBeatsItDeclares()
    {
        using var heartbeats = new HeartbeatService(new ManualTimeProvider(), new RecordingUiDispatcher());

        heartbeats.Declare("uno", TimeSpan.FromMilliseconds(10), () => { });
        heartbeats.Declare("dos", TimeSpan.FromMilliseconds(20), () => { });

        heartbeats.DeclaredNames.Should().Equal(["uno", "dos"], "el registro se lee en orden de declaración");
        heartbeats.Find("dos")!.Period.Should().Be(TimeSpan.FromMilliseconds(20));
        heartbeats.Find("tres").Should().BeNull("un latido que nadie declaró no existe");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La medida y el andamiaje
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Busca en el registro el latido declarado y comprueba que está declarado y arrancado.</summary>
    private static IHeartbeat DeclaredAndRunning(IHeartbeatService heartbeats, string name, TimeSpan period)
    {
        IHeartbeat beat = Declared(heartbeats, name, period);

        beat.IsRunning.Should().BeTrue($"'{name}' late desde que su componente existe: es su ciclo de vida");

        return beat;
    }

    /// <summary>El latido declarado con ese nombre, y con el periodo que declara su componente.</summary>
    private static IHeartbeat Declared(IHeartbeatService heartbeats, string name, TimeSpan period)
    {
        IHeartbeat? beat = heartbeats.Find(name);

        beat.Should().NotBeNull($"'{name}' tiene que estar declarado en el registro: un latido que no se declara no existe para nadie");

        beat!.Period.Should().Be(period,
            "el registro guarda el periodo declarado por el componente: la prueba de cadencia avanza el reloj contra ese número, no contra una copia");
        beat.Name.Should().Be(name);

        return beat;
    }

    /// <summary>
    /// Mide un latido: un tick antes del periodo no entrega nada; al cumplirse entrega <b>uno</b>; cada periodo
    /// siguiente, otro; y parado, ninguno. <paramref name="methodName"/> acota la cuenta a los despachos de ese
    /// método; con <c>null</c> se cuentan todos.
    /// </summary>
    private static void MeasureHeartbeat(
        ManualTimeProvider clock,
        RecordingUiDispatcher ui,
        IHeartbeat beat,
        string? methodName,
        int periods)
    {
        int Deliveries() => methodName is null ? ui.PostCount : ui.CountPostsOf(methodName);

        clock.PendingTimerCount.Should().Be(1, "el latido tiene que vivir en el reloj inyectado, no en uno propio");

        clock.AdvanceBy(beat.Period - TimeSpan.FromTicks(1));
        Deliveries().Should().Be(0, "un latido no puede adelantarse a su periodo");

        clock.AdvanceBy(TimeSpan.FromTicks(1));
        Deliveries().Should().Be(1, "cumplido el periodo, el latido entrega su trabajo exactamente una vez");

        // Un periodo por vez: el reloj manual no inventa latidos que nadie atendió (un salto largo dispara una
        // vez, como un temporizador real que se quedó esperando y coalesce), así que la cadencia se mide paso a
        // paso —que es justo lo que se quiere afirmar: uno por periodo—.
        for (int i = 0; i < periods; i++)
        {
            clock.AdvanceBy(beat.Period);
        }

        Deliveries().Should().Be(1 + periods, "el latido es periódico: cada periodo entrega otro");

        beat.Stop();
        clock.AdvanceBy(beat.Period * 3);

        Deliveries().Should().Be(1 + periods, "y parado no entrega ninguno más");
    }
}
