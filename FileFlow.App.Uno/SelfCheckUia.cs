using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.UI.Xaml;

namespace FileFlow.App.Uno;

/// <summary>
/// El sondeo UIA desde fuera del proceso (<c>FileFlow.App.Uno.exe --selfcheck-uia</c>): la app
/// arranca completa (DI, plugins, ejemplo cargado) y un HIJO EXTERNO (python + pywinauto, la vía
/// sin UIAccess ya probada por las sondas QA de los hitos 237 y 238) la observa por UI Automation
/// — anclas por AutomationId, foco del lienzo, estado del zoom, spotlight, buscador. La app escribe
/// una señal de "lista" (la ventana ya materializada; la app confirma su árbol por las anclas del
/// pasa el pid del proceso al hijo y espera su veredicto: el código de salida del hijo es el de la
/// app. El reporte queda en <c>selfcheck-uia-report.txt</c> junto al ejecutable.
///
/// <para><b>Por qué un hijo externo y no el propio proceso</b>: UIA responde a través de los
/// mensajes de la ventana (WM_GETOBJECT); un proceso bloqueado en una espera no despacha mensajes
/// y la observación externa moriría con timeout. La espera corre en hilo de fondo y el hilo de UI
/// sigue bombeando. Por eso también la app NO corre <c>--selfcheck</c> en este modo: el add/remove
/// masivo del sondeo interno deja la materialización de WinUI frágil (la lección one-shot del 3.6)
/// y contaminaría la observación externa.</para>
///
/// <para><b>El instrumento</b>: python es obligatorio (el modo falla si falta) — la parte UIA del
/// tramo QA corre por pywinauto 0.6.9 en el site de usuario; <c>qa_uia_anchors.py</c> (la Sonda C
/// del 238) ya demostró esta vía contra la app viva. El instrumento puede apuntarse a otro
/// fichero con la variable de entorno <c>FILEFLOW_UIA_PROBE</c>.</para>
/// </summary>
public static class SelfCheckUia
{
    /// <summary>Timeout del hijo: el sondeo completo de las sondas QA corre en menos de un minuto.</summary>
    private static readonly TimeSpan ChildTimeout = TimeSpan.FromSeconds(180);

    /// <summary>La señal de escena lista del fixture del inspector (hito 245), junto al ejecutable.</summary>
    private static string FixtureSignalPath => Path.Combine(AppContext.BaseDirectory, "selfcheck-uia-fixture-ready.txt");

    private static string ReadySignalPath => Path.Combine(AppContext.BaseDirectory, "selfcheck-uia-ready.txt");

    private static string ReportPath => Path.Combine(AppContext.BaseDirectory, "selfcheck-uia-report.txt");

    /// <summary>
    /// Corre el sondeo UIA: escena del inspector montada y ASENTADA sin cliente, señal de listo,
    /// hijo externo, veredicto por su código de salida. Devuelve -1 (el proceso termina dentro
    /// del sondeo, por <see cref="Environment.Exit"/>).
    /// </summary>
    public static int Run(Window? mainWindow)
    {
        // Hilo de fondo: el hilo de UI tiene que seguir bombeando mensajes para que el proveedor
        // UIA del proceso (WM_GETOBJECT) responda a la observación externa — un proceso bloqueado
        // en una espera no responde y el hijo moriría con timeout.
        new Thread(() =>
        {
            try
            {
                File.WriteAllText(ReadySignalPath, DateTime.Now.ToString("HH:mm:ss.fff"));
            }
            catch
            {
                // La señal es una conveniencia de diagnóstico; su falta no decide el veredicto.
            }

            // La escena ANTES del hijo (hito 245): la materialización del contenido con Expander
            // dispara una tormenta de eventos UIA que, CON un cliente conectado, tumba el proceso
            // (medido: exit 127 sin WER ni excepción). Montar y asentar sin cliente, lanzar al
            // observador a escena quieta.
            RuntimeSelfCheck.MountUiaExternalScene(mainWindow);

            int exitCode = RunChildProbe(out string output);

            try
            {
                File.WriteAllText(ReportPath, output);
            }
            catch
            {
            }

            Console.Out.Flush();
            Console.WriteLine(output);
            Console.Out.Flush();
            Environment.Exit(exitCode);
        })
        {
            IsBackground = true,
            Name = "SelfCheckUia"
        }.Start();

        return -1; // el proceso termina por Environment.Exit dentro del sondeo
    }

    /// <summary>
    /// Lanza el instrumento externo contra este proceso y devuelve (código, salida). Sin python en
    /// PATH el modo FALLA (no hay veredicto sin observación externa): es la honestidad del modo.
    /// </summary>
    private static int RunChildProbe(out string output)
    {
        string probe = ResolveProbePath();
        if (!File.Exists(probe))
        {
            output = "[selfcheck-uia] FALLO: el instrumento no está en '" + probe
                + "' (variable FILEFLOW_UIA_PROBE para apuntar a otro fichero).";
            return 3;
        }

        var start = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = "\"" + probe + "\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };
        start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        start.EnvironmentVariables["FILEFLOW_UIA_TARGET_PID"] = Process.GetCurrentProcess().Id.ToString();

        // La ruta de la señal del fixture (hito 245): el observador espera el fichero antes del
        // primer switch — conmutar el Pivot dentro de su propia reconstrucción tumba el proceso.
        start.EnvironmentVariables["FILEFLOW_UIA_FIXTURE_SIGNAL"] = FixtureSignalPath;

        using var child = new Process { StartInfo = start };
        try
        {
            child.Start();
        }
        catch (Exception ex)
        {
            output = "[selfcheck-uia] FALLO: python no está disponible ('" + ex.Message
                + "'): el modo exige el observador externo (pywinauto).";
            return 3;
        }

        var buffer = new StringBuilder();
        var pump = new Thread(() =>
        {
            try
            {
                while (!child.StandardOutput.EndOfStream)
                {
                    string line = child.StandardOutput.ReadLine() ?? string.Empty;
                    lock (buffer)
                    {
                        buffer.AppendLine(line);
                    }
                }
            }
            catch
            {
            }
        })
        {
            IsBackground = true,
            Name = "SelfCheckUiaPump"
        };
        pump.Start();

        string errorTail = string.Empty;
        var errorPump = new Thread(() =>
        {
            try
            {
                errorTail = child.StandardError.ReadToEnd();
            }
            catch
            {
            }
        })
        {
            IsBackground = true
        };
        errorPump.Start();

        if (!child.WaitForExit((int)ChildTimeout.TotalMilliseconds))
        {
            try
            {
                child.Kill();
            }
            catch
            {
            }

            lock (buffer)
            {
                buffer.AppendLine("[selfcheck-uia] FALLO: el observador externo agotó "
                    + ChildTimeout.TotalSeconds + " s y fue terminado.");
                if (errorTail.Length > 0)
                {
                    buffer.AppendLine("--- stderr ---").Append(errorTail);
                }

                output = buffer.ToString();
            }

            return 4;
        }

        lock (buffer)
        {
            if (errorTail.Length > 0)
            {
                buffer.AppendLine("--- stderr ---").Append(errorTail);
            }

            output = buffer.ToString();
        }

        return child.ExitCode;
    }

    /// <summary>
    /// El instrumento: la variable <c>FILEFLOW_UIA_PROBE</c> gana; por defecto, el de la casa
    /// (<c>docs/qa/selfcheck_uia_probe.py</c>), subiendo desde el directorio del ejecutable hasta
    /// la raíz del repositorio.
    /// </summary>
    private static string ResolveProbePath()
    {
        string? configured = Environment.GetEnvironmentVariable("FILEFLOW_UIA_PROBE");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int depth = 0; dir is not null && depth < 8; depth++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "docs", "qa", "selfcheck_uia_probe.py");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine("docs", "qa", "selfcheck_uia_probe.py");
    }
}
