using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using FileFlow.App.Models;
using FileFlow.App.Uno.Controls;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using FileFlow.Sdk;
using FileFlow.Sdk.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno;

/// <summary>
/// El sondeo de los PANELES DE NODO del host (<c>--selfcheck-dialogs</c>): la puerta del usuario a los modales
/// del nodo, abiertos y ejercidos de verdad sobre el flujo cargado, con el veredicto leído del PARÁMETRO del
/// nodo (lo que el nodo ejecuta, no el texto del diálogo). Es un <b>modo propio</b> porque su medición abre
/// modales sobre la misma raíz y escribe en el nodo inspeccionado: la escena que necesita no es la que miden
/// los otros modos.
///
/// <para><b>Trae consigo</b> sus dos lecturas de fila —<c>IsPlainTextRow</c> (el resto del selector,
/// compartido con el escritorio) y <c>Truncate</c>— y la lectura del almacén de presets
/// (<c>PresetDescriptionInFile</c>, que se lee como JSON y no como texto porque el serializador escapa los
/// acentos con hex en mayúsculas: comparar contra el escapado de otro serializador medía el escapado, no el
/// valor guardado). Termina con el veredicto por el código de salida y por
/// <c>selfcheck-dialogs-report.txt</c>, junto al ejecutable.</para>
///
/// <para><b>Quién afirma</b>: este archivo MIDE y afirma; la regla la fija <c>UnoNodeDialogsGuardTests</c>.</para>
/// </summary>
internal static class SelfCheckDialogs
{
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
    public static int Run(Window window, DispatcherQueue dispatcher)
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
                    canvas = SelfCheckTree.Find<EditorCanvasControl>(window.Content);
                    inspector = SelfCheckTree.Find<NodeInspectorPanel>(window.Content);
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

                    NodeCardView? card = SelfCheckTree.FindAll<NodeCardView>(window.Content)
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
                    NodeCardView? card = SelfCheckTree.FindAll<NodeCardView>(window.Content)
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
                    NodeCardView? card = SelfCheckTree.FindAll<NodeCardView>(window.Content)
                        .FirstOrDefault(c => ReferenceEquals((c.DataContext as NodeCardViewModel)?.Node, transcoder));
                    if (card is null)
                    {
                        return false;
                    }

                    Button? action = SelfCheckTree.FindAll<Button>(card)
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

                // ── 3d. El GESTOR DE CONTRASEÑAS: la capacidad que se OFRECÍA sin poder servirse (hito 279) ──
                // El nodo del desempaquetador ofrece «🔑 Claves...» y el botón de la fila de su lista de claves pide
                // la MISMA superficie. Lo que se mide es que ESTE host la SIRVA —el cuerpo abre, lo que el usuario
                // escribe se guarda en el parámetro del nodo y la fila lo enseña— y que NO salga el aviso de
                // frontera que el usuario leía (que esa ventana era del escritorio).
                const string unpackType = "SmartUnpackNode";
                NodeViewModel? unpacker = null;
                bool unpackerAdded = Step(() =>
                {
                    unpacker = canvas!.Editor!.AddNode(unpackType, new Point(120, 420));
                    return unpacker is not null;
                });
                NodeParameterViewModel? passwordParam = null;
                bool passwordScene = unpackerAdded && Step(() =>
                {
                    passwordParam = unpacker!.Parameters.FirstOrDefault(p => p.IsPasswordList);
                    if (passwordParam is null)
                    {
                        return false;
                    }

                    inspector!.InspectForProbe(unpacker!);
                    return true;
                });
                Check(passwordScene,
                    "el nodo del desempaquetador expone su fila de claves ('" + (passwordParam?.Key ?? "—") + "')");

                bool passwordAnchor = passwordScene && Step(() =>
                    inspector!.ParameterControl("ParamPassword_" + (passwordParam?.Key ?? string.Empty)) is not null);
                Check(passwordAnchor,
                    "la fila de claves expone su botón «🔑» con su ancla (ParamPassword_"
                    + (passwordParam?.Key ?? "—") + "): la puerta que antes no se dibujaba aquí mientras la tarjeta "
                    + "del nodo ofrecía la misma capacidad");

                bool passwordPressed = passwordScene && Step(() =>
                {
                    Control? button = inspector!.ParameterControl("ParamPassword_" + (passwordParam?.Key ?? string.Empty));
                    return button is not null && UnoWindowService.Press(button);
                });
                bool passwordUp = passwordPressed && WaitUntil(() =>
                    UnoWindowService.ActivePasswordManager is not null, 9000);
                Check(passwordUp,
                    "su botón abre el GESTOR DE CONTRASEÑAS de ESTE host (clave '" + DialogKeys.PasswordManager
                    + "') en vez de avisar de que la ventana es del escritorio");

                const string keysSeed = "alfa\r\nbeta";
                bool keysTyped = passwordUp && Step(() =>
                {
                    TextBox? editor = UnoWindowService.ActivePasswordManager?.Editor;
                    if (editor is null)
                    {
                        return false;
                    }

                    editor.Text = keysSeed;
                    return true;
                });
                bool keysInModel = keysTyped && WaitUntil(() =>
                    string.Equals(UnoWindowService.ActivePasswordManager?.Vm.PasswordsText, "alfa; beta", StringComparison.Ordinal), 5000);
                // La lectura del cuerpo va por `Probe` también en el TEXTO del veredicto: el modal es de WinUI y
                // leerlo desde el hilo de la sonda revienta con RPC_E_WRONG_THREAD (medido al escribir esta
                // misma línea: el veredicto moría antes de escribirse).
                string keysInView = Probe(() => UnoWindowService.ActivePasswordManager?.Vm.PasswordsText) ?? "—";
                Check(keysInModel,
                    "lo que el usuario escribe llega al view model portable con su regla —una clave por línea—: '"
                    + keysInView + "'");

                bool keysSaved = keysInModel && Step(() =>
                {
                    Button? primary = UnoWindowService.ActivePrimaryButton;
                    return primary is not null && UnoWindowService.Press(primary);
                });
                bool keysClosed = keysSaved && WaitUntil(() => UnoWindowService.ActiveDialog is null, 9000);
                bool keysWritten = keysClosed && WaitUntil(() =>
                    unpacker?.NodeInstance.Parameters.TryGetValue("PasswordList", out var stored) == true
                    && string.Equals(stored?.ToString(), "alfa; beta", StringComparison.Ordinal), 5000);
                Check(keysWritten && string.Equals(passwordParam?.Value?.ToString(), "alfa; beta", StringComparison.Ordinal),
                    "«Guardar Claves» escribe la lista en el PARÁMETRO del nodo y la fila la enseña ('"
                    + (passwordParam?.Value?.ToString() ?? "—") + "')");

                // La OTRA puerta —la acción del nodo, el mismo botón que el de la tarjeta del lienzo— abre el MISMO
                // cuerpo: la oferta y la capacidad dicen lo mismo por las dos.
                bool passwordActionPressed = Step(() =>
                    UnoWindowService.Press(inspector!.ActionControl("ManagePasswords")!));
                bool passwordActionUp = passwordActionPressed && WaitUntil(() =>
                    UnoWindowService.ActivePasswordManager is not null, 9000);
                Check(passwordActionUp,
                    "y la ACCIÓN del nodo —la puerta de la tarjeta— abre el mismo gestor: las dos dicen lo mismo");

                bool passwordActionClosed = passwordActionUp && Step(() =>
                {
                    Button? close = UnoWindowService.ActiveCloseButton;
                    return close is not null && UnoWindowService.Press(close);
                }) && WaitUntil(() => UnoWindowService.ActiveDialog is null, 9000);
                Check(passwordActionClosed,
                    "y cancelarla la cierra sin dejar ninguna superficie abierta ni cable a medias");

                Check(!Probe(() => UnoWindowService.DeclinedDialogs.Contains(DialogKeys.PasswordManager)),
                    "y su clave NO queda en la traza de lo que este host no sirve: la capacidad se cumple, no se declara");

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
}
