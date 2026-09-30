using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using FileFlow.App.Uno.Controls;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Services;
using FileFlow.Sdk.VirtualFileSystem;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno;

/// <summary>
/// El sondeo del MENÚ PRINCIPAL del host (<c>--selfcheck-controlbar</c>): la barra de control y su cajón,
/// ejercidos por el MISMO canal que el usuario —el peer de automatización de cada entrada: el Invoke del
/// botón, el Toggle de la casilla—, con el estado por contexto leído del view model portable. Trae consigo el
/// censo que declara cuántas entradas TIENE que exponer cada superficie (<c>ControlBarEntryIds</c> y
/// <c>DrawerEntryIds</c>) y las tres lecturas que sólo su ciclo necesita: la cola de la consola del núcleo y
/// los dos rastros del ciclo en el documento (snapshots del grafo y diffs del inspector).
///
/// <para><b>Por qué en modo propio</b>: su ciclo de ejecución mueve el documento (snapshots y diff del nodo
/// fuente) y las sondas del lienzo miden una escena que no tolera esa mudanza a mitad. Un proceso limpio para
/// esta superficie es, además, el reparto de las sondas anteriores. Termina con el veredicto por el código de
/// salida y por <c>selfcheck-controlbar-report.txt</c>, junto al ejecutable.</para>
///
/// <para><b>Quién afirma</b>: este archivo MIDE y afirma; la regla —su modo propio, su canal de usuario y su
/// censo— la fija <c>UnoControlBarParityGuardTests</c>.</para>
/// </summary>
internal static class SelfCheckControlBar
{
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
    public static int Run(Window window, DispatcherQueue dispatcher)
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
                            + " | " + SelfCheckTree.DescribeFrame(ex));
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
                            + " | " + SelfCheckTree.DescribeFrame(ex));
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
                    bar = SelfCheckTree.Find<ControlBar>(window.Content);
                    drawer = SelfCheckTree.Find<MainMenuDrawer>(window.Content);
                    settings = SelfCheckTree.Find<SettingsPanel>(window.Content);
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
                        var panel = SelfCheckTree.Find<NodeInspectorPanel>(window.Content);
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
                    // mismo estado de la versión anterior. Aquí se mide con los dos valores del propio view model.
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

                    // El AVISO DE ACTUALIZACIÓN: el host lo comprueba al arrancar (como la versión anterior) y su
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
                    // correr (igual que en la versión anterior: F5 sin ejecución tampoco hace nada allí).
                    int shortcutsInTable = ControlBar.RoutedShortcuts.Length;
                    Check(shortcutsInTable == 6 && !ControlBar.DeclaredUnroutedShortcuts.Any(),
                        "los " + shortcutsInTable + " atajos del menú de la versión anterior están enrutados y ninguno queda "
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
}
