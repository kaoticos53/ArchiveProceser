using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using FileFlow.App.Services;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.App;

/// <summary>
/// Guardias del registro de incidentes (<see cref="CrashLogWriter"/>): un emisor que falle en bucle —el caso
/// medido fue un sondeo contra un servicio local apagado que dejaba la tarea sin observar— no puede volcar
/// cientos de entradas ni hacer crecer el fichero sin límite.
///
/// <para><b>Medición que motivó estas guardias (20 sep, sesión real):</b> 18.433 entradas y 49 MB de
/// <c>crash.log</c>, con ráfagas exactas de ~350 entradas por sesión, todas <c>HttpRequestException</c> contra
/// <c>localhost:1234</c> (servidor VLM apagado). La guardia principal reproduce esa ráfaga de 350 y exige que
/// el fichero quede por debajo de 4 KB.</para>
/// </summary>
public class CrashLogWriterTests : IDisposable
{
    private const string RefusedMessage =
        "No se puede establecer una conexión ya que el equipo de destino denegó expresamente dicha conexión. (localhost:1234)";

    private static readonly DateTime Origin = new(2026, 9, 20, 12, 0, 0);

    private readonly string _dir;
    private readonly string _logPath;
    private readonly string _fallbackPath;

    public CrashLogWriterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "FileFlow_CrashLogTests_" + Guid.NewGuid().ToString("N"));
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

    private CrashLogWriter CreateWriter(long maxBytes = CrashLogWriter.DefaultMaxBytes) =>
        new(_logPath, maxBytes, CrashLogWriter.DefaultDedupeWindow, _fallbackPath);

    /// <summary>Excepción con la forma exacta que entregaba el runtime: tarea no observada con conexión rechazada.</summary>
    private static AggregateException RefusedConnectionBurst() =>
        new(new HttpRequestException(RefusedMessage, new SocketException((int)SocketError.ConnectionRefused)));

    [Fact]
    public void Write_WithTheMeasuredBurstOfIdenticalFailures_ShouldKeepTheLogBounded()
    {
        // Arrange
        var writer = CreateWriter();
        int fullEntries = 0;

        // Act: 350 fallos idénticos seguidos, la ráfaga medida por sesión en el log real
        for (int i = 0; i < 350; i++)
        {
            if (writer.Write(RefusedConnectionBurst(), Origin.AddMilliseconds(i * 10)))
            {
                fullEntries++;
            }
        }

        // Assert
        var file = new FileInfo(_logPath);
        file.Exists.Should().BeTrue();
        file.Length.Should().BeLessThan(4 * 1024,
            "un arranque o una sesión no puede volver a escribir cientos de entradas");
        fullEntries.Should().BeLessThanOrEqualTo(4,
            "se registra la primera ocurrencia y un resumen acotado cada 100 repeticiones");
        File.ReadAllText(_logPath).Should().Contain("[CrashLog]");
    }

    [Fact]
    public void Write_WithDistinctExceptions_ShouldRecordEveryOne()
    {
        // Arrange
        var writer = CreateWriter();

        // Act
        int written = 0;
        for (int i = 0; i < 20; i++)
        {
            if (writer.Write(new InvalidOperationException($"incidente distinto {i}"), Origin.AddMinutes(i)))
            {
                written++;
            }
        }

        // Assert: la deduplicación no puede ocultar fallos diferentes
        written.Should().Be(20);
        string content = File.ReadAllText(_logPath);
        content.Should().Contain("incidente distinto 0");
        content.Should().Contain("incidente distinto 19");
    }

    [Fact]
    public void Write_WhenLogReachesTheLimit_ShouldRotateAndStayBounded()
    {
        // Arrange
        const long cap = 4 * 1024;
        var writer = CreateWriter(cap);

        // Act: muchos incidentes distintos con carga suficiente para superar el límite varias veces
        for (int i = 0; i < 200; i++)
        {
            writer.Write(new InvalidOperationException(new string('x', 200) + " #" + i), Origin.AddMinutes(i));
        }

        // Assert
        File.Exists(writer.RotatedFilePath).Should().BeTrue("el log rota en lugar de crecer sin límite");
        new FileInfo(_logPath).Length.Should().BeLessThan(cap + 4096);
        new FileInfo(writer.RotatedFilePath).Length.Should().BeLessThan(cap + 4096);
    }

    [Fact]
    public void Write_AfterDedupeWindowExpires_ShouldRecordAgain()
    {
        // Arrange
        var writer = CreateWriter();

        // Act & Assert
        writer.Write(new InvalidOperationException("misma firma"), Origin).Should().BeTrue();
        writer.Write(new InvalidOperationException("misma firma"), Origin.AddSeconds(30)).Should().BeFalse();
        writer.Write(new InvalidOperationException("misma firma"), Origin.AddMinutes(30)).Should().BeTrue(
            "pasada la ventana de deduplicación, la reaparición vuelve a registrarse");
    }

    [Fact]
    public void Write_WithNetworkNoise_ShouldStoreASingleInformativeLineWithoutStackTrace()
    {
        // Arrange
        var writer = CreateWriter();

        // Act
        writer.Write(RefusedConnectionBurst(), Origin).Should().BeTrue();

        // Assert: sin traza interna de HttpClient (no aporta diagnóstico y es la que inflaba el fichero)
        string content = File.ReadAllText(_logPath);
        content.Should().Contain("Fallo de red esperado (no es un crash)");
        content.Should().NotContain("at System.Net.Http");
    }

    [Fact]
    public void Write_WhenPrimaryFileIsUnusable_ShouldFallBackToTheTemporaryFile()
    {
        // Arrange: una ruta cuyo directorio padre es en realidad un fichero
        string blocked = Path.Combine(_dir, "blocked");
        File.WriteAllText(blocked, "no soy un directorio");
        var writer = new CrashLogWriter(
            Path.Combine(blocked, "logs", "crash.log"),
            CrashLogWriter.DefaultMaxBytes,
            CrashLogWriter.DefaultDedupeWindow,
            _fallbackPath);

        // Act
        writer.Write(new InvalidOperationException("incidente sin ruta utilizable"), Origin).Should().BeTrue();

        // Assert
        File.Exists(_fallbackPath).Should().BeTrue();
        File.ReadAllText(_fallbackPath).Should().Contain("incidente sin ruta utilizable");
    }

    [Fact]
    public void IsExpectedNetworkNoise_ShouldClassifyNetworkFailures_ButNotRealBugs()
    {
        CrashLogWriter.IsExpectedNetworkNoise(RefusedConnectionBurst()).Should().BeTrue();
        CrashLogWriter.IsExpectedNetworkNoise(new InvalidOperationException("defecto real")).Should().BeFalse();
    }

    [Fact]
    public async Task Write_ShouldBeThreadSafe_AndLoseNoDistinctEntries()
    {
        // Arrange
        var writer = CreateWriter();

        // Act: escrituras concurrentes como las de varias tareas de fondo fallando a la vez
        await Task.WhenAll(Enumerable.Range(0, 8).Select(thread => Task.Run(() =>
        {
            for (int i = 0; i < 25; i++)
            {
                writer.Write(new InvalidOperationException($"hilo {thread} caso {i}"), Origin.AddMilliseconds(i));
            }
        })));

        // Assert
        string content = File.ReadAllText(_logPath);
        content.Should().Contain("hilo 7 caso 24");
        content.Should().Contain("hilo 0 caso 0");
    }
}
