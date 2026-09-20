using System;
using System.Collections.Generic;
using System.IO;
using FileFlow.App.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardias del <b>arranque visible</b>.
///
/// Antes, cualquier fallo del arranque acababa en un <c>catch</c> genérico que registraba la excepción y
/// volvía a lanzarla: el proceso moría sin abrir ninguna ventana y el síntoma era «la aplicación no se abre».
/// Estas pruebas fijan el contrato contrario:
/// <list type="bullet">
///   <item>El fallo se atribuye a una <b>etapa</b> concreta (servicios, preferencias, tema, plugins, XAML).</item>
///   <item>El arranque se detiene de forma controlada y no ejecuta las etapas siguientes.</item>
///   <item>El fallo queda en el registro de incidentes y, además, se hace visible (una sola ventana, no una
///   cascada por cada excepción).</item>
///   <item>Reportar nunca puede lanzar: se ejecuta en el último recurso del arranque.</item>
/// </list>
/// </summary>
public class StartupOrchestratorTests : IDisposable
{
    private readonly string _dir;
    private readonly string _logPath;
    private readonly string _fallbackPath;

    public StartupOrchestratorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "FileFlow_StartupFailureTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _logPath = Path.Combine(_dir, "logs", "crash.log");
        _fallbackPath = Path.Combine(_dir, "fallback.log");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }
        catch
        {
            // Limpieza best-effort del directorio temporal.
        }
    }

    private CrashLogWriter CreateWriter() =>
        new(_logPath, CrashLogWriter.DefaultMaxBytes, CrashLogWriter.DefaultDedupeWindow, _fallbackPath);

    /// <summary>Reporter que captura los informes en memoria en lugar de abrir la ventana real.</summary>
    private (StartupFailureReporter Reporter, List<StartupFailureReport> Reports) CreateReporter()
    {
        var reports = new List<StartupFailureReport>();
        var reporter = new StartupFailureReporter(CreateWriter(), reports.Add);
        return (reporter, reports);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Aislamiento y atribución de etapas
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryExecute_WhenAPhaseFails_ShouldReportTheFailingPhaseAndStopTheStartup()
    {
        // Arrange
        var (reporter, reports) = CreateReporter();
        var startup = new StartupOrchestrator(reporter);
        bool shellRan = false;

        // Act: falla la etapa de servicios, como el ciclo de dependencias que dejaba la app sin abrir
        bool services = startup.TryExecute(StartupPhase.Services, () =>
            throw new InvalidOperationException("A circular dependency was detected for the service of type 'EditorViewModel'."));

        bool shell = startup.TryExecute(StartupPhase.Shell, () => shellRan = true);

        // Assert
        services.Should().BeFalse();
        shell.Should().BeFalse("tras un fallo no tiene sentido seguir arrancando");
        shellRan.Should().BeFalse("la etapa siguiente no debe ejecutarse");
        startup.IsAborted.Should().BeTrue();
        startup.FailedPhase.Should().Be(StartupPhase.Services);

        reports.Should().ContainSingle("el fallo debe hacerse visible");
        reports[0].Phase.Should().Be(StartupPhase.Services);
        reports[0].PhaseLabel.Should().NotBeNullOrWhiteSpace();
        reports[0].Exception.Should().BeOfType<InvalidOperationException>();
        reports[0].Exception.Message.Should().Contain("circular dependency");
        reports[0].LogFilePath.Should().Be(_logPath);
    }

    [Fact]
    public void TryExecute_WhenEveryPhaseSucceeds_ShouldNotReportAnything()
    {
        // Arrange
        var (reporter, reports) = CreateReporter();
        var startup = new StartupOrchestrator(reporter);
        var executed = new List<StartupPhase>();

        // Act
        bool ok = startup.TryExecute(StartupPhase.Resources, () => executed.Add(StartupPhase.Resources))
                   && startup.TryExecute(StartupPhase.Services, () => executed.Add(StartupPhase.Services))
                   && startup.TryExecute(StartupPhase.Shell, () => executed.Add(StartupPhase.Shell));

        // Assert
        ok.Should().BeTrue();
        executed.Should().Equal(StartupPhase.Resources, StartupPhase.Services, StartupPhase.Shell);
        startup.IsAborted.Should().BeFalse();
        reports.Should().BeEmpty();
    }

    [Fact]
    public void TryExecute_WithResultFactory_ShouldReturnTheBuiltValueOrFailWithoutInvokingTheRest()
    {
        // Arrange
        var (reporter, reports) = CreateReporter();
        var startup = new StartupOrchestrator(reporter);

        // Act: la fábrica produce la ventana principal
        bool ok = startup.TryExecute(StartupPhase.Shell, () => 42, out int window);

        // Assert
        ok.Should().BeTrue();
        window.Should().Be(42);

        // Act 2: la misma etapa, pero el XAML falla
        var failing = new StartupOrchestrator(reporter);
        bool failed = failing.TryExecute(StartupPhase.Shell, () => throw new InvalidOperationException("XAML inválido"), out int missing);

        // Assert 2
        failed.Should().BeFalse();
        missing.Should().Be(0, "un resultado por defecto es la señal de que la etapa falló");
        failing.FailedPhase.Should().Be(StartupPhase.Shell);
        reports.Should().HaveCount(1);
        reports[0].Exception.Message.Should().Be("XAML inválido");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Visibilidad: una sola ventana, y el registro siempre
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Report_ShouldRecordTheFailureInTheIncidentLog()
    {
        // Arrange
        var (reporter, _) = CreateReporter();

        // Act
        reporter.Report(StartupPhase.Plugins, new InvalidOperationException("descubrimiento de plugins roto"));

        // Assert: es un crash real, no ruido de red, así que se guarda la entrada completa con su traza
        string log = File.ReadAllText(_logPath);
        log.Should().Contain("Unhandled Exception");
        log.Should().Contain("descubrimiento de plugins roto");
    }

    [Fact]
    public void Report_WhenSeveralFailuresHappen_ShouldSurfaceOnlyTheFirstOne()
    {
        // Arrange
        var (reporter, reports) = CreateReporter();

        // Act: una cascada de fallos (p. ej. el mismo error reintentado) no puede abrir una ventana por excepción
        reporter.Report(StartupPhase.Theme, new InvalidOperationException("tema roto"));
        reporter.Report(StartupPhase.Shell, new InvalidOperationException("ventana rota"));
        reporter.Report(StartupPhase.Shell, new InvalidOperationException("ventana rota"));

        // Assert
        reporter.ReportCount.Should().Be(3, "todos los fallos se reportan");
        reports.Should().ContainSingle("pero sólo el primero se muestra al usuario");
        reports[0].Phase.Should().Be(StartupPhase.Theme);
        reporter.LastReport!.Phase.Should().Be(StartupPhase.Shell, "el último informe sigue disponible para diagnóstico");
    }

    [Fact]
    public void Report_WhenTheSinkItselfThrows_ShouldNotPropagateTheFailure()
    {
        // Arrange: el último recurso del arranque no puede fallar por fallar
        var reporter = new StartupFailureReporter(
            CreateWriter(),
            _ => throw new InvalidOperationException("no hay interfaz disponible"));

        // Act
        Action report = () => reporter.Report(StartupPhase.Shell, new InvalidOperationException("fallo original"));

        // Assert
        report.Should().NotThrow();
        reporter.ReportCount.Should().Be(1);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Contenido del informe
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildDetails_ShouldCarryEverythingNeededToReportTheProblem()
    {
        // Arrange
        var exception = new InvalidOperationException("No animator registered for the property RenderTransform.");
        var report = new StartupFailureReport(
            StartupPhase.Shell,
            StartupPhaseDescriptions.Describe(StartupPhase.Shell),
            exception,
            _logPath,
            new DateTime(2026, 9, 20, 19, 0, 0, DateTimeKind.Utc));

        // Act
        string details = report.BuildDetails();

        // Assert
        details.Should().Contain("Shell", "la etapa debe quedar identificada por nombre y no solo por su etiqueta");
        details.Should().Contain("2026-09-20 19:00:00");
        details.Should().Contain(_logPath);
        details.Should().Contain("InvalidOperationException");
        details.Should().Contain("No animator registered");
        details.Should().Contain(StartupFailureReport.ApplicationVersion);
        report.PhaseLine.Should().Contain(report.PhaseLabel);
        report.ExceptionSummary.Should().Be($"InvalidOperationException: {exception.Message}");
    }

    [Theory]
    [InlineData(StartupPhase.Resources)]
    [InlineData(StartupPhase.Services)]
    [InlineData(StartupPhase.Preferences)]
    [InlineData(StartupPhase.Theme)]
    [InlineData(StartupPhase.Plugins)]
    [InlineData(StartupPhase.Shell)]
    [InlineData(StartupPhase.Runtime)]
    public void EveryStartupPhase_ShouldHaveALegibleName_EvenWithoutLocalization(StartupPhase phase)
    {
        // La ventana de error se muestra precisamente cuando la localización o el XAML pueden ser lo que falló:
        // ninguna etapa puede quedarse sin nombre legible.
        StartupPhaseDescriptions.Describe(phase).Should().NotBeNullOrWhiteSpace();
        StartupPhaseDescriptions.Fallback(phase).Should().NotBeNullOrWhiteSpace();
        StartupPhaseDescriptions.Key(phase).Should().Contain(phase.ToString());
    }
}
