using System;
using System.Runtime.InteropServices;
using System.Text;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.Services;

/// <summary>
/// Fotografía de un fallo ocurrido durante el arranque: qué etapa, qué excepción, dónde quedó registrada y en
/// qué entorno. Es lo que hace <b>visible</b> un fallo que antes sólo provocaba que el proceso desapareciera
/// sin abrir ninguna ventana.
/// </summary>
public sealed record StartupFailureReport(
    StartupPhase Phase,
    string PhaseLabel,
    Exception Exception,
    string? LogFilePath,
    DateTime TimestampUtc,
    string? EnvironmentOverride = null)
{
    /// <summary>
    /// Descripción del entorno incluida en el informe. En producción se detecta del sistema; las capturas
    /// visuales de la ventana de error la fijan, porque una línea base sólo sirve si es idéntica en cualquier
    /// equipo.
    /// </summary>
    public string EnvironmentDescription => EnvironmentOverride ?? BuildEnvironmentDescription();
    /// <summary>Título de la ventana de error (localizado con respaldo).</summary>
    public string Headline => Localize("Startup_FailureTitle", "FileFlow Studio no pudo iniciarse");

    /// <summary>Línea con la etapa fallida, ya formateada.</summary>
    public string PhaseLine => string.Format(
        Localize("Startup_FailurePhase", "Etapa fallida: {0}"),
        PhaseLabel);

    /// <summary>Resumen de una línea de la excepción (tipo + mensaje) para mostrar en la ventana.</summary>
    public string ExceptionSummary => $"{Exception.GetType().Name}: {Exception.Message}";

    /// <summary>
    /// Detalle completo listo para copiar a un informe de error: etapa, momento, excepción con traza,
    /// ruta del registro de incidentes y entorno (SO, runtime y versión de la aplicación).
    /// </summary>
    public string BuildDetails()
    {
        var builder = new StringBuilder();

        builder.AppendLine(Localize("Startup_DetailsTitle", "FileFlow Studio — fallo de arranque"));
        builder.Append(Localize("Startup_DetailsPhase", "Etapa")).Append(": ")
               .Append(PhaseLabel).Append(" (").Append(Phase).Append(')').AppendLine();
        builder.Append(Localize("Startup_DetailsMoment", "Momento (UTC)")).Append(": ")
               .Append(TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine();
        builder.Append(Localize("Startup_DetailsLog", "Registro de incidentes")).Append(": ")
               .Append(LogFilePath ?? Localize("Startup_DetailsLogUnavailable", "(no disponible)")).AppendLine();
        builder.Append(Localize("Startup_DetailsEnvironment", "Entorno")).Append(": ")
               .Append(EnvironmentDescription).AppendLine();
        builder.AppendLine();
        builder.Append(Exception);

        return builder.ToString();
    }

    /// <summary>Versión del ensamblado de la aplicación, para poder reproducir el entorno del informe.</summary>
    public static string ApplicationVersion =>
        typeof(StartupFailureReport).Assembly.GetName().Version?.ToString() ?? "desconocida";

    private static string BuildEnvironmentDescription() =>
        $"{RuntimeInformation.OSDescription} · {RuntimeInformation.FrameworkDescription} · FileFlow.App {ApplicationVersion}";

    private static string Localize(string key, string fallback)
    {
        try
        {
            string localized = LocalizationManager.Instance.GetString(key, fallback);
            return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
        }
        catch
        {
            return fallback;
        }
    }
}
