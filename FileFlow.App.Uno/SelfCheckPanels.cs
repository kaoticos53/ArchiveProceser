using System;
using System.IO;
using System.Linq;
using System.Threading;
using FileFlow.App.Uno.Controls;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using Microsoft.UI.Xaml;

namespace FileFlow.App.Uno;

/// <summary>
/// La medida de los DOS PANELES del host —el cajón de herramientas y la ficha del inspector— y de las puertas
/// que cada uno abre. Es una preocupación aparte del resto del sondeo y vive en su propio archivo: los dos
/// paneles se miden juntos porque comparten sujeto (el flujo cargado y sus nodos), no porque compartan tema.
///
/// <para><b>Qué mide</b>, por orden: el catálogo poblado y las chips de categoría con caja dentro del cajón
/// (hito 272), el filtro que reduce, añadir por el método del doble clic, el favorito por comando y el
/// conmutador compacto/detallado (246); y en la ficha: la selección que la abre, los editores de parámetros, el
/// encabezado de Parámetros y las ACCIONES del nodo (269/274), el write-through al <c>NodeInstance</c>, el
/// cierre por comando, el «Probar» cableado (240) y OFRECIDO (274), las pestañas de snapshots y diff con los
/// datos del nodo y del VM (242), las secciones con caja dentro de la ficha (273) y la Telemetría montada y
/// pintada (275). Todo por el MISMO camino del usuario y con restauración: el sondeo no puede dejar el grafo,
/// una preferencia ni la ficha a medias.
///
/// <para><b>Quién afirma</b>: este archivo mide y afirma con el comprobador que le pasa
/// <see cref="RuntimeSelfCheck"/>; las medidas salen de la superficie de observación de cada panel
/// (<c>NodeToolboxPanel</c> y <c>NodeInspectorPanel.Probes.cs</c>), que nunca decide un veredicto.</para>
/// </summary>
internal static class SelfCheckPanels
{
    /// <summary>
    /// Las comprobaciones de los dos paneles sobre el flujo cargado. Los paneles se los pasa quien los
    /// resuelve del árbol (pueden ser <c>null</c>: la sonda lo dice y sigue).
    /// </summary>
    internal static void Check(
        NodeToolboxPanel? toolbox,
        NodeInspectorPanel? inspector,
        EditorViewModel? editor,
        Action<bool, string> check)
    {
        // Todo por el mismo camino del usuario y con restauración: el grafo, una preferencia y la ficha
        // quedan al terminar como estaban al entrar.
        try
        {
            check(toolbox is not null, "panel del cajón de herramientas montado en la ventana");
            check(inspector is not null, "panel del inspector montado en la ventana");

            if (toolbox is { } tb && inspector is { } insp && editor is { })
            {
                // 1. Catálogo poblado desde el VM del núcleo.
                int visible = tb.VisibleItemCount;
                check(visible > 0, $"catálogo del cajón poblado desde el ToolboxViewModel: {visible} ítems");

                // Las QUINCE chips de categoría, cada una con caja propia DENTRO del cajón (hito 272):
                // con la tira en una sola línea, once nacían recortadas contra el borde —caja vacía, sin
                // scroll ni rueda que las alcanzara— y ningún censo por declaración lo veía.
                var chipBoxes = tb.ChipBoxesForProbe();
                check(chipBoxes.Count == tb.DeclaredCategoryCount && chipBoxes.Count > 0
                        && chipBoxes.All(b => b.Width > 0 && b.Height > 0
                            && b.Left >= -0.5 && b.Top >= -0.5
                            && b.Right <= tb.ActualWidth + 0.5 && b.Bottom <= tb.ActualHeight + 0.5),
                    $"las {chipBoxes.Count} chips de categoría del view model tienen caja dentro del cajón (categorías declaradas: {tb.DeclaredCategoryCount})");

                // 2. Filtro de búsqueda que reduce el catálogo (el mismo setter que el binding).
                int beforeFilter = tb.VisibleItemCount;
                tb.SearchForProbe("Folder");
                int afterFilter = tb.VisibleItemCount;
                tb.SearchForProbe(string.Empty);
                check(afterFilter > 0 && afterFilter < beforeFilter,
                    $"el filtro reduce el catálogo: {beforeFilter} -> {afterFilter} con 'Folder' (restaurado)");

                // 3. Añadir el primer ítem por el método del doble clic, con undo de restauración.
                int nodesBefore = tb.EditorNodeCount;
                bool added = tb.TryAddFirstItemOfGroupForProbe();
                int nodesAfter = tb.EditorNodeCount;
                if (added)
                {
                    editor.UndoRedoService.Undo();
                }

                check(added && nodesAfter == nodesBefore + 1,
                    $"doble clic añade el nodo por EditorViewModel.AddNode: {nodesBefore} -> {nodesAfter} (undo restaurado)");

                // 4. Favorito por el comando del VM (el toggle de la estrella), reportando el estado.
                // El segundo toggle RESTAURA el estado original: el sondeo no puede dejar una
                // preferencia persistida del usuario a medio camino.
                bool favOk = tb.ToggleFavoriteViaCommand();
                bool favRestored = favOk && tb.ToggleFavoriteViaCommand();
                check(favOk && favRestored, "favorito conmutado por ToggleFavoriteCommand (y restaurado)");

                // Hito 246: el toggle compacto/detallado del cajón — el ÚLTIMO pendiente de código
                // de la rebanada 4. La sonda conmuta por el MISMO comando del VM que el botón y
                // compara el estado del árbol. El ritmo lo impone el diseño del VM: cada toggle
                // persiste en preferencias y el refresco REGENERA el catálogo (Save ->
                // PreferencesChanged -> RefreshToolbox) — entre pasos, asentar el dispatcher.
                // La sonda expande el primer grupo: con el acordeón colapsado no hay contenedores
                // que contar (medido: 0 de 0).
                var firstGroup = tb.FirstGroupWithItemsForProbe();
                check(firstGroup is not null, "el cajón trae grupos con ítems para la sonda del modo");

                if (firstGroup is { } group)
                {
                    group.IsExpanded = true;
                    Thread.Sleep(400);

                    var (detTotal, detHidden, detVisible) = tb.ProbeDetailsBlocks();
                    bool compactOk = tb.IsCompact && detVisible == 0;
                    check(compactOk,
                        $"el cajón arranca en compacto: {detTotal} bloques detallados materializados y {detVisible} visibles (0 esperados)");

                    tb.ToggleViewModeViaCommand();
                    Thread.Sleep(600);
                    var (d2, h2, v2) = tb.ProbeDetailsBlocks();
                    bool detailedOk = !tb.IsCompact && v2 > 0;
                    check(detailedOk,
                        $"el toggle conmuta a detallado por ToggleViewModeCommand: {v2} de {d2} bloques visibles (>0)");

                    tb.ToggleViewModeViaCommand();
                    Thread.Sleep(600);
                    var (d3, h3, v3) = tb.ProbeDetailsBlocks();
                    bool restoredOk = tb.IsCompact && v3 == 0;
                    check(restoredOk,
                        $"la vuelta a compacto oculta los detalles otra vez: {v3} de {d3} visibles (0 esperados)");

                    group.IsExpanded = false;
                    Thread.Sleep(200);
                }

                // 5. Inspector: abrir sobre un nodo real de la ventana (el flujo cargado) — la ficha
                // con parámetros materializados, el write-through al NodeInstance y el cierre por comando.
                var firstNode = editor.Nodes.FirstOrDefault();
                if (firstNode is null)
                {
                    check(false, "inspector: sin nodo para inspeccionar (el ejemplo no cargó)");
                }
                else
                {
                    insp.InspectForProbe(firstNode);
                    check(insp.Visibility == Visibility.Visible, "la selección abre el inspector (IsOpen del VM)");

                    int paramEditors = insp.ParameterEditorCount;
                    check(paramEditors == firstNode.Parameters.Count,
                        $"editores de parámetros materializados: {paramEditors} de {firstNode.Parameters.Count} parámetros");

                    // Hito 274: y el encabezado de la sección SIGUE a sus editores (la misma regla del bloque de
                    // acciones): un encabezado sobre una lista vacía promete algo que no hay.
                    check(insp.ParametersHeaderOffered == (paramEditors > 0),
                        $"el encabezado de Parámetros sigue a sus editores: {paramEditors} editores, encabezado "
                        + (insp.ParametersHeaderOffered ? "ofrecido" : "colapsado"));

                    // Hito 269: las ACCIONES del nodo en la ficha. Son la puerta a sus superficies (el gestor de
                    // presets, la configuración del VLM, el estudio de scripts...) y hasta aquí vivían SÓLO en el
                    // panel plegable de la tarjeta: quien no supiera desplegarla no llegaba a la superficie. La
                    // cuenta tiene que ser la de las acciones del nodo inspeccionado, ni una más ni una menos; el
                    // caso CON acciones —el botón y su ancla— se mide sobre el transcodificador, más abajo, donde
                    // el flujo de los paneles lo añade al lienzo.
                    check(insp.ActionButtonCount == firstNode.CustomActions.Count,
                        $"acciones del nodo en la ficha: {insp.ActionButtonCount} de "
                        + $"{firstNode.CustomActions.Count} declaradas por el nodo inspeccionado");

                    // Write-through: el mismo camino que la edición del usuario (p.Value = ...), una
                    // pareja parámetro/instancia con la MISMA clave.
                    var param = firstNode.Parameters.FirstOrDefault(p => !p.IsVariableInjectorNode);
                    if (param is null)
                    {
                        check(false, "sin parámetro editable para el write-through");
                    }
                    else
                    {
                        var inst = firstNode.NodeInstance.Parameters;
                        string key = param.Key;
                        string beforeValue = inst.TryGetValue(key, out var v0) ? v0?.ToString() ?? "" : "<sin clave>";
                        param.Value = "__probe__";
                        string afterValue = inst.TryGetValue(key, out var v1) ? v1?.ToString() ?? "" : "<sin clave>";
                        param.Value = beforeValue == "<sin clave>" ? null : beforeValue;
                        check(afterValue == "__probe__",
                            $"la edición escribe al NodeInstance (write-through): '{key}' = '{afterValue}'");
                    }

                    bool closed = insp.CloseViaCommand();
                    check(closed, "el comando de cierre oculta el inspector (ClosePanelCommand del VM)");

                    // Hito 240: el botón «Probar» existe, canta su AutomationId para la observación
                    // UIA externa, está atado al comando canónico del núcleo y queda localizado.
                    bool testButtonOk = insp.HasWiredTestButton();
                    check(testButtonOk,
                        "el botón Probar existe y ejecuta TestNodeWithCustomFileCommand (variante async del diálogo)");

                    // Hito 274: y no basta con que exista y esté cableado — se OFRECÍA también sin nodo (dibujado y
                    // habilitado) y su clic no hacía nada. La sonda recorre los dos estados por la propiedad del VM
                    // (el mismo camino del arranque y de la selección del lienzo) y mide la OFERTA, no el cableado.
                    var testOffer = insp.ProbeTestButtonOffer(firstNode);
                    check(!testOffer.OfferedWithoutNode,
                        "el «Probar» NO se ofrece sin nodo inspeccionado: no hay nada que probar, así que no se dibuja");
                    check(testOffer.OfferedWithNode,
                        "el «Probar» sí se ofrece con el nodo inspeccionado (dibujado y habilitado): la prueba aislada tiene a quién probar");

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
                    check(snapshotCards == firstNode.InputSnapshots.Count + firstNode.OutputSnapshots.Count,
                        $"las tarjetas de snapshots materializan las colecciones del nodo ({snapshotCards} = entradas + salidas)");
                    check(diffRows > 0,
                        $"la pestaña de diff pinta las filas que el VM computa ({diffRows}, Added/Removed/Modified)");
                    check(tabSwitch,
                        "el conmutador de secciones conmuta: la sección pedida queda visible y sola, " +
                        "conserva sus tarjetas y Entradas/Salidas llevan EXACTAMENTE su colección del nodo");

                    // Las CINCO secciones de la ficha, cada una con caja propia DENTRO de la ficha (hito
                    // 273). El Pivot repartía sus rótulos en el ancho y no los envolvía: con la ficha en sus
                    // 300 lógicos medía «Salidas» y «Diff» con rectángulo VACÍO —fuera del alcance del
                    // ratón— y «Entradas» recortada contra el borde, sin scroll ni rueda que las alcanzara.
                    // El censo de declaración no lo veía (las cinco existían para el view model).
                    var tabBoxes = insp.TabButtonBoxesForProbe();
                    double panelWidth = insp.ActualWidth, panelHeight = insp.ActualHeight;
                    check(tabBoxes.Count == NodeInspectorPanel.DeclaredSectionCount
                            && insp.SectionPanes.Count == NodeInspectorPanel.DeclaredSectionCount
                            && tabBoxes.All(b => b.Box.Width > 0 && b.Box.Height > 0
                                && b.Box.Left >= -0.5 && b.Box.Top >= -0.5
                                && b.Box.Right <= panelWidth + 0.5 && b.Box.Bottom <= panelHeight + 0.5),
                        $"las {tabBoxes.Count} secciones de la ficha tienen cuerpo y caja dentro de la ficha "
                        + $"({panelWidth:F0}x{panelHeight:F0}: "
                        + string.Join(" ", tabBoxes.Select(b => $"{b.Aid}={b.Box.Width:F0}x{b.Box.Height:F0}@({b.Box.Left:F0},{b.Box.Top:F0})"))
                        + ")");

                    // Hito 275: la sección de TELEMETRÍA se OFRECE y PINTA. El defecto que mide esta sonda no se veía:
                    // el panel construía las filas y no las montaba en ninguna parte, así que la ficha no enseñaba ni
                    // una medida y la guardia que existía medía el cableado (que las filas se construyeran), no el
                    // montaje. La sonda comprueba la cadena entera: pestaña declarada, cuerpo dentro del host de
                    // paneles con la sección por contenido, la conmutación que la deja visible y sola, y las filas
                    // pintadas —una fila en blanco no es una medida.
                    var telemetry = insp.ProbeTelemetrySection();
                    check(telemetry.Ok,
                        $"la sección de Telemetría está montada en su pestaña, se muestra sola y PINTA sus filas "
                        + $"(filas: {telemetry.Rows}, en blanco: {telemetry.EmptyRows}) — {telemetry.Detail}");

                    firstNode.InputSnapshots.Clear();
                    insp.InspectForProbe(firstNode);
                }
            }
        }
        catch (Exception ex)
        {
            check(false, "sonda de paneles (rebanada 4) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

    }
}
