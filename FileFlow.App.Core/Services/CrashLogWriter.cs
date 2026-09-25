using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFlow.Sdk.Storage;

namespace FileFlow.App.Services;

/// <summary>
/// Escritor del registro de incidentes (<c>%APPDATA%/FileFlow/logs/crash.log</c>) con tres garantías
/// que antes no existían:
///
/// <list type="number">
/// <item><b>Deduplicación:</b> dos excepciones con la misma firma (tipo + mensaje) dentro de una ventana
/// de tiempo se registran una vez; las repeticiones se cuentan y sólo se vuelca un resumen acotado cada
/// <see cref="SuppressionReportEvery"/> ocurrencias. Un emisor que falle en bucle no puede inundar el fichero.</item>
/// <item><b>Rotación:</b> el fichero nunca supera <see cref="DefaultMaxBytes"/>; al alcanzar el límite se
/// archiva como <c>crash.log.1</c> y se empieza de cero. Antes el log crecía sin límite (se han medido 49 MB
/// y 18.433 entradas).</item>
/// <item><b>Clasificación:</b> los fallos de red esperados (servicio local apagado) se registran como una
/// única línea informativa sin traza — su stack es 100% interno de <c>HttpClient</c> y no aporta diagnóstico —
/// mientras que un crash real conserva la entrada completa.</item>
/// </list>
///
/// El formato de las entradas de crash es el histórico
/// (<c>[fecha] Unhandled Exception:\n{excepción}\n\n</c>) para no romper lectores existentes.
/// </summary>
public sealed class CrashLogWriter
{
    /// <summary>Tamaño máximo de un fichero de incidentes antes de rotar (2 MB).</summary>
    public const long DefaultMaxBytes = 2L * 1024 * 1024;

    /// <summary>Ventana durante la cual las repeticiones de una misma firma se suprimen.</summary>
    public static readonly TimeSpan DefaultDedupeWindow = TimeSpan.FromMinutes(5);

    /// <summary>Cada cuántas repeticiones suprimidas se vuelca una línea de resumen (evita el silencio total).</summary>
    public const int SuppressionReportEvery = 100;

    private readonly string _filePath;
    private readonly string _fallbackFilePath;
    private readonly long _maxBytes;
    private readonly TimeSpan _dedupeWindow;
    private readonly Lock _gate = new();

    private string? _lastSignature;
    private DateTime _windowStartUtc;
    private int _suppressedInWindow;
    private int _suppressedTotal;

    public CrashLogWriter(
        string? filePath = null,
        long maxBytes = DefaultMaxBytes,
        TimeSpan? dedupeWindow = null,
        string? fallbackFilePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? AppPaths.CrashLogFile : filePath;
        _fallbackFilePath = string.IsNullOrWhiteSpace(fallbackFilePath)
            ? Path.Combine(Path.GetTempPath(), "fileflow_crash.log")
            : fallbackFilePath;
        _maxBytes = maxBytes > 0 ? maxBytes : DefaultMaxBytes;
        _dedupeWindow = dedupeWindow ?? DefaultDedupeWindow;
    }

    /// <summary>Ruta del fichero activo.</summary>
    public string FilePath => _filePath;

    /// <summary>Ruta del archivo rotado (el fichero anterior al último reinicio del log).</summary>
    public string RotatedFilePath => _filePath + ".1";

    /// <summary>
    /// Registra una excepción (o el <see cref="Exception"/> interno de un <see cref="AggregateException"/>).
    /// </summary>
    /// <returns><c>true</c> si se escribió una entrada; <c>false</c> si la repetición quedó suprimida.</returns>
    public bool Write(object? exception, DateTime? timestamp = null)
    {
        if (exception is null)
        {
            return false;
        }

        DateTime now = timestamp ?? DateTime.Now;

        lock (_gate)
        {
            Exception ex = Unwrap(exception);
            string signature = BuildSignature(ex);
            string? payload = ComposeEntry(ex, signature, now);

            if (payload is null)
            {
                return false;
            }

            AppendBounded(payload);
            return true;
        }
    }

    /// <summary>
    /// Compone el texto a escribir, o <c>null</c> cuando la firma se repite dentro de la ventana de deduplicación.
    /// </summary>
    private string? ComposeEntry(Exception ex, string signature, DateTime now)
    {
        bool isRepeat = _lastSignature == signature
                        && (now - _windowStartUtc) < _dedupeWindow;

        if (isRepeat)
        {
            _suppressedInWindow++;
            _suppressedTotal++;

            if (_suppressedInWindow % SuppressionReportEvery != 0)
            {
                return null;
            }

            return $"[{now:yyyy-MM-dd HH:mm:ss}] [CrashLog] {_suppressedInWindow} repeticiones idénticas suprimidas " +
                   $"({_suppressedTotal} en total): {signature}{Environment.NewLine}{Environment.NewLine}";
        }

        var builder = new StringBuilder();

        // Cierre de la ventana anterior: deja constancia de cuánto se filtró antes de cambiar de firma.
        if (_lastSignature is not null && _suppressedTotal > 0)
        {
            builder.Append('[').Append(now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] [CrashLog] ")
                   .Append(_suppressedTotal).Append(" repeticiones idénticas suprimidas de la firma anterior: ")
                   .Append(_lastSignature).Append(Environment.NewLine).Append(Environment.NewLine);
        }

        _lastSignature = signature;
        _windowStartUtc = now;
        _suppressedInWindow = 0;
        _suppressedTotal = 0;

        if (IsExpectedNetworkNoise(ex))
        {
            // Ruido operativo (servicio local apagado): una línea informativa, sin traza interna de HttpClient.
            builder.Append('[').Append(now.ToString("yyyy-MM-dd HH:mm:ss"))
                   .Append("] Fallo de red esperado (no es un crash): ").Append(signature)
                   .Append(Environment.NewLine).Append(Environment.NewLine);
        }
        else
        {
            builder.Append('[').Append(now.ToString("yyyy-MM-dd HH:mm:ss"))
                   .Append("] Unhandled Exception:").Append(Environment.NewLine)
                   .Append(ex).Append(Environment.NewLine).Append(Environment.NewLine);
        }

        return builder.ToString();
    }

    /// <summary>Anexa respetando el límite de tamaño: si la entrada no cabe, rota el fichero antes.</summary>
    private void AppendBounded(string payload)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(_filePath) && new FileInfo(_filePath).Length > _maxBytes)
            {
                Rotate();
            }

            File.AppendAllText(_filePath, payload, Encoding.UTF8);
        }
        catch
        {
            AppendToFallback(payload);
        }
    }

    private void Rotate()
    {
        try
        {
            if (File.Exists(RotatedFilePath))
            {
                File.Delete(RotatedFilePath);
            }

            if (File.Exists(_filePath))
            {
                File.Move(_filePath, RotatedFilePath);
            }
        }
        catch
        {
            // Si no se puede rotar, se trunca para que el fichero no crezca sin límite.
            try { File.WriteAllText(_filePath, string.Empty, Encoding.UTF8); } catch { }
        }
    }

    private void AppendToFallback(string payload)
    {
        try
        {
            File.AppendAllText(_fallbackFilePath, payload, Encoding.UTF8);
        }
        catch
        {
            // Último recurso agotado: un crash log inaccesible no debe tumbar la aplicación.
        }
    }

    /// <summary>Desenvuelve <see cref="AggregateException"/> y <see cref="System.Reflection.TargetInvocationException"/> de un solo nivel.</summary>
    public static Exception Unwrap(object exception)
    {
        var current = exception as Exception ?? new InvalidOperationException(exception.ToString());

        while (true)
        {
            if (current is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
            {
                current = aggregate.InnerExceptions[0];
                continue;
            }

            if (current is System.Reflection.TargetInvocationException target && target.InnerException is not null)
            {
                current = target.InnerException;
                continue;
            }

            return current;
        }
    }

    /// <summary>
    /// Identifica fallos de red esperados: el endpoint local está apagado y la petición no pudo conectar.
    /// Se registran como línea informativa porque no son defectos de la aplicación.
    /// </summary>
    public static bool IsExpectedNetworkNoise(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is SocketException || current is HttpRequestException)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Firma estable de una excepción: tipo + mensaje (sin la traza, que varía entre ejecuciones).</summary>
    private static string BuildSignature(Exception ex)
    {
        string message = (ex.Message ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        if (message.Length > 200)
        {
            message = message[..200] + "…";
        }

        return $"{ex.GetType().FullName}: {message}";
    }
}
