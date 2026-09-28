using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FileFlow.App.Services;
using FileFlow.App.ViewModels;
using FileFlow.App.Views.Components;
using FileFlow.Core.Engine;
using Nodify.Avalonia;
using Nodify.Avalonia.Connections;
using Nodify.Avalonia.Nodes;
using AvaloniaPoint = Avalonia.Point;
using SdkPoint = FileFlow.Sdk.Point;

namespace FileFlow.App.SelfCheck;

/// <summary>
/// La sonda de autorrevisión del <b>host de escritorio</b> (<c>FileFlow.App.exe --selfcheck</c>): arranca la
/// aplicación real —contenedor de servicios, plugins descubiertos, vistas y estilos del producto— y mide el
/// <b>lienzo</b> con puntero inyectado, hasta terminar el proceso con el veredicto (0 verificado, 1 alguna
/// comprobación en rojo) y el informe en <c>selfcheck-report.txt</c>, junto al ejecutable. Es la hermana de las
/// sondas del host Uno (<c>--selfcheck</c>, <c>--selfcheck-controlbar</c>, <c>--selfcheck-dialogs</c>,
/// <c>--selfcheck-settings</c>), y responde a la frontera que el hito 266 dejó declarada: el escritorio tenía su
/// medida de la <i>figura</i> del cable, pero no tenía sonda propia ni había medido el trazo con un gesto
/// encima.
///
/// <para><b>Qué mide</b>, con el gesto entrando por el pipeline de entrada real de Avalonia (movimiento,
/// pulsación, arrastre y rueda sobre el árbol montado):</para>
/// <list type="number">
///   <item>El <b>trazo del cable</b>: cada cable dibujado es el control del host y su curva es la del trazador
///   compartido del núcleo (<see cref="ConnectionGeometry"/>), y su extremo cae sobre el punto dibujado de su
///   socket —medido en píxeles de <b>ventana</b>, no en unidades del grafo—.</item>
///   <item>El <b>pan</b> con el botón derecho: el encuadre se mueve lo que el puntero dividido por el zoom, el
///   contenido sigue a la mano y el trazo no se despega de sus sockets.</item>
///   <item>El <b>zoom</b> con la rueda: acerca, aleja, tiene techo (el <c>MaxViewportZoom</c> de la vista) y el
///   trazo sigue cayendo sobre el socket con la escala cambiada — el caso que un ancla medida sin la escala de
///   la cadena rompe, que es el defecto que el host Uno midió en el hito 254.</item>
///   <item>El <b>reparto de botones</b>: el arrastre <b>izquierdo</b> sobre el fondo libre no mueve el plano —el
///   paneo es del derecho—, y el trazo sigue sobre sus sockets después del gesto. El arrastre de una TARJETA no
///   lo mide la sonda: pulsar una tarjeta la lleva al frente (reordena la colección del documento) y arrastrarla
///   cerca del borde hace que el editor auto-panee, dos efectos del producto que no son el reparto de botones; su
///   medida vive en el suite, con la escena bajo control.</item>
/// </list>
///
/// <para><b>Por qué corre sobre la plataforma headless de Avalonia</b> (y qué NO es eso): en el escritorio no
/// hay forma de meter un puntero por el camino de entrada de la aplicación desde dentro del proceso —la API
/// pública para inyectar entrada cruda no existe—, así que la sonda arranca la <b>misma</b> aplicación
/// (<c>App</c>, contenedor, vistas, estilos, plugins) sobre la plataforma headless con Skia real, cuya
/// inyección de puntero <b>sí</b> recorre el pipeline de entrada del framework: hit-testing, gestos del editor,
/// captura de puntero. Lo que se mide es el comportamiento del producto ante la entrada; lo que <b>no</b> se
/// mide —y no se puede declarar medido— es el dedo del sistema operativo sobre la ventana nativa, que sigue
/// siendo materia de una sesión manual. La frontera va escrita en el propio informe.</para>
///
/// <para><b>Modo propio</b>: su ciclo de ejecución mueve el encuadre y arrastra una tarjeta —la escena que
/// cualquier otra medida del mismo proceso estaría midiendo—, así que no convive con otras sondas en el mismo
/// arranque: se pide con su argumento y termina el proceso.</para>
/// </summary>
public static class DesktopSelfCheck
{
    /// <summary>El argumento que enciende el modo. Cualquier variante (<c>--selfcheck-x</c>) también lo enciende.</summary>
    public const string Argument = "--selfcheck";

    /// <summary>El informe, junto al ejecutable.</summary>
    public const string ReportFileName = "selfcheck-report.txt";

    /// <summary>Tolerancia del aterrizaje del trazo sobre su socket, en píxeles de ventana.</summary>
    public const double LandingTolerance = 0.5;

    /// <summary>El techo de zoom que declara la vista del lienzo (<c>MaxViewportZoom</c> en el XAML).</summary>
    public const double MaxZoom = 2.5;

    /// <summary>Cuántas muescas de rueda se giran para comprobar que el zoom llega a su techo y se queda ahí.</summary>
    private const int CeilingNotches = 20;

    /// <summary>En cuántos tramos se entrega un arrastre: es lo que lo hace un gesto y no un salto.</summary>
    private const int DragSteps = 8;

    /// <summary>¿La línea de comandos pidió la sonda? Sin el argumento esto no hace nada, ni un fichero.</summary>
    public static bool IsRequested(IEnumerable<string> args) =>
        args.Any(argument => argument.StartsWith(Argument, StringComparison.Ordinal));

    /// <summary>
    /// El anfitrión de la sonda: la aplicación del producto sobre la plataforma headless con <b>Skia real</b>
    /// (sin dibujo real, la cadena de transformaciones que se quiere medir no llega a componerse).
    /// </summary>
    public static AppBuilder ConfigureHost() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
                FrameBufferFormat = PixelFormat.Rgba8888,
                ShouldRenderOnUIThread = true
            });

    /// <summary>
    /// Lanza la sonda en un hilo de fondo (nunca bloquea el hilo de UI) y termina el proceso con el veredicto:
    /// la medida se despacha al hilo de UI, que es el dueño del árbol, y el hilo de UI sigue bombeando mientras
    /// tanto —igual que hacen las sondas del host Uno—.
    /// </summary>
    public static void Run(Window window, IClassicDesktopStyleApplicationLifetime desktop)
    {
        new Thread(() =>
        {
            var report = new StringBuilder();
            bool verified = false;

            try
            {
                // Primero ESPERAR a que el lienzo esté montado, sin tocar nada: la ventana acaba de aparecer y
                // las plantillas se materializan en el pase de layout siguiente. La espera no mide —y por eso no
                // reintenta la medida—: repetir la medida cambiaría la escena de la vez anterior (el encuadre
                // ya paneado, el zoom ya al tope) y el informe acabaría hablando de una escena que el usuario
                // nunca ve. Se espera y se mide UNA vez.
                bool ready = false;
                for (int attempt = 0; attempt < 40 && !ready; attempt++)
                {
                    Thread.Sleep(attempt == 0 ? 600 : 150);
                    ready = Marshal(window, () => WaitForCanvas(window));
                }

                if (!ready)
                {
                    report.AppendLine("Sonda del host de escritorio (FileFlow.App --selfcheck)");
                    report.AppendLine();
                    report.AppendLine("[FALLO] el lienzo no llegó a materializarse con tamaño: no hay nada que medir");
                    report.AppendLine();
                    report.AppendLine("0 [OK] · 1 [FALLO] · FALLO");
                }
                else
                {
                    verified = Marshal(window, () => Inspect(window, report));
                }

                // El informe se escribe en cuanto hay veredicto (y otra vez al salir, por si el hilo muere
                // antes): el fichero es la evidencia de lo que se midió, no un residuo de la ejecución.
                WriteReport(report);
            }
            catch (Exception ex)
            {
                report.AppendLine($"EXCEPCIÓN fuera de la sonda: {ex.GetType().Name}: {ex.Message}");
                WriteReport(report);
            }

            Console.Out.Flush();
            Console.WriteLine(report.ToString());
            Console.Out.Flush();

            Dispatcher.UIThread.Post(() => desktop.Shutdown(verified ? 0 : 1));
        })
        {
            IsBackground = true,
            Name = "DesktopSelfCheck"
        }.Start();
    }

    /// <summary>
    /// Ejecuta una función en el hilo de UI (dueño del árbol) y espera su resultado. El hilo de UI sigue
    /// bombeando mensajes mientras tanto, que es lo que hace que la ventana pueda seguir asentándose.
    /// </summary>
    private static T Marshal<T>(Window window, Func<T> action)
    {
        T result = default!;
        var done = new ManualResetEventSlim(false);

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                Note(window, ex);
            }
            finally
            {
                done.Set();
            }
        });

        done.Wait(TimeSpan.FromSeconds(120));
        return result;
    }

    /// <summary>Un fallo dentro de la medida se convierte en informe: el proceso no puede morir sin veredicto.</summary>
    private static void Note(Window window, Exception exception)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, ReportFileName),
                $"EXCEPCIÓN en la sonda: {exception.GetType().Name}: {exception.Message}{Environment.NewLine}{exception.StackTrace}{Environment.NewLine}");
        }
        catch
        {
            // Sin informe no hay evidencia, pero el veredicto (código de salida) sigue siendo válido.
        }
    }

    /// <summary>Escribe el informe junto al ejecutable. Un informe que no se puede escribir no tumba la sonda.</summary>
    private static void WriteReport(StringBuilder report)
    {
        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, ReportFileName), report.ToString());
        }
        catch
        {
            // El veredicto es el código de salida; el informe es la evidencia.
        }
    }

    /// <summary>
    /// ¿Está el lienzo materializado? Se comprueba sin tocar nada: hasta que el árbol no tiene tamaño no hay
    /// nada que medir, y una sonda que mide un lienzo sin tamaño miente con un rojo que no es un defecto.
    /// </summary>
    private static bool WaitForCanvas(Window window) =>
        window.GetVisualDescendants().OfType<NodifyEditor>()
            .Any(canvas => canvas.Bounds.Width > 200 && canvas.Bounds.Height > 200);

    // ─────────────────────────────────────────────────────────────────────────────
    // La medida (siempre en el hilo de UI)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La medida completa: escena, trazo, pan, zoom y reparto de botones. Devuelve el veredicto.
    /// </summary>
    private static bool Inspect(Window window, StringBuilder report)
    {
        var checks = new Checks(report);
        report.Clear();

        report.AppendLine("Sonda del host de escritorio (FileFlow.App --selfcheck)");
        report.AppendLine($"plataforma: headless de Avalonia con Skia real · puntero inyectado por el pipeline de "
            + "entrada del framework (el dedo del sistema operativo NO entra aquí: eso es una sesión manual)");
        report.AppendLine();

        var main = window.DataContext as MainViewModel;
        checks.Check(main is not null, "el ViewModel raíz del host está montado");
        if (main is null)
        {
            return checks.Finish();
        }

        EditorViewModel editor = main.Editor;
        var canvas = window.GetVisualDescendants().OfType<NodifyEditor>().FirstOrDefault();
        checks.Check(canvas is not null && canvas.Bounds.Width > 200 && canvas.Bounds.Height > 200,
            $"el lienzo está materializado y con tamaño ({Format(canvas?.Bounds)}), no una raíz sin layout");
        if (canvas is null)
        {
            return checks.Finish();
        }

        // ── La escena: un grafo de verdad, cargado por el mismo camino que usa el host ──
        LoadSampleFlow(editor, checks);
        Settle(window);

        checks.Check(editor.Nodes.Count > 0, $"el grafo tiene nodos ({editor.Nodes.Count})");
        checks.Check(editor.Connections.Count > 0, $"el grafo tiene cables ({editor.Connections.Count})");
        if (editor.Connections.Count == 0)
        {
            return checks.Finish();
        }

        // ── El trazo, con el plano quieto ──
        report.AppendLine();
        report.AppendLine("· el trazo del cable (plano quieto)");
        var atRest = Measure(window, checks);
        CheckLanding(checks, atRest, "en reposo");

        // ── El pan con el botón derecho ──
        report.AppendLine();
        report.AppendLine("· el pan con el botón derecho (puntero real)");
        var panDelta = new Vector(120, 80);
        var beforePan = editor.ViewportLocation;
        double zoomAtPan = editor.ViewportZoom;
        var strokeBeforePan = atRest;

        RightDragOnTheCanvas(window, checks, panDelta);

        var panMoved = new Vector(
            editor.ViewportLocation.X - beforePan.X,
            editor.ViewportLocation.Y - beforePan.Y);

        checks.Check(Approximately(panMoved.X, -panDelta.X / zoomAtPan, 0.75)
                     && Approximately(panMoved.Y, -panDelta.Y / zoomAtPan, 0.75),
            $"el encuadre se mueve el gesto dividido por el zoom (medido {Format(panMoved)}, esperado "
            + $"{Format(new Vector(-panDelta.X / zoomAtPan, -panDelta.Y / zoomAtPan))})");

        var afterPan = Measure(window, checks);
        CheckLanding(checks, afterPan, "tras el pan");

        if (strokeBeforePan is not null && afterPan is not null)
        {
            var strokeShift = afterPan.StrokeStart - strokeBeforePan.StrokeStart;
            checks.Check(Approximately(strokeShift.X, panDelta.X / zoomAtPan, 1.5)
                         && Approximately(strokeShift.Y, panDelta.Y / zoomAtPan, 1.5),
                $"el trazo sigue a la mano: se desplaza en la ventana {Format(strokeShift)} con un gesto de {Format(panDelta)}");
        }

        checks.Check(editor.ViewportLocation != beforePan,
            "hay un encuadre nuevo que medir: sin movimiento, lo de arriba no probaría nada");

        // ── El zoom con la rueda ──
        report.AppendLine();
        report.AppendLine("· el zoom con la rueda (puntero real)");

        // Si la escena arrancara ya con el zoom al tope (un documento que se abriera así), "acercar" no tendría
        // nada que medir: se aleja primero con la rueda —otro gesto real— hasta que haya margen para acercar.
        for (int guard = 0; guard < 6 && editor.ViewportZoom >= MaxZoom; guard++)
        {
            WheelOverTheCanvas(window, checks, -1);
        }

        double zoomBefore = editor.ViewportZoom;

        WheelOverTheCanvas(window, checks, 1);
        double zoomedIn = editor.ViewportZoom;
        checks.Check(zoomedIn > zoomBefore, $"la rueda hacia arriba acerca ({zoomBefore:F3} → {zoomedIn:F3})");
        CheckLanding(checks, Measure(window, checks), "acercado");

        WheelOverTheCanvas(window, checks, -2);
        double zoomedOut = editor.ViewportZoom;
        checks.Check(zoomedOut < zoomedIn, $"la rueda hacia abajo aleja ({zoomedIn:F3} → {zoomedOut:F3})");
        CheckLanding(checks, Measure(window, checks), "alejado");

        var strokeAtZoomIn = Measure(window, checks)?.StrokeStart;
        for (int notch = 0; notch < CeilingNotches; notch++)
        {
            WheelOverTheCanvas(window, checks, 1);
        }

        double atCeiling = editor.ViewportZoom;
        checks.Check(atCeiling <= MaxZoom + 0.0001,
            $"el zoom tiene techo: {CeilingNotches} muescas seguidas lo dejan en {atCeiling:F3} (tope {MaxZoom:F2})");
        checks.Check(atCeiling > 2, $"el techo se alcanza de verdad ({atCeiling:F3}), no se queda a medio camino");

        var atTop = Measure(window, checks);
        CheckLanding(checks, atTop, $"con el zoom al tope ({atCeiling:F2})");

        if (strokeAtZoomIn is AvaloniaPoint zoomedStroke && atTop is not null)
        {
            checks.Check(Gap(atTop.StrokeStart, zoomedStroke) > 1,
                "cambiar el zoom mueve el trazo en la ventana: si no se moviera, el aterrizaje no se estaría midiendo en píxeles");
        }

        // ── El botón que NO panea ──
        report.AppendLine();
        report.AppendLine("· el izquierdo sobre el fondo (el gesto que NO panea)");

        // El arrastre de una TARJETA se mide en el suite, con la escena bajo control (el mismo gesto arrastra el
        // documento: la tarjeta se lleva al frente —lo que reordena la colección— y cerca del borde el editor
        // auto-paneea mientras se arrastra, que es otro gesto). Aquí se mide lo que sostiene el reparto de
        // botones: IZQUIERDO sobre el fondo libre no mueve el plano, sólo el DERECHO panea.
        var viewportBeforeLeft = editor.ViewportLocation;
        AvaloniaPoint? leftStart = FreeCanvasPoint(window, checks);
        Vector leftDelta = new(90, 60);

        if (leftStart is null)
        {
            checks.Check(false, "había fondo libre donde probar el botón izquierdo");
        }
        else
        {
            Drag(window, leftStart.Value, leftDelta, MouseButton.Left);

            checks.Check(editor.ViewportLocation == viewportBeforeLeft,
                "el botón izquierdo NO panea: si el paneo respondiera al izquierdo, seleccionar o arrastrar sobre el fondo movería el plano entero");
        }

        CheckLanding(checks, Measure(window, checks), "tras el gesto con el izquierdo");

        return checks.Finish();
    }

    /// <summary>
    /// Carga el primer flujo de ejemplo con cables que encuentre en las carpetas canónicas del producto, con el
    /// mismo camino que el host Uno usa en su arranque (<c>WorkflowGraph.FromJson</c> + <c>LoadFromGraphModel</c>)
    /// y sin tocar las preferencias del usuario: la escena de la medida es un grafo real, y el informe dice cuál.
    /// </summary>
    private static void LoadSampleFlow(EditorViewModel editor, Checks checks)
    {
        if (editor.Connections.Count > 0)
        {
            checks.Check(true, "el lienzo ya traía un grafo con cables: se mide ése");
            return;
        }

        string? root = AppContext.BaseDirectory;
        while (root is not null && !Directory.Exists(Path.Combine(root, "docs", "examples")))
        {
            root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
        }

        foreach (string candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "Examples"),
                     root is null ? string.Empty : Path.Combine(root, "docs", "examples")
                 })
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(candidate, "*.json", SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var graph = WorkflowGraph.FromJson(File.ReadAllText(file));
                    if (graph.Edges.Count == 0)
                    {
                        continue;
                    }

                    editor.LoadFromGraphModel(graph);
                    checks.Check(true,
                        $"el ejemplo de la medida se cargó ({graph.Nodes.Count} nodos, {graph.Edges.Count} cables: "
                        + $"{Path.GetFileName(file)})");
                    return;
                }
                catch (Exception ex)
                {
                    // Un ejemplo que no se puede leer no es el de la medida: se prueba el siguiente.
                    checks.Note($"ejemplo ilegible, se salta: {Path.GetFileName(file)} ({ex.GetType().Name})");
                }
            }
        }

        checks.Check(false,
            "había un ejemplo con cables que cargar: sin grafo no hay trazo que medir (¿se ejecuta desde el repositorio?)");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // La medida del trazo
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Lo medido de un cable, con los puntos crudos para que un rojo diga <b>dónde</b> quedó el trazo.</summary>
    private sealed record Landing(
        string Connection,
        double SourceGap,
        double TargetGap,
        AvaloniaPoint StrokeStart,
        AvaloniaPoint StrokeEnd,
        AvaloniaPoint SourceSocket,
        AvaloniaPoint TargetSocket)
    {
        public string Describe(string when) =>
            $"{when}: {Connection} va de {StrokeStart} a {StrokeEnd}; los puntos dibujados de sus sockets están en "
            + $"{SourceSocket} y {TargetSocket} (huecos: origen {SourceGap:F2} px, destino {TargetGap:F2} px)";
    }

    /// <summary>
    /// Mide el trazo de <b>todos</b> los cables del lienzo: que cada uno sea del control del host y salga del
    /// trazador del núcleo, y que su extremo caiga sobre el punto dibujado de su socket en píxeles de ventana.
    /// </summary>
    private static Landing? Measure(Window window, Checks checks)
    {
        var editor = (window.DataContext as MainViewModel)?.Editor;
        if (editor is null)
        {
            return null;
        }

        var wires = window.GetVisualDescendants().OfType<FlowConnection>().ToList();
        checks.Check(wires.Count == editor.Connections.Count,
            $"cada cable del grafo está dibujado con el control del host ({wires.Count} dibujados de "
            + $"{editor.Connections.Count})");

        Landing? first = null;

        foreach (var wire in wires)
        {
            if (wire.DataContext is not ConnectionViewModel connection)
            {
                checks.Check(false, "un cable dibujado no tiene su ViewModel de conexión: no hay ancla que medir");
                continue;
            }

            var landing = Measure(window, wire, connection, checks);
            first ??= landing;
        }

        return first;
    }

    private static Landing? Measure(Window window, FlowConnection wire, ConnectionViewModel connection, Checks checks)
    {
        var figure = (wire.DefiningGeometry as PathGeometry)?.Figures?.FirstOrDefault();
        if (figure is null || figure.Segments?.FirstOrDefault() is not BezierSegment curve)
        {
            checks.Check(false, "la figura del cable es una sola curva Bézier (lo que traza el núcleo)");
            return null;
        }

        // La curva es la del trazador compartido: los cuatro puntos se comparan con el núcleo, que es lo que
        // distingue el cable del producto de la curva que traía el control de conexión de Nodify.
        ConnectionGeometry.WirePath expected = ConnectionGeometry.BuildWire(
            connection.Source.Anchor,
            connection.Target.Anchor,
            ConnectionGeometry.FlowDirection.Forward);

        checks.Check(
            Same(figure.StartPoint, expected.Source) && Same(curve.Point1, expected.Exit)
            && Same(curve.Point2, expected.Arrival) && Same(curve.Point3, expected.Target),
            "la curva del cable es la del núcleo (nace en el ancla, muere en el ancla y el cuello es el suyo)");

        var source = SocketOf(window, connection.Source, isInput: false);
        var target = SocketOf(window, connection.Target, isInput: true);
        if (source is null || target is null)
        {
            checks.Check(false, "los dos sockets del cable están materializados en el árbol visual");
            return null;
        }

        // El trazo se mide donde se PINTA (su figura va en unidades del grafo) y se proyecta al espacio de la
        // ventana con la cadena entera: shape → contenedor → lienzo → transformación del encuadre.
        AvaloniaPoint strokeStart = wire.TranslatePoint(figure.StartPoint, window) ?? default;
        AvaloniaPoint strokeEnd = wire.TranslatePoint(curve.Point3, window) ?? default;
        AvaloniaPoint sourceSocket = SocketDotCentre(source, window);
        AvaloniaPoint targetSocket = SocketDotCentre(target, window);

        return new Landing(
            connection.Source.DisplayName ?? "origen",
            Gap(strokeStart, sourceSocket),
            Gap(strokeEnd, targetSocket),
            strokeStart,
            strokeEnd,
            sourceSocket,
            targetSocket);
    }

    /// <summary>El veredicto del aterrizaje: el trazo sale y entra por donde el usuario ve el socket.</summary>
    private static void CheckLanding(Checks checks, Landing? landing, string when)
    {
        if (landing is null)
        {
            checks.Check(false, $"({when}) había un trazo que medir");
            return;
        }

        checks.Check(landing.SourceGap < LandingTolerance && landing.TargetGap < LandingTolerance,
            landing.Describe(when));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Los gestos: puntero inyectado por el pipeline de entrada de Avalonia
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Arrastra el fondo del lienzo con el botón derecho (el paneo que mapea el editor).</summary>
    private static void RightDragOnTheCanvas(Window window, Checks checks, Vector delta)
    {
        var start = FreeCanvasPoint(window, checks);
        if (start is null)
        {
            checks.Check(false, "había fondo libre en el lienzo donde empezar un paneo");
            return;
        }

        Drag(window, start.Value, delta, MouseButton.Right);
    }

    /// <summary>
    /// El gesto de arrastre completo: mover, pulsar, los pasos y soltar. En pasos, como lo hace una mano: un
    /// salto único no ejercita el arrastre continuo —y es en los pasos donde el editor decide que está paneando
    /// o que la tarjeta se está moviendo—.
    /// </summary>
    private static void Drag(Window window, AvaloniaPoint start, Vector delta, MouseButton button)
    {
        Input(window, start);
        window.MouseDown(start, button);
        Settle(window);

        for (int step = 1; step <= DragSteps; step++)
        {
            Input(window, start + (delta * (step / (double)DragSteps)));
        }

        window.MouseUp(start + delta, button);
        Settle(window);
    }

    /// <summary>Gira la rueda sobre el fondo libre del lienzo.</summary>
    private static void WheelOverTheCanvas(Window window, Checks checks, int notches)
    {
        var point = FreeCanvasPoint(window, checks);
        if (point is null)
        {
            checks.Check(false, "había fondo libre en el lienzo donde girar la rueda");
            return;
        }

        Input(window, point.Value);
        window.MouseWheel(point.Value, new Vector(0, notches * 120));
        Settle(window);
    }

    private static void Input(Window window, AvaloniaPoint point)
    {
        window.MouseMove(point);
        Settle(window);
    }

    /// <summary>
    /// Asienta el árbol: el layout y el render pendientes se despachan antes de medir. Sin esto se mediría el
    /// árbol de antes del gesto y el rojo (o el verde) hablaría del fotograma equivocado.
    /// </summary>
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Un punto del fondo libre del lienzo, buscado por <b>hit-testing</b>: el lienzo tiene encima piezas
    /// flotantes legítimas (la barra de zoom, el HUD de telemetría al pie, el buscador) y las tarjetas están
    /// donde el grafo las ponga, así que fijar un punto —la esquina inferior derecha, por ejemplo— manda el
    /// gesto a la pieza que esté encima y la sonda mide el silencio del lienzo.
    /// </summary>
    private static AvaloniaPoint? FreeCanvasPoint(Window window, Checks checks)
    {
        var canvas = window.GetVisualDescendants().OfType<NodifyEditor>().FirstOrDefault();
        if (canvas is null)
        {
            return null;
        }

        for (double y = 80; y < canvas.Bounds.Height - 80; y += 24)
        {
            for (double x = 80; x < canvas.Bounds.Width - 80; x += 24)
            {
                var inWindow = canvas.TranslatePoint(new AvaloniaPoint(x, y), window);
                if (inWindow is not null && IsFreeCanvas(window, canvas, inWindow.Value))
                {
                    return inWindow.Value;
                }
            }
        }

        checks.Note("no se encontró fondo libre: el grafo o las piezas flotantes tapan el lienzo entero");
        return null;
    }

    /// <summary>
    /// ¿El punto lo responde el <b>lienzo</b>? Ni una tarjeta, ni un decorador, ni un cable, ni una de las
    /// piezas flotantes del editor (barra de zoom, HUD, buscador), que están encima y se quedan el clic.
    /// </summary>
    private static bool IsFreeCanvas(Window window, NodifyEditor canvas, AvaloniaPoint point)
    {
        for (Visual? hit = window.InputHitTest(point) as Visual; hit is not null; hit = hit.GetVisualParent())
        {
            if (ReferenceEquals(hit, canvas))
            {
                return true;
            }

            if (hit is ItemContainer or DecoratorContainer or ConnectionContainer
                || hit.GetType().Name.Contains("ZoomBar", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Piezas compartidas de la medida
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El <b>punto dibujado</b> del socket: el centro de la figura de la plantilla de socket (la clase
    /// <c>socket</c> o su triángulo), que es lo que el usuario ve y donde Nodify sitúa el ancla del puerto.
    /// Medir contra el control del conector entero —que incluye la etiqueta del puerto— da un hueco que es de la
    /// etiqueta, no del cable.
    /// </summary>
    private static AvaloniaPoint SocketDotCentre(Visual connector, Visual window)
    {
        Control? dot = connector.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.Classes.Contains("socket") || control.Classes.Contains("socketTriangle"));

        return CentreOf(dot ?? connector, window);
    }

    private static AvaloniaPoint CentreOf(Visual control, Visual window) =>
        control.TranslatePoint(
            new AvaloniaPoint(control.Bounds.Width / 2, control.Bounds.Height / 2),
            window) ?? default;

    /// <summary>
    /// El socket de un puerto, por el camino del árbol real: su cabecera lleva el puerto del ViewModel (el
    /// DataContext del control de Nodify es la tarjeta; el puerto llega por el <c>Header</c>).
    /// </summary>
    private static Visual? SocketOf(Visual root, PortViewModel port, bool isInput)
    {
        if (isInput)
        {
            return root.GetVisualDescendants().OfType<NodeInput>().Cast<Visual>()
                .FirstOrDefault(socket => Serves(socket, ((NodeInput)socket).Header, port));
        }

        return root.GetVisualDescendants().OfType<NodeOutput>().Cast<Visual>()
            .FirstOrDefault(socket => Serves(socket, ((NodeOutput)socket).Header, port));
    }

    private static bool Serves(Visual socket, object? header, PortViewModel port) =>
        ReferenceEquals(header, port)
        || socket.GetVisualDescendants().OfType<Control>()
            .Any(descendant => ReferenceEquals(descendant.DataContext, port));

    private static double Gap(AvaloniaPoint a, AvaloniaPoint b) => new Vector(a.X - b.X, a.Y - b.Y).Length;

    private static bool Same(AvaloniaPoint drawn, SdkPoint expected) =>
        Gap(drawn, expected.ToAvalonia()) < 0.0001;

    private static bool Approximately(double actual, double expected, double tolerance) =>
        Math.Abs(actual - expected) <= tolerance;

    private static string Format(AvaloniaPoint point) =>
        $"({point.X.ToString("F1", CultureInfo.InvariantCulture)}, {point.Y.ToString("F1", CultureInfo.InvariantCulture)})";

    private static string Format(Vector vector) =>
        $"({vector.X.ToString("F1", CultureInfo.InvariantCulture)}, {vector.Y.ToString("F1", CultureInfo.InvariantCulture)})";

    private static string Format(Rect? rect) =>
        rect is null ? "sin tamaño" : $"{Format(rect.Value.TopLeft)} {rect.Value.Width:F0}×{rect.Value.Height:F0}";

    /// <summary>
    /// El cuaderno de comprobaciones: una línea <c>[OK]</c>/<c>[FALLO]</c> por expectativa, el recuento al pie y
    /// el veredicto del informe. El ancho no se recorta: el informe es para leerlo, no para una consola estrecha.
    /// </summary>
    private sealed class Checks(StringBuilder report)
    {
        private int _ok;
        private int _failed;

        public void Note(string message) => report.AppendLine($"       · {message}");

        public void Check(bool holds, string description)
        {
            if (holds)
            {
                _ok++;
                report.AppendLine($"[OK]    {description}");
                return;
            }

            _failed++;
            report.AppendLine($"[FALLO] {description}");
        }

        public bool Finish()
        {
            report.AppendLine();
            report.AppendLine($"{_ok} [OK] · {_failed} [FALLO] · {(_failed == 0 ? "VERIFICADO" : "FALLO")}");
            return _failed == 0;
        }
    }
}
