using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using FileFlow.App.Models;
using FileFlow.App.Uno.Controls;
using FileFlow.Sdk;
using Microsoft.UI.Dispatching;
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
            MountUiaExternalScene(mainWindow);

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

    /// <summary>
    /// El fixture de la observación externa (hito 245): con <c>--selfcheck-uia</c> la app deja el
    /// inspector ABIERTO sobre el primer nodo con snapshots REALES — 1 entrada y 3 salidas (una por
    /// puerto del nodo), todos por la vía de producción (CreateInput/CreateOutput con un
    /// FileItemContext, la misma fábrica que usa el motor). El instrumento externo no puede montar
    /// el fixture — su ventana al árbol es la observación, no la manipulación — así que la escena la
    /// prepara la app antes de lanzar al hijo. Los snapshots quedan VIVOS (no se retiran): son los
    /// datos que el observador va a contar, y el proceso vive solo para ser observado.
    /// </summary>
    /// <param name="window">La ventana principal ya materializada (tras el Activate).</param>
    /// <param name="dispatcher">La cola del hilo de UI: el fixture corre dentro de ella.</param>
    /// <summary>
    /// Monta la escena de la observación externa (hito 245) BLOQUEANDO al hilo llamador (el de
    /// fondo de <see cref="SelfCheckUia"/>, nunca el de UI): con REINTENTOS (la lección de
    /// materialización del 3.6) el inspector queda abierto sobre el primer nodo con sus snapshots
    /// reales — 1 entrada y 3 salidas, vía de producción (CreateInput/CreateOutput) — y la
    /// combinada pre-seleccionada por la vía programática.
    ///
    /// <para><b>El orden que la medición impuso</b>: la materialización del contenido con Expander
    /// dispara una tormenta de eventos UIA que TUMBA el proceso si un cliente observador está
    /// conectado (medido: switch + cliente = exit 127 sin WER ni excepción; switch sin cliente =
    /// el selfcheck interno sobrevive; cliente sin switch = sobrevive). Por eso la escena se
    /// monta y ASENTE (4 s) SIN cliente, y solo entonces el modo lanza al hijo.</para>
    ///
    /// <para>Devuelve true si la escena quedó montada. La señal SIEMPRE se escribe (ready/FAILED):
    /// el observador no espera de más y el reporte cuenta lo que hubo — una escena caída canta
    /// los sondeos como FALLO honesto.</para>
    /// </summary>
    public static bool MountUiaExternalScene(Window? window)
    {
        // Señal STALE fuera ANTES de montar (hito 245): el fichero solo existe entre el fin del
        // fixture y el próximo arranque.
        try
        {
            File.Delete(FixtureSignalPath);
        }
        catch
        {
        }

        bool sceneMounted = false;
        string mountError = "sin intento completado";
        DispatcherQueue dispatcher = window?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();            for (int attempt = 0; attempt < 30 && !sceneMounted; attempt++)
            {
                Thread.Sleep(attempt == 0 ? 1500 : 500);
                var completed = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        sceneMounted = TryMountUiaScene(window!);
                    }
                    catch (Exception ex)
                    {
                        mountError = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        completed.Set();
                    }
                });

                completed.Wait(TimeSpan.FromSeconds(10));
            }

        if (sceneMounted)
        {
            // El asentamiento SIN cliente: la tormenta de materialización del contenido con
            // Expander pasa aquí — el hijo (cliente UIA) llega después, a escena quieta.
            Thread.Sleep(4000);
        }

        try
        {
            File.WriteAllText(FixtureSignalPath, sceneMounted
                ? "ready " + DateTime.Now.ToString("HH:mm:ss.fff")
                : "FAILED " + mountError);
        }
        catch
        {
        }

        return sceneMounted;
    }

    /// <summary>
    /// Un intento de montaje de la escena (idempotente): el inspector abierto sobre el primer nodo
    /// con sus snapshots reales y la combinada pre-seleccionada. Devuelve false si el árbol aún no
    /// está listo (reintento).
    /// </summary>
    private static bool TryMountUiaScene(Window window)
    {
        var canvas = SelfCheckTree.Find<EditorCanvasControl>(window.Content);
        var inspector = SelfCheckTree.Find<NodeInspectorPanel>(window.Content);
        if (canvas?.Editor is not { } editor || inspector is null)
        {
            return false;
        }

        var firstNode = editor.Nodes.FirstOrDefault();
        if (firstNode is null)
        {
            return false;
        }

        // La escena determinista: 1 entrada + 3 salidas (el primer nodo del ejemplo tiene 3
        // puertos de salida; con menos, los que haya). «Category» es la PRIMERA clave de cada
        // metadato: la fila Added 'InspectorDiffKey_Category' nace en el orden del Dictionary y el
        // observador la busca por nombre sin descifrar el orden.
        if (firstNode.InputSnapshots.Count == 0)
        {
            var probeItem = new FileItemContext(Path.Combine(Path.GetTempPath(), "__uia_probe__.txt"));
            probeItem.Metadata["Category"] = "Probe";
            firstNode.InputSnapshots.Add(NodeDataSnapshot.CreateInput(firstNode.Id, "In", probeItem));
        }

        if (firstNode.OutputSnapshots.Count == 0)
        {
            int outputPorts = Math.Max(firstNode.OutputPorts.Count, 1);
            for (int i = 0; i < Math.Min(outputPorts, 3); i++)
            {
                string portName = i < firstNode.OutputPorts.Count
                    ? firstNode.OutputPorts[i].Name
                    : "Out";
                var outItem = new FileItemContext(Path.Combine(Path.GetTempPath(), "__uia_probe_out_" + i + ".txt"));
                outItem.Metadata["Category"] = "Out" + i;
                firstNode.OutputSnapshots.Add(NodeDataSnapshot.CreateOutput(firstNode.Id, portName, outItem));
            }
        }

        inspector.InspectForProbe(firstNode);

        // La pestaña activa queda en PARÁMETROS (la ligera por defecto): el contenido de snapshots
        // EN PIE —cabecera con Expander materializada— tumba al proveedor UIA del proceso con
        // retardo (la frontera medida del 245: montado=True y muerte ~2-4 s después, sin WER ni
        // excepción; el selfcheck interno sobrevive porque su try/finally DESMONTA al restaurar).
        // La combinada la conmuta el selfcheck interno (vía segura probada) — nunca en pie para el
        // observador externo.
        return true;
    }
}
