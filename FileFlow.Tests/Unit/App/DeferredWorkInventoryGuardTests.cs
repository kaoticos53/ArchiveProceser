using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardia del <b>inventario de trabajo aplazado en el tiempo</b>: falla cuando el código de producción gana un
/// temporizador o una espera que ninguna prueba ejercita y nadie ha explicado.
///
/// <para><b>Por qué existe</b>: la capa que sólo corre con la aplicación en marcha o cuando pasa el tiempo es la
/// que peor envejece, y cada vez se descubrió a mano. El barrido de la splash estuvo muerto varios hitos sin que
/// nadie lo notara (hito 169); los cuatro latidos no se ejercitaban en ninguna prueba (hito 173); y los dos
/// relojes con duración semántica no tenían vencimiento probado hasta el hito 174. Este lint convierte esa
/// revisión en algo que <b>se hace sola</b>: el inventario se recalcula del código en cada ejecución y cada sitio
/// tiene que estar <b>ejercitado</b> por una prueba nombrada o <b>explicado</b> con un motivo.</para>
///
/// <para>La política —qué es un sitio y qué queda fuera— vive en
/// <see cref="DeferredWorkInventoryAnalyzer"/> y aquí se auto-testea con snippets; este fichero sólo contiene el
/// registro de decisiones y las tres comprobaciones sobre el árbol real.</para>
///
/// <para><b>Qué hacer cuando falle</b>: si el sitio es nuevo, hazlo alcanzable con el patrón del hito 173 (el
/// mismo método que llama el temporizador, público y sin argumentos), escribe la prueba que lo ejercita y añade
/// aquí su decisión <c>Exercised</c>; si es una espera que por diseño quiere tiempo real, añade su
/// <c>RealTime</c> con el motivo. Si el sitio desapareció, borra su entrada: una entrada huérfana también falla.</para>
/// </summary>
public class DeferredWorkInventoryGuardTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Registro: una decisión por sitio del inventario
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cada sitio del inventario, con lo que se ha decidido sobre él. <c>Exercised</c> significa «hay un paso
    /// público que lo ejecuta y una prueba que lo nombra»; <c>RealTime</c> significa «esta espera quiere tiempo
    /// real por diseño», y entonces el motivo es obligatorio y se lee aquí.
    /// </summary>
    private static readonly DeferredWorkDecision[] Registry =
    [
        // ── La aplicación: el barrido de la splash (hito 169) y el registro de latidos (hito 178) ──
        Exercised("FileFlow.App/Views/SplashScreenWindow.axaml.cs::.ctor::Timer", "AdvanceShimmer", "SplashScreenStartupTests"),

        // Los cuatro latidos del hito 173 programaban cada uno su temporizador; desde el hito 178 hay un solo
        // sitio que programa latidos —el registro— y su paso público es el arranque del latido, con la declaración
        // como evidencia: `HeartbeatCadenceTests` declara latidos y mide su cadencia una vez por el mecanismo
        // compartido, en lugar de cuatro veces por cuatro copias. Los pasos de cada latido siguen siendo públicos
        // y los vigila `ApplicationHeartbeatContractTests`.
        Exercised("FileFlow.App/Services/HeartbeatService.cs::Start::Timer", "Start", "HeartbeatCadenceTests", "Declare"),

        // ── Los dos relojes con duración semántica del hito 174: reloj inyectado y vencimiento probado ──
        Exercised("FileFlow.App/ViewModels/EditorViewModel.cs::ExpireConnectionPulseAsync::Delay", "PulseConnectionEnergy", "ConnectionEnergyTests"),
        Exercised("FileFlow.App/ViewModels/NodeParameterViewModel.cs::CopyEvaluatedValueAsync::Delay", "CopyEvaluatedValueAsync", "NodeParameterViewModelTests"),

        // El aviso de cierre del arranque: espera a que el primer fotograma esté pintado antes de salir a la red
        // a buscar actualizaciones. No hay paso público y su duración sólo tiene sentido con una interfaz real
        // detrás; la prueba de humo de arranque recorre el arranque, no esta espera.
        RealTime("FileFlow.App/App.axaml.cs::StartBackgroundWork::Delay", "espera del arranque a que la interfaz esté pintada antes de consultar actualizaciones (no compite con el primer fotograma)"),

        // El fundido de cierre avanza por fotogramas de 16 ms: la duración <i>es</i> la animación.
        RealTime("FileFlow.App/Views/SplashScreenWindow.axaml.cs::CloseWithFadeAsync::Delay", "el fundido de cierre avanza fotograma a fotograma; ningún test lo llama porque la splash se cierra de verdad en el arranque"),

        // ── Core: reintentos, planificador, sondeo del vigilante y la cola de la telemetría ──
        // Las pruebas del reintento usan initialBackoffMs: 10, así que la espera se ejecuta (corta, pero real).
        Exercised("FileFlow.Core/Engine/ExecutionRetryHelper.cs::ExecuteWithRetryAsync::Delay#1", "ExecuteWithRetryAsync", "AutomationAndResilienceTests"),
        Exercised("FileFlow.Core/Engine/ExecutionRetryHelper.cs::ExecuteWithRetryAsync::Delay#2", "ExecuteWithRetryAsync", "AutomationAndResilienceTests"),
        Exercised("FileFlow.Core/Engine/FlowSchedulerService.cs::RunTimerAsync::Timer", "StartInterval", "AutomationAndResilienceTests"),
        Exercised("FileFlow.Core/Engine/FolderWatcherService.cs::ProcessPendingQueueAsync::Delay", "Start", "WorkflowFolderWatcherTests"),
        Exercised("FileFlow.Core/Telemetry/SqliteLogStore.cs::ProcessIngestionQueueAsync::Delay", "FlushPendingLogsAsync", "SqliteLogStoreTests"),

        // ── Plugins ──
        // Los tres retardos del cliente VLM (dos backoffs y la pausa entre intentos) se ejercitan con la escala
        // de reintento que la suite pone al 0 %, que existe precisamente para eso.
        Exercised("FileFlow.Plugin.AI/Engines/MultimodalVlmClientEngine.cs::ExecuteChatCompletionAsync::Delay#1", "ExecuteChatCompletionAsync", "VlmEndpointFloodGuardTests"),
        Exercised("FileFlow.Plugin.AI/Engines/MultimodalVlmClientEngine.cs::ExecuteChatCompletionAsync::Delay#2", "ExecuteChatCompletionAsync", "VlmEndpointFloodGuardTests"),
        Exercised("FileFlow.Plugin.AI/Engines/MultimodalVlmClientEngine.cs::ExecuteChatCompletionAsync::Delay#3", "ExecuteChatCompletionAsync", "VlmEndpointFloodGuardTests"),
        Exercised("FileFlow.Plugin.AI/ViewModels/MultimodalVlmConfigViewModel.cs::TestConnectionAsync::Delay", "TestConnectionAsync", "MultimodalVlmConfigViewModelTests"),

        // El nodo de retardo se ejecuta de verdad en un flujo de prueba con DelayMilliseconds = 50; la evidencia
        // es el tipo porque el test monta el nodo en el grafo y no nombra su ExecuteAsync.
        Exercised("FileFlow.Plugin.Logic/ThrottleDelayNode.cs::ExecuteAsync::Delay", "ExecuteAsync", "WorkflowBottleneckTelemetryTests", evidence: "ThrottleDelayNode"),

        // El retardo del origen sintético simula un origen lento y lo configura el usuario (EmissionDelayMs). La
        // prueba lo ejecuta con una latencia pequeña —5 ms por muestra sobre tres muestras— y afirma el hueco
        // entre emisiones: el retardo se pisa de verdad, y el hueco sólo puede venir de él porque el armado de
        // las muestras ocurre entero antes de la primera emisión. Es el tercer sitio que sale de «tiempo real»
        // por la vía barata: una latencia que la prueba puede permitirse, en lugar del reloj inyectado que el
        // hito 174 sí necesitó (este nodo no lo tiene: su fábrica lo construye sin dependencias).
        Exercised("FileFlow.Plugin.FileSystem/Nodes/Sources/SyntheticDataSourceNode.cs::ExecuteAsync::Delay", "ExecuteAsync", "SyntheticDataSourceNodeTests")
    ];

    /// <summary>Un paso público que una prueba nombra: la decisión fuerte.</summary>
    private static DeferredWorkDecision Exercised(string key, string step, string evidenceTest, string? evidence = null) =>
        DeferredWorkDecision.Exercised(key, step, evidenceTest, evidence);

    /// <summary>La espera quiere tiempo real por diseño: el motivo es obligatorio y se lee en el registro.</summary>
    private static DeferredWorkDecision RealTime(string key, string reason) =>
        DeferredWorkDecision.RealTime(key, reason);

    // ─────────────────────────────────────────────────────────────────────────────
    // Las comprobaciones sobre el árbol real
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryTimerOrWaitInProduction_ShouldBeExercisedOrExplained()
    {
        var inventory = DeferredWorkInventoryAnalyzer.ScanRepository(TestRepositoryLocator.RepositoryRoot());

        var decided = Registry.ToDictionary(decision => decision.Key, StringComparer.Ordinal);
        var found = inventory.Select(site => site.Key).ToHashSet(StringComparer.Ordinal);

        var uninventoried = inventory
            .Where(site => !decided.ContainsKey(site.Key))
            .Select(site => $"    {site}")
            .ToList();

        uninventoried.Should().BeEmpty(
            "un temporizador o una espera nueva tiene que decidirse: o se ejercita con una prueba —haz " +
            "alcanzable el paso que ejecuta el temporizador, como en el hito 173— o se explica por qué quiere " +
            "tiempo real. Sitios sin decisión:" + Environment.NewLine + string.Join(Environment.NewLine, uninventoried));

        var stale = decided.Keys
            .Where(key => !found.Contains(key))
            .Select(key => $"    {decided[key]}")
            .ToList();

        stale.Should().BeEmpty(
            "una decisión sin sitio miente sobre lo que hay en producción (el sitio se movió, se renombró o " +
            "desapareció): actualiza el registro" + Environment.NewLine + string.Join(Environment.NewLine, stale));
    }

    [Fact]
    public void ExercisedSites_ShouldNameAPublicStepAndTheTestThatReachesIt()
    {
        string root = TestRepositoryLocator.RepositoryRoot();
        var inventory = DeferredWorkInventoryAnalyzer.ScanRepository(root)
            .ToDictionary(site => site.Key, StringComparer.Ordinal);

        var problems = new List<string>();

        foreach (var decision in Registry)
        {
            if (!decision.IsExercised)
            {
                decision.Reason.Should().NotBeNullOrWhiteSpace(
                    $"la decisión de '{decision.Key}' tiene que decir por qué no se prueba");
                continue;
            }

            if (!inventory.TryGetValue(decision.Key, out var site))
            {
                continue; // el registro huérfano ya lo delata la comprobación del inventario.
            }

            string source = SourceText.CodeWithoutComments(site.File);

            if (!DeferredWorkInventoryAnalyzer.HasPublicMember(source, decision.Step!))
            {
                problems.Add(
                    $"    {decision.Key}: '{decision.Step}' no es un miembro público de {site.File}. El paso que " +
                    "ejecuta el temporizador tiene que ser alcanzable desde las pruebas, no una copia");
            }

            if (!TestThatNamesTheEvidence(root, decision.EvidenceTest!, decision.Evidence!, out string? reason))
            {
                problems.Add($"    {decision.Key}: {reason}");
            }
        }

        problems.Should().BeEmpty(
            "una decisión 'Exercised' es una afirmación verificable: el paso tiene que existir, ser público y " +
            "estar nombrado por la prueba que se cita" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    /// <summary>Sin esto, un barrido que no encontrase nada pasaría siempre y la guardia sería decorativa.</summary>
    [Fact]
    public void Sweep_ShouldReachEveryProductionProject()
    {
        var inventory = DeferredWorkInventoryAnalyzer.ScanRepository(TestRepositoryLocator.RepositoryRoot());

        inventory.Should().NotBeEmpty("un inventario vacío haría pasar la guardia sin mirar nada");

        inventory.Select(site => site.File).Should().Contain(
            path => path.StartsWith("FileFlow.App/", StringComparison.Ordinal),
            "la aplicación es donde viven los latidos de la interfaz");

        inventory.Select(site => site.File).Should().Contain(
            path => path.StartsWith("FileFlow.Core/", StringComparison.Ordinal),
            "el motor también aplaza trabajo (reintentos, sondeos, planificador)");

        inventory.Select(site => site.File).Should().Contain(
            path => path.StartsWith("FileFlow.Plugin.", StringComparison.Ordinal),
            "los plugins aplazan trabajo y no pueden quedar fuera del inventario");

        // Sitios conocidos: si alguien mueve o reescribe uno, el barrido deja de verlo en silencio y esto lo dice.
        inventory.Select(site => site.Key).Should().Contain(
            [
                "FileFlow.App/Services/HeartbeatService.cs::Start::Timer",
                "FileFlow.App/Views/SplashScreenWindow.axaml.cs::.ctor::Timer",
                "FileFlow.App/ViewModels/EditorViewModel.cs::ExpireConnectionPulseAsync::Delay",
                "FileFlow.App/ViewModels/NodeParameterViewModel.cs::CopyEvaluatedValueAsync::Delay"
            ]);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Auto-tests del analizador: qué es un sitio y qué no
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyzer_ShouldFlagATimerAndAWaitWithTheirMemberAndLine()
    {
        const string Source = """
            using System.Threading.Tasks;
            using Avalonia.Threading;

            public class Consola
            {
                private DispatcherTimer _timer;

                public Consola()
                {
                    _timer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(40) };
                }

                private async Task EsperarAsync()
                {
                    await Task.Delay(1500);
                }
            }
            """;

        var sites = DeferredWorkInventoryAnalyzer.Inventory(
            DeferredWorkInventoryAnalyzer.AnalyzeSource(Source, "Consola.cs"));

        sites.Should().HaveCount(2);

        sites.Should().ContainSingle(site => site.Kind == DeferredWorkKind.Timer)
            .Which.Member.Should().Be(".ctor", "el reloj vive en el constructor");

        var wait = sites.Should().ContainSingle(site => site.Kind == DeferredWorkKind.Delay).Which;
        wait.Member.Should().Be("EsperarAsync");
        wait.Line.Should().Be(15, "la línea reportada es la del código, no la de una copia");
        wait.Construct.Should().Be("Task.Delay(1500)");
        wait.UsesInjectedClock.Should().BeFalse("este retardo cuelga del reloj del sistema");
    }

    [Fact]
    public void Analyzer_ShouldRecognizeAnInjectedClock_AndOtherTimerFactories()
    {
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public class Relojes
            {
                private readonly TimeProvider _timeProvider = TimeProvider.System;

                public async Task EsperarAsync(int ms)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(ms), _timeProvider).ConfigureAwait(false);
                }

                public void Programar(TimeSpan intervalo) => _ = new PeriodicTimer(intervalo);

                public ITimer Crear(TimeSpan cada) => _timeProvider.CreateTimer(_ => { }, null, cada, cada);
            }
            """;

        var sites = DeferredWorkInventoryAnalyzer.Inventory(
            DeferredWorkInventoryAnalyzer.AnalyzeSource(Source, "Relojes.cs"));

        sites.Should().HaveCount(3);

        sites.Single(site => site.Kind == DeferredWorkKind.Delay).UsesInjectedClock.Should().BeTrue(
            "Task.Delay(duración, timeProvider) es la sobrecarga que hace probable el vencimiento");

        sites.Count(site => site.Kind == DeferredWorkKind.Timer).Should().Be(2);
        sites.Single(site => site.Construct == "_timeProvider.CreateTimer").UsesInjectedClock.Should().BeTrue();
    }

    [Fact]
    public void Analyzer_ShouldIgnoreCancellationDeadlinesThreadMarshallingAndText()
    {
        // Fuera de alcance por regla, no por olvido: una cancelación programada no ejecuta trabajo —despierta un
        // token— y el despacho al hilo de UI es marshalado (en headless ese trabajo sí corre en las pruebas).
        //
        // El alias del despachador es deliberado: la guardia del contrato de colecciones busca ese nombre en el
        // TEXTO del suite, y aquí el despacho es una cadena de ejemplo, no una llamada que toque la sesión.
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using UiDispatcher = Avalonia.Threading.Dispatcher;

            public class FueraDeAlcance
            {
                public string Texto => "Task.Delay(1000) y new DispatcherTimer() escritos en una cadena";

                public void ProgramarCancelacion(CancellationTokenSource cts) => cts.CancelAfter(TimeSpan.FromSeconds(5));

                public void Despachar() => UiDispatcher.UIThread.Post(() => Console.WriteLine("hola"));

                public Task DespacharAsync() => UiDispatcher.UIThread.InvokeAsync(() => Console.WriteLine("hola")).GetTask();
            }
            """;

        DeferredWorkInventoryAnalyzer
            .AnalyzeSource(Source, "FueraDeAlcance.cs")
            .Should().BeEmpty(
                "el inventario es de relojes y esperas: los plazos de cancelación y el marshalado de hilo no lo " +
                "son, y el texto no es código");
    }

    [Fact]
    public void Analyzer_ShouldIgnoreWhatIsCommented()
    {
        const string Source = """
            using Avalonia.Threading;

            public class Comentado
            {
                // _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                /*
                    await Task.Delay(3000);
                */
                public void Nada() { }
            }
            """;

        DeferredWorkInventoryAnalyzer
            .AnalyzeSource(Source, "Comentado.cs")
            .Should().BeEmpty("un lint de texto se conforma con encontrar la línea comentada (lección del hito 165)");
    }

    [Fact]
    public void Analyzer_ShouldReportTheLocalFunctionThatAwaits_NotTheEnclosingMethod()
    {
        const string Source = """
            using System.Threading.Tasks;

            public class ConFuncionLocal
            {
                public void Disparar()
                {
                    async Task VencerAsync()
                    {
                        await Task.Delay(900);
                    }

                    _ = VencerAsync();
                }
            }
            """;

        DeferredWorkInventoryAnalyzer
            .AnalyzeSource(Source, "ConFuncionLocal.cs")
            .Should().ContainSingle()
            .Which.Member.Should().Be("VencerAsync", "la clave del inventario distingue dónde vive cada espera");
    }

    [Fact]
    public void Inventory_ShouldUseOrdinalsOnlyWhenAMemberDefersMoreThanOnce()
    {
        const string Source = """
            using System.Threading.Tasks;

            public class DosEsperas
            {
                public async Task EsperarAsync()
                {
                    await Task.Delay(100);
                    await Task.Delay(200);
                }

                public async Task OtraAsync() => await Task.Delay(300);
            }
            """;

        var keys = DeferredWorkInventoryAnalyzer
            .Inventory(DeferredWorkInventoryAnalyzer.AnalyzeSource(Source, "DosEsperas.cs"))
            .Select(site => site.Key)
            .ToList();

        keys.Should().BeEquivalentTo(
        [
            "DosEsperas.cs::EsperarAsync::Delay#1",
            "DosEsperas.cs::EsperarAsync::Delay#2",
            "DosEsperas.cs::OtraAsync::Delay"
        ]);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Localización de la evidencia
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La prueba citada tiene que existir y nombrar la evidencia (el paso, o el tipo del sitio cuando la prueba
    /// lo maneja sin nombrar su método). Es el eslabón que convierte «está inventariado» en «algo lo ejercita»:
    /// sin él, el registro sería una lista de buenas intenciones que nadie comprueba.
    /// </summary>
    private static bool TestThatNamesTheEvidence(string root, string testClass, string evidence, out string? problem)
    {
        string testsDirectory = Path.Combine(root, PluginSourceLocator.TestsProject);

        var files = Directory.EnumerateFiles(testsDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !PluginSourceLocator.IsBuildArtifact(path));

        string declaration = $@"\b(?:class|record)\s+{Regex.Escape(testClass)}\b";

        foreach (string file in files)
        {
            string code = SourceText.CodeWithoutComments(Path.GetRelativePath(root, file).Replace('\\', '/'));

            if (!Regex.IsMatch(code, declaration))
            {
                continue;
            }

            if (Regex.IsMatch(code, $@"\b{Regex.Escape(evidence)}\b"))
            {
                problem = null;
                return true;
            }

            problem = $"la prueba citada '{testClass}' existe pero no nombra la evidencia '{evidence}'";
            return false;
        }

        problem = $"la prueba citada '{testClass}' no existe en el suite";
        return false;
    }
}
