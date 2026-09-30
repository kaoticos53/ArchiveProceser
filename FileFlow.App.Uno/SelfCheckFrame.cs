using System;
using Microsoft.UI.Xaml;

namespace FileFlow.App.Uno;

/// <summary>
/// La medida del MARCO del host (hito 272): dónde cae cada zona de la ventana y si sus asas redimensionan.
/// Es una preocupación aparte del resto del sondeo —geometría del marco, no contenido— y por eso vive en su
/// propio archivo: el porqué de cada comprobación está en el bloque que sigue, entero, junto a su instrumento
/// (<see cref="BoxInWindow"/>, que sólo esta medida necesita).
///
/// <para><b>El defecto que mide</b>: la regresión del 270 —el <c>Workspace</c> sin su <c>Grid.Row</c>, pintado
/// ENCIMA de la barra, con el menú y los ajustes fuera del alcance del ratón y la franja inferior en negro— pasó
/// con 85 <c>[OK]</c> porque todas las sondas pulsan la barra por MÉTODO, no por puntero, y ninguna medía dónde
/// cae cada zona del marco. Las ASAS se miden por el MISMO camino que el arrastre del puntero
/// (<c>DragBy</c>): el puntero inyectado entrega pulsaciones pero no movimientos, así que el arrastre no se
/// puede medir con dedos.</para>
///
/// <para><b>Quién afirma</b>: este archivo MIDE y devuelve; el <c>[OK]</c>/<c>[FALLO]</c> y el veredicto del
/// sondeo los escribe <see cref="RuntimeSelfCheck"/>, que es quien pasa su comprobador. La regla queda fijada
/// en el árbol de pruebas por su guardia de fuente.</para>
/// </summary>
internal static class SelfCheckFrame
{
    /// <summary>
    /// Las comprobaciones del marco: las tres zonas con caja propia y disjuntas, y el asa del cajón por ida,
    /// tope del lienzo y vuelta al ancho de partida (el estado queda como al entrar).
    /// </summary>
    internal static void Check(Window window, Action<bool, string> check)
    {
        // Las tres zonas del marco, con caja propia y disjuntas (el porqué, en la cabecera de la clase).
        if (window is MainWindow marco)
        {
            var barBox = BoxInWindow(marco.FrameBar);
            var workBox = BoxInWindow(marco.FrameWorkspace);
            var statusBox = BoxInWindow(marco.FrameStatus);
            check(barBox.Width > 0 && barBox.Height > 0 && workBox.Width > 0 && workBox.Height > 0,
                $"las zonas del marco tienen caja: barra {barBox.Width:F0}x{barBox.Height:F0}, editor {workBox.Width:F0}x{workBox.Height:F0}");
            check(barBox.Bottom <= workBox.Top + 0.5,
                $"la barra queda ARRIBA y el editor no la tapa (barra hasta y={barBox.Bottom:F0}, editor desde y={workBox.Top:F0})");
            check(workBox.Bottom <= statusBox.Top + 0.5 && statusBox.Height > 0,
                $"la franja de estado queda DEBAJO del editor (editor hasta y={workBox.Bottom:F0}, estado desde y={statusBox.Top:F0})");

            // Las ASAS del marco, por su MISMO camino que el arrastre del puntero (`DragBy`): hasta el hito
            // 272 el método existía y no lo llamaba nadie —ni la sonda ni el ratón, que en este entorno no
            // puede inyectar movimientos de puntero (medido: 0 px de hover en seis controles a la vez que
            // el pulsado sí llega)—, así que «el asa redimensiona» era una afirmación sin medida. Ida,
            // tope del lienzo y vuelta al ancho de partida.
            var asa = marco.FrameToolboxSplitter;
            double start = marco.FrameToolboxColumn.ActualWidth;
            double widened = asa.DragBy(40);
            window.Content.UpdateLayout();
            check(Math.Abs(widened - (start + 40)) < 0.51
                    && Math.Abs(marco.FrameToolboxColumn.ActualWidth - widened) < 0.51,
                $"el asa del cajón ensancha su columna y el marco lo aplica: {start:F0} -> {marco.FrameToolboxColumn.ActualWidth:F0}");
            double ceiling = asa.DragBy(100000);
            window.Content.UpdateLayout();
            check(marco.FrameCanvasColumn.ActualWidth >= marco.FrameCanvasColumn.MinWidth - 0.51,
                $"el arrastre del asa no deja al lienzo por debajo de su mínimo: {marco.FrameCanvasColumn.ActualWidth:F0} de {marco.FrameCanvasColumn.MinWidth:F0} (tope del cajón {ceiling:F0})");
            asa.DragBy(start - ceiling);
            window.Content.UpdateLayout();
            check(Math.Abs(marco.FrameToolboxColumn.ActualWidth - start) < 0.51,
                $"el asa devuelve la columna a su ancho de partida: {marco.FrameToolboxColumn.ActualWidth:F0} (era {start:F0})");

            // Comprobación de que cerrar el inspector devuelve el espacio al lienzo (reclamación de espacio)
            double canvasBeforeCollapse = marco.FrameCanvasColumn.ActualWidth;
            double inspectorWidthBefore = marco.FrameInspectorColumn.ActualWidth;
            marco.ApplyInspectorVisibility(false);
            window.Content.UpdateLayout();
            check(marco.FrameInspectorColumn.ActualWidth < 0.51 && marco.FrameCanvasColumn.ActualWidth > canvasBeforeCollapse + (inspectorWidthBefore - 1),
                $"al cerrar el inspector la columna colapsa a 0 y el lienzo recupera su espacio: lienzo {canvasBeforeCollapse:F0} -> {marco.FrameCanvasColumn.ActualWidth:F0} (+{marco.FrameCanvasColumn.ActualWidth - canvasBeforeCollapse:F0}px)");
            marco.ApplyInspectorVisibility(true);
            window.Content.UpdateLayout();
            check(Math.Abs(marco.FrameInspectorColumn.ActualWidth - inspectorWidthBefore) < 0.51,
                $"al reabrir el inspector recupera su ancho previo: {marco.FrameInspectorColumn.ActualWidth:F0}px (era {inspectorWidthBefore:F0}px)");
        }
        else
        {
            check(false, "marco: la ventana no es MainWindow — la geometría del marco no se pudo medir");
        }
    }

    /// <summary>
    /// La caja de un elemento en el sistema de la VENTANA: lo que la sonda del marco compara (barra arriba,
    /// editor debajo, franja de estado al final). Es la medida que faltaba —las cajas del árbol visual—
    /// frente a las comprobaciones de presencia que ya había.
    /// </summary>
    private static Windows.Foundation.Rect BoxInWindow(FrameworkElement element)
    {
        return element.TransformToVisual(null).TransformBounds(
            new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
    }
}
