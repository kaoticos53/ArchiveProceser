using System.Diagnostics;
using FluentAssertions;
using Xunit.Abstractions;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Esqueleto compartido de los benchmarks con aserción de regresión del suite.
///
/// <para><b>Contrato</b>: los benchmarks no miden rendimiento absoluto — detectan regresiones comparando
/// la carga contra lo que <b>esta máquina acaba de demostrar</b>. Cada ejecución se calibra midiendo la
/// propia carga sin aserción (lo que además precalienta el JIT) y el umbral es
/// <see cref="TimeLimitFactor"/> veces esa calibración: el mismo factor detecta la regresión real en
/// cualquier hardware, insensible a lo lenta que sea la máquina de turno. Los umbrales de reloj de pared
/// fijos son la receta clásica del test inestable (pasan en una máquina y fallan en CI o en un portátil
/// a batería); aquí no existen.</para>
///
/// <para><b>Mediana de repeticiones</b>: en un suite paralelo otras colecciones compiten por CPU y una
/// medición única puede salir manchada; se mide <see cref="RepeatMeasurements"/> veces y se aserte sobre
/// la mediana y la PEOR repetición (la contaminada es una muestra, no el resultado).</para>
///
/// <para><b>Qué no pertenece aquí</b>: contadores de colecciones del GC ni deltas de memoria del proceso
/// — con el heap compartido entre colecciones paralelas no son atribuibles al test que los lee. Pueden
/// reportarse como diagnóstico, nunca asertarse.</para>
/// </summary>
public static class CalibratedBenchmark
{
    /// <summary>Veces que se ejecuta la carga para calibrar (también precalienta el JIT).</summary>
    public const int WarmupIterations = 3;

    /// <summary>Veces que se mide el bucle completo; la aserción usa la mediana y la peor.</summary>
    public const int RepeatMeasurements = 3;

    /// <summary>
    /// Margen de regresión del umbral: el límite es la calibración de esta máquina multiplicada por este
    /// factor. Con ×40, una regresión real (una consulta SQL dentro del bucle, la pérdida de una
    /// vectorización, un clon que pasa a redimensionar) sale disparada, mientras que la varianza normal
    /// de contención del suite —que afecta por igual a calibración y medición— pasa.
    /// </summary>
    public const double TimeLimitFactor = 40.0;

    /// <summary>
    /// Calibra, mide y aserte que la carga no ha regredido respecto a la capacidad demostrada de la
    /// máquina. Devuelve la mediana en segundos para que la prueba derive métricas de informe
    /// (throughput, ms/unidad).
    /// </summary>
    /// <param name="output">Salida diagnóstica de la prueba (el informe queda en el TRX).</param>
    /// <param name="label">Nombre de la carga para el informe y el mensaje de fallo.</param>
    /// <param name="work">La carga a medir. Debe ser determinista: datos sintéticos, sin red, y sin
    /// tocar estado global de proceso.</param>
    /// <param name="unitsPerMeasurement">Unidades de trabajo que ejecuta cada medición (p. ej. 50.000
    /// interpolaciones o 100 MB) — sólo para el informe de throughput.</param>
    /// <param name="unitName">Nombre de la unidad para el informe (p. ej. "ops", "MB", "imágenes").</param>
    /// <param name="betweenMeasurements">Drenaje entre mediciones (p. ej. un flush de canal), excluido
    /// del tiempo medido a propósito.</param>
    /// <param name="timeLimitFactor">Anula el factor por defecto para cargas con varianza estructural
    /// alta (p. ej. I/O de disco real); usar con medida y justificado en la prueba.</param>
    public static double MeasureAndAssert(
        ITestOutputHelper output,
        string label,
        Action work,
        double unitsPerMeasurement = 1,
        string unitName = "ops",
        Action? betweenMeasurements = null,
        double timeLimitFactor = TimeLimitFactor)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(work);

        // Calibración: la velocidad real de ESTA máquina con la carga completa. Precalienta el JIT.
        var calibration = Stopwatch.StartNew();
        for (int i = 0; i < WarmupIterations; i++)
        {
            work();
            betweenMeasurements?.Invoke();
        }
        calibration.Stop();
        double calibrationSeconds = calibration.Elapsed.TotalSeconds / WarmupIterations;

        // Medición: repeticiones con JIT caliente; la mediana absorbe las manchas de contención.
        var measured = new double[RepeatMeasurements];
        for (int r = 0; r < RepeatMeasurements; r++)
        {
            var sw = Stopwatch.StartNew();
            work();
            sw.Stop();
            measured[r] = sw.Elapsed.TotalSeconds;
            betweenMeasurements?.Invoke();
        }

        var sorted = measured.OrderBy(x => x).ToArray();
        double median = sorted[sorted.Length / 2];
        double worst = sorted[^1];
        double threshold = calibrationSeconds * timeLimitFactor;

        double throughput = unitsPerMeasurement / median;
        output.WriteLine($"=== {label.ToUpperInvariant()} ===");
        output.WriteLine($"Calibración ({WarmupIterations} pasadas): {calibrationSeconds:F3} s/pasada");
        output.WriteLine($"Mediciones ({RepeatMeasurements}): [{string.Join(", ", measured.Select(x => $"{x:F3}"))}] s — mediana {median:F3} s, peor {worst:F3} s");
        output.WriteLine(throughput >= 1
            ? $"Throughput mediano: {throughput:N0} {unitName}/s"
            : $"Tiempo mediano por unidad: {(median / Math.Max(unitsPerMeasurement, 1)) * 1000:F2} ms/{unitName}");
        output.WriteLine($"Umbral de regresión: {threshold:F3} s ({timeLimitFactor}x la calibración)");

        worst.Should().BeLessThanOrEqualTo(
            threshold,
            $"{label}: una regresión real ralentiza la carga más de {timeLimitFactor}x respecto a lo que " +
            $"esta misma máquina acaba de demostrar (calibración {calibrationSeconds:F3} s, mediana " +
            $"{median:F3} s, peor repetición {worst:F3} s, umbral {threshold:F3} s)");

        return median;
    }
}
