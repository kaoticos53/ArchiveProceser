using System;
using System.Collections.Generic;
using System.Linq;
using FileFlow.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// La SUPERFICIE DE OBSERVACIÓN de la ficha del inspector: la otra mitad de
/// <see cref="NodeInspectorPanel"/>, aparte del código que la construye y la rellena.
///
/// <para><b>Por qué está separada</b>. Seis pases fueron dejando aquí, pegadas al final de un fichero de
/// más de mil líneas, las medidas que el sondeo en runtime del host (<c>--selfcheck</c>) y las guardias de
/// fuente necesitan: accesos por ancla, censos de lo materializado y sondas de estado. Cada miembro de este
/// fichero existe SÓLO para observar — ninguno lo llama la aplicación —, y su sitio no es el mismo que el
/// del código que pinta la ficha: quien lee el panel para cambiarlo no debería tropezarse con el
/// instrumento que lo mide, y quien lee el instrumento no debería tener que buscar sus miembros entre la
/// construcción de las pestañas.</para>
///
/// <para><b>Qué NO vive aquí</b>: nada que la aplicación use. Si un miembro de este fichero pasa a ser
/// llamado por el producto, su sitio es el fichero principal del panel.</para>
///
/// <para><b>El contrato con el sondeo</b>: los métodos <c>Probe*</c> recorren el estado por el MISMO
/// camino que el usuario (las propiedades del VM portable, los comandos del núcleo) y <b>restauran</b> lo
/// que había: el sondeo no puede dejar la ficha a medias. Lo que devuelven son medidas (cuentas, cajas,
/// textos), nunca un veredicto: quien afirma —y quien escribe el <c>[OK]</c>/<c>[FALLO]</c>— es el sondeo
/// (<c>SelfCheckPanels</c>), y quien fija la regla en el árbol de pruebas es su guardia
/// (<c>UnoInspectorPanelGuardTests</c> / <c>UnoInspectorTelemetryGuardTests</c>).</para>
/// </summary>
public sealed partial class NodeInspectorPanel : UserControl
{
    // ── Superficie interna para el sondeo en runtime (--selfcheck) ──
    /// <summary>El control de una fila de parámetro por su AutomationId (null si esa fila no lo tiene).</summary>
    internal Control? ParameterControl(string automationId) =>
        _paramControls.TryGetValue(automationId, out Control? control) ? control : null;

    /// <summary>
    /// Ancla un botón de acción del nodo (su propia tabla: el censo de las filas se vacía al reconstruirlas).
    /// </summary>
    private void AnchorAction(string automationId, Control control)
    {
        AutomationProperties.SetAutomationId(control, automationId);
        _actionControls[automationId] = control;
    }

    /// <summary>El botón de una acción del nodo por su ActionId (null si el nodo no la declara).</summary>
    internal Control? ActionControl(string actionId) =>
        _actionControls.TryGetValue("InspectorAction_" + actionId, out Control? control) ? control : null;

    /// <summary>El pincel de un token Canvas* resuelto de los recursos de la app (el patrón del lienzo).</summary>
    private static Brush Brush(string key)
    {
        return (Brush)Application.Current.Resources[key];
    }

    // ── Superficie interna para el sondeo en runtime (--selfcheck) ──

    /// <summary>Los editores de parámetros materializados (uno por parámetro con editor).</summary>
    internal int ParameterEditorCount => _paramsHost.Children.Count;

    /// <summary>
    /// El encabezado de Parámetros está OFRECIDO (no colapsado): la reconstrucción lo ata a sus editores, así que
    /// sin parámetros no promete nada. La sonda lo compara con la cuenta de editores del nodo inspeccionado.
    /// </summary>
    internal bool ParametersHeaderOffered => _paramsHeader.Visibility == Visibility.Visible;

    /// <summary>
    /// Los botones de ACCIÓN materializados en la ficha: la cuenta que la sonda compara con las acciones del
    /// nodo inspeccionado (una acción declarada y no dibujada es una puerta que falta).
    /// </summary>
    internal int ActionButtonCount => _actionsHost.Children.Count;

    /// <summary>Abre el panel sobre un nodo, como haría la selección del lienzo (mismo método del VM).</summary>
    internal void InspectForProbe(NodeViewModel node)
    {
        _vm?.InspectNode(node, autoOpen: true);
    }

    /// <summary>
    /// La sonda del «Probar» (hito 240): el botón existe en la cabecera, canta su AutomationId para
    /// la observación UIA externa y está atado al comando canónico del núcleo (la variante async
    /// del diálogo vive en el VM; el host no abre pickers por su cuenta).
    /// </summary>
    internal bool HasWiredTestButton()
    {
        return _testButton is not null
            && AutomationProperties.GetAutomationId(_testButton) == "InspectorTestButton"
            && _vm?.TestNodeWithCustomFileCommand is not null;
    }

    /// <summary>
    /// La sonda de lo que el «Probar» SE OFRECE en cada estado (hito 274): recorre los dos estados por el MISMO
    /// camino del usuario —la propiedad <c>InspectedNode</c> del VM y su <c>IsOpen</c>, que es lo que la selección
    /// del lienzo y el arranque escriben— y lee el estado del botón. Restaura lo que había para no dejar la ficha a
    /// medias.
    ///
    /// <para><b>Por qué el cableado no bastaba</b>: <see cref="HasWiredTestButton"/> sólo mide que el botón exista
    /// y apunte al comando del núcleo, y con eso el botón pasaba la guardia mientras se ofrecía sin nodo para no
    /// hacer nada. Lo que se mide aquí es la OFERTA: sin nodo no está dibujado y con el nodo inspeccionado está
    /// dibujado y habilitado.</para>
    /// </summary>
    internal (bool OfferedWithoutNode, bool OfferedWithNode) ProbeTestButtonOffer(NodeViewModel node)
    {
        if (_vm is null)
        {
            return (false, false);
        }

        bool wasOpen = _vm.IsOpen;
        var previousNode = _vm.InspectedNode;
        try
        {
            // Sin nodo y con la ficha abierta: el estado del arranque (el panel nace abierto y sin selección).
            _vm.InspectedNode = null;
            _vm.IsOpen = true;
            bool withoutNode = TestButtonIsOffered();

            // Con nodo, por el camino de la selección del lienzo.
            InspectForProbe(node);
            bool withNode = TestButtonIsOffered();

            return (withoutNode, withNode);
        }
        finally
        {
            _vm.InspectedNode = previousNode;
            _vm.IsOpen = wasOpen;
        }
    }

    /// <summary>El «Probar» está OFRECIDO de verdad: dibujado (no colapsado) y habilitado para el clic.</summary>
    private bool TestButtonIsOffered() =>
        _testButton.Visibility == Visibility.Visible && _testButton.IsEnabled;

    /// <summary>
    /// La sonda de la sección de TELEMETRÍA (hito 275): el defecto que este método mide es el que no se veía —
    /// la sección se construía, se rellenaba y <b>no se montaba en ninguna parte</b>, así que la ficha no tenía
    /// ni una medida y ninguna guardia lo decía. Se comprueba la cadena entera: su pestaña declarada con su
    /// ancla, el cuerpo dentro del host de paneles y con la sección por contenido, la conmutación que la deja
    /// visible y sola, y que las filas estén pintadas con rótulo y valor (una fila vacía no es una medida).
    /// <para>Mide además el ida y vuelta al nodo: sin nodo la sección se queda sin filas —no se ofrece— y al
    /// volver a atarle el suyo las materializa otra vez con sus medidas.</para>
    /// <para>Devuelve cuántas filas midió y cuántas salieron en blanco, además del detalle para el informe.</para>
    /// </summary>
    internal (bool Ok, int Rows, int EmptyRows, string Detail) ProbeTelemetrySection()
    {
        int index = Array.FindIndex(InspectorTabs, tab => tab.Aid == "InspectorTabTelemetry");
        if (index < 0 || _telemetryPane is null || _tabButtons[index] is null)
        {
            return (false, 0, 0, "la ficha no declara la sección de Telemetría");
        }

        bool mounted = _paneHost.Children.Contains(_telemetryPane)
            && ReferenceEquals(_telemetryPane.Content, _telemetrySection)
            && _tabPanes.Contains(_telemetryPane)
            && AutomationProperties.GetAutomationId(_tabButtons[index]) == "InspectorTabTelemetry";

        int previous = _selectedTab;
        bool shown;
        try
        {
            ShowTab(index);
            shown = _telemetryPane.Visibility == Visibility.Visible
                && _tabButtons[index].IsChecked == true
                && _tabPanes.Where((pane, i) => i != index).All(pane => pane.Visibility == Visibility.Collapsed);
        }
        finally
        {
            ShowTab(previous);
        }

        int withNode = _telemetrySection.RowCount;
        _telemetrySection.Bind(null);
        int withoutNode = _telemetrySection.RowCount;
        _telemetrySection.Bind(_inspected);

        var rows = _telemetrySection.RowTextsForProbe();
        int empty = rows.Count(row => string.IsNullOrWhiteSpace(row.Label) || string.IsNullOrWhiteSpace(row.Value));
        bool ok = mounted && shown && rows.Count > 0 && empty == 0
            && _telemetrySection.RowCount == rows.Count && rows.Count == withNode && withoutNode == 0;
        return (ok, rows.Count, empty,
            $"{rows.Count} filas ({empty} en blanco), montada={mounted}, mostrada={shown}, "
            + $"con nodo={withNode} sin nodo={withoutNode}, "
            + "filas=" + string.Join(" | ", rows.Select(row => row.Label + ": " + row.Value)));
    }

    /// <summary>
    /// La sonda de las pestañas nuevas (hito 242): tarjetas de snapshots materializadas desde las
    /// colecciones del nodo, filas de diff pintadas desde el VM (el VM computa al seleccionar un
    /// snapshot), y la conmutación del Pivot dejando las tarjetas en el árbol.
    /// </summary>
    internal (int SnapshotCards, int DiffRows, bool TabSwitch) ProbeSnapshotTabs()
    {
        var inspected = _inspected;
        if (_vm?.InspectedNode is null || inspected is null || _snapshotsPane is null)
        {
            return (0, 0, false);
        }

        int cards = _snapshotsHost.Children.Count;
        int diffRows = _diffHost.Children.Count;

        // Cada sección tiene su cuerpo y su envoltorio desplazable con NOMBRE, y el cuerpo de
        // Entradas/Salidas es el host de su colección del nodo.
        bool separatedOk = _inputsPane is not null && _outputsPane is not null && _diffPane is not null
            && _paramsPane is not null
            && _inputsHost.Children.Count == inspected.InputSnapshots.Count
            && _outputsHost.Children.Count == inspected.OutputSnapshots.Count
            && _tabButtons.Length == InspectorTabs.Length
            && _tabButtons.All(b => b is not null
                && AutomationProperties.GetAutomationId(b) is { Length: > 0 })
            && ReferenceEquals(_inputsPane.Content, _inputsHost)
            && ReferenceEquals(_outputsPane.Content, _outputsHost)
            && ReferenceEquals(_diffPane.Content, _diffHost)
            && ReferenceEquals(_snapshotsPane.Content, _snapshotsHost);

        int previousIndex = _selectedTab;
        int snapshotsIndex = Array.FindIndex(InspectorTabs, t => t.Aid == "InspectorTabSnapshots");
        try
        {
            // La conmutación deja visible la sección pedida —y sólo esa— y conserva sus tarjetas.
            ShowTab(snapshotsIndex);
            bool switchOk = _selectedTab == snapshotsIndex
                && _snapshotsPane.Visibility == Visibility.Visible
                && _tabButtons[snapshotsIndex].IsChecked == true
                && _tabPanes.Where((pane, i) => i != snapshotsIndex)
                    .All(pane => pane.Visibility == Visibility.Collapsed)
                && _snapshotsHost.Children.Count == cards;
            return (cards, diffRows, switchOk && separatedOk);
        }
        finally
        {
            ShowTab(previousIndex);
        }
    }

    /// <summary>
    /// El censo de la tira de secciones para la guardia del hito 273: la caja de CADA rótulo DENTRO de la
    /// ficha. El Pivot repartía los cinco rótulos en el ancho y no los envolvía, así que «Salidas» y «Diff»
    /// quedaban con caja vacía —fuera del alcance del ratón— con la ficha en sus 300 lógicos.
    /// </summary>
    internal IReadOnlyList<(string Aid, Windows.Foundation.Rect Box)> TabButtonBoxesForProbe()
    {
        var boxes = new List<(string, Windows.Foundation.Rect)>(_tabButtons.Length);
        foreach (var button in _tabButtons)
        {
            if (button is null)
            {
                continue;
            }

            var box = button.TransformToVisual(this).TransformBounds(
                new Windows.Foundation.Rect(0, 0, button.ActualWidth, button.ActualHeight));
            boxes.Add((AutomationProperties.GetAutomationId(button), box));
        }

        return boxes;
    }

    /// <summary>Las cinco secciones que la tira declara (la guardia del 273).</summary>
    internal static int DeclaredSectionCount => InspectorTabs.Length;

    /// <summary>Los cinco cuerpos de la ficha, para que la sonda compruebe que existen de verdad.</summary>
    internal IReadOnlyList<UIElement> SectionPanes => _tabPanes;

    /// <summary>Cierra el panel por el comando del VM (el botón de la cabecera).</summary>
    internal bool CloseViaCommand()
    {
        if (_vm is null)
        {
            return false;
        }

        _vm.ClosePanelCommand.Execute(null);
        return !_vm.IsOpen;
    }
}
