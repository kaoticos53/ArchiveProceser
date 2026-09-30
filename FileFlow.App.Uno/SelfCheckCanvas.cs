using System;
using System.Linq;
using System.Text;
using FileFlow.App.Uno.Controls;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno;

/// <summary>
/// El sondeo del LIENZO del host (<c>--selfcheck</c>, el modo base): arranca la aplicación real y recorre el
/// árbol visual de la tarjeta para confirmar que los bindings, recursos y conversores se resuelven —la prueba
/// que la compilación no puede dar—. Mide lo que se VE y lo que el núcleo registró: tarjetas materializadas
/// con su título, su icono y su posición proyectada, el área de clic contra la tarjeta dibujada, los cables, y
/// las fases 3.2/3.3/3.4 por los MISMOS métodos que ejecutan los handlers (selección, conexión, edición de
/// decoradores), siempre con restauración.
///
/// <para><b>Quién afirma</b>: la sonda —el <c>[OK]</c>/<c>[FALLO]</c> y el veredicto los escribe el
/// comprobador que este archivo construye y pasa a cada preocupación—. El marco (hito 272) se mide en
/// <c>SelfCheckFrame.cs</c>, los dos paneles en <c>SelfCheckPanels.cs</c> y lo que el puntero no puede
/// recorrer aquí en <c>SelfCheckPointerless.cs</c>. Las fases 3.5 (el tema en caliente) y 3.6 (el rendimiento
/// con el grafo de referencia) son de este archivo porque su sujeto es el lienzo: la 3.6 es ONE-SHOT y deja
/// el proceso entero sin reintento (<c>PerformanceProbeRan</c>, que lee el despachador). Sus reglas las fijan
/// <c>UnoHitTestSpaceGuardTests</c>, <c>UnoCanvasWireGuardTests</c>, <c>UnoCanvasKeyboardGuardTests</c> y
/// <c>UnoSelfCheckLayoutGuardTests</c>.</para>
/// </summary>
internal static class SelfCheckCanvas
{

    /// <summary>
    /// La sonda de rendimiento (fase 3.6) ya corrió en este proceso: el árbol queda ONE-SHOT. La escribe la
    /// fase 3.6 —sólo cuando el árbol llegó sano— y la lee el bucle de reintentos del despachador, que tras
    /// ella no vuelve a medir (el add/remove masivo de 40 tarjetas deja la materialización de WinUI frágil).
    /// </summary>
    internal static bool PerformanceProbeRan { get; private set; }

    /// <summary>
    /// El recorrido del modo base: el marco, el lienzo y sus tarjetas, el área de clic, las fases 3.2 a 3.4 de
    /// interacción, los dos paneles, lo que el puntero no puede recorrer aquí, el tema en caliente y el
    /// rendimiento. Devuelve el veredicto; el <c>[OK]</c>/<c>[FALLO]</c> y el informe los escribe el
    /// comprobador que este método construye.
    /// </summary>
    internal static bool Run(Window window, StringBuilder report)
    {
        bool ok = true;

        void Check(bool condition, string what)
        {
            report.AppendLine((condition ? "[OK]   " : "[FALLO]") + " " + what);
            ok &= condition;
        }

        report.AppendLine("=== Sondeo en runtime del host Uno (fase 3.1) ===");

        // El MARCO (hito 272): la barra arriba, el editor debajo y las tres zonas con caja propia y
        // disjuntas. Su instrumento y el porqué de cada medida viven en SelfCheckFrame.
        SelfCheckFrame.Check(window, Check);

        var canvas = SelfCheckTree.Find<EditorCanvasControl>(window.Content);
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
        var cards = SelfCheckTree.FindAll<NodeCardView>(window.Content).ToList();
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
            var container = SelfCheckTree.FindAscendant<Microsoft.UI.Xaml.Controls.ContentPresenter>(card);
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
            var allPaths = SelfCheckTree.FindAll<Microsoft.UI.Xaml.Shapes.Path>(card).ToList();
            int withData = allPaths.Count(p => p.Data is not null);
            report.AppendLine("       [diagnóstico] Paths con Data: " + withData + "/" + allPaths.Count
                + " | x:Name TypeIcon " + (icon is null ? "NO resuelto por FindName" : "resuelto"));

            // Barra de acento: el color hex del núcleo llegó como Color de WinUI.
            var accent = vm.AccentBrushColor;
            Check(accent.A == 255 && (accent.R != 0 || accent.G != 0 || accent.B != 0),
                $"barra de acento con color parseado: #{accent.R:X2}{accent.G:X2}{accent.B:X2}");

            // Puertos dibujados con la matriz: el socket (borde >= 2 en un cuadro pequeno) o el triangulo.
            var expectedPorts = node.InputPorts.Count + node.OutputPorts.Count;
            var triangles = SelfCheckTree.FindAll<Microsoft.UI.Xaml.Shapes.Path>(card).Count();
            var boxes = SelfCheckTree.FindAll<Border>(card)
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
        Check(wirePaths == editor.Connections.Count,
            $"cables dibujados en la capa: {(wirePaths < 0 ? "capa no encontrada" : wirePaths.ToString())} (esperados {editor.Connections.Count})");

        // La SELECCIÓN del cable (el defecto del usuario: «no puedo seleccionar las conexiones para
        // borrarlas»): cada cable se VE y cada cable se puede PULSAR —su diana en la capa, con la misma
        // figura—, y el menú que abre lleva la orden del núcleo con su rótulo del diccionario. Las dos
        // mitades se miden juntas: una diana sin orden es un clic que no borra nada, y una orden sin diana
        // es una capacidad invisible.
        try
        {
            var (wireConnections, hitTargets, sameFigure, coreOrder, menuText,
                 markIsVisible, deleteByShortcut, restored) = canvas.ProbeWireSelection();
            Check(wireConnections > 0 && hitTargets == wireConnections && sameFigure,
                $"cada cable del grafo tiene su diana en la capa ({hitTargets} de {wireConnections}): "
                + "el cable que se ve se puede pulsar");
            Check(coreOrder && !string.IsNullOrWhiteSpace(menuText) && menuText != "Uno_Connection_Delete",
                $"y su menú es el del núcleo, con el rótulo del diccionario: '{menuText}'");
            Check(markIsVisible,
                "el cable marcado SE VE marcado: su trazo pasa al acento de selección y engorda");
            Check(deleteByShortcut && restored,
                "y el Supr de la tabla del núcleo borra el cable marcado, con el undo restaurándolo");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de selección del cable lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // La REGLA DE SELECCIÓN (hito 285): pulsar REEMPLAZA y Ctrl AÑADE —en las tarjetas y en los cables—,
        // y el conjunto marcado se borra con UNA sola operación de deshacer. El estado queda como al entrar.
        //
        // Por qué corre AQUÍ y no al final del bloque de selección: el gesto vivo toca IsSelected de una
        // tarjeta, y su refresco pinta el resalte sobre el árbol visual. Después del ciclo borrar+deshacer
        // (más abajo) las tarjetas se reconstruyeron sin pase de layout —el sondeo es síncrono—, así que el
        // resalte de una recién creada no tiene contenedor y el refresco falla con E_FAIL. Delante de ese
        // ciclo las tarjetas están materializadas, que es el estado del gesto real.
        try
        {
            var (nodeReplaces, nodeAdds, wireReplaces, wireAdds, batchOneUndo, detail) = canvas.ProbeSelectionRule();
            Check(nodeReplaces && wireReplaces,
                "pulsar reemplaza la selección: los demás nodos se sueltan y elegir un cable suelta los nodos");
            Check(nodeAdds && wireAdds,
                "y con Ctrl se AÑADE a lo elegido: dos nodos o dos cables quedan elegidos a la vez");
            Check(batchOneUndo,
                "los cables marcados se borran de UNA vez y un solo deshacer los devuelve: " + detail);
        }
        catch (Exception ex)
        {
            Check(false, "sonda de la regla de selección lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // El RECTÁNGULO de selección (hito 286): marca tarjetas Y cables A LA VEZ —lo que lo distingue del
        // clic— y el modificador decide lo de fuera (con Ctrl no se suelta nada; sin Ctrl manda el área).
        try
        {
            var (rubberNodes, rubberWires, marksBoth, ctrlKeeps, plainReleases, rubberDetail) =
                canvas.ProbeRubberBand();
            Check(rubberNodes > 0 && rubberWires > 0 && marksBoth,
                "el rectángulo elige tarjetas y cables A LA VEZ (lo que el clic no hace): " + rubberDetail);
            Check(ctrlKeeps && plainReleases,
                "y con Ctrl AÑADE sin soltar lo de fuera, mientras sin Ctrl lo suelta todo");
        }
        catch (Exception ex)
        {
            Check(false, "sonda del rectángulo de selección lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // El BORRADO MIXTO (hito 287): lo que deja el rectángulo —un nodo y un cable a la vez— se borra entero
        // con Supr y UN solo deshacer lo devuelve. Antes el Supr se llevaba los cables y dejaba los nodos.
        try
        {
            var (bothGone, oneUndoRestoresBoth, mixedDetail) = canvas.ProbeMixedDeletion();
            Check(bothGone && oneUndoRestoresBoth,
                "Supr se lleva el nodo Y el cable de golpe, y un solo deshacer devuelve los dos: " + mixedDetail);
        }
        catch (Exception ex)
        {
            Check(false, "sonda del borrado mixto lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

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
            Check(groupOk, "grupo creado y borrado (detras de las notas, como en la versión anterior)");
            Check(spotlightOk, "spotlight añade un nodo real en el punto del grafo pedido");
            Check(breadcrumbOk, "migas de subflujo navegables (comando del núcleo)");
        }
        catch (Exception ex)
        {
            Check(false, "sonda de decoradores 3.4 lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // Rebanada 4 — los dos paneles del host: su instrumento vive en SelfCheckPanels.
        SelfCheckPanels.Check(
            SelfCheckTree.Find<NodeToolboxPanel>(window.Content),
            SelfCheckTree.Find<NodeInspectorPanel>(window.Content),
            editor,
            Check);


        // Las medidas que este entorno NO puede recorrer con el puntero real —la superficie que la
        // observación externa alcanza, el foco, el enrutado de los atajos, el seguimiento de los cables y
        // la reclamación del teclado—: su instrumento vive en SelfCheckPointerless, que recibe este mismo
        // comprobador (mide y devuelve; el veredicto lo escribe quien lo llama).
        SelfCheckPointerless.Check(canvas, Check);

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
                PerformanceProbeRan = true;

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
                PerformanceProbeRan = true;
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
            ?? SelfCheckTree.FindDescendantNamed<Panel>(canvas, "WireLayer"); // respaldo: el x:Name genera campo, pero el
                                                                 // nombre puede variar entre compilaciones
        return wires is null ? -1 : wires.Children.OfType<Microsoft.UI.Xaml.Shapes.Path>()
            .Count(p => Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(p)
                == EditorCanvasControl.WireAnchor);
    }
}
