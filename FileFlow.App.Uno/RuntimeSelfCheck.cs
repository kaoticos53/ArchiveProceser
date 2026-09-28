using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using FileFlow.App.Models;
using FileFlow.App.Uno.Controls;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using FileFlow.Plugin.AI;
using FileFlow.Sdk;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.VirtualFileSystem;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno;

/// <summary>
/// El sondeo en runtime del host Uno (<c>FileFlow.App.Uno.exe --selfcheck</c>): arranca la aplicación
/// real (DI completa, plugins descubiertos, ejemplo cargado) y recorre el árbol visual de la tarjeta
/// para confirmar que los bindings, recursos y conversores del host se resuelven — la prueba que la
/// compilación no puede dar. Imprime el inventario en consola y termina con código 0 (verificado) o 1
/// (alguna expectativa vacía), sin interacción.
///
/// <para>Lo que confirma y lo que no: el sondeo valida que cada pieza enlazada del árbol tiene valor
/// real (título, categoría, color de acento, icono con geometría, sockets con borde, telemetría con
/// texto) y que los cables del lienzo están dibujados; no valida el píxel (la comparación visual con
/// el escritorio queda sin demostrar en este entorno) ni la interacción (fases 3.2/3.3).</para>
/// </summary>
public static class RuntimeSelfCheck
{
    /// <summary>La sonda de rendimiento ya corrió en este proceso: el árbol queda one-shot.</summary>
    private static bool _performanceProbeRan;
    /// <summary>
    /// Corre el sondeo en un hilo de fondo (nunca bloquea el hilo de UI): reintenta en el dispatcher
    /// hasta ver las tarjetas materializadas o agotar la ventana de espera, y termina el proceso con el
    /// veredicto. Devuelve -1 (el proceso termina dentro del sondeo).
    /// </summary>
    public static int Run(Window window, DispatcherQueue dispatcher)
    {        new Thread(() =>
        {
            var lastReport = new StringBuilder("[sin intento completado]");
            var ok = false;

            // La materialización de las plantillas ocurre en el pase de layout, después del Activate.
            for (int attempt = 0; attempt < 30 && !ok && !_performanceProbeRan; attempt++)
            {
                Thread.Sleep(attempt == 0 ? 300 : 200);

                // Cada intento parte de un bloque limpio: selfcheck-report.txt cuenta SIEMPRE lo que el
                // último intento vio, no la historia de los intentos de espera («lienzo sin tamaño»,
                // «grafo sin cargar»), que era ruido de diagnóstico. La consola recibe el bloque final.
                lastReport.Clear();

                var completed = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        ok = Inspect(window, lastReport);
                    }
                    catch (Exception ex)
                    {
                        lastReport.AppendLine("EXCEPCIÓN en el sondeo: " + ex.GetType().Name + ": " + ex.Message
                            + Environment.NewLine + ex.StackTrace);
                    }
                    finally
                    {
                        completed.Set();
                    }
                });

                completed.Wait(TimeSpan.FromSeconds(10));

                // Escritura POR intento: si el proceso muere a mitad del sondeo, el fichero cuenta el
                // último intento completo y no queda a medias con una mezcla de épocas.
                try
                {
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selfcheck-report.txt"), lastReport.ToString());
                }
                catch { }
            }

            // Exit desde un hilo de fondo no purga buffers ni ejecuta finalizers: imprimir el bloque final
            // y forzar el flush antes de salir.
            Console.Out.Flush();
            Console.WriteLine(lastReport.ToString());
            Console.Out.Flush();
            Environment.Exit(ok ? 0 : 1);
        })
        {
            IsBackground = true,
            Name = "RuntimeSelfCheck"
        }.Start();

        return -1; // el proceso termina por Environment.Exit dentro del sondeo
    }

    /// <summary>
    /// El sondeo de la superficie de AJUSTES (hito 255), en un <b>modo propio</b> (<c>--selfcheck-settings</c>)
    /// porque su medición es invasiva por naturaleza: cambia el tema y el idioma —estado global de la
    /// aplicación— y los sondeos del lienzo que ya corrían miden un árbol que no tolera esa mudanza a mitad
    /// (medido: dejarlos convivir hace fallar la sonda de selección, la de paneles y la de foco, que no tienen
    /// nada que ver con los ajustes). Un proceso limpio para esta superficie es, además, el reparto del
    /// fixture UIA: la escena se monta, se asienta y sólo entonces se mide.
    ///
    /// <para><b>Los dos tiempos</b>: el primer callback despliega la superficie y deja visible su sección de
    /// apariencia; el layout asienta (el enlace de un control recién hecho visible no está vivo en el mismo
    /// callback) y el segundo mide. El veredicto sale por el código de salida y por
    /// <c>selfcheck-settings-report.txt</c>.</para>
    /// </summary>
    public static int RunSettingsProbe(Window window, DispatcherQueue dispatcher)
    {
        new Thread(() =>
        {
            var report = new StringBuilder();
            bool ok = false;

            try
            {
                // Primer tiempo: desplegar y dejar la sección de apariencia a la vista.
                bool opened = false;
                var openedGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = Find<SettingsPanel>(window.Content);
                        opened = panel is not null && panel.OpenForMeasurement();
                    }
                    catch (Exception ex)
                    {
                        report.AppendLine("EXCEPCIÓN al desplegar la superficie: " + ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        openedGate.Set();
                    }
                });
                openedGate.Wait(TimeSpan.FromSeconds(10));

                // El asentamiento del layout (el mismo tiempo de espera que el fixture del 245).
                Thread.Sleep(800);

                // Segundo tiempo: medir, restaurar y recoger.
                var measuredGate = new ManualResetEventSlim(false);
                (bool Opened, bool Startup, bool Catalog, bool ThemeApplied, bool ThemeRestored, bool LanguageChanged,
                    bool LanguageRestored, bool PreferenceStored, bool Restored, string Detail) result = default;
                (bool SixSections, string Detail) census = default;
                (bool Ok, string Detail) aiModels = default;
                (bool Ok, string Detail) updates = default;
                string? failure = null;
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = Find<SettingsPanel>(window.Content);
                        if (panel is null)
                        {
                            failure = "la superficie de ajustes no está montada en la ventana";
                            return;
                        }

                        result = panel.ProbeSettingsSurface();
                        census = panel.MeasureSectionCensus();
                        panel.ShowPortedSection("AiModels");
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        measuredGate.Set();
                    }
                });
                measuredGate.Wait(TimeSpan.FromSeconds(30));

                // Las dos SECCIONES portadas en el hito 261 se miden con su propio tiempo: conmutar y medir en
                // el mismo tic mediría un control cuyo enlace todavía no ha corrido —la lección que el sondeo
                // de esta superficie ya trae escrita—, así que cada una se deja visible, se le da el pase de
                // layout y se mide después.
                Thread.Sleep(400);
                var aiGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = Find<SettingsPanel>(window.Content);
                        if (panel is not null)
                        {
                            aiModels = panel.MeasureAiModelsSection();
                            panel.ShowPortedSection("Updates");
                        }
                    }
                    catch (Exception ex)
                    {
                        aiModels = (false, ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        aiGate.Set();
                    }
                });
                aiGate.Wait(TimeSpan.FromSeconds(15));

                Thread.Sleep(400);
                var updatesGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = Find<SettingsPanel>(window.Content);
                        if (panel is not null)
                        {
                            updates = panel.MeasureUpdatesSection();
                            panel.ShowPortedSection("Appearance");
                        }
                    }
                    catch (Exception ex)
                    {
                        updates = (false, ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        updatesGate.Set();
                    }
                });
                updatesGate.Wait(TimeSpan.FromSeconds(15));

                // ── Hito 262: el editor de URLs por modelo, de punta a punta ────────────────────────────────
                //
                // Se pulsa la ACCIÓN DIBUJADA de la fila (su peer de automatización, el mismo canal que un
                // lector de pantalla), se escribe en la CAJA REAL del editor que se abre, se pulsa su botón
                // primario y se comprueba DÓNDE acaba lo escrito: en el almacén del gestor del núcleo, que es
                // el mismo sitio donde lo escribe el escritorio. Después se deja la configuración del usuario
                // como estaba (instantánea previa, restaurada por las APIs del propio gestor).
                const string probeUrl = "https://probe.invalid/fileflow.model.onnx";
                string urlModelId = string.Empty;
                string urlExpectedName = string.Empty;
                bool urlActionPressed = false;
                bool urlSurfaceUp = false;
                string urlKeyWhileOpen = string.Empty;
                string urlBodyModelName = string.Empty;
                string urlBodyValue = string.Empty;
                string urlCountAfterEdit = "";
                string urlVmValueAfterEdit = string.Empty;
                bool urlClosedBySave = false;
                bool urlChangeLanded = false;
                bool urlRestored = false;
                var originalUrls = new List<string>();
                bool originalWasCustom = false;

                Thread.Sleep(400);
                var urlSeamGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = Find<SettingsPanel>(window.Content);
                        if (panel is not null)
                        {
                            panel.ShowPortedSection("AiModels");
                            urlModelId = panel.FirstModelId ?? string.Empty;
                            if (urlModelId.Length > 0)
                            {
                                originalWasCustom = AiModelManager.HasCustomUrls(urlModelId);
                                originalUrls = [.. AiModelManager.GetConfiguredUrls(urlModelId)];
                                urlExpectedName = AiModelManager.Catalog.TryGetValue(urlModelId, out var info)
                                    ? info.FriendlyName
                                    : urlModelId;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        urlSeamGate.Set();
                    }
                });
                urlSeamGate.Wait(TimeSpan.FromSeconds(15));
                Thread.Sleep(500);

                var urlPressGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = Find<SettingsPanel>(window.Content);
                        Button? action = panel?.FirstModelUrlActionButton();
                        urlActionPressed = action is not null && UnoWindowService.Press(action);
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        urlPressGate.Set();
                    }
                });
                urlPressGate.Wait(TimeSpan.FromSeconds(15));
                urlSurfaceUp = WaitFor(() => UnoWindowService.ActiveWindowKey == DialogKeys.AiModelUrlsConfig, 8000);

                Thread.Sleep(400);
                var urlReadGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        if (UnoWindowService.ActiveDialog?.Content is AiModelUrlsConfigBody body)
                        {
                            urlKeyWhileOpen = UnoWindowService.ActiveWindowKey ?? "—";
                            urlBodyModelName = body.ModelNameShown;
                            urlBodyValue = body.Vm.UrlsText;
                            body.UrlsEditor.Text = probeUrl;   // la caja REAL, en dos sentidos
                            urlCountAfterEdit = body.CountText;
                            urlVmValueAfterEdit = body.Vm.UrlsText;
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        urlReadGate.Set();
                    }
                });
                urlReadGate.Wait(TimeSpan.FromSeconds(15));
                Thread.Sleep(200);

                var urlSaveGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        UnoWindowService.Press(UnoWindowService.ActivePrimaryButton);
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        urlSaveGate.Set();
                    }
                });
                urlSaveGate.Wait(TimeSpan.FromSeconds(15));
                urlClosedBySave = WaitFor(() => UnoWindowService.ActiveWindowKey is null, 8000);

                Thread.Sleep(200);
                var urlVerifyGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        if (urlModelId.Length > 0)
                        {
                            urlChangeLanded = AiModelManager.GetConfiguredUrls(urlModelId)
                                .Any(u => string.Equals(u, probeUrl, StringComparison.OrdinalIgnoreCase));

                            // La sonda mide, no configura: se devuelve la instantánea que se tomó antes.
                            if (originalWasCustom)
                            {
                                AiModelManager.SetCustomUrls(urlModelId, originalUrls);
                            }
                            else
                            {
                                AiModelUrlConfig.ResetCustomUrls(urlModelId);
                            }

                            urlRestored = AiModelManager.HasCustomUrls(urlModelId) == originalWasCustom
                                && AiModelManager.GetConfiguredUrls(urlModelId)
                                    .SequenceEqual(originalUrls, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        urlVerifyGate.Set();
                    }
                });
                urlVerifyGate.Wait(TimeSpan.FromSeconds(15));

                report.AppendLine("=== Sondeo de la superficie de AJUSTES del host Uno (hitos 255, 261 y 262) ===");

                if (failure is not null)
                {
                    report.AppendLine("[FALLO] la medición lanzó: " + failure);
                }
                else
                {
                    void Check(bool condition, string what)
                    {
                        report.AppendLine((condition ? "[OK]   " : "[FALLO]") + " " + what);
                        ok &= condition;
                    }

                    ok = opened && !string.IsNullOrEmpty(result.Detail);
                    Check(result.Opened, "la superficie de ajustes se abre con sus seis secciones");
                    Check(result.Startup, "el tema y el idioma GUARDADOS del usuario son los que están puestos al arrancar");
                    Check(result.Catalog, "los catálogos del view model portable están poblados (temas, idiomas, estrategias de conflicto y niveles de log)");
                    Check(result.ThemeApplied, "el tema elegido en su desplegable llega al gestor de temas y el token del host cambia de color");
                    Check(result.ThemeRestored, "la superficie devuelve el tema que estaba aplicado al entrar");
                    Check(result.LanguageChanged, "el idioma elegido reescribe los textos del marco en caliente (diccionario del host)");
                    Check(result.LanguageRestored, "la superficie devuelve el idioma que estaba al entrar");
                    Check(result.PreferenceStored, "una preferencia editada en su campo llega al servicio de preferencias (write-through)");
                    Check(result.Restored, "la superficie deja las preferencias guardadas del usuario como estaban al entrar");

                    // ── Las dos secciones portadas en el hito 261: Modelos de IA y Actualizaciones ──
                    Check(census.SixSections,
                        "la superficie tiene las SEIS secciones con su rótulo: almacenamiento, apariencia, rendimiento, herramientas, modelos de IA y actualizaciones");

                    // ── El editor de URLs por modelo (hito 262), ejercido de punta a punta ──
                    Check(urlActionPressed && urlSurfaceUp,
                        "la acción «URLs» de la fila del catálogo abre el editor del modelo (fila pulsada=" + urlActionPressed
                        + ", clave abierta=" + urlKeyWhileOpen + ")");
                    Check(urlBodyModelName.Length > 0 && urlBodyModelName == urlExpectedName,
                        "y el editor es la vista del view model portable del modelo elegido ('" + urlBodyModelName
                        + "', esperado '" + urlExpectedName + "')");
                    Check(!string.IsNullOrWhiteSpace(urlBodyValue),
                        "el editor se abre con las URLs que el gestor tiene configuradas para ese modelo ("
                        + urlBodyValue.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length + " URL(s))");
                    Check(urlVmValueAfterEdit.Trim() == probeUrl && urlCountAfterEdit.Contains("1 URL"),
                        "escribir en su caja llega al view model y su recuento se pone al día ('" + urlCountAfterEdit.Trim() + "')");
                    Check(urlClosedBySave && urlChangeLanded,
                        "y pulsar Guardar escribe el cambio DONDE LO ESCRIBE EL ESCRITORIO (el almacén del gestor del núcleo: "
                        + (urlChangeLanded ? "contiene la URL escrita" : "NO contiene la URL escrita") + ")");
                    Check(urlRestored,
                        "la sonda deja la configuración del usuario como estaba (instantánea previa restaurada por el gestor)");
                }

                if (!string.IsNullOrEmpty(result.Detail))
                {
                    report.AppendLine("       [medición] " + result.Detail);
                }

                if (failure is null)
                {
                    report.AppendLine((aiModels.Ok ? "[OK]   " : "[FALLO]") +
                        " la sección «Modelos de IA» enseña el catálogo del gestor del núcleo, su carpeta y su estado");
                    ok &= aiModels.Ok;
                    report.AppendLine((updates.Ok ? "[OK]   " : "[FALLO]") +
                        " la sección «Actualizaciones» enseña la versión, el formato, los canales y la comprobación automática del view model portable");
                    ok &= updates.Ok;
                }

                if (!string.IsNullOrEmpty(census.Detail))
                {
                    report.AppendLine("       [medición] " + census.Detail);
                }

                report.AppendLine("       [medición] " + aiModels.Detail);
                report.AppendLine("       [medición] " + updates.Detail);
                report.AppendLine("       [medición] editor de URLs: modelo=" + (urlModelId.Length > 0 ? urlModelId : "—")
                    + " leído=" + urlBodyValue.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length
                    + " escrito='" + urlVmValueAfterEdit + "'"
                    + " en el gestor=" + (urlChangeLanded ? "sí" : "no")
                    + " restaurado=" + urlRestored);

                report.AppendLine(ok
                    ? "=== RESULTADO: VERIFICADO (la superficie de ajustes escribe y restaura) ==="
                    : "=== RESULTADO: FALLOS (ver [FALLO] arriba) ===");
            }
            catch (Exception ex)
            {
                report.AppendLine("[FALLO] el sondeo de ajustes murió: " + ex.GetType().Name + ": " + ex.Message);
            }

            // La espera de la sonda de ajustes: los pasos de esta superficie se dan en tics del hilo de UI
            // separados (medir un control cuyo enlace no ha corrido mide otra cosa), así que entre uno y el
            // siguiente hay que esperar un EFECTO observable —el modal que se abre, el que se cierra—, no un
            // tiempo fijo a ciegas.
            bool WaitFor(Func<bool> condition, int milliseconds)
            {
                var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
                while (DateTime.UtcNow < until)
                {
                    if (condition())
                    {
                        return true;
                    }

                    Thread.Sleep(50);
                }

                return condition();
            }

            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selfcheck-settings-report.txt"), report.ToString());
            }
            catch
            {
            }

            Console.Out.Flush();
            Console.WriteLine(report.ToString());
            Console.Out.Flush();
            Environment.Exit(ok ? 0 : 1);
        })
        {
            IsBackground = true,
            Name = "RuntimeSettingsProbe"
        }.Start();

        return -1;
    }

    /// <summary>
    /// La cola de la consola del núcleo (el canal de la ejecución, hito 243): la prueba de que una orden
    /// llegó al motor y de lo que el motor hizo con ella. Se alcanza por el contenedor porque la consola es
    /// del <c>MainViewModel</c> y la barra la consume por su cuenta (no la expone).
    /// </summary>
    private static (int Count, string Last) ConsoleTail()
    {
        try
        {
            if (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<MainViewModel>(App.Services).LogConsole is not { } log)
            {
                return (-1, "sin consola");
            }

            int count = log.Logs.Count;
            return (count, count == 0 ? "(vacía)" : log.Logs[count - 1].Message.Replace('\n', ' '));
        }
        catch (Exception ex)
        {
            return (-1, "sin consola: " + ex.GetType().Name);
        }
    }

    /// <summary>Los snapshots que el grafo lleva encima (el rastro del ciclo en el documento).</summary>
    private static int SnapshotCount(ControlBarViewModel model) =>
        model.Editor.Nodes.Sum(node => node.InputSnapshots.Count + node.OutputSnapshots.Count);

    /// <summary>Los diffs de metadatos que el inspector tiene calculados (el otro rastro del ciclo).</summary>
    private static int SnapshotDiffCount(ControlBarViewModel model) => model.NodeInspector.MetadataDiffs.Count;

    /// <summary>Las entradas que la barra de control del host TIENE que exponer (el censo de la guardia).</summary>
    internal static readonly string[] ControlBarEntryIds =
    [
        "ControlBarMenuButton", "ControlBarDryRunToggle", "ControlBarWatchButton", "ControlBarRunButton",
        "ControlBarDebugButton", "ControlBarStepButton", "ControlBarContinueButton", "ControlBarPauseButton",
        "ControlBarStopButton", "ControlBarUndoButton", "ControlBarRedoButton", "ControlBarRollbackButton",
        "ControlBarInspectorButton", "ControlBarVfsButton", "ControlBarUpdateBadge", "SettingsButton",
    ];

    /// <summary>
    /// Las entradas que el CAJÓN del host tiene que exponer (hitos 258, 259 y 261): las cinco suyas, más las
    /// tres de flujo que cumplen la ventana con sus diálogos asíncronos, las tres de ayuda, las tres de
    /// VENTANA del hito 259 (Estudio de temas, Métricas y VFS), que ejecutan las órdenes canónicas del núcleo,
    /// y el DISEÑADOR DE DATASETS, cuya superficie declara el NODO al SDK (hito 261).
    /// </summary>
    internal static readonly string[] DrawerEntryIds =
    [
        "ControlBarDrawerNewButton", "ControlBarDrawerLoadButton", "ControlBarDrawerSaveButton",
        "ControlBarThemeCombo", "ControlBarLanguageCombo", "ControlBarDrawerThemeStudioButton",
        "ControlBarDrawerSettingsButton", "ControlBarDrawerInspectorButton",
        "ControlBarDrawerMetricsButton", "ControlBarDrawerVfsButton", "ControlBarDrawerDataSetButton",
        "ControlBarDrawerManualButton", "ControlBarDrawerExamplesButton",
        "ControlBarDrawerAboutButton", "ControlBarDrawerCloseButton",
    ];

    /// <summary>
    /// El sondeo del MENÚ PRINCIPAL del host (<c>--selfcheck-controlbar</c>): la barra de control y su
    /// cajón, ejercidos por el MISMO canal que el usuario —el peer de automatización de cada entrada: el
    /// Invoke del botón, el Toggle de la casilla—, con el estado por contexto leído del view model portable.
    ///
    /// <para><b>Por qué en modo propio</b>: su ciclo de ejecución mueve el documento (snapshots y diff del
    /// nodo fuente) y las sondas del lienzo miden una escena que no tolera esa mudanza a mitad. Un proceso
    /// limpio para esta superficie es, además, el reparto de las sondas anteriores.</para>
    ///
    /// <para>Termina con el veredicto por el código de salida y por <c>selfcheck-controlbar-report.txt</c>.</para>
    /// </summary>
    public static int RunControlBarProbe(Window window, DispatcherQueue dispatcher)
    {
        new Thread(() =>
        {
            var report = new StringBuilder();
            bool ok = true;

            // Lee un dato del hilo de UI sin pulsar nada (el mismo reparto que la sonda de los diálogos).
            T? Probe<T>(Func<T> read)
            {
                T? value = default;
                var gate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        value = read();
                    }
                    catch (Exception ex)
                    {
                        // El marco que lanzó, no sólo el mensaje: una excepción del hilo de UI sin su traza
                        // obliga a adivinar qué lectura la provocó (y el informe es la única evidencia).
                        report.AppendLine("[excepción] " + ex.GetType().Name + ": " + ex.Message
                            + " | " + DescribeFrame(ex));
                    }
                    finally
                    {
                        gate.Set();
                    }
                });

                gate.Wait(TimeSpan.FromSeconds(10));
                return value;
            }

            // Espera a que se cumpla una condición del host (un modal que se abre, un diálogo que se cierra):
            // la UI no es síncrona y medir en el mismo tick mediría el fotograma anterior.
            bool WaitUntil(Func<bool> condition, int milliseconds)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (clock.ElapsedMilliseconds < milliseconds)
                {
                    if (Probe(condition))
                    {
                        return true;
                    }

                    Thread.Sleep(80);
                }

                return Probe(condition);
            }

            // Cada paso corre EN el hilo de UI y espera su asentamiento: pulsar sin dejar asentar el layout
            // mide un árbol que todavía no refleja la orden.
            bool Step(Func<bool> action)
            {
                bool result = false;
                var gate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        result = action();
                    }
                    catch (Exception ex)
                    {
                        // El marco que lanzó, no sólo el mensaje: una excepción del hilo de UI sin su traza
                        // obliga a adivinar qué lectura la provocó (y el informe es la única evidencia).
                        report.AppendLine("[excepción] " + ex.GetType().Name + ": " + ex.Message
                            + " | " + DescribeFrame(ex));
                    }
                    finally
                    {
                        gate.Set();
                    }
                });

                if (!gate.Wait(TimeSpan.FromSeconds(20)))
                {
                    return false;
                }

                Thread.Sleep(260);
                return result;
            }

            try
            {
                report.AppendLine("=== Sondeo del MENÚ PRINCIPAL del host Uno (hito 257) ===");

                ControlBar? bar = null;
                MainMenuDrawer? drawer = null;
                SettingsPanel? settings = null;
                bool mounted = Step(() =>
                {
                    bar = Find<ControlBar>(window.Content);
                    drawer = Find<MainMenuDrawer>(window.Content);
                    settings = Find<SettingsPanel>(window.Content);
                    return bar is not null && drawer is not null && settings is not null;
                });

                void Check(bool condition, string what)
                {
                    report.AppendLine((condition ? "[OK]   " : "[FALLO]") + " " + what);
                    ok &= condition;
                }

                Check(mounted, "la barra de control, su cajón y la superficie de ajustes están montados en la ventana");

                if (!mounted)
                {
                    report.AppendLine("       [medición] sin barra/cajón no hay nada que ejercer");
                }
                else
                {
                    var vm = bar!.Vm;
                    Check(vm is not null, "la barra lleva el ControlBarViewModel portable (el del núcleo, no una copia)");

                    bool census = Step(() => bar!.EntryCensus().Select(e => e.Id)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .SequenceEqual(ControlBarEntryIds.OrderBy(id => id, StringComparer.Ordinal)));
                    Check(census, "la barra expone sus " + ControlBarEntryIds.Length + " entradas con su AutomationId");

                    // El estado por contexto: la barra no copia el ciclo, lo lee del view model.
                    bool context = Step(() =>
                    {
                        var model = bar!.Vm!;
                        var entries = bar.EntryCensus().ToDictionary(e => e.Id);
                        return entries["ControlBarRunButton"].Visible == !model.IsRunning
                            && entries["ControlBarStopButton"].Visible == model.IsRunning
                            && entries["ControlBarPauseButton"].Visible == model.IsRunning
                            && entries["ControlBarStepButton"].Visible == model.IsDebugging
                            && entries["ControlBarUndoButton"].Enabled == model.Editor.CanUndo
                            && entries["ControlBarRedoButton"].Enabled == model.Editor.CanRedo;
                    });
                    Check(context, "el estado de las entradas es el del view model (ejecutar fuera / pausar y detener dentro, deshacer según el editor)");

                    // ── El Inspector: la entrada conmuta el panel del marco ──
                    bool inspectorBefore = false;
                    bool inspectorPressed = Step(() =>
                    {
                        inspectorBefore = bar!.Vm!.NodeInspector.IsOpen;
                        var entry = bar.EntryById("ControlBarInspectorButton");
                        return entry is not null && ControlBar.Press(entry);
                    });
                    bool inspectorFlipped = Step(() =>
                    {
                        var model = bar!.Vm!;
                        var panel = Find<NodeInspectorPanel>(window.Content);
                        return model.NodeInspector.IsOpen != inspectorBefore
                            && panel is not null
                            && (panel.Visibility == Visibility.Visible) == model.NodeInspector.IsOpen;
                    });
                    Check(inspectorPressed && inspectorFlipped,
                        "pulsar el Inspector conmuta el panel del host y la visibilidad del marco lo sigue");

                    bool inspectorRestored = Step(() =>
                    {
                        var entry = bar!.EntryById("ControlBarInspectorButton");
                        if (entry is not null)
                        {
                            ControlBar.Press(entry);
                        }

                        return true;
                    }) && Step(() => bar!.Vm!.NodeInspector.IsOpen == inspectorBefore);
                    Check(inspectorRestored, "y volver a pulsarlo lo deja como estaba");

                    // ── El menú: el cajón sigue el MISMO estado (IsMenuOpen) ──
                    bool menuPressed = Step(() =>
                    {
                        var entry = bar!.EntryById("ControlBarMenuButton");
                        return entry is not null && ControlBar.Press(entry);
                    });
                    bool menuOpen = Step(() => bar!.Vm!.IsMenuOpen && drawer!.IsOpen);
                    Check(menuPressed && menuOpen,
                        "pulsar «Menú» despliega el cajón por el mismo estado del view model (IsMenuOpen)");

                    bool drawerCensus = Step(() => drawer!.Entries().Select(e => e.Id)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .SequenceEqual(DrawerEntryIds.OrderBy(id => id, StringComparer.Ordinal))
                        && drawer.ThemeSelector.Items.Count > 0
                        && drawer.LanguageSelector.Items.Count == 2);
                    Check(drawerCensus, "el cajón expone sus " + DrawerEntryIds.Length
                        + " entradas (flujo, apariencia, paneles, ventanas y ayuda), con el catálogo de temas del núcleo y los dos idiomas");

                    bool settingsPressed = Step(() => drawer!.Press("ControlBarDrawerSettingsButton"));
                    bool settingsOpened = Step(() => settings!.IsOpen);
                    Check(settingsPressed && settingsOpened,
                        "la entrada «Ajustes» del cajón abre la MISMA superficie de ajustes del host");

                    bool drawerClosed = Step(() =>
                    {
                        settings!.Close();
                        return drawer!.Press("ControlBarDrawerCloseButton");
                    });
                    bool closedState = Step(() => !drawer!.IsOpen && !settings!.IsOpen && !bar!.Vm!.IsMenuOpen);
                    Check(drawerClosed && closedState,
                        "el botón de cerrar del cajón lo recoge y deja la superficie como estaba");

                    // ── El Modo Prueba: conmuta la simulación del ciclo ──
                    bool dryBefore = false;
                    bool dryPressed = Step(() =>
                    {
                        dryBefore = bar!.Vm!.IsDryRun;
                        var entry = bar.EntryById("ControlBarDryRunToggle");
                        return entry is not null && ControlBar.Press(entry);
                    });
                    bool dryFlipped = Step(() => bar!.Vm!.IsDryRun != dryBefore);
                    Check(dryPressed && dryFlipped, "la entrada «Modo Prueba» conmuta la simulación del ciclo");

                    bool dryRestored = Step(() =>
                    {
                        var entry = bar!.EntryById("ControlBarDryRunToggle");
                        if (entry is not null)
                        {
                            ControlBar.Press(entry);
                        }

                        return true;
                    }) && Step(() => bar!.Vm!.IsDryRun == dryBefore);
                    Check(dryRestored, "y volver a pulsarla devuelve la simulación a como estaba");

                    // ── El ciclo: pulsar Ejecutar corre el flujo (y su contexto es el del view model) ──
                    //
                    // El ciclo de este grafo dura menos que un fotograma de sonda, así que se mide por su
                    // RASTRO en el documento (los snapshots del grafo y los diffs del inspector que el
                    // motor escribe) y por el invariante del contexto en cada muestra. El enlace de la
                    // isla —ejecutar fuera, pausar y detener dentro— se mide además con el estado del
                    // propio view model, que es el dato que la vista NO puede dejar de seguir.
                    int snapshotsBefore = 0;
                    int diffsBefore = 0;
                    int logsBefore = 0;
                    string lastLogBefore = string.Empty;
                    Step(() =>
                    {
                        snapshotsBefore = SnapshotCount(bar!.Vm!);
                        diffsBefore = SnapshotDiffCount(bar!.Vm!);
                        (logsBefore, lastLogBefore) = ConsoleTail();
                        return true;
                    });
                    string statusBefore = MainWindow.ExecutionStatusLine;

                    bool runPressed = Step(() =>
                    {
                        var entry = bar.EntryById("ControlBarRunButton");
                        return entry is not null && ControlBar.Press(entry);
                    });

                    bool sawRunning = false;
                    bool invariantHeld = true;
                    int samples = 0;
                    int runningSamples = 0;
                    string statusAfter = statusBefore;
                    for (int i = 0; i < 100; i++)
                    {
                        Thread.Sleep(120);
                        bool observed = Step(() =>
                        {
                            var model = bar!.Vm!;
                            var entries = bar.EntryCensus().ToDictionary(e => e.Id);
                            bool running = model.IsRunning;
                            samples++;
                            if (running)
                            {
                                runningSamples++;
                            }

                            bool invariant = entries["ControlBarRunButton"].Visible == !running
                                && entries["ControlBarStopButton"].Visible == running
                                && entries["ControlBarPauseButton"].Visible == running;
                            return running || invariant;
                        });

                        invariantHeld &= observed;
                        int now = snapshotsBefore;
                        Step(() => { now = SnapshotCount(bar!.Vm!); return true; });
                        statusAfter = MainWindow.ExecutionStatusLine;
                        sawRunning = sawRunning || runningSamples > 0;

                        // El ciclo corrió de verdad: o se le vio correr o dejó su rastro en el documento.
                        if (now > snapshotsBefore && !sawRunning)
                        {
                            break;
                        }
                    }

                    int snapshotsAfter = snapshotsBefore;
                    int diffsAfter = diffsBefore;
                    int logsAfter = logsBefore;
                    string lastLogAfter = lastLogBefore;
                    Step(() =>
                    {
                        snapshotsAfter = SnapshotCount(bar!.Vm!);
                        diffsAfter = SnapshotDiffCount(bar!.Vm!);
                        (logsAfter, lastLogAfter) = ConsoleTail();
                        return true;
                    });
                    bool ranTrace = snapshotsAfter > snapshotsBefore || diffsAfter > diffsBefore || logsAfter > logsBefore;

                    Check(runPressed && (sawRunning || ranTrace),
                        "pulsar «Ejecutar» despacha la orden canónica del núcleo (corriendo=" + sawRunning
                        + ", snapshots " + snapshotsBefore + "->" + snapshotsAfter
                        + ", diffs " + diffsBefore + "->" + diffsAfter
                        + ", consola " + logsBefore + "->" + logsAfter + ")");

                    // El contexto con el ciclo en marcha: se fuerza el estado en el VIEW MODEL y se lee la
                    // barra. Un invariante que se cumple no demuestra el enlace si nunca se ve el estado
                    // contrario; este paso lo ve, y es el mismo dato que mueve los botones en el ciclo real.
                    bool forcedRunning = Step(() => { bar!.Vm!.IsRunning = true; return true; });
                    bool forcedContext = Step(() =>
                    {
                        var model = bar!.Vm!;
                        var entries = bar.EntryCensus().ToDictionary(e => e.Id);
                        return model.IsRunning
                            && !entries["ControlBarRunButton"].Visible
                            && !entries["ControlBarDebugButton"].Visible
                            && entries["ControlBarStopButton"].Visible
                            && entries["ControlBarPauseButton"].Visible;
                    });
                    bool forcedBack = Step(() => { bar!.Vm!.IsRunning = false; return true; });
                    bool idleContext = Step(() =>
                    {
                        var model = bar!.Vm!;
                        var entries = bar.EntryCensus().ToDictionary(e => e.Id);
                        return !model.IsRunning
                            && entries["ControlBarRunButton"].Visible
                            && entries["ControlBarDebugButton"].Visible
                            && !entries["ControlBarStopButton"].Visible
                            && !entries["ControlBarPauseButton"].Visible;
                    });

                    Check(forcedRunning && forcedContext && forcedBack && idleContext && invariantHeld,
                        "el contexto de la isla sigue al view model: con el ciclo en marcha salen ejecutar y "
                        + "depurar y entran pausar y detener, y al volver a inactivo se deshace ("
                        + samples + " muestras, " + runningSamples + " con el ciclo corriendo)");

                    // ── Hito 258 — las ENTRADAS nuevas del cajón y los ATAJOS del menú ──
                    //
                    // Las tres de FLUJO se cumplen por el canal del HOST (confirmación y pickers
                    // asíncronos + métodos del view model portable), así que no basta con ver un modal: se
                    // mide DÓNDE acaba el flujo, el grafo. Las de AYUDA ejecutan las órdenes canónicas del
                    // núcleo, y «Acerca de» llega al servicio de ventanas del host. De los atajos, F5 se
                    // mide por su EFECTO (reanudar deja su línea en la consola) y los demás por el
                    // despacho de la tabla, que es lo único observable de una tecla sin nada que correr.
                    bool drawerOpenAgain = Step(() =>
                    {
                        var entry = bar!.EntryById("ControlBarMenuButton");
                        return entry is not null && ControlBar.Press(entry);
                    }) && WaitUntil(() => drawer!.IsOpen, 3000);

                    bool aboutPressed = drawerOpenAgain && Step(() => drawer!.Press("ControlBarDrawerAboutButton"));
                    bool aboutUp = aboutPressed && WaitUntil(() => UnoWindowService.ActiveDialog is not null, 8000);
                    string aboutVersion = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as AboutDialogBody)?.VersionLine) ?? "(sin superficie)";
                    Check(aboutPressed, "la entrada «Acerca de» del cajón se pulsa (cajón desplegado="
                        + drawerOpenAgain + ", pulsada=" + aboutPressed + ")");
                    Check(aboutUp && aboutVersion.Contains("net10.0", StringComparison.Ordinal),
                        "y abre la superficie del host, con la versión del producto ('" + aboutVersion
                        + "'; superficie abierta=" + aboutUp + ")");

                    bool aboutClosed = Step(() =>
                    {
                        var close = UnoWindowService.ActiveCloseButton;
                        return close is not null && UnoWindowService.Press(close);
                    }) && WaitUntil(() => UnoWindowService.ActiveDialog is null, 5000);
                    Check(aboutClosed, "y cerrarla la retira: el host queda sin ningún diálogo abierto");

                    // ── Las VENTANAS del menú (hito 259): Estudio de temas, Métricas y VFS ──
                    //
                    // Cada entrada del cajón ejecuta la orden CANÓNICA del núcleo, que pide su ventana por
                    // el catálogo de diálogos; el host la sirve y la sonda lee QUÉ clave se abrió
                    // (ActiveWindowKey) y qué enseña su cuerpo —el view model portable, no una copia—.
                    bool studioPressed = Step(() => drawer!.Press("ControlBarDrawerThemeStudioButton"));
                    bool studioUp = studioPressed && WaitUntil(
                        () => UnoWindowService.ActiveWindowKey == DialogKeys.ThemeCustomizer, 8000);
                    int studioThemes = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as ThemeCustomizerBody)?.ThemeCount ?? -1);
                    int studioSections = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as ThemeCustomizerBody)?.SectionCount ?? -1);
                    int studioRows = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as ThemeCustomizerBody)?.RowCount ?? -1);
                    Check(studioUp && studioThemes > 0 && studioSections > 0 && studioRows >= studioSections,
                        "la entrada «Estudio de Temas» del cajón abre el estudio del host con el catálogo del núcleo ("
                        + studioThemes + " temas, " + studioSections + " secciones, " + studioRows + " ajustes editables)");

                    // El editor escribe de verdad: un ajuste de color por la MISMA vía que el usuario.
                    string accentBefore = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as ThemeCustomizerBody)?.ReadColorSetting("AccentPrimary")) ?? string.Empty;
                    bool studioWrote = Step(() =>
                    {
                        var body = UnoWindowService.ActiveDialog?.Content as ThemeCustomizerBody;
                        return body is not null
                            && body.WriteColorSetting("AccentPrimary", "#FF00AA")
                            && string.Equals(body.ReadColorSetting("AccentPrimary"), "#FF00AA", StringComparison.Ordinal);
                    });
                    bool studioRestored = Step(() =>
                    {
                        var body = UnoWindowService.ActiveDialog?.Content as ThemeCustomizerBody;
                        return body is not null
                            && body.WriteColorSetting("AccentPrimary", accentBefore)
                            && string.Equals(body.ReadColorSetting("AccentPrimary"), accentBefore, StringComparison.Ordinal);
                    });
                    Check(studioWrote && studioRestored,
                        "y su editor escribe en el tema ('AccentPrimary' '" + accentBefore + "' -> '#FF00AA' -> '"
                        + accentBefore + "'), que es el mismo camino del usuario");

                    bool studioClosed = Step(() =>
                    {
                        var close = UnoWindowService.ActiveCloseButton;
                        return close is not null && UnoWindowService.Press(close);
                    }) && WaitUntil(() => UnoWindowService.ActiveWindowKey is null, 5000);
                    Check(studioClosed, "y cerrarlo deja el host sin superficie abierta");

                    int nodesForMetrics = Probe(() => bar!.Vm!.Editor.Nodes.Count);
                    bool metricsPressed = Step(() => drawer!.Press("ControlBarDrawerMetricsButton"));
                    bool metricsUp = metricsPressed && WaitUntil(
                        () => UnoWindowService.ActiveWindowKey == DialogKeys.WorkflowMetricsDashboard, 8000);
                    int metricsRows = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as MetricsDashboardBody)?.RowCount ?? -1);
                    string metricsDuration = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as MetricsDashboardBody)?.TotalDurationText) ?? string.Empty;
                    string metricsStatus = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as MetricsDashboardBody)?.StatusLine) ?? string.Empty;
                    Check(metricsUp && metricsRows == nodesForMetrics && metricsRows > 0 && metricsDuration.Length > 0,
                        "la entrada «Métricas y Rendimiento» abre el panel del host con una fila por nodo del lienzo ("
                        + metricsRows + " de " + nodesForMetrics + ") y su duración formateada ('" + metricsDuration
                        + "', pie: '" + metricsStatus + "', abierto=" + metricsUp + ")");

                    bool metricsClosed = Step(() =>
                    {
                        var close = UnoWindowService.ActiveCloseButton;
                        return close is not null && UnoWindowService.Press(close);
                    }) && WaitUntil(() => UnoWindowService.ActiveWindowKey is null, 5000);
                    Check(metricsClosed, "y cerrarlo también la retira");

                    // El EXPLORADOR VIRTUAL con un almacén de verdad, por el MISMO camino del servicio que
                    // usa la orden del núcleo (la carga útil es el almacén de la última ejecución).
                    var sandboxStore = new FileFlow.Core.Engine.VirtualFileSystemStore();
                    sandboxStore.AddOrUpdateFile(new VirtualFileEntry(
                        "Movies/Alien (1979).mkv", @"C:\origen\Alien (1979).mkv", "Alien (1979).mkv", ".mkv",
                        "Movies", 4096, VirtualOperationType.Original, "FolderSource", "n1",
                        Role: VirtualFileRole.Source));
                    sandboxStore.AddOrUpdateFile(new VirtualFileEntry(
                        "Movies/Alien (1979)/poster.jpg", @"C:\origen\poster.jpg", "poster.jpg", ".jpg",
                        "Movies/Alien (1979)", 2048, VirtualOperationType.Saved, "ImageNode", "n2"));
                    sandboxStore.AddOrUpdateFile(new VirtualFileEntry(
                        "Movies/Alien (1979).nfo", @"C:\origen\Alien (1979).nfo", "Alien (1979).nfo", ".nfo",
                        "Movies", 512, VirtualOperationType.Copied, "DataNode", "n3"));

                    var windowService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                        .GetService<IWindowService>(App.Services);
                    bool vfsOpened = Step(() =>
                    {
                        windowService?.ShowWindow(DialogKeys.VirtualFileSystemExplorer, sandboxStore);
                        return true;
                    }) && WaitUntil(() => UnoWindowService.ActiveWindowKey == DialogKeys.VirtualFileSystemExplorer, 8000);
                    int vfsTotal = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as VirtualFileSystemExplorerBody)?.TotalFiles ?? -1);
                    int vfsRows = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as VirtualFileSystemExplorerBody)?.RowCount ?? -1);
                    int vfsMetadata = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as VirtualFileSystemExplorerBody)?.MetadataCount ?? -1);
                    Check(vfsOpened && vfsTotal == 3 && vfsRows == 3 && vfsMetadata > 0,
                        "el explorador VFS que pide el núcleo se sirve con el almacén de la carga útil ("
                        + vfsTotal + " ficheros, " + vfsRows + " filas, " + vfsMetadata + " metadatos del seleccionado)");

                    bool vfsClosed = Step(() =>
                    {
                        var close = UnoWindowService.ActiveCloseButton;
                        return close is not null && UnoWindowService.Press(close);
                    }) && WaitUntil(() => UnoWindowService.ActiveWindowKey is null, 5000);
                    Check(vfsClosed, "y cerrarlo la retira sin dejar rastro en el host");

                    // ── El ESTADO DE CONTEXTO de las dos entradas de la barra (hito 259) ──
                    //
                    // El chip del VFS y el distintivo de actualización no se ven siempre: aparecen cuando el
                    // view model dice que hay algo que enseñar (HasVirtualFiles / HasPendingUpdate), que es el
                    // mismo estado del escritorio. Aquí se mide con los dos valores del propio view model.
                    bool vfsContextOff = Step(() =>
                    {
                        var model = bar!.Vm!;
                        var entry = bar.EntryCensus().ToDictionary(e => e.Id)["ControlBarVfsButton"];
                        return entry.Visible == model.HasVirtualFiles;
                    });
                    bool vfsContextOn = Step(() =>
                    {
                        var model = bar!.Vm!;
                        model.HasVirtualFiles = true;
                        model.VirtualFilesCount = 7;
                        return true;
                    }) && Step(() =>
                        bar!.EntryCensus().ToDictionary(e => e.Id)["ControlBarVfsButton"].Visible
                        && bar.VfsChipText.Contains("7", StringComparison.Ordinal));
                    Check(vfsContextOff && vfsContextOn,
                        "el chip del VFS aparece con el estado del view model y lleva su recuento ('"
                        + Probe(() => bar!.VfsChipText) + "')");

                    // El AVISO DE ACTUALIZACIÓN: el host lo comprueba al arrancar (como el escritorio) y su
                    // distintivo lleva la versión nueva; aquí se le da una novedad para medir el camino
                    // entero, que en la aplicación normal sólo ocurre cuando hay release nueva de verdad.
                    var announcedUpdate = new AppUpdateInfo(
                        "v9.9.9", new SemVersion(9, 9, 9), "FileFlow Studio 9.9.9",
                        "Una versión de prueba para la sonda.", DateTime.UtcNow, false, null,
                        "https://example.invalid/fileflow");
                    bool updateBadgeOff = Step(() =>
                    {
                        var model = bar!.Vm!;
                        var entry = bar.EntryCensus().ToDictionary(e => e.Id)["ControlBarUpdateBadge"];
                        return entry.Visible == model.HasPendingUpdate && !model.HasPendingUpdate;
                    });
                    bool updateBadgeOn = Step(() =>
                    {
                        bar!.Vm!.SetPendingUpdate(announcedUpdate);
                        return true;
                    }) && Step(() =>
                        bar!.EntryCensus().ToDictionary(e => e.Id)["ControlBarUpdateBadge"].Visible
                        && bar.UpdateBadgeText.Contains("v9.9.9", StringComparison.Ordinal));
                    Check(updateBadgeOff && updateBadgeOn,
                        "el distintivo de actualización aparece al dejar una novedad pendiente, con su versión ('"
                        + Probe(() => bar!.UpdateBadgeText) + "')");

                    bool updatePressed = Step(() =>
                    {
                        var entry = bar!.EntryById("ControlBarUpdateBadge");
                        return entry is not null && ControlBar.Press(entry);
                    });
                    bool updateUp = updatePressed && WaitUntil(
                        () => UnoWindowService.ActiveWindowKey == DialogKeys.UpdateDialog, 8000);
                    string updateVersion = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as UpdateDialogBody)?.NewVersionText) ?? "(sin superficie)";
                    Check(updateUp && updateVersion.Contains("9.9.9", StringComparison.Ordinal),
                        "y su comando abre el AVISO DE ACTUALIZACIÓN del host, con la versión que se va a instalar ('"
                        + updateVersion + "'; pulsado=" + updatePressed + ", abierto=" + updateUp
                        + ", clave=" + (UnoWindowService.ActiveWindowKey ?? "—") + ")");

                    // El aviso se retira por su propia orden («recordármelo luego»), que es la que cierra
                    // desde dentro: el view model lo pide y el servicio de ventanas obedece.
                    bool updateClosed = Step(() =>
                    {
                        var body = UnoWindowService.ActiveDialog?.Content as UpdateDialogBody;
                        return body is not null && UnoWindowService.Press(body.Remind);
                    }) && WaitUntil(() => UnoWindowService.ActiveWindowKey is null, 5000);
                    Check(updateClosed, "y la orden «recordármelo luego» del propio aviso lo retira");

                    bool updateCleared = Step(() =>
                    {
                        var model = bar!.Vm!;
                        model.HasPendingUpdate = false;
                        model.PendingUpdateVersionTag = string.Empty;
                        return true;
                    }) && Step(() =>
                        !bar!.EntryCensus().ToDictionary(e => e.Id)["ControlBarUpdateBadge"].Visible);
                    Check(updateCleared, "y sin novedad pendiente el distintivo vuelve a esconderse");

                    // ── El DISEÑADOR DE DATASETS (hito 261): la superficie la declara el NODO, no el cajón ──
                    //
                    // El nodo de datos sintéticos la declara al SDK (qué diálogo quiere y qué contiene), y el
                    // host lo sirve con su propia vista sobre el view model portable del plugin. Se mide el
                    // camino entero por el canal del usuario: la entrada del cajón, la superficie del host y
                    // una orden REAL del diseñador (añadir y quitar un archivo del árbol del dataset), sin
                    // guardar nada — el diseñador guarda cuando el usuario lo pide, y la sonda no lo pide.
                    string declaredKey = Probe(() => bar!.Vm!.GetDataSetDesignerSurface()?.DialogKey) ?? "(sin declarar)";
                    Check(string.Equals(declaredKey, DialogKeys.DataSetDesigner, StringComparison.Ordinal),
                        "el nodo de datos sintéticos declara su superficie al SDK ('" + declaredKey + "')");

                    bool designerDrawerOpen = Step(() =>
                    {
                        var entry = bar!.EntryById("ControlBarMenuButton");
                        return entry is not null && ControlBar.Press(entry);
                    }) && WaitUntil(() => drawer!.IsOpen, 3000);
                    bool designerPressed = designerDrawerOpen && Step(() => drawer!.Press("ControlBarDrawerDataSetButton"));
                    bool designerUp = designerPressed && WaitUntil(
                        () => UnoWindowService.ActiveWindowKey == DialogKeys.DataSetDesigner, 8000);
                    int designerDataSets = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as DataSetDesignerBody)?.Vm.FilteredDataSets.Count ?? -1);
                    int designerRows = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as DataSetDesignerBody)?.Vm.EditableItems.Count ?? -1);
                    string designerDatasetName = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as DataSetDesignerBody)?.Vm.DataSetName) ?? string.Empty;
                    Check(designerUp && designerDataSets > 0 && designerRows > 0,
                        "la entrada «Diseñador de Datasets» del cajón abre la superficie del host sobre el view model "
                        + "del plugin (" + designerDataSets + " datasets en el catálogo, '" + designerDatasetName
                        + "' con " + designerRows + " elementos; abierto=" + designerUp + ")");

                    bool designerDslTab = Step(() =>
                    {
                        var body = UnoWindowService.ActiveDialog?.Content as DataSetDesignerBody;
                        if (body is null)
                        {
                            return false;
                        }

                        body.ShowTab("dsl");
                        return body.VisibleTab == 1 && body.DslPaneVisible && !body.TreePaneVisible;
                    });
                    Step(() =>
                    {
                        (UnoWindowService.ActiveDialog?.Content as DataSetDesignerBody)?.ShowTab("tree");
                        return true;
                    });
                    Check(designerDslTab, "y sus tres pestañas (árbol / DSL / JSON) conmutan sobre el cuerpo del host");

                    // Una orden REAL del diseñador, por el botón del host: añadir un archivo al árbol y quitarlo.
                    int designerFilesBefore = Probe(() =>
                        (UnoWindowService.ActiveDialog?.Content as DataSetDesignerBody)?.Vm.TotalFiles ?? -1);
                    bool designerAdded = Step(() =>
                    {
                        var add = UnoWindowService.FindByName<Button>(UnoWindowService.ActiveDialog, "AddFileButton");
                        return add is not null && UnoWindowService.Press(add);
                    }) && Step(() =>
                        (UnoWindowService.ActiveDialog?.Content as DataSetDesignerBody)?.Vm.TotalFiles == designerFilesBefore + 1);

                    bool designerRemoved = Step(() =>
                    {
                        var remove = UnoWindowService.FindByName<Button>(UnoWindowService.ActiveDialog, "RemoveNodeButton");
                        return remove is not null && UnoWindowService.Press(remove);
                    }) && Step(() =>
                        (UnoWindowService.ActiveDialog?.Content as DataSetDesignerBody)?.Vm.TotalFiles == designerFilesBefore);
                    Check(designerAdded && designerRemoved,
                        "y sus órdenes son las del view model del plugin: añadir un archivo al árbol lo cuenta ("
                        + designerFilesBefore + " -> " + (designerFilesBefore + 1) + " -> " + designerFilesBefore
                        + " archivos) y quitarlo lo devuelve");

                    bool designerClosed = Step(() =>
                    {
                        var close = UnoWindowService.FindByName<Button>(UnoWindowService.ActiveDialog, "CloseButton");
                        return close is not null && UnoWindowService.Press(close);
                    }) && WaitUntil(() => UnoWindowService.ActiveWindowKey is null, 5000);
                    Check(designerClosed, "y su pie cierra la superficie sin dejar diálogo abierto");

                    // «Nuevo Flujo», rama de CANCELAR: el grafo tiene que quedar como estaba.
                    int nodesBeforeNew = Probe(() => bar!.Vm!.Editor.Nodes.Count);
                    bool newPressed = Step(() => drawer!.Press("ControlBarDrawerNewButton"));
                    bool confirmOpen = newPressed
                        && WaitUntil(() => UnoDialogService.ActiveConfirmation is not null, 5000);
                    bool confirmCancelled = Step(() =>
                    {
                        var close = UnoWindowService.FindByName<Button>(UnoDialogService.ActiveConfirmation, "CloseButton");
                        return close is not null && ControlBar.Press(close);
                    }) && WaitUntil(() => UnoDialogService.ActiveConfirmation is null, 5000);
                    int nodesAfterCancel = Probe(() => bar!.Vm!.Editor.Nodes.Count);
                    Check(confirmOpen && confirmCancelled && nodesAfterCancel == nodesBeforeNew,
                        "«Nuevo Flujo» pide confirmación con el diálogo asíncrono del host y CANCELAR deja el grafo "
                        + "intacto (" + nodesBeforeNew + " -> " + nodesAfterCancel + " nodos)");

                    // La rama de CONFIRMAR: el flujo nuevo vacía el lienzo, y el flujo anterior se recupera
                    // por los métodos del view model portable que ya no dependen del diálogo (la ruta la
                    // pone quien la tenga: aquí, la sonda).
                    string roundTripPath = Path.Combine(Path.GetTempPath(),
                        "fileflow-selfcheck-258-" + Guid.NewGuid().ToString("N") + ".json");

                    // El guardado portable (la mitad sin diálogo de Guardar Flujo): la ruta la pone quien
                    // la tenga —el picker del host en la aplicación, la sonda aquí— y el ViewModel escribe.
                    bool saveStarted = Step(() =>
                    {
                        _ = bar!.Vm!.SaveWorkflowToFileAsync(roundTripPath);
                        return true;
                    });
                    bool savedToDisk = saveStarted && WaitUntil(() => File.Exists(roundTripPath), 8000)
                        && WaitUntil(() => new FileInfo(roundTripPath).Length > 0, 4000);
                    Check(savedToDisk, "el guardado del host escribe el flujo en disco ('"
                        + Path.GetFileName(roundTripPath) + "', las mismas " + nodesBeforeNew + " nodos del grafo)");

                    bool newConfirmed = Step(() => drawer!.Press("ControlBarDrawerNewButton")) && WaitUntil(
                        () => UnoDialogService.ActiveConfirmation is not null, 5000) && Step(() =>
                    {
                        var primary = UnoWindowService.FindByName<Button>(UnoDialogService.ActiveConfirmation, "PrimaryButton");
                        return primary is not null && ControlBar.Press(primary);
                    }) && WaitUntil(() => UnoDialogService.ActiveConfirmation is null, 5000)
                        && WaitUntil(() => bar!.Vm!.Editor.Nodes.Count == 0, 5000);
                    Check(newConfirmed,
                        "«Nuevo Flujo» CONFIRMADO crea el flujo nuevo: el lienzo queda en 0 nodos (antes "
                        + nodesBeforeNew + ")");

                    bool loadedBack = Step(() =>
                    {
                        _ = bar!.Vm!.LoadWorkflowFromFileAsync(roundTripPath);
                        return true;
                    }) && WaitUntil(() => bar!.Vm!.Editor.Nodes.Count == nodesBeforeNew, 8000);
                    int nodesAfterLoad = Probe(() => bar!.Vm!.Editor.Nodes.Count);
                    try
                    {
                        File.Delete(roundTripPath);
                    }
                    catch
                    {
                    }

                    Check(loadedBack, "y cargarla de vuelta por el mismo canal devuelve el grafo ("
                        + nodesAfterLoad + " nodos, como antes de crear el flujo nuevo)");

                    // Los ATAJOS: la tabla es la que enruta. F5 se mide por su efecto —reanudar escribe en
                    // la consola—, y los demás por el despacho, que es lo observable cuando no hay nada que
                    // correr (igual que en el escritorio: F5 sin ejecución tampoco hace nada allí).
                    int shortcutsInTable = ControlBar.RoutedShortcuts.Length;
                    Check(shortcutsInTable == 6 && !ControlBar.DeclaredUnroutedShortcuts.Any(),
                        "los " + shortcutsInTable + " atajos del menú del escritorio están enrutados y ninguno queda "
                        + "declarado sin ruta");

                    int logsBeforeF5 = 0;
                    string lastLogBeforeF5 = string.Empty;
                    Step(() =>
                    {
                        (logsBeforeF5, lastLogBeforeF5) = ConsoleTail();
                        bar!.Vm!.IsPaused = true;
                        return true;
                    });
                    bool routedF5 = Step(() => bar!.RouteShortcut(Windows.System.VirtualKey.F5, false, false));
                    bool f5Effect = WaitUntil(() =>
                    {
                        var (count, last) = ConsoleTail();
                        return count > logsBeforeF5 || bar!.Vm!.IsPaused == false;
                    }, 4000);
                    string f5Log = Probe(() => ConsoleTail().Last) ?? string.Empty;
                    Check(routedF5 && f5Effect && bar!.Vm!.IsPaused == false,
                        "el atajo F5 llega a la orden del ciclo: reanuda y lo deja escrito en la consola ('"
                        + f5Log + "')");

                    bool routedRest = Step(() => bar!.RouteShortcut(Windows.System.VirtualKey.F10, false, false)
                        && bar.RouteShortcut(Windows.System.VirtualKey.F5, false, true));
                    bool unknownFree = Step(() => !bar!.RouteShortcut(Windows.System.VirtualKey.F7, false, false));
                    Check(routedRest && unknownFree,
                        "F10 y Shift+F5 se despachan a las órdenes del ciclo, y una tecla que no es del menú no se "
                        + "traga (F7 sigue siendo de quien la reclame)");

                    int nodesBeforeCtrlN = Probe(() => bar!.Vm!.Editor.Nodes.Count);
                    bool routedCtrlN = Step(() => bar!.RouteShortcut(Windows.System.VirtualKey.N, true, false));
                    bool ctrlNDialog = routedCtrlN
                        && WaitUntil(() => UnoDialogService.ActiveConfirmation is not null, 5000) && Step(() =>
                        {
                            var close = UnoWindowService.FindByName<Button>(UnoDialogService.ActiveConfirmation, "CloseButton");
                            return close is not null && ControlBar.Press(close);
                        }) && WaitUntil(() => UnoDialogService.ActiveConfirmation is null, 5000);
                    int nodesAfterCtrlN = Probe(() => bar!.Vm!.Editor.Nodes.Count);
                    Check(ctrlNDialog && nodesAfterCtrlN == nodesBeforeCtrlN,
                        "el atajo Ctrl+N abre la MISMA confirmación que la entrada del cajón, y cancelarla deja el "
                        + "grafo como estaba (" + nodesBeforeCtrlN + " nodos)");

                    string cycleLine = string.Empty;
                    string censusLine = string.Empty;
                    Step(() =>
                    {
                        var model = bar!.Vm!;
                        cycleLine = "ciclo=" + (model.IsRunning ? "running" : "idle")
                            + " dryRun=" + model.IsDryRun + " debug=" + model.IsDebugging
                            + " inspector=" + model.NodeInspector.IsOpen + " menu=" + model.IsMenuOpen
                            + " snapshots=" + snapshotsBefore + "->" + snapshotsAfter
                            + " diffs=" + diffsBefore + "->" + diffsAfter
                            + " | canal(antes)='" + statusBefore.Trim() + "' canal(despues)='" + statusAfter.Trim() + "'";
                        cycleLine += Environment.NewLine + "       [medición] consola " + logsBefore + "->" + logsAfter
                            + " ultima(antes)='" + lastLogBefore + "' ultima(despues)='" + lastLogAfter + "'";
                        censusLine = bar.CensusLine();
                        return true;
                    });
                    report.AppendLine("       [medición] " + cycleLine);
                    report.AppendLine("       [medición] entradas " + ControlBarEntryIds.Length + ": " + censusLine);
                }

                report.AppendLine(ok
                    ? "=== RESULTADO: VERIFICADO (la barra y su menú ejecutan las órdenes del núcleo) ==="
                    : "=== RESULTADO: FALLOS (ver [FALLO] arriba) ===");
            }
            catch (Exception ex)
            {
                report.AppendLine("[FALLO] el sondeo del menú murió: " + ex.GetType().Name + ": " + ex.Message);
                ok = false;
            }

            try
            {
                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "selfcheck-controlbar-report.txt"), report.ToString());
            }
            catch
            {
            }

            Console.Out.Flush();
            Console.WriteLine(report.ToString());
            Console.Out.Flush();
            Environment.Exit(ok ? 0 : 1);
        })
        {
            IsBackground = true,
            Name = "RuntimeControlBarProbe"
        }.Start();

        return -1;
    }

    /// <summary>
    /// El sondeo de los PANELES DE NODO del host (<c>--selfcheck-dialogs</c>): la puerta del usuario a los
    /// dos diálogos —los botones «✎» y «{x}» de una fila del inspector—, el diálogo que abren y el VALOR que
    /// queda escrito en el parámetro del nodo al confirmarlo.
    ///
    /// <para><b>Qué mide, y por qué así.</b> El botón se pulsa por el peer de automatización del propio
    /// control (el canal de un lector de pantalla), la caja del diálogo se escribe como la escribe el usuario
    /// y el botón primario del modal se pulsa por su peer: lo que se lee al final no es el diálogo sino el
    /// <c>NodeParameterViewModel.Value</c> del parámetro —el mismo dato que el nodo ejecuta—, así que un
    /// diálogo que se abre y no escribe nada cae aquí igual que un botón que no abre nada.</para>
    ///
    /// <para><b>Por qué en modo propio</b>: el ciclo abre y cierra modales y deja parámetros escritos en el
    /// nodo inspeccionado —la escena que las sondas del lienzo están midiendo— y un modal abierto deja al
    /// resto de sondas mirando una ventana que no responde. Un proceso limpio para esta superficie es,
    /// además, el reparto de las sondas anteriores.</para>
    ///
    /// <para><b>La escena</b>: el flujo de ejemplo no trae ningún parámetro de texto LARGO, así que el sondeo
    /// añade un nodo por el mismo camino que el cajón de herramientas (<c>EditorViewModel.AddNode</c>, el que
    /// ejecuta el doble clic) y lo retira con Undo al terminar; el selector de variables se ejerce sobre un
    /// nodo del propio ejemplo, que ya trae un parámetro de texto libre. Sin el nodo añadido, la puerta del
    /// editor no existiría en el grafo de arranque y el sondeo mediría el vacío.</para>
    ///
    /// <para>Termina con el veredicto por el código de salida y por <c>selfcheck-dialogs-report.txt</c>.</para>
    /// </summary>
    public static int RunDialogsProbe(Window window, DispatcherQueue dispatcher)
    {
        new Thread(() =>
        {
            var report = new StringBuilder();
            bool ok = true;

            // Cada paso corre EN el hilo de UI y espera su asentamiento: pulsar sin dejar asentar el layout
            // mide un árbol que todavía no refleja la orden.
            bool Step(Func<bool> action)
            {
                bool result = false;
                var gate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        result = action();
                    }
                    catch (Exception ex)
                    {
                        // El marco que lanzó, no sólo el mensaje: una excepción del hilo de UI sin su traza
                        // obliga a adivinar qué lectura la provocó (y el informe es la única evidencia).
                        report.AppendLine("[excepción] " + ex.GetType().Name + ": " + ex.Message
                            + " | " + DescribeFrame(ex));
                    }
                    finally
                    {
                        gate.Set();
                    }
                });

                if (!gate.Wait(TimeSpan.FromSeconds(20)))
                {
                    return false;
                }

                Thread.Sleep(260);
                return result;
            }

            // Una LECTURA del hilo de UI (nunca un efecto): el estado del diálogo abierto y de sus controles
            // vive en el árbol, así que se lee donde se puede leer.
            T? Probe<T>(Func<T> read)
            {
                T? value = default;
                var gate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        value = read();
                    }
                    catch
                    {
                        // Sin valor: el llamador decide (una lectura fallida no es un veredicto).
                    }
                    finally
                    {
                        gate.Set();
                    }
                });

                gate.Wait(TimeSpan.FromSeconds(15));
                return value;
            }

            bool WaitUntil(Func<bool> condition, int milliseconds)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < milliseconds)
                {
                    if (Probe(condition))
                    {
                        return true;
                    }

                    Thread.Sleep(120);
                }

                return false;
            }

            // El veredicto se escribe TAMBIÉN conforme se mide (y no sólo al final): si el ciclo se cuelga en
            // un modal, el informe ya en disco dice por dónde iba — la única forma de diagnosticar un cuelgue
            // que no llega al final.
            void TryWrite()
            {
                try
                {
                    File.WriteAllText(
                        Path.Combine(AppContext.BaseDirectory, "selfcheck-dialogs-report.txt"), report.ToString());
                }
                catch
                {
                }
            }

            void Measure(string what)
            {
                report.AppendLine("       [medición] " + what);
                TryWrite();
            }

            void Check(bool condition, string what)
            {
                report.AppendLine((condition ? "[OK]   " : "[FALLO]") + " " + what);
                ok &= condition;
                TryWrite();
            }

            // El aviso de guardado del gestor es una capa DENTRO del modal con una sola salida («Aceptar»): el
            // usuario la pulsa, y la sonda también. Dejarla puesta tapaba el cuerpo —la orden siguiente no se
            // veía— y convertía el borrado en «no pasa nada», que es justo lo que no puede pasar.
            void DismissNotice()
            {
                WaitUntil(() => UnoWindowService.IsAskingInline, 3000);
                if (!Probe(() => UnoWindowService.IsAskingInline))
                {
                    return;
                }

                Step(() =>
                {
                    Button? accept = UnoWindowService.ActiveConfirmationAccept;
                    return accept is not null && UnoWindowService.Press(accept);
                });
                WaitUntil(() => !UnoWindowService.IsAskingInline, 5000);
            }

            try
            {
                report.AppendLine("=== Sondeo de los PANELES DE NODO del host Uno (hito 258) ===");
                TryWrite();

                EditorCanvasControl? canvas = null;
                NodeInspectorPanel? inspector = null;
                bool mounted = Step(() =>
                {
                    canvas = Find<EditorCanvasControl>(window.Content);
                    inspector = Find<NodeInspectorPanel>(window.Content);
                    return canvas?.Editor is not null && inspector is not null;
                });
                Check(mounted, "el lienzo y el inspector del host están montados en la ventana");

                int nodesBefore = Probe(() => canvas?.Editor?.Nodes.Count ?? -1);

                // ── 1. El SELECTOR DE VARIABLES sobre un nodo del propio ejemplo ──
                // El parámetro de texto libre es el que el escritorio manda al catálogo: su fila lleva el
                // botón «{x}» y su valor es un texto que el usuario compone.
                NodeViewModel? exampleNode = null;
                NodeParameterViewModel? exampleParam = null;
                bool exampleScene = Step(() =>
                {
                    foreach (NodeViewModel node in canvas!.Editor!.Nodes)
                    {
                        NodeParameterViewModel? plain = node.Parameters.FirstOrDefault(IsPlainTextRow);
                        if (plain is not null)
                        {
                            exampleNode = node;
                            exampleParam = plain;
                            inspector!.InspectForProbe(node);
                            return true;
                        }
                    }

                    return false;
                });
                Check(exampleScene,
                    "el ejemplo trae un parámetro de texto libre con la puerta del catálogo: nodo '"
                    + (exampleNode?.Title ?? "—") + "', parámetro '" + (exampleParam?.Key ?? "—") + "'");

                string exampleKey = exampleParam?.Key ?? string.Empty;
                string exampleOriginalBefore = exampleParam?.Value?.ToString() ?? string.Empty;
                string seedExamples = "jpg";

                // El valor de partida se escribe en la CAJA de la fila (el editor del inspector), no por el
                // view model: el catálogo tiene que escribir SOBRE lo que el usuario ya tiene en su campo, y
                // una cadena vacía no distingue «escribió el token» de «escribió cualquier cosa».
                bool seeded = Step(() =>
                {
                    TextBox? box = inspector!.ParameterControl("ParamBox_" + exampleKey) as TextBox;
                    if (box is null)
                    {
                        return false;
                    }

                    box.Text = seedExamples;
                    return string.Equals(box.Text, seedExamples, StringComparison.Ordinal);
                });
                // El valor se lee FUERA del paso: el TextChanged del cuadro se levanta de forma asíncrona, así
                // que leerlo en el mismo tick mide una carrera del sondeo y no el camino del usuario.
                string exampleOriginal = Probe(() => exampleParam?.Value?.ToString() ?? string.Empty) ?? string.Empty;
                Check(seeded && string.Equals(exampleOriginal, seedExamples, StringComparison.Ordinal),
                    "la caja de la fila escribe el valor de partida en el parámetro del nodo: '" + exampleOriginal + "'");

                bool pickerButtonExists = Step(() =>
                    inspector!.ParameterControl("ParamVariable_" + exampleKey) is not null);
                Check(pickerButtonExists,
                    "la fila del texto libre expone su botón «{x}» con su ancla (ParamVariable_" + exampleKey + ")");

                bool pickerPressed = Step(() =>
                {
                    Control? button = inspector!.ParameterControl("ParamVariable_" + exampleKey);
                    return button is not null && UnoWindowService.Press(button);
                });
                bool pickerOpen = WaitUntil(() => UnoWindowService.ActivePicker is not null, 9000);
                Check(pickerPressed && pickerOpen,
                    "pulsar «{x}» abre el CATÁLOGO DE VARIABLES del host (el diálogo está abierto)");

                int catalogSize = Probe(() => UnoWindowService.ActivePicker?.Vm?.FilteredVariables.Count ?? -1);
                string pickerTitle = Probe(() => UnoWindowService.ActiveDialog?.Title?.ToString() ?? string.Empty);
                string pickerListId = Probe(() => UnoWindowService.ActivePicker is { } body
                    ? Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(body.Variables)
                    : string.Empty);
                Check(catalogSize > 0,
                    "el catálogo abre POBLADO desde el descubrimiento del núcleo: " + catalogSize + " variables");
                Check(pickerTitle.Length > 0 && pickerListId == "VariablePickerList",
                    "el diálogo lleva su título del diccionario del host ('" + pickerTitle
                    + "') y su lista su AutomationId ('" + pickerListId + "')");

                // El buscador: se teclea en su cuadro y se sale de él — es el gesto del usuario (el cuadro
                // del modal es el único punto por el que este host escribe el filtro).
                bool filtered = Step(() =>
                {
                    VariablePickerDialogBody? body = UnoWindowService.ActivePicker;
                    TextBox? search = body?.Search;
                    if (body is null || search is null)
                    {
                        return false;
                    }

                    search.Text = "Guid";
                    body.Variables.Focus(FocusState.Programmatic);
                    return true;
                });
                string filterText = Probe(() => UnoWindowService.ActivePicker?.Vm?.SearchText) ?? "";
                Measure("buscador del catálogo: 'Guid' escrito en su cuadro → SearchText del view model='" + filterText + "'");
                bool filterApplied = WaitUntil(() => UnoWindowService.ActivePicker?.Vm?.FilteredVariables.Count is > 0
                                                     && UnoWindowService.ActivePicker.Vm.FilteredVariables.Count < catalogSize, 5000);
                int filteredCount = Probe(() => UnoWindowService.ActivePicker?.Vm?.FilteredVariables.Count ?? -1);
                Check(filtered && filterApplied && filteredCount > 0 && filteredCount < catalogSize,
                    "la caja de búsqueda del catálogo filtra en caliente: " + catalogSize + " -> " + filteredCount);

                // La fila se selecciona por el CONTROL (el mismo cambio que hace el clic del usuario) y el
                // token tiene que llegar al view model portable y a su panel de detalle. El token elegido es
                // el de un nombre conocido: un «el primero de la lista» haría depender la medición del orden.
                const string knownToken = "{FileName}";
                string chosenToken = string.Empty;
                bool selected = Step(() =>
                {
                    VariablePickerDialogBody? body = UnoWindowService.ActivePicker;
                    if (body?.Vm is not { } picker)
                    {
                        return false;
                    }

                    // El filtro vuelve a su sitio por el mismo setter que usó el cuadro.
                    picker.SearchText = string.Empty;
                    VariableItem? target = picker.FilteredVariables
                        .FirstOrDefault(v => string.Equals(v.Token, knownToken, StringComparison.Ordinal));
                    if (target is null)
                    {
                        return false;
                    }

                    body.Variables.SelectedItem = target;
                    chosenToken = target.Token;
                    return true;
                });
                bool detailLive = WaitUntil(() =>
                    string.Equals(UnoWindowService.ActivePicker?.DetailTokenText, chosenToken, StringComparison.Ordinal), 4000);
                bool insertEnabled = Probe(() => UnoWindowService.ActivePrimaryButton?.IsEnabled ?? false);
                Check(selected && detailLive && insertEnabled,
                    "seleccionar la fila de '" + knownToken + "' la escribe en el detalle ('" + chosenToken
                    + "') y habilita «Insertar Variable»");

                Measure("antes de insertar: SearchText='" + (Probe(() => UnoWindowService.ActivePicker?.Vm?.SearchText) ?? "")
                     + "' SelectedToken='" + (Probe(() => UnoWindowService.ActivePicker?.Vm?.SelectedToken) ?? "") + "'");
                bool insertPressed = Step(() =>
                {
                    Button? primary = UnoWindowService.ActivePrimaryButton;
                    return primary is not null && UnoWindowService.Press(primary);
                });
                bool pickerClosed = WaitUntil(() => UnoWindowService.ActiveDialog is null, 9000);
                string expectedValue = exampleOriginal + chosenToken;
                bool pickerWrote = WaitUntil(() =>
                    string.Equals(exampleParam?.Value?.ToString(), expectedValue, StringComparison.Ordinal), 9000);
                Check(insertPressed && pickerClosed && pickerWrote,
                    "«Insertar Variable» escribe el token ELEGIDO en el parámetro del nodo: '"
                    + Truncate(exampleOriginal) + "' -> '" + Truncate(exampleParam?.Value?.ToString() ?? "") + "'");

                Step(() =>
                {
                    if (exampleParam is not null)
                    {
                        exampleParam.Value = exampleOriginalBefore;
                    }

                    return true;
                });

                // ── 2. El EDITOR DE TEXTO sobre un nodo que el propio sondeo añade ──
                string editorType = "LogOutputNode";
                NodeViewModel? added = null;
                bool addedOk = Step(() =>
                {
                    added = canvas!.Editor!.AddNode(editorType, new Point(120, 260));
                    return added is not null;
                });
                int nodesAfter = Probe(() => canvas?.Editor?.Nodes.Count ?? -1);
                Check(addedOk && nodesAfter == nodesBefore + 1,
                    "el sondeo añade un nodo con editor de texto por el mismo camino que el cajón ("
                    + editorType + ": " + nodesBefore + " -> " + nodesAfter + " nodos)");

                NodeParameterViewModel? longParam = null;
                NodeParameterViewModel? nodeVariableParam = null;
                bool addedScene = Step(() =>
                {
                    if (added is null)
                    {
                        return false;
                    }

                    longParam = added.Parameters.FirstOrDefault(p => p.IsMultiLine);
                    // La fila que lleva el botón «{x}» en este nodo: un texto libre si lo hay y, si no, el
                    // propio texto largo (que también lo lleva, como en la ficha del escritorio).
                    nodeVariableParam = added.Parameters.FirstOrDefault(IsPlainTextRow) ?? longParam;
                    inspector!.InspectForProbe(added);
                    return longParam is not null && nodeVariableParam is not null;
                });
                Check(addedScene,
                    "el nodo añadido trae los parámetros de la puerta: texto largo ('"
                    + (longParam?.Key ?? "—") + "') y su fila de variables ('" + (nodeVariableParam?.Key ?? "—") + "')");

                string longKey = longParam?.Key ?? string.Empty;
                string newNodeVariableKey = nodeVariableParam?.Key ?? string.Empty;

                bool rowAnchors = Step(() =>
                    inspector!.ParameterControl("ParamEditor_" + longKey) is not null
                    && inspector.ParameterControl("ParamVariable_" + longKey) is not null
                    && inspector.ParameterControl("ParamVariable_" + newNodeVariableKey) is not null
                    && inspector.ParameterControl("ParamBox_" + longKey) is not null);
                Check(rowAnchors,
                    "las filas del inspector exponen sus anclas de acción (ParamEditor_" + longKey
                    + ", ParamVariable_" + longKey + ", ParamVariable_" + newNodeVariableKey + ")");

                // El texto del editor se escribe antes en la CAJA de la fila: así la semilla que el diálogo
                // enseña es un valor del usuario y no una cadena vacía (que no probaría nada).
                string seedLong = "[sondeo 258] ";
                bool seededRow = Step(() =>
                {
                    TextBox? box = inspector!.ParameterControl("ParamBox_" + longKey) as TextBox;
                    if (box is null)
                    {
                        return false;
                    }

                    box.Text = seedLong;
                    return string.Equals(box.Text, seedLong, StringComparison.Ordinal);
                });
                bool rowValueWritten = string.Equals(
                    Probe(() => longParam?.Value?.ToString() ?? string.Empty), seedLong, StringComparison.Ordinal);
                Check(seededRow && rowValueWritten,
                    "la caja de la fila del texto largo escribe su valor en el parámetro: '" + seedLong.Trim() + "'");

                bool editorPressed = Step(() =>
                {
                    Control? button = inspector!.ParameterControl("ParamEditor_" + longKey);
                    return button is not null && UnoWindowService.Press(button);
                });
                bool editorOpen = WaitUntil(() => UnoWindowService.ActiveEditor is not null, 9000);
                Check(editorPressed && editorOpen,
                    "pulsar «✎» en la fila del texto largo abre el EDITOR DE TEXTO del host");

                string editorSeed = Probe(() => UnoWindowService.ActiveEditor?.Editor.Text ?? string.Empty);
                string editorBoxId = Probe(() => UnoWindowService.ActiveEditor is { } body
                    ? Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(body.Editor)
                    : string.Empty);
                string editorTitle = Probe(() => UnoWindowService.ActiveDialog?.Title?.ToString() ?? string.Empty);
                Check(string.Equals(editorSeed, seedLong, StringComparison.Ordinal),
                    "el editor abre con el VALOR de la fila, no vacío: '" + Truncate(editorSeed) + "'");
                Check(editorBoxId == "TextEditorBox" && editorTitle.Length > 0,
                    "la caja del editor canta su AutomationId ('" + editorBoxId + "') y el modal su título ('"
                    + editorTitle + "')");

                // El panel de variables del propio editor (el camino del escritorio dentro del modal).
                bool panelPressed = Step(() =>
                {
                    Button? insert = UnoWindowService.ActiveEditor?.InsertVariable;
                    return insert is not null && UnoWindowService.Press(insert);
                });
                bool panelOpen = WaitUntil(() => UnoWindowService.ActiveEditor?.IsVariablePanelOpen == true, 5000);
                int sideCount = Probe(() => UnoWindowService.ActiveEditor?.Variables.Items.Count ?? -1);
                Check(panelPressed && panelOpen && sideCount > 0,
                    "«Insertar Variable» del editor despliega su panel con el catálogo del view model ("
                    + sideCount + " variables)");

                string newText = seedLong + "aplicado";
                bool typed = Step(() =>
                {
                    TextBox? box = UnoWindowService.ActiveEditor?.Editor;
                    if (box is null)
                    {
                        return false;
                    }

                    box.Text = newText;
                    return box.Text == newText;
                });
                bool savePressed = Step(() =>
                {
                    Button? primary = UnoWindowService.ActivePrimaryButton;
                    return primary is not null && UnoWindowService.Press(primary);
                });
                bool editorClosed = WaitUntil(() => UnoWindowService.ActiveDialog is null, 9000);
                bool editorWrote = WaitUntil(() =>
                    string.Equals(longParam?.Value?.ToString(), newText, StringComparison.Ordinal), 9000);
                Check(typed && savePressed && editorClosed && editorWrote,
                    "«Guardar y Aplicar» escribe el texto en el parámetro del nodo: '" + Truncate(newText) + "'");

                // ── 2.b El catálogo, ahora sobre el nodo AÑADIDO (el mismo camino, otro nodo) ──
                string secondToken = string.Empty;
                string secondOriginal = nodeVariableParam?.Value?.ToString() ?? string.Empty;
                bool secondPressed = Step(() =>
                {
                    Control? button = inspector!.ParameterControl("ParamVariable_" + newNodeVariableKey);
                    return button is not null && UnoWindowService.Press(button);
                });
                bool secondOpen = WaitUntil(() => UnoWindowService.ActivePicker is not null, 9000);
                bool secondChosen = secondOpen && Step(() =>
                {
                    VariablePickerDialogBody? body = UnoWindowService.ActivePicker;
                    if (body?.Vm is not { } picker)
                    {
                        return false;
                    }

                    picker.SearchText = string.Empty;
                    VariableItem? first = picker.FilteredVariables.FirstOrDefault();
                    if (first is null)
                    {
                        return false;
                    }

                    body.Variables.SelectedItem = first;
                    secondToken = first.Token;
                    return true;
                });
                bool secondInserted = secondChosen && Step(() =>
                {
                    Button? primary = UnoWindowService.ActivePrimaryButton;
                    return primary is not null && UnoWindowService.Press(primary);
                });
                string secondExpected = secondOriginal + secondToken;
                bool secondWrote = WaitUntil(() =>
                    string.Equals(nodeVariableParam?.Value?.ToString(), secondExpected, StringComparison.Ordinal), 9000);
                Check(secondPressed && secondOpen && secondInserted && secondWrote,
                    "y sobre el nodo añadido ('" + newNodeVariableKey + "'): '" + Truncate(secondOriginal)
                    + "' -> '" + Truncate(nodeVariableParam?.Value?.ToString() ?? string.Empty) + "'");

                // ── 3. El GESTOR DE PRESETS sobre el nodo de transcodificación ──
                // La MISMA puerta del escritorio: el botón «🎬» de la fila del preset. Lo que se mide no es
                // que se abra una superficie, sino que la EDICIÓN quede escrita en el almacén que lee el nodo
                // que transcodifica —y que el view model sea el mismo que pinta el escritorio—.
                const string transcoderType = "MediaTranscoderNode";
                const string presetDescription = "Sondeo de los paneles de nodo: descripción editada en el gestor";

                var presetStore = FileFlow.Plugin.Integrations.UI.Services.MediaPresetManagerService.Instance;
                string presetStoreFile = FileFlow.Sdk.Storage.AppPaths.MediaPresetsFile;
                int presetsBefore = Probe(() => presetStore.GetPresets().Count);
                string presetStoreBefore = File.Exists(presetStoreFile) ? File.ReadAllText(presetStoreFile) : string.Empty;

                NodeViewModel? transcoder = null;
                int nodesAtPresets = Probe(() => canvas?.Editor?.Nodes.Count ?? -1);
                bool transcoderAdded = Step(() =>
                {
                    transcoder = canvas!.Editor!.AddNode(transcoderType, new Point(320, 260));
                    return transcoder is not null;
                });
                int nodesAfterTranscoder = Probe(() => canvas?.Editor?.Nodes.Count ?? -1);
                Check(transcoderAdded && nodesAfterTranscoder == nodesAtPresets + 1,
                    "el sondeo añade el nodo del gestor de presets por el mismo camino que el cajón ("
                    + transcoderType + ": " + nodesAtPresets + " -> " + nodesAfterTranscoder + " nodos)");

                NodeParameterViewModel? presetParam = null;
                bool presetScene = Step(() =>
                {
                    if (transcoder is null)
                    {
                        return false;
                    }

                    presetParam = transcoder.Parameters.FirstOrDefault(p => p.IsMediaPreset);
                    if (presetParam is null)
                    {
                        return false;
                    }

                    inspector!.InspectForProbe(transcoder);
                    return true;
                });
                Check(presetScene,
                    "el nodo de transcodificación expone su fila de preset ('" + (presetParam?.Key ?? "—") + "')");

                bool presetAnchor = Step(() =>
                    inspector!.ParameterControl("ParamPreset_" + (presetParam?.Key ?? string.Empty)) is not null);
                Check(presetAnchor,
                    "la fila del preset expone su botón «🎬» con su ancla (ParamPreset_" + (presetParam?.Key ?? "—") + ")");

                // La ACCIÓN del nodo en la ficha (hito 269): la misma puerta que el botón de la tarjeta, ahora
                // también en el inspector. Se mide sobre un nodo que SÍ declara acciones y por su ancla: la
                // superficie del nodo no puede depender de que el usuario sepa desplegar una tarjeta del lienzo.
                bool actionPainted = Step(() => transcoder is not null
                    && inspector!.ActionButtonCount == transcoder.CustomActions.Count
                    && inspector.ActionControl("ManageMediaPresets") is not null);
                Check(actionPainted,
                    "la ficha del inspector pinta las acciones del nodo ("
                    + (transcoder?.CustomActions.Count ?? 0) + " botón(es), ancla 'InspectorAction_ManageMediaPresets')");

                bool presetPressed = Step(() =>
                {
                    Control? button = inspector!.ParameterControl("ParamPreset_" + (presetParam?.Key ?? string.Empty));
                    return button is not null && UnoWindowService.Press(button);
                });
                bool presetUp = presetPressed && WaitUntil(() =>
                    string.Equals(UnoWindowService.ActiveWindowKey, DialogKeys.MediaPresetManager, StringComparison.Ordinal), 9000);
                // El cuerpo se lee SIEMPRE por el despachador (Probe/Step): su `Content` es un objeto COM del
                // hilo de UI y leerlo desde el hilo del sondeo levanta RPC_E_WRONG_THREAD.
                bool presetBodyUp = Probe(() => UnoWindowService.ActivePresetManager is not null);
                Check(presetUp && presetBodyUp,
                    "el botón «🎬» de la fila abre el gestor en la superficie de ESTE host (clave del catálogo '"
                    + DialogKeys.MediaPresetManager + "', no la ventana del toolkit)");

                int shownPresets = Probe(() => UnoWindowService.ActivePresetManager?.Vm.Presets.Count ?? -1);
                int listedPresets = Probe(() => UnoWindowService.ActivePresetManager?.PresetRows.Items.Count ?? -1);
                Check(shownPresets == presetsBefore && listedPresets == presetsBefore && presetsBefore > 0,
                    "la lista del gestor pinta el catálogo del ALMACÉN: " + presetsBefore + " preset(s) en el almacén, "
                    + shownPresets + " en el view model portable y " + listedPresets + " fila(s) en la lista");

                string editedPresetId = Probe(() => UnoWindowService.ActivePresetManager?.Vm.SelectedPreset?.Id) ?? string.Empty;
                string descriptionBefore = Probe(() => presetStore.GetPresets()
                    .FirstOrDefault(p => p.Id == editedPresetId)?.Description) ?? string.Empty;

                // El valor se escribe en la CAJA real (no por el view model): lo que se mide es el camino del
                // usuario —cuadro, enlace, view model, almacén—, y una escritura directa al view model se
                // saltaría justo la mitad que puede estar rota.
                bool presetTyped = Step(() =>
                {
                    TextBox? box = UnoWindowService.ActivePresetManager?.DescriptionEditor;
                    if (box is null)
                    {
                        return false;
                    }

                    box.Text = presetDescription;
                    return true;
                });
                bool typedInModel = WaitUntil(() => string.Equals(
                    UnoWindowService.ActivePresetManager?.Vm.PresetDescription, presetDescription, StringComparison.Ordinal), 5000);
                Check(presetTyped && typedInModel,
                    "la caja de la descripción escribe en el view model portable del gestor ('"
                    + Truncate(descriptionBefore) + "' -> '" + Truncate(presetDescription) + "')");

                bool saved = Step(() =>
                {
                    Button? save = UnoWindowService.ActivePresetManager?.SaveAction;
                    return save is not null && UnoWindowService.Press(save);
                });
                DismissNotice();
                bool storeWrote = saved && WaitUntil(() => string.Equals(
                    presetStore.GetPresets().FirstOrDefault(p => p.Id == editedPresetId)?.Description,
                    presetDescription, StringComparison.Ordinal), 9000);
                Measure("almacén tras guardar: '" + Truncate(presetStore.GetPresets()
                    .FirstOrDefault(p => p.Id == editedPresetId)?.Description ?? "—") + "'");
                // El fichero se lee como JSON, no como texto: el serializador ESCAPA los acentos (`\u00F3`, con
                // hex en mayúsculas) y comparar contra lo que produce otro serializador medía el escapado, no el
                // valor guardado (medido: el fichero traía el texto y la comprobación decía que no).
                bool fileWrote = WaitUntil(() => PresetDescriptionInFile(presetStoreFile, editedPresetId, presetDescription), 9000);
                Check(storeWrote && fileWrote,
                    "«Guardar» escribe la descripción en el ALMACÉN que lee el nodo que transcodifica y en su fichero "
                    + "(preset '" + Truncate(editedPresetId) + "')");

                // ── La vuelta: el valor del usuario se restaura por el mismo camino ──
                bool restoredTyped = Step(() =>
                {
                    TextBox? box = UnoWindowService.ActivePresetManager?.DescriptionEditor;
                    if (box is null)
                    {
                        return false;
                    }

                    box.Text = descriptionBefore;
                    return true;
                });
                bool restoredInModel = WaitUntil(() => string.Equals(
                    UnoWindowService.ActivePresetManager?.Vm.PresetDescription, descriptionBefore, StringComparison.Ordinal), 5000);
                bool restoredSaved = Step(() =>
                {
                    Button? save = UnoWindowService.ActivePresetManager?.SaveAction;
                    return save is not null && UnoWindowService.Press(save);
                });
                DismissNotice();
                bool restored = restoredTyped && restoredInModel && restoredSaved && WaitUntil(() => string.Equals(
                    presetStore.GetPresets().FirstOrDefault(p => p.Id == editedPresetId)?.Description,
                    descriptionBefore, StringComparison.Ordinal), 9000);

                // ── 3b. La CONFIRMACIÓN de la orden destructiva (hito 263) ──
                // La misma orden y el mismo camino, pero mirando lo que de verdad decide: la pregunta tiene que
                // SALIR, el «no» no puede borrar y el «sí» tiene que borrar. Antes de este tramo, la puerta de
                // la fila borraba sin preguntar (el servicio Nulo contesta «sí») y la de la tarjeta no borraba
                // ni avisaba (la confirmación síncrona devuelve «no» desde el hilo de UI): dos comportamientos
                // para una sola regla, y ninguno preguntaba.
                // El alta se mide por SU PRESET (el que el gestor deja elegido y guarda en el almacén), no por lo
                // que crezca el catálogo: el almacén reemplaza por NOMBRE, así que un preset de una corrida
                // anterior con el nombre de fábrica se sustituye y el recuento no crece (medido).
                bool presetCreated = Step(() =>
                {
                    Button? create = UnoWindowService.ActivePresetManager?.NewAction;
                    return create is not null && UnoWindowService.Press(create);
                });
                string createdId = Probe(() => UnoWindowService.ActivePresetManager?.Vm.SelectedPreset?.Id) ?? string.Empty;
                bool createdPresent = presetCreated && WaitUntil(() =>
                    UnoWindowService.ActivePresetManager?.Vm.SelectedPreset is { IsSystemDefault: false } chosen
                    && presetStore.GetPresets().Any(p => p.Id == chosen.Id), 9000);
                Check(createdPresent, "la sonda da de alta un preset PROPIO en el almacén para poder preguntar por su "
                    + "borrado (id '" + Truncate(createdId) + "', " + Probe(() => presetStore.GetPresets().Count)
                    + " presets en el catálogo)");

                bool deleteAsked = Step(() =>
                {
                    Button? remove = UnoWindowService.ActivePresetManager?.DeleteAction;
                    return remove is not null && UnoWindowService.Press(remove);
                });
                bool questionUp = deleteAsked && WaitUntil(() => UnoWindowService.IsAskingInline, 8000);
                Check(questionUp,
                    "«Eliminar» PREGUNTA antes de destruir: la pregunta está en pantalla DENTRO del modal abierto "
                    + "(un segundo ContentDialog no cabe en WinUI) y el cuerpo sigue debajo");
                Check(Probe(() => UnoWindowService.ActivePresetManager is not null),
                    "y mientras se pregunta, el gestor sigue siendo el mismo cuerpo (la capa no lo sustituye)");

                Thread.Sleep(300);
                Check(Probe(() => presetStore.GetPresets().Any(p => p.Id == createdId)),
                    "y con la pregunta en pantalla NO se ha borrado nada todavía");

                bool cancelPressed = Step(() =>
                {
                    Button? cancel = UnoWindowService.ActiveConfirmationCancel;
                    return cancel is not null && UnoWindowService.Press(cancel);
                });
                bool cancelled = cancelPressed && WaitUntil(() => !UnoWindowService.IsAskingInline, 5000);
                Check(cancelled, "la pregunta se contesta por su botón de cancelar (el control real)");
                Thread.Sleep(300);
                Check(Probe(() => presetStore.GetPresets().Any(p => p.Id == createdId)),
                    "un «no» NO borra: el preset sigue en el catálogo del almacén");

                bool deleteAgain = Step(() =>
                {
                    Button? remove = UnoWindowService.ActivePresetManager?.DeleteAction;
                    return remove is not null && UnoWindowService.Press(remove);
                });
                bool questionAgain = deleteAgain && WaitUntil(() => UnoWindowService.IsAskingInline, 8000);
                bool acceptPressed = questionAgain && Step(() =>
                {
                    Button? accept = UnoWindowService.ActiveConfirmationAccept;
                    return accept is not null && UnoWindowService.Press(accept);
                });
                bool confirmed = acceptPressed && WaitUntil(() => !UnoWindowService.IsAskingInline, 5000)
                    && WaitUntil(() => presetStore.GetPresets().All(p => p.Id != createdId), 9000);
                Check(confirmed, "y un «sí» SÍ borra: el preset dado de alta ya no está en el almacén ("
                    + Probe(() => presetStore.GetPresets().Count) + " presets)");

                // La OTRA orden destructiva del gestor —«Restablecer» vacía el catálogo del usuario— pregunta
                // igual. La sonda la CANCELA: medir no es configurar.
                int presetsBeforeReset = Probe(() => presetStore.GetPresets().Count);
                string catalogBeforeReset = Probe(() => string.Join("|", presetStore.GetPresets().Select(p => p.Id)));
                bool resetPressed = Step(() =>
                {
                    Button? reset = UnoWindowService.ActivePresetManager?.ResetAction;
                    return reset is not null && UnoWindowService.Press(reset);
                });
                bool resetAsked = resetPressed && WaitUntil(() => UnoWindowService.IsAskingInline, 8000);
                bool resetCancelled = resetAsked && Step(() =>
                {
                    Button? cancel = UnoWindowService.ActiveConfirmationCancel;
                    return cancel is not null && UnoWindowService.Press(cancel);
                }) && WaitUntil(() => !UnoWindowService.IsAskingInline, 5000);
                Thread.Sleep(300);
                Check(resetCancelled && Probe(() => presetStore.GetPresets().Count) == presetsBeforeReset
                        && Probe(() => string.Join("|", presetStore.GetPresets().Select(p => p.Id))) == catalogBeforeReset,
                    "y «Restablecer» pregunta igual: un «no» deja el catálogo del usuario intacto ("
                    + presetsBeforeReset + " presets, los mismos)");

                bool presetClosed = Step(() =>
                {
                    Button? close = UnoWindowService.FindByName<Button>(UnoWindowService.ActiveDialog, "CloseButton");
                    return close is not null && UnoWindowService.Press(close);
                });
                bool presetGone = presetClosed && WaitUntil(() =>
                    UnoWindowService.ActiveDialog is null && UnoWindowService.ActiveWindowKey is null, 9000);
                Check(presetGone, "y cerrar el gestor deja al host sin ninguna superficie abierta");

                // ── 3c. La MISMA orden destructiva por la OTRA puerta: la TARJETA del nodo ──
                // Hasta este tramo, las dos puertas no se comportaban igual: la de la fila borraba sin preguntar
                // (el servicio Nulo contestaba «sí») y la de la tarjeta no borraba ni avisaba (la confirmación
                // síncrona devuelve «no» desde el hilo de UI). Se mide la tarjeta con su BOTÓN real —la acción
                // personalizada del nodo, que vive en el panel de acciones rápidas y cuelga de IsExpanded—.
                bool cardExpandedForAction = Step(() =>
                {
                    if (transcoder is null)
                    {
                        return false;
                    }

                    NodeCardView? card = FindAll<NodeCardView>(window.Content)
                        .FirstOrDefault(c => ReferenceEquals((c.DataContext as NodeCardViewModel)?.Node, transcoder));
                    if (card?.FindName("ParametersToggle") is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton toggle)
                    {
                        toggle.IsChecked = true;
                        return true;
                    }

                    return false;
                });
                bool cardExpandedState = cardExpandedForAction && Step(() =>
                {
                    NodeCardView? card = FindAll<NodeCardView>(window.Content)
                        .FirstOrDefault(c => ReferenceEquals((c.DataContext as NodeCardViewModel)?.Node, transcoder));
                    if (card?.DataContext is not NodeCardViewModel doorVm)
                    {
                        return false;
                    }

                    var panel = card.FindName("ParametersPanel") as Border;
                    var chevron = card.FindName("ParametersIcon") as Microsoft.UI.Xaml.Shapes.Path;

                    // La puerta de un nodo CON acciones: el conmutador escribe el estado en el NÚCLEO y el panel
                    // se materializa con él, con su chevron resuelto (la misma medida del 263, sobre la única
                    // tarjeta que despliega algo). La tarjeta se queda DESPLEGADA a propósito: la medida que
                    // sigue pulsa el botón que vive dentro de ese panel.
                    return doorVm.Node.IsExpanded
                        && panel?.Visibility == Visibility.Visible
                        && chevron?.Data is not null && chevron.Data.Bounds.Width > 0;
                });
                Check(cardExpandedState,
                    "el conmutador escribe el estado en el núcleo y despliega el panel de la tarjeta con su chevron "
                    + "(la geometría resuelta), que es donde vive el botón de la acción");

                bool cardActionPressed = cardExpandedForAction && Step(() =>
                {
                    NodeCardView? card = FindAll<NodeCardView>(window.Content)
                        .FirstOrDefault(c => ReferenceEquals((c.DataContext as NodeCardViewModel)?.Node, transcoder));
                    if (card is null)
                    {
                        return false;
                    }

                    Button? action = FindAll<Button>(card)
                        .FirstOrDefault(b => b.Content is string title
                            && title.Contains("Preset", StringComparison.OrdinalIgnoreCase));
                    return action is not null && UnoWindowService.Press(action);
                });
                bool cardManagerUp = cardActionPressed && WaitUntil(() =>
                    string.Equals(UnoWindowService.ActiveWindowKey, DialogKeys.MediaPresetManager, StringComparison.Ordinal)
                    && UnoWindowService.ActivePresetManager is not null, 9000);
                Check(cardManagerUp,
                    "la TARJETA del nodo —su botón de acción, en el panel que despliega su conmutador— abre el mismo "
                    + "gestor (clave '" + DialogKeys.MediaPresetManager + "')");

                bool cardCreated = Step(() =>
                {
                    Button? create = UnoWindowService.ActivePresetManager?.NewAction;
                    return create is not null && UnoWindowService.Press(create);
                });
                string cardCreatedId = Probe(() => UnoWindowService.ActivePresetManager?.Vm.SelectedPreset?.Id) ?? string.Empty;
                bool cardGrew = cardCreated && WaitUntil(() =>
                    UnoWindowService.ActivePresetManager?.Vm.SelectedPreset is { IsSystemDefault: false } cardChosen
                    && presetStore.GetPresets().Any(p => p.Id == cardChosen.Id), 9000);
                Check(cardGrew, "(por la tarjeta) se da de alta un preset propio para poder preguntar por su borrado "
                    + "(id '" + Truncate(cardCreatedId) + "')");

                bool cardDeletePressed = Step(() =>
                {
                    Button? remove = UnoWindowService.ActivePresetManager?.DeleteAction;
                    return remove is not null && UnoWindowService.Press(remove);
                });
                bool cardAsked = cardDeletePressed && WaitUntil(() => UnoWindowService.IsAskingInline, 8000);
                bool cardCancelPressed = cardAsked && Step(() =>
                {
                    Button? cancel = UnoWindowService.ActiveConfirmationCancel;
                    return cancel is not null && UnoWindowService.Press(cancel);
                });
                bool cardCancelled = cardCancelPressed && WaitUntil(() => !UnoWindowService.IsAskingInline, 5000);
                Thread.Sleep(300);
                Check(cardCancelled && Probe(() => presetStore.GetPresets().Any(p => p.Id == cardCreatedId)),
                    "por la TARJETA, «Eliminar» también PREGUNTA y un «no» tampoco borra: la misma respuesta manda "
                    + "en las dos puertas");

                bool cardDeleteAgain = Step(() =>
                {
                    Button? remove = UnoWindowService.ActivePresetManager?.DeleteAction;
                    return remove is not null && UnoWindowService.Press(remove);
                });
                bool cardAskedAgain = cardDeleteAgain && WaitUntil(() => UnoWindowService.IsAskingInline, 8000);
                bool cardAcceptPressed = cardAskedAgain && Step(() =>
                {
                    Button? accept = UnoWindowService.ActiveConfirmationAccept;
                    return accept is not null && UnoWindowService.Press(accept);
                });
                bool cardConfirmed = cardAcceptPressed && WaitUntil(() => !UnoWindowService.IsAskingInline, 5000)
                    && WaitUntil(() => presetStore.GetPresets().All(p => p.Id != cardCreatedId), 9000);
                Check(cardConfirmed, "y un «sí» por la tarjeta borra de verdad: el preset se va del almacén ("
                    + Probe(() => presetStore.GetPresets().Count) + " presets)");

                bool cardClosed = Step(() =>
                {
                    Button? close = UnoWindowService.FindByName<Button>(UnoWindowService.ActiveDialog, "CloseButton");
                    return close is not null && UnoWindowService.Press(close);
                });
                bool cardGone = cardClosed && WaitUntil(() =>
                    UnoWindowService.ActiveDialog is null && UnoWindowService.ActiveWindowKey is null, 9000);
                Check(cardGone, "y el gestor abierto por la tarjeta se cierra como el de la fila");

                string presetStoreAfter = File.Exists(presetStoreFile) ? File.ReadAllText(presetStoreFile) : string.Empty;
                Check(restored && presetStoreAfter == presetStoreBefore,
                    "el catálogo de presets del usuario queda como estaba, byte a byte ("
                    + presetStoreAfter.Length + " bytes)");

                // ── 4. La FRONTERA: lo que este host no sirve queda declarado, no en silencio ──
                IWindowService? windows = Probe(() =>
                    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                        .GetService<IWindowService>(App.Services));
                UnoWindowService.ClearDeclined();
                bool asked = Step(() =>
                {
                    if (windows is null)
                    {
                        return false;
                    }

                    _ = windows.ShowDialogAsync(DialogKeys.About);
                    return true;
                });
                bool declined = asked && WaitUntil(() => UnoWindowService.DeclinedDialogs.Count > 0, 5000);
                Check(declined,
                    "un diálogo que este host no sirve queda DECLARADO en su traza: '"
                    + (UnoWindowService.DeclinedDialogs.FirstOrDefault() ?? "—") + "'");

                int allKeys = UnoWindowService.AllDialogKeys().Count;
                Check(UnoWindowService.ImplementedDialogs.Length + UnoWindowService.DeclaredPendingDialogs.Length == allKeys,
                    "el censo de diálogos cubre las " + allKeys + " claves de DialogKeys ("
                    + UnoWindowService.ImplementedDialogs.Length + " servidas + "
                    + UnoWindowService.DeclaredPendingDialogs.Length + " declaradas)");

                // ── 4b. La frontera del ESCRITORIO en el botón de un nodo (hito 270) ──
                // Un nodo con ventana del toolkit no puede montarla aquí: la DECLARA (hito 268) por los diálogos
                // de quien lo abrió. Lo que se mide es que el aviso LLEGUE de verdad —el contexto del botón iba
                // sin el servicio, así que la frontera se quedaba en una traza de consola y el usuario pulsaba un
                // botón que no hacía nada y no avisaba— y que la escena quede limpia al retirarlo.
                const string desktopOnlyType = "CustomScriptNode";
                const string desktopOnlyAction = "OpenScriptStudio";
                NodeViewModel? desktopOnlyNode = null;
                bool desktopOnlyAdded = Step(() =>
                {
                    desktopOnlyNode = canvas!.Editor!.AddNode(desktopOnlyType, new Point(120, 260));
                    return desktopOnlyNode is not null;
                });
                bool desktopOnlyAnchored = desktopOnlyAdded && Step(() =>
                {
                    inspector!.InspectForProbe(desktopOnlyNode!);
                    return inspector.ActionControl(desktopOnlyAction) is not null;
                });
                Check(desktopOnlyAnchored,
                    "el nodo con ventana del ESCRITORIO declara su acción y la ficha la pinta (ancla '"
                    + "InspectorAction_" + desktopOnlyAction + "')");

                // El nombre de la ventana que el aviso tiene que nombrar lo pone el diccionario del plugin (el
                // host no escribe las palabras): la sonda lo lee en vez de fijar un literal de un idioma.
                string surfaceName = Probe(() => FileFlow.Sdk.Localization.LocalizationManager.Instance.GetString(
                    "ScriptStudio_Title", "Estudio de Scripts"));
                bool desktopOnlyPressed = desktopOnlyAnchored && Step(() =>
                    UnoWindowService.Press(inspector!.ActionControl(desktopOnlyAction)!));

                // La espera y la lectura van SEPARADAS a propósito: `WaitUntil` ya envuelve la condición en
                // `Probe`, así que anidar un `Probe` dentro de él deja al hilo de UI esperándose a sí mismo
                // (la lectura interior sólo corre cuando la exterior suelta el hilo, o sea tarde) y devuelve
                // un «no» falso —medido al escribir esta misma sonda—.
                bool warned = desktopOnlyPressed
                    && WaitUntil(() => UnoWindowService.ActiveDialog?.Content is string, 9000);
                string warningText = Probe(() => UnoWindowService.ActiveDialog?.Content as string) ?? string.Empty;
                Check(warned && warningText.Contains(surfaceName, StringComparison.Ordinal),
                    "su botón AVISA en la superficie de este host, nombrando la ventana que falta ('"
                    + Truncate(warningText) + "')");

                bool warningDismissed = warned && Step(() =>
                {
                    UnoWindowService.ActiveDialog?.Hide();
                    return true;
                });
                bool warningGone = warningDismissed && WaitUntil(() =>
                    UnoWindowService.ActiveDialog is null && UnoWindowService.ActiveWindowKey is null, 8000);
                Check(warningGone,
                    "y el aviso se retira como cualquier modal del host (no queda un diálogo abierto tapando la "
                    + "escena que sigue)");

                // ── 5. La escena vuelve a como estaba ──
                bool graphRestored = Step(() =>
                {
                    for (int i = 0; i < 8 && canvas!.Editor!.Nodes.Count > nodesBefore; i++)
                    {
                        canvas.Editor.UndoRedoService.Undo();
                    }

                    return canvas.Editor.Nodes.Count == nodesBefore;
                });
                Check(graphRestored,
                    "el nodo añadido se retira con Undo: el grafo vuelve a " + nodesBefore + " nodo(s)");

                bool inspectorClosed = Step(() => inspector!.CloseViaCommand());
                Check(inspectorClosed, "y el inspector se cierra por el comando del host (la escena queda como estaba)");

                report.AppendLine(ok
                    ? "=== RESULTADO: VERIFICADO (los paneles de nodo abren sus diálogos y escriben el valor) ==="
                    : "=== RESULTADO: FALLOS (ver [FALLO] arriba) ===");
            }
            catch (Exception ex)
            {
                report.AppendLine("[FALLO] el sondeo de los paneles de nodo murió: " + ex.GetType().Name + ": " + ex.Message);
                report.AppendLine("       [pila] " + ex);
                ok = false;
            }

            try
            {
                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "selfcheck-dialogs-report.txt"), report.ToString());
            }
            catch
            {
            }

            Console.Out.Flush();
            Console.WriteLine(report.ToString());
            Console.Out.Flush();
            Environment.Exit(ok ? 0 : 1);
        })
        {
            IsBackground = true,
            Name = "RuntimeDialogsProbe"
        }.Start();

        return -1;
    }

    /// <summary>
    /// ¿La fila es de TEXTO LIBRE? Es el mismo resto del selector del escritorio y del inspector del host:
    /// ni casilla, ni deslizador, ni desplegable, ni ruta con explorar, ni multilínea. Sólo esas filas llevan
    /// el botón «{x}» del catálogo.
    /// </summary>
    private static bool IsPlainTextRow(NodeParameterViewModel p) =>
        !p.IsToggle && !p.IsSlider && !p.IsDropdown && !p.HasBrowseButton && !p.IsMultiLine;

    /// <summary>Un valor para los renglones de medición: entero, en una línea, sin desbordar el informe.</summary>
    private static string Truncate(string value)
    {
        string flat = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
        return flat.Length <= 48 ? flat : flat[..48] + "…";
    }

    /// <summary>
    /// El marco donde nació una excepción del sondeo, para poder nombrarlo en el informe. Sin esto, una
    /// excepción del hilo de UI sólo deja su mensaje y hay que adivinar qué lectura la lanzó.
    /// </summary>
    private static string DescribeFrame(Exception ex)
    {
        string? frame = ex.StackTrace?
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Contains("FileFlow", StringComparison.Ordinal));
        if (string.IsNullOrEmpty(frame))
        {
            return "(sin traza de FileFlow)";
        }

        // «en <ruta>:línea N» se cae (la ruta de esta máquina no dice nada al informe): queda el método.
        foreach (string cut in new[] { " en ", " in " })
        {
            int at = frame.IndexOf(cut, StringComparison.Ordinal);
            if (at > 0)
            {
                frame = frame[..at];
                break;
            }
        }

        return frame;
    }

    /// <summary>
    /// ¿El fichero del almacén de presets lleva esa descripción para ese preset? Se lee como JSON y no como
    /// texto: el serializador escapa los acentos con hex en MAYÚSCULAS (<c>\u00F3</c>) y comparar contra el
    /// escapado de otro serializador medía el escapado, no el valor guardado.
    /// </summary>
    private static bool PresetDescriptionInFile(string path, string presetId, string description)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            foreach (System.Text.Json.JsonElement preset in document.RootElement.EnumerateArray())
            {
                if (preset.TryGetProperty("Id", out var id) && id.GetString() == presetId
                    && preset.TryGetProperty("Description", out var saved) && saved.GetString() == description)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
        {
            // Una lectura a medias (el fichero escribiéndose) no es un veredicto: se reintenta.
            return false;
        }
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

    /// <summary>La señal de escena lista del fixture (hito 245), junto al ejecutable.</summary>
    private static string FixtureSignalPath => Path.Combine(AppContext.BaseDirectory, "selfcheck-uia-fixture-ready.txt");

    /// <summary>
    /// Un intento de montaje de la escena (idempotente): el inspector abierto sobre el primer nodo
    /// con sus snapshots reales y la combinada pre-seleccionada. Devuelve false si el árbol aún no
    /// está listo (reintento).
    /// </summary>
    private static bool TryMountUiaScene(Window window)
    {
        var canvas = Find<EditorCanvasControl>(window.Content);
        var inspector = Find<NodeInspectorPanel>(window.Content);
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

    private static bool Inspect(Window window, StringBuilder report)
    {
        bool ok = true;

        void Check(bool condition, string what)
        {
            report.AppendLine((condition ? "[OK]   " : "[FALLO]") + " " + what);
            ok &= condition;
        }

        report.AppendLine("=== Sondeo en runtime del host Uno (fase 3.1) ===");

        var canvas = Find<EditorCanvasControl>(window.Content);
        Check(canvas is not null, "EditorCanvasControl montado en la ventana");

        if (canvas is null)
        {
            return false;
        }

        var editor = canvas.Editor;
        Check(editor is not null, "EditorViewModel resuelto y asignado al lienzo");
        if (editor is null)
        {
            return false;
        }

        Check(editor.Nodes.Count > 0, $"nodos del flujo de ejemplo cargados: {editor.Nodes.Count}");
        if (window is MainWindow mw && !string.IsNullOrEmpty(mw.SampleLoadError))
        {
            report.AppendLine("       [error de carga] " + mw.SampleLoadError);
        }
        Check(editor.Connections.Count > 0, $"cables del ejemplo: {editor.Connections.Count}");

        // Las tarjetas materializadas en el árbol visual (el ItemsControl materializa su plantilla).
        var cards = FindAll<NodeCardView>(window.Content).ToList();
        Check(cards.Count == editor.Nodes.Count,
            $"tarjetas materializadas: {cards.Count} (esperadas {editor.Nodes.Count})");

        foreach (var card in cards)
        {
            var vm = card.DataContext as NodeCardViewModel;
            if (vm is null)
            {
                Check(false, "tarjeta sin su NodeCardViewModel (DataContext no aplicado)");
                continue;
            }

            var node = vm.Node;
            Check(!string.IsNullOrEmpty(vm.Title), $"título enlazado: '{vm.Title}'");

            // Binding en el control real: el x:Name del TextBlock permite leer lo que el binding escribió
            // en el árbol — no lo que el ViewModel ya sabía. Es la única sonda de texto que valida el
            // enlace de verdad; los demás [OK] de este bloque leen propiedades del adaptador.
            var titleControl = card.FindName("TitleText") as TextBlock;
            Check(titleControl is not null && !string.IsNullOrEmpty(titleControl.Text),
                $"binding del título produjo texto en el control: '{titleControl?.Text ?? "<sin control>"}'");

            // La posición proyectada: el code-behind la aplica con el conversor (regla del 217).
            var container = FindAscendant<Microsoft.UI.Xaml.Controls.ContentPresenter>(card);
            double left = Microsoft.UI.Xaml.Controls.Canvas.GetLeft(container!);
            double top = Microsoft.UI.Xaml.Controls.Canvas.GetTop(container!);
            Check(double.IsFinite(left) && double.IsFinite(top) && (left != 0 || top != 0),
                $"posición proyectada aplicada al contenedor: ({left:F1}, {top:F1}) para '{node.Title}'");
            Check(Math.Abs(left - vm.Position.X) < 0.01 && Math.Abs(top - vm.Position.Y) < 0.01,
                "la posición del contenedor coincide con NodeCardViewModel.Position (proyección del 217)");

            // Icono del tipo, en dos niveles para que el fallo diga de QUÉ nivel es:
            // (a) datos: el conversor resuelve la path data del paquete a una geometría con bounds;
            // (b) control: el PathIcon del árbol recibió su Data (el binding del XAML llegó).
            Microsoft.UI.Xaml.Media.Geometry? iconGeometry;
            try
            {
                iconGeometry = FileFlow.App.Uno.Platform.MaterialIconKindToGeometryConverter.ToGeometry(node.Icon);
            }
            catch (Exception ex)
            {
                iconGeometry = null;
                report.AppendLine("       [error del conversor] " + ex.GetType().Name + ": " + ex.Message);
            }

            var iconDataBounds = iconGeometry?.Bounds ?? Windows.Foundation.Rect.Empty;
            Check(iconGeometry is not null && !iconDataBounds.IsEmpty && iconDataBounds.Width > 0,
                $"conversor de icono resuelve la path data del paquete: '{node.Icon}' ({iconDataBounds.Width:F0}x{iconDataBounds.Height:F0})");

            var icon = card.FindName("TypeIcon") as Microsoft.UI.Xaml.Shapes.Path;
            if (icon is null)
            {
                Check(false, $"Path del tipo PRESENTE en el árbol (x:Name TypeIcon): '{node.Icon}'");
            }
            else
            {
                var b = icon.Data?.Bounds ?? Windows.Foundation.Rect.Empty;
                Check(icon.Data is not null && b != Windows.Foundation.Rect.Empty && b.Width > 0,
                    $"icono del tipo enlazado en el control: '{node.Icon}' (Data={icon.Data?.GetType().Name ?? "null"}, bounds={b.Width:F1}x{b.Height:F1})");
            }

            // Diagnóstico fino (se imprimen siempre): cuántos Path de la tarjeta recibieron geometría y
            // el estado del x:Name — distingue «el binding del tipo falló» de «ninguna geometría aterriza».
            var allPaths = FindAll<Microsoft.UI.Xaml.Shapes.Path>(card).ToList();
            int withData = allPaths.Count(p => p.Data is not null);
            report.AppendLine("       [diagnóstico] Paths con Data: " + withData + "/" + allPaths.Count
                + " | x:Name TypeIcon " + (icon is null ? "NO resuelto por FindName" : "resuelto"));

            // Barra de acento: el color hex del núcleo llegó como Color de WinUI.
            var accent = vm.AccentBrushColor;
            Check(accent.A == 255 && (accent.R != 0 || accent.G != 0 || accent.B != 0),
                $"barra de acento con color parseado: #{accent.R:X2}{accent.G:X2}{accent.B:X2}");

            // Puertos dibujados con la matriz: el socket (borde >= 2 en un cuadro pequeno) o el triangulo.
            var expectedPorts = node.InputPorts.Count + node.OutputPorts.Count;
            var triangles = FindAll<Microsoft.UI.Xaml.Shapes.Path>(card).Count();
            var boxes = FindAll<Border>(card)
                .Count(b => b.BorderThickness.Left >= 2 && b.Width <= 20 && b.Height <= 20);
            Check(boxes + triangles >= expectedPorts * 2,
                $"elementos de socket dibujados: {boxes}+{triangles} para {expectedPorts} puertos (borde+triangulo)");
        }

        // La PUERTA de acciones de la tarjeta (hito 263, reencuadrado en el 269): el panel —y con él las acciones
        // rápidas del nodo, donde vive el «🎬 Presets...» del transcodificador— cuelga de IsExpanded y sólo se
        // dibuja si el nodo declara acciones. El listado de parámetros que lo acompañaba era una lista muerta
        // —nombres sin editor, que se pulsaban y no hacían nada— y se quitó: los parámetros se editan en la ficha
        // del inspector.
        //
        // Se mide el CONTRATO en todas las tarjetas del lienzo: lo que la vista enseña tiene que ser lo que dice
        // el adaptador (el estado desplegado es del núcleo, no de la vista). El ejercicio completo —desplegar la
        // tarjeta y abrir la acción— se mide sobre la del transcodificador, que es la que declara acciones:
        // aquí, que la puerta esté donde dice, y en el flujo de los paneles, que al abrirla se materialice.
        var doorMismatch = new List<string>();
        int cardsShowingDoor = 0;
        foreach (var card in cards)
        {
            if (card.DataContext is not NodeCardViewModel doorVm)
            {
                continue;
            }

            var doorToggle = card.FindName("ParametersToggle") as Microsoft.UI.Xaml.Controls.Primitives.ToggleButton;
            var doorPanel = card.FindName("ParametersPanel") as Border;
            bool toggleOk = doorToggle is not null
                && doorToggle.Visibility == (doorVm.HasCustomActions ? Visibility.Visible : Visibility.Collapsed);
            bool panelOk = doorPanel is not null
                && doorPanel.Visibility == (doorVm.ActionsPanelVisible ? Visibility.Visible : Visibility.Collapsed);
            if (doorVm.HasCustomActions)
            {
                cardsShowingDoor++;
            }

            if (!toggleOk || !panelOk)
            {
                doorMismatch.Add(doorVm.Title);
            }
        }

        Check(doorMismatch.Count == 0,
            $"la puerta de acciones coincide con el adaptador en las {cards.Count} tarjetas "
            + $"({cardsShowingDoor} con acciones declaradas; las demás sin conmutador ni panel: nada que desplegar)"
            + (doorMismatch.Count == 0 ? string.Empty : " — descuadran: " + string.Join(", ", doorMismatch)));

        // Cables: las Bézier del núcleo materializadas como Paths en la capa de cables.
        int wirePaths = CountWirePaths(canvas);
        Check(wirePaths >= editor.Connections.Count,
            $"cables dibujados en la capa: {(wirePaths < 0 ? "capa no encontrada" : wirePaths.ToString())} (esperados >= {editor.Connections.Count})");

        // Área de clic del lienzo (hito 249): el centro DIBUJADO de cada tarjeta —medido del árbol visual—
        // tiene que resolver esa MISMA tarjeta por el `CardAt` que usan los handlers. La sesión con puntero
        // real del 247 midió el defecto que esta sonda atrapa sin puntero: el área de clic caía desplazada
        // la posición del lienzo en la ventana ((280, 41): la columna del cajón y la barra superior).
        try
        {
            var (clickMeasured, clickMatched, clickDetail) = canvas.ProbeHitAreas();
            Check(clickMeasured > 0 && clickMatched == clickMeasured,
                $"el área de clic coincide con la tarjeta dibujada ({clickMatched}/{clickMeasured}): {clickDetail}");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de área de clic (249) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // Fase 3.2 (lo verificable sin puntero ni foco): selección con reacción del núcleo, contenedor
        // del glow presente, Delete por comando canónico y restauración por el undo del propio núcleo.
        // El estado queda como al entrar: el sondeo no puede dejar la app borrada.
        try
        {
            var (nodesBefore, nodesAfterDelete, nodesAfterUndo, selectionStuck, glowContainerExists) =
                canvas.ProbeSelectionRoundTrip();
            Check(nodesBefore > 0 && nodesAfterDelete == nodesBefore - 1,
                $"selección + Delete borran el nodo seleccionado: {nodesBefore} -> {nodesAfterDelete}");
            Check(nodesAfterUndo == nodesBefore,
                $"el undo del núcleo restaura el grafo: {nodesAfterDelete} -> {nodesAfterUndo}");
            Check(selectionStuck,
                "IsSelected reacciona en el núcleo (SelectedNode asignado): la selección es del VM, no del control");
            Check(glowContainerExists,
                "la tarjeta seleccionada tiene contenedor materializado: el glow de selección se pinta sobre el árbol real");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de selección 3.2 lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // Fase 3.3 (sin puntero): el ciclo completo de conexión por los mismos métodos que los handlers —
        // anclas write-back reales, conectar por comandos, estados de puerto, desconectar y restaurar.
        try
        {
            var (anchorsReal, connected, statesRefreshed, disconnected, restored) = canvas.ProbeConnectionRoundTrip();
            Check(anchorsReal, "anclas de puerto calculadas del árbol (write-back real, no estimadas)");
            Check(connected, "conectar vía StartConnection+FinishConnection añade la conexión al grafo");
            Check(statesRefreshed, "los estados de los puertos se refrescan (IsConnected en ambos extremos)");
            Check(disconnected, "desconectar por comando (click derecho del socket) quita la conexión");
            Check(restored, "el undo restaura la conexión deshecha (la pila del núcleo gobierna)");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de conexión 3.3 lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // Fase 3.4 (sin puntero): la edición completa del flujo por los métodos de los handlers — nota
        // creada/movida/borrada, grupo creado/borrado, spotlight que añade un nodo real, migas navegadas.
        try
        {
            var (noteOk, groupOk, spotlightOk, breadcrumbOk) = canvas.ProbeDecoratorsRoundTrip();
            Check(noteOk, "nota creada, movida por su Location y borrada (capa de decoradores al día)");
            Check(groupOk, "grupo creado y borrado (detras de las notas, como en el escritorio)");
            Check(spotlightOk, "spotlight añade un nodo real en el punto del grafo pedido");
            Check(breadcrumbOk, "migas de subflujo navegables (comando del núcleo)");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de decoradores 3.4 lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // Rebanada 4 — los dos paneles: catálogo poblado, filtro que reduce, añadir por el método del
        // handler, favorito por comando, inspector por selección con parámetros del nodo y write-through
        // al NodeInstance (el mismo camino que la edición del usuario). La restauración deja el grafo y
        // el favorito como al entrar.
        try
        {
            var toolbox = Find<NodeToolboxPanel>(window.Content);
            var inspector = Find<NodeInspectorPanel>(window.Content);
            Check(toolbox is not null, "panel del cajón de herramientas montado en la ventana");
            Check(inspector is not null, "panel del inspector montado en la ventana");

            if (toolbox is { } tb && inspector is { } insp && editor is { })
            {
                // 1. Catálogo poblado desde el VM del núcleo.
                int visible = tb.VisibleItemCount;
                Check(visible > 0, $"catálogo del cajón poblado desde el ToolboxViewModel: {visible} ítems");

                // 2. Filtro de búsqueda que reduce el catálogo (el mismo setter que el binding).
                int beforeFilter = tb.VisibleItemCount;
                tb.SearchForProbe("Folder");
                int afterFilter = tb.VisibleItemCount;
                tb.SearchForProbe(string.Empty);
                Check(afterFilter > 0 && afterFilter < beforeFilter,
                    $"el filtro reduce el catálogo: {beforeFilter} -> {afterFilter} con 'Folder' (restaurado)");

                // 3. Añadir el primer ítem por el método del doble clic, con undo de restauración.
                int nodesBefore = tb.EditorNodeCount;
                bool added = tb.TryAddFirstItemOfGroupForProbe();
                int nodesAfter = tb.EditorNodeCount;
                if (added)
                {
                    editor.UndoRedoService.Undo();
                }

                Check(added && nodesAfter == nodesBefore + 1,
                    $"doble clic añade el nodo por EditorViewModel.AddNode: {nodesBefore} -> {nodesAfter} (undo restaurado)");

                // 4. Favorito por el comando del VM (el toggle de la estrella), reportando el estado.
                // El segundo toggle RESTAURA el estado original: el sondeo no puede dejar una
                // preferencia persistida del usuario a medio camino.
                bool favOk = tb.ToggleFavoriteViaCommand();
                bool favRestored = favOk && tb.ToggleFavoriteViaCommand();
                Check(favOk && favRestored, "favorito conmutado por ToggleFavoriteCommand (y restaurado)");

                // Hito 246: el toggle compacto/detallado del cajón — el ÚLTIMO pendiente de código
                // de la rebanada 4. La sonda conmuta por el MISMO comando del VM que el botón y
                // compara el estado del árbol. El ritmo lo impone el diseño del VM: cada toggle
                // persiste en preferencias y el refresco REGENERA el catálogo (Save ->
                // PreferencesChanged -> RefreshToolbox) — entre pasos, asentar el dispatcher.
                // La sonda expande el primer grupo: con el acordeón colapsado no hay contenedores
                // que contar (medido: 0 de 0).
                var firstGroup = tb.FirstGroupWithItemsForProbe();
                Check(firstGroup is not null, "el cajón trae grupos con ítems para la sonda del modo");

                if (firstGroup is { } group)
                {
                    group.IsExpanded = true;
                    Thread.Sleep(400);

                    var (detTotal, detHidden, detVisible) = tb.ProbeDetailsBlocks();
                    bool compactOk = tb.IsCompact && detVisible == 0;
                    Check(compactOk,
                        $"el cajón arranca en compacto: {detTotal} bloques detallados materializados y {detVisible} visibles (0 esperados)");

                    tb.ToggleViewModeViaCommand();
                    Thread.Sleep(600);
                    var (d2, h2, v2) = tb.ProbeDetailsBlocks();
                    bool detailedOk = !tb.IsCompact && v2 > 0;
                    Check(detailedOk,
                        $"el toggle conmuta a detallado por ToggleViewModeCommand: {v2} de {d2} bloques visibles (>0)");

                    tb.ToggleViewModeViaCommand();
                    Thread.Sleep(600);
                    var (d3, h3, v3) = tb.ProbeDetailsBlocks();
                    bool restoredOk = tb.IsCompact && v3 == 0;
                    Check(restoredOk,
                        $"la vuelta a compacto oculta los detalles otra vez: {v3} de {d3} visibles (0 esperados)");

                    group.IsExpanded = false;
                    Thread.Sleep(200);
                }

                // 5. Inspector: abrir sobre un nodo real de la ventana (el flujo cargado) — la ficha
                // con parámetros materializados, el write-through al NodeInstance y el cierre por comando.
                var firstNode = editor.Nodes.FirstOrDefault();
                if (firstNode is null)
                {
                    Check(false, "inspector: sin nodo para inspeccionar (el ejemplo no cargó)");
                }
                else
                {
                    insp.InspectForProbe(firstNode);
                    Check(insp.Visibility == Visibility.Visible, "la selección abre el inspector (IsOpen del VM)");

                    int paramEditors = insp.ParameterEditorCount;
                    Check(paramEditors == firstNode.Parameters.Count,
                        $"editores de parámetros materializados: {paramEditors} de {firstNode.Parameters.Count} parámetros");

                    // Hito 269: las ACCIONES del nodo en la ficha. Son la puerta a sus superficies (el gestor de
                    // presets, la configuración del VLM, el estudio de scripts...) y hasta aquí vivían SÓLO en el
                    // panel plegable de la tarjeta: quien no supiera desplegarla no llegaba a la superficie. La
                    // cuenta tiene que ser la de las acciones del nodo inspeccionado, ni una más ni una menos; el
                    // caso CON acciones —el botón y su ancla— se mide sobre el transcodificador, más abajo, donde
                    // el flujo de los paneles lo añade al lienzo.
                    Check(insp.ActionButtonCount == firstNode.CustomActions.Count,
                        $"acciones del nodo en la ficha: {insp.ActionButtonCount} de "
                        + $"{firstNode.CustomActions.Count} declaradas por el nodo inspeccionado");

                    // Write-through: el mismo camino que la edición del usuario (p.Value = ...), una
                    // pareja parámetro/instancia con la MISMA clave.
                    var param = firstNode.Parameters.FirstOrDefault(p => !p.IsVariableInjectorNode);
                    if (param is null)
                    {
                        Check(false, "sin parámetro editable para el write-through");
                    }
                    else
                    {
                        var inst = firstNode.NodeInstance.Parameters;
                        string key = param.Key;
                        string beforeValue = inst.TryGetValue(key, out var v0) ? v0?.ToString() ?? "" : "<sin clave>";
                        param.Value = "__probe__";
                        string afterValue = inst.TryGetValue(key, out var v1) ? v1?.ToString() ?? "" : "<sin clave>";
                        param.Value = beforeValue == "<sin clave>" ? null : beforeValue;
                        Check(afterValue == "__probe__",
                            $"la edición escribe al NodeInstance (write-through): '{key}' = '{afterValue}'");
                    }

                    bool closed = insp.CloseViaCommand();
                    Check(closed, "el comando de cierre oculta el inspector (ClosePanelCommand del VM)");

                    // Hito 240: el botón «Probar» existe, canta su AutomationId para la observación
                    // UIA externa, está atado al comando canónico del núcleo y queda localizado.
                    bool testButtonOk = insp.HasWiredTestButton();
                    Check(testButtonOk,
                        "el botón Probar existe y ejecuta TestNodeWithCustomFileCommand (variante async del diálogo)");

                    // Hito 242: las pestañas de snapshots y diff, con los datos del NODO y del VM.
                    // El flujo de ejemplo no trae snapshots: la sonda crea uno de ENTRADA por la vía
                    // de producción (CreateInput con un FileItemContext, la misma fábrica que usa el
                    // motor) y re-inspecciona — el diff del VM exige un snapshot seleccionado.
                    var probeItem = new FileItemContext(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "__selfcheck_probe__.txt"));
                    probeItem.Metadata["Category"] = "Probe";
                    probeItem.Metadata["Status"] = "Selfcheck";
                    firstNode.InputSnapshots.Add(NodeDataSnapshot.CreateInput(firstNode.Id, "In", probeItem));
                    insp.InspectForProbe(firstNode);

                    var (snapshotCards, diffRows, tabSwitch) = insp.ProbeSnapshotTabs();
                    Check(snapshotCards == firstNode.InputSnapshots.Count + firstNode.OutputSnapshots.Count,
                        $"las tarjetas de snapshots materializan las colecciones del nodo ({snapshotCards} = entradas + salidas)");
                    Check(diffRows > 0,
                        $"la pestaña de diff pinta las filas que el VM computa ({diffRows}, Added/Removed/Modified)");
                    Check(tabSwitch,
                        "el Pivot conmuta: la combinada conserva sus tarjetas y Entradas/Salidas " +
                        "separadas (hito 244) llevan EXACTAMENTE su colección del nodo");

                    firstNode.InputSnapshots.Clear();
                    insp.InspectForProbe(firstNode);
                }
            }
        }
        catch (Exception ex)
        {
            Check(false, "sonda de paneles (rebanada 4) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // Superficie UIA (hito 238): lo que una observación EXTERNA (pywinauto, sin UIAccess) puede
        // alcanzar del lienzo — la ancla explícita con su peer expuestos, el foco programático (la vía
        // del SetFocus UIA) entrando sin puntero, y el estado del zoom observable en la barra. Es la
        // sonda interna de las mismas anclas que las sondas UIA externas van a citar.
        try
        {
            var (anchorExposed, focusEntered, zoomStateObservable) = canvas.ProbeUiAccessibility();
            Check(anchorExposed, "la ancla 'CanvasRoot' del lienzo llega al árbol UIA con su peer expuesto");
            Check(focusEntered, "el lienzo acepta el foco programático (la vía del SetFocus UIA externo, sin puntero)");
            Check(zoomStateObservable, "el estado del zoom es observable: la barra refleja el nivel cambiado y restaurado");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de superficie UIA (238) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // El FOCO del puntero (hito 252): el clic tiene que dejar el foco en el CONTROL del lienzo —de eso
        // dependía que el 250 midiera Ctrl+Z, Ctrl+Y y Supr sin llegar— y no robarle el suyo ni al cuadro
        // de texto (buscador del spotlight, renombrado) ni a la barra de zoom. El puntero no se puede
        // inyectar en este entorno: lo que se mide es el MISMO camino que ejecuta el clic.
        try
        {
            var (focusDelivered, respectsOwners, focusDetail) = canvas.ProbePointerFocus();
            Check(focusDelivered && respectsOwners,
                $"el clic del puntero entrega el foco al lienzo y respeta a los otros dueños del teclado: {focusDetail}");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de foco del puntero (252) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // El ENRUTADO de los atajos (hito 252): entregar el foco no bastó —el rastro con puntero real midió
        // que un panel se lo lleva ~0,5 s después del clic y que Ctrl+Z, Ctrl+Y, Supr y F2 morían con él—,
        // así que el arreglo no depende del foco: la ventana enruta al lienzo las teclas que nadie consumió.
        // La sonda resuelve teclas sin foco (Espacio abre el buscador, Escape lo cierra) por el MISMO método
        // que usa el enrutador, y comprueba la cortesía con los cuadros de texto.
        try
        {
            var (resolvedWithoutFocus, respectsTextInput, routingDetail) = canvas.ProbeShortcutResolution();
            Check(resolvedWithoutFocus,
                $"el atajo del editor se resuelve sin ser dueño del foco (el caso que el 250 midió con puntero real): {routingDetail}");
            Check(respectsTextInput,
                "el enrutado del atajo respeta al cuadro de texto (no le secuestra el teclado) y no abre nada por su cuenta");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de enrutado de atajos (252) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // El SEGUIMIENTO de los cables (hito 254): el extremo dibujado del cable tiene que tocar su socket
        // EN LA RAÍZ (con el pan y el zoom ya dentro, que es lo que ve el usuario) antes y después de mover el
        // plano y de cambiar el zoom. Reportado desde la app: al mover o ajustar el zoom los cables quedaban
        // fuera de su sitio.
        try
        {
            var (beforeOk, afterOk, crowdedOk, wireDetail) = canvas.ProbeWireTracking();
            Check(beforeOk, $"el cable dibujado toca su socket con el plano sin mover: {wireDetail}");
            Check(afterOk, $"el cable sigue tocando su socket tras mover el plano y cambiar el zoom: {wireDetail}");
            Check(crowdedOk, $"la forma del cable cabe en el hueco estrecho (sin el rulo del 2): {wireDetail}");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de seguimiento de cables (254) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // La RECLAMACIÓN del teclado (hito 253): el rastro con puntero real mide que un envoltorio de la
        // plantilla de ventana se lleva el foco 78–141 ms después del clic. El lienzo lo recupera mientras la
        // ventana de propiedad de ese clic siga viva, y lo respeta si el nuevo dueño es un cuadro de texto,
        // algo suyo o un panel del editor.
        try
        {
            var (reclaimed, respectsOwners, reclaimDetail) = canvas.ProbeKeyboardReclaim();
            Check(reclaimed,
                $"el lienzo recupera el teclado cuando el framework se lo lleva tras el clic: {reclaimDetail}");
            Check(respectsOwners,
                "la reclamación del teclado no se lo quita a un cuadro de texto, ni a un control del lienzo, ni a un panel del editor");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de reclamación del teclado (253) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // Fase 3.5 (sin puntero): cambiar el tema por la API del núcleo tiene que re-tematizar el
        // lienzo EN CALIENTE — fondo y tarjetas con los valores del tema nuevo (los pinceles que los
        // ThemeResource del XAML consumen, republicados por UnoThemeHost). La vuelta deja el tema que
        // estaba activo al entrar (el que el usuario tenga guardado, no uno fijo).
        try
        {
            var (backgroundChanged, cardChanged, variantChanged, restored, themeColors) = canvas.ProbeThemeRepublish();
            Check(backgroundChanged, "cambiar el tema re-tematiza el fondo del lienzo en caliente");
            Check(cardChanged, "las tarjetas adoptan el color del tema nuevo (ThemeResource ya evaluado se actualiza)");
            Check(variantChanged, "la variante clara/oscurecida se publica en la raíz del contenido");
            Check(restored, "la restauración del tema deja el tema de la entrada activo (grafo como al entrar)");
            report.AppendLine("       [color] " + themeColors);
        }
        catch (Exception ex)
        {
            Check(false, "sonda de temas 3.5 lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // Fase 3.6 — el rendimiento MEDIDO con el grafo de referencia (40 nodos + cables): construir,
        // re-posicionar todo (el coste por frame de arrastre) y un frame de drag real. Umbrales del
        // plan: build < 5 s, re-posicionado < 60 ms, frame de drag < 33 ms (30 fps sin tirones).
        // ONE-SHOT y sólo con el árbol sano: el add/remove masivo de 40 tarjetas deja la
        // materialización de WinUI frágil (la sonda 3.2 de un reintento lanza COMException), así que
        // (1) sólo corre si todo lo anterior pasó, y (2) tras correrla no hay reintento del sondeo.
        if (ok)
        {
            try
            {
                int nodesBefore = editor.Nodes.Count;
                int connectionsBefore = editor.Connections.Count;
                var (nodesBuilt, wiresDrawn, buildMs, repositionMs, dragFrameMs) = canvas.ProbePerformanceGraph40();
                _performanceProbeRan = true;

                report.AppendLine(string.Format(
                    "       [medición] build {0} nodos + {1} cables: {2:F0} ms | re-posicionado total: {3:F1} ms | frame de drag: {4:F1} ms",
                    nodesBuilt, wiresDrawn, buildMs, repositionMs, dragFrameMs));

                Check(nodesBuilt == 40, $"el grafo de referencia se construye completo: {nodesBuilt}/40 nodos");
                Check(wiresDrawn >= 20, $"los pares encadenables del grafo se conectan y dibujan: {wiresDrawn} (los fuentes sin entrada reducen el encadenado)");
                Check(buildMs < 5000, $"build bajo el umbral del plan (< 5000 ms): {buildMs:F0} ms");
                Check(repositionMs < 60, $"re-posicionado total bajo el umbral (< 60 ms): {repositionMs:F1} ms");
                Check(dragFrameMs < 33, $"frame de drag bajo 30 fps (< 33 ms): {dragFrameMs:F1} ms");

                bool restoredExactly = editor.Nodes.Count == nodesBefore && editor.Connections.Count == connectionsBefore;
                Check(restoredExactly, "la restauración exacta deja el grafo de ejemplo como al entrar");
            }
            catch (Exception ex)
            {
                _performanceProbeRan = true;
                Check(false, "sonda de rendimiento 3.6 lanzó: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        else
        {
            report.AppendLine("       [omitida] sonda de rendimiento 3.6: el árbol no llegó sano (una sonda anterior falló); reintento con árbol limpio");
        }

        report.AppendLine(ok
            ? "=== RESULTADO: VERIFICADO (inventario completo con valores reales) ==="
            : "=== RESULTADO: FALLOS (ver [FALLO] arriba) ===");

        return ok;
    }

    private static int CountWirePaths(EditorCanvasControl canvas)
    {
        var field = typeof(EditorCanvasControl).GetField("WireLayer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var wires = field?.GetValue(canvas) as Panel
            ?? FindDescendantNamed<Panel>(canvas, "WireLayer"); // respaldo: el x:Name genera campo, pero el
                                                                 // nombre puede variar entre compilaciones
        return wires is null ? -1 : wires.Children.OfType<Microsoft.UI.Xaml.Shapes.Path>().Count();
    }

    /// <summary>El descendiente por Name de framework (independiente del reflejo sobre campos generados).</summary>
    private static T? FindDescendantNamed<T>(object? root, string name) where T : FrameworkElement
    {
        if (root is FrameworkElement element && element.Name == name && element is T match)
        {
            return match;
        }

        foreach (var child in Children(root))
        {
            var found = FindDescendantNamed<T>(child, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static T Find<T>(object? root) where T : class
    {
        if (root is T match)
        {
            return match;
        }

        foreach (var child in Children(root))
        {
            var found = Find<T>(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static System.Collections.Generic.IEnumerable<T> FindAll<T>(object? root) where T : class
    {
        if (root is T match)
        {
            yield return match;
        }

        foreach (var child in Children(root))
        {
            foreach (var found in FindAll<T>(child))
            {
                yield return found;
            }
        }
    }

    /// <summary>El ascendiente de un tipo dado (el ContentPresenter del contenedor, p. ej.).</summary>
    private static T FindAscendant<T>(DependencyObject start) where T : DependencyObject
    {
        var current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(start);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }

        return default;
    }

    /// <summary>Los hijos visuales por VisualTreeHelper (la única vía de WinUI).</summary>
    private static System.Collections.Generic.IEnumerable<object?> Children(object? node)
    {
        if (node is not DependencyObject dep)
        {
            yield break;
        }

        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(dep);
        for (int i = 0; i < count; i++)
        {
            yield return Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(dep, i);
        }
    }
}
