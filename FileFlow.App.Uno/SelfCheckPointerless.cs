using System;
using FileFlow.App.Uno.Controls;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno;

/// <summary>
/// Las medidas del lienzo que en ESTE entorno no se pueden recorrer con el puntero real —el puntero inyectado
/// entrega pulsaciones pero no movimientos (medido: 0 px de hover mientras el pulsado llega)—: la superficie
/// que una observación externa alcanza (hito 238), el foco que el clic entrega (252), el enrutado de los
/// atajos (252), el seguimiento de los cables al mover el plano y el zoom (254), el GESTO DEL CABLE de puerto
/// a puerto (278) y la reclamación del teclado (253). Cada una entra por el MISMO método que ejecuta el
/// handler: medir por otro camino certificaría un comportamiento que el usuario no recorre.
///
/// <para><b>Quién afirma</b>: nadie por su cuenta — recibe el comprobador de quien la llama (el modo del
/// lienzo, <c>SelfCheckCanvas</c>) y se limita a medir. Sus reglas las fijan <c>UnoCanvasWireGuardTests</c> y
/// <c>UnoCanvasKeyboardGuardTests</c>.</para>
/// </summary>
internal static class SelfCheckPointerless
{
    /// <summary>
    /// Corre las seis medidas sobre el lienzo vivo y las declara por el comprobador del modo del lienzo.
    /// Cada bloque lleva su propio try/catch: una medida que lance no puede llevarse el resto.
    /// </summary>
    internal static void Check(EditorCanvasControl canvas, Action<bool, string> check)
    {
        // Superficie UIA (hito 238): lo que una observación EXTERNA (pywinauto, sin UIAccess) puede
        // alcanzar del lienzo — la ancla explícita con su peer expuestos, el foco programático (la vía
        // del SetFocus UIA) entrando sin puntero, y el estado del zoom observable en la barra. Es la
        // sonda interna de las mismas anclas que las sondas UIA externas van a citar.
        try
        {
            var (anchorExposed, focusEntered, zoomStateObservable) = canvas.ProbeUiAccessibility();
            check(anchorExposed, "la ancla 'CanvasRoot' del lienzo llega al árbol UIA con su peer expuesto");
            check(focusEntered, "el lienzo acepta el foco programático (la vía del SetFocus UIA externo, sin puntero)");
            check(zoomStateObservable, "el estado del zoom es observable: la barra refleja el nivel cambiado y restaurado");
        }
        catch (Exception ex)
        {
            check(false, "sonda de superficie UIA (238) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // El FOCO del puntero (hito 252): el clic tiene que dejar el foco en el CONTROL del lienzo —de eso
        // dependía que el 250 midiera Ctrl+Z, Ctrl+Y y Supr sin llegar— y no robarle el suyo ni al cuadro
        // de texto (buscador del spotlight, renombrado) ni a la barra de zoom. El puntero no se puede
        // inyectar en este entorno: lo que se mide es el MISMO camino que ejecuta el clic.
        try
        {
            var (focusDelivered, respectsOwners, focusDetail) = canvas.ProbePointerFocus();
            check(focusDelivered && respectsOwners,
                $"el clic del puntero entrega el foco al lienzo y respeta a los otros dueños del teclado: {focusDetail}");
        }
        catch (Exception ex)
        {
            check(false, "sonda de foco del puntero (252) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // El ENRUTADO de los atajos (hito 252): entregar el foco no bastó —el rastro con puntero real midió
        // que un panel se lo lleva ~0,5 s después del clic y que Ctrl+Z, Ctrl+Y, Supr y F2 morían con él—,
        // así que el arreglo no depende del foco: la ventana enruta al lienzo las teclas que nadie consumió.
        // La sonda resuelve teclas sin foco (Espacio abre el buscador, Escape lo cierra) por el MISMO método
        // que usa el enrutador, y comprueba la cortesía con los cuadros de texto.
        try
        {
            var (resolvedWithoutFocus, respectsTextInput, routingDetail) = canvas.ProbeShortcutResolution();
            check(resolvedWithoutFocus,
                $"el atajo del editor se resuelve sin ser dueño del foco (el caso que el 250 midió con puntero real): {routingDetail}");
            check(respectsTextInput,
                "el enrutado del atajo respeta al cuadro de texto (no le secuestra el teclado) y no abre nada por su cuenta");
        }
        catch (Exception ex)
        {
            check(false, "sonda de enrutado de atajos (252) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // El SEGUIMIENTO de los cables (hito 254): el extremo dibujado del cable tiene que tocar su socket
        // EN LA RAÍZ (con el pan y el zoom ya dentro, que es lo que ve el usuario) antes y después de mover el
        // plano y de cambiar el zoom. Reportado desde la app: al mover o ajustar el zoom los cables quedaban
        // fuera de su sitio.
        try
        {
            var (beforeOk, afterOk, crowdedOk, wireDetail) = canvas.ProbeWireTracking();
            check(beforeOk, $"el cable dibujado toca su socket con el plano sin mover: {wireDetail}");
            check(afterOk, $"el cable sigue tocando su socket tras mover el plano y cambiar el zoom: {wireDetail}");
            check(crowdedOk, $"la forma del cable cabe en el hueco estrecho (sin el rulo del 2): {wireDetail}");
        }
        catch (Exception ex)
        {
            check(false, "sonda de seguimiento de cables (254) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // El GESTO DEL CABLE (hito 278): los tres tiempos del gesto de un puerto a otro —pulsar, mover y
        // soltar—. El usuario reportó los dos desenlaces rotos (pulsar el puerto arrastraba la tarjeta, o no
        // pasaba nada) y el gesto no tenía ni sonda ni guardia: se recorre por los MISMOS tres métodos que
        // ejecutan los handlers, con las anclas reales y las tarjetas materializadas.
        try
        {
            var (gestureStarted, gestureFollowed, gestureConnected, gestureCancelled, gestureDetail) =
                canvas.ProbeSocketGesture();
            check(gestureStarted,
                $"pulsar un puerto arranca el cable y NO arma el arrastre de la tarjeta: {gestureDetail}");
            check(gestureFollowed, $"el cable arrancado sigue al puntero: {gestureDetail}");
            check(gestureConnected,
                $"soltarlo sobre un puerto compatible crea la conexión y deja el estado limpio: {gestureDetail}");
            check(gestureCancelled,
                $"soltarlo en el vacío o sobre un destino incompatible la CANCELA (sin cable ni pendiente): {gestureDetail}");
        }
        catch (Exception ex)
        {
            check(false, "sonda del gesto del cable (278) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }

        // La RECLAMACIÓN del teclado (hito 253): el rastro con puntero real mide que un envoltorio de la
        // plantilla de ventana se lleva el foco 78–141 ms después del clic. El lienzo lo recupera mientras la
        // ventana de propiedad de ese clic siga viva, y lo respeta si el nuevo dueño es un cuadro de texto,
        // algo suyo o un panel del editor.
        try
        {
            var (reclaimed, respectsOwners, reclaimDetail) = canvas.ProbeKeyboardReclaim();
            check(reclaimed,
                $"el lienzo recupera el teclado cuando el framework se lo lleva tras el clic: {reclaimDetail}");
            check(respectsOwners,
                "la reclamación del teclado no se lo quita a un cuadro de texto, ni a un control del lienzo, ni a un panel del editor");
        }
        catch (Exception ex)
        {
            check(false, "sonda de reclamación del teclado (253) lanzó: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
