using System;
using System.Threading;

namespace FileFlow.App.Services;

/// <summary>
/// Convierte un fallo de arranque en algo <b>visible</b>: lo deja en el registro de incidentes acotado y
/// muestra la ventana de error. Antes, el arranque registraba la excepción y volvía a lanzarla, con lo que el
/// proceso moría sin abrir ninguna ventana: el síntoma era «la aplicación no se abre».
///
/// <para><b>Un solo diálogo</b>: una cascada de fallos (o un fallo por cada reintento) no debe abrir una
/// ventana por excepción. El primero es el que se muestra; los siguientes se registran y se cuentan. La
/// deduplicación del texto la hace <see cref="CrashLogWriter"/>.</para>
///
/// <para><b>Sink inyectable</b>: en producción muestra la ventana real; en las pruebas se sustituye por un
/// receptor en memoria, de modo que el contrato («el fallo se reporta con su etapa») se verifica sin depender
/// del renderizado.</para>
/// </summary>
public sealed class StartupFailureReporter
{
    private readonly CrashLogWriter _writer;
    private readonly Action<StartupFailureReport>? _sink;
    private readonly Lock _gate = new();

    private int _dialogsShown;

    /// <param name="writer">Registro de incidentes; por defecto el acotado de producción.</param>
    /// <param name="sink">
    /// Receptor del informe. Si es <c>null</c>, se muestra la ventana de error real
    /// (<see cref="Views.StartupErrorWindow.ShowFailure"/>).
    /// </param>
    public StartupFailureReporter(CrashLogWriter? writer = null, Action<StartupFailureReport>? sink = null)
    {
        _writer = writer ?? new CrashLogWriter();
        _sink = sink;
    }

    /// <summary>Último informe emitido, si hubo alguno.</summary>
    public StartupFailureReport? LastReport { get; private set; }

    /// <summary>Cuántos fallos se han reportado (incluidos los que no abrieron ventana).</summary>
    public int ReportCount { get; private set; }

    /// <summary>
    /// Reporta un fallo: lo registra y, la <b>primera</b> vez, lo hace visible.
    /// No puede lanzar: se está ejecutando precisamente porque algo ya falló.
    /// </summary>
    public StartupFailureReport Report(StartupPhase phase, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var report = new StartupFailureReport(
            phase,
            StartupPhaseDescriptions.Describe(phase),
            exception,
            _writer.FilePath,
            DateTime.UtcNow);

        // Primero el registro: aunque la interfaz no pueda mostrarse, el fallo queda en disco con su traza.
        try
        {
            _writer.Write(exception);
        }
        catch
        {
            // Un registro inaccesible no puede impedir que el usuario vea el motivo.
        }

        bool shouldSurface;

        lock (_gate)
        {
            LastReport = report;
            ReportCount++;
            shouldSurface = Interlocked.CompareExchange(ref _dialogsShown, 1, 0) == 0;
        }

        if (!shouldSurface)
        {
            return report;
        }

        try
        {
            if (_sink is not null)
            {
                _sink(report);
            }
            else
            {
                Views.StartupErrorWindow.ShowFailure(report);
            }
        }
        catch
        {
            // Mostrar el error es el último recurso: si también falla, el log ya tiene el informe.
        }

        return report;
    }
}
