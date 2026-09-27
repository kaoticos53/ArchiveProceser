using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using FileFlow.App.Uno.Controls;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
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
    {        new Thread(() =>        {            var lastReport = new StringBuilder("[sin intento completado]");            var ok = false;            // La materialización de las plantillas ocurre en el pase de layout, después del Activate.            for (int attempt = 0; attempt < 30 && !ok && !_performanceProbeRan; attempt++)            {                Thread.Sleep(attempt == 0 ? 300 : 200);                // Cada intento parte de un bloque limpio: selfcheck-report.txt cuenta SIEMPRE lo que el                // último intento vio, no la historia de los intentos de espera («lienzo sin tamaño»,                // «grafo sin cargar»), que era ruido de diagnóstico. La consola recibe el bloque final.                lastReport.Clear();                var completed = new ManualResetEventSlim(false);                dispatcher.TryEnqueue(() =>                {                    try                    {                        ok = Inspect(window, lastReport);                    }                    catch (Exception ex)                    {                        lastReport.AppendLine("EXCEPCIÓN en el sondeo: " + ex.GetType().Name + ": " + ex.Message                            + Environment.NewLine + ex.StackTrace);                    }                    finally                    {                        completed.Set();                    }                });                completed.Wait(TimeSpan.FromSeconds(10));                // Escritura POR intento: si el proceso muere a mitad del sondeo, el fichero cuenta el                // último intento completo y no queda a medias con una mezcla de épocas.                try                {                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selfcheck-report.txt"), lastReport.ToString());                }                catch { }            }            // Exit desde un hilo de fondo no purga buffers ni ejecuta finalizers: imprimir el bloque final            // y forzar el flush antes de salir.            Console.Out.Flush();            Console.WriteLine(lastReport.ToString());            Console.Out.Flush();            Environment.Exit(ok ? 0 : 1);        })
        {
            IsBackground = true,
            Name = "RuntimeSelfCheck"
        }.Start();

        return -1; // el proceso termina por Environment.Exit dentro del sondeo
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

        // Cables: las Bézier del núcleo materializadas como Paths en la capa de cables.
        int wirePaths = CountWirePaths(canvas);
        Check(wirePaths >= editor.Connections.Count,
            $"cables dibujados en la capa: {(wirePaths < 0 ? "capa no encontrada" : wirePaths.ToString())} (esperados >= {editor.Connections.Count})");

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
                        "el Pivot conmuta a la pestaña de snapshots y las tarjetas quedan en el árbol");

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

        // Fase 3.5 (sin puntero): cambiar el tema por la API del núcleo tiene que re-tematizar el
        // lienzo EN CALIENTE — fondo y tarjetas con los valores del tema nuevo (los pinceles que los
        // ThemeResource del XAML consumen, republicados por UnoThemeHost). La restauración deja el
        // dark_fluent activo como al entrar.
        try
        {
            var (backgroundChanged, cardChanged, variantChanged, restored) = canvas.ProbeThemeRepublish();
            Check(backgroundChanged, "cambiar el tema re-tematiza el fondo del lienzo en caliente");
            Check(cardChanged, "las tarjetas adoptan el color del tema nuevo (ThemeResource ya evaluado se actualiza)");
            Check(variantChanged, "la variante clara/oscurecida se publica en la raíz del contenido");
            Check(restored, "la restauración del tema deja el dark_fluent activo (grafo como al entrar)");
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
