using System;

namespace FileFlow.App.Services;

/// <summary>
/// Ejecuta el arranque <b>por etapas aisladas</b>. Cada etapa que falla se reporta con su nombre y el
/// arranque se detiene de forma controlada (ventana de error + salida con código de error) en lugar de
/// propagar la excepción y morir sin interfaz.
///
/// <para>El aislamiento por etapas es lo que da diagnóstico: antes, cualquier fallo —servicios, tema, XAML—
/// acababa en el mismo <c>catch</c> genérico con un <c>throw</c>, y el usuario sólo veía desaparecer el
/// proceso. Ahora el informe dice <i>en qué etapa</i> se rompió.</para>
/// </summary>
public sealed class StartupOrchestrator
{
    private readonly StartupFailureReporter _reporter;

    public StartupOrchestrator(StartupFailureReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(reporter);
        _reporter = reporter;
    }

    /// <summary>¿Se ha detenido el arranque por un fallo?</summary>
    public bool IsAborted { get; private set; }

    /// <summary>Etapa en la que falló el arranque, si falló.</summary>
    public StartupPhase? FailedPhase { get; private set; }

    /// <summary>
    /// Ejecuta una etapa. Devuelve <c>false</c> si la etapa falló (el fallo queda reportado y el arranque
    /// marcado como abortado) y <c>true</c> si completó.
    /// </summary>
    public bool TryExecute(StartupPhase phase, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsAborted)
        {
            return false;
        }

        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            IsAborted = true;
            FailedPhase = phase;
            _reporter.Report(phase, ex);
            return false;
        }
    }

    /// <summary>
    /// Variante con resultado para etapas que producen algo (por ejemplo, la ventana principal). Devuelve
    /// <c>false</c> y no asigna <paramref name="result"/> si la etapa falló.
    /// </summary>
    public bool TryExecute<T>(StartupPhase phase, Func<T> factory, out T? result)
    {
        ArgumentNullException.ThrowIfNull(factory);

        result = default;

        if (IsAborted)
        {
            return false;
        }

        try
        {
            result = factory();
            return true;
        }
        catch (Exception ex)
        {
            IsAborted = true;
            FailedPhase = phase;
            _reporter.Report(phase, ex);
            return false;
        }
    }
}
