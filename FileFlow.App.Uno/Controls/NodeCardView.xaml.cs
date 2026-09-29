using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using FileFlow.App.ViewModels;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// La tarjeta de nodo del lienzo Uno: cabecera con identidad y estado, puertos dibujados con la matriz
/// del núcleo, panel de parámetros y telemetría.
///
/// <para><b>Fase 3.2</b>: el renombrado (F2 en el lienzo) confirma con Enter o al perder el foco, y
/// cancela con Escape — las mismas teclas de la caja del escritorio (<c>TitleEditBox_KeyDown</c> de
/// <c>NodeCardView.axaml.cs</c>). Es teclado de la CAJA, no del lienzo: el KeyDown del lienzo no
/// secuestra las teclas de un TextBox (la guardia de atajos lo vigila).</para>
///
/// <para>La geometría de los iconos viaja por binding desde el conversor
/// <c>MaterialIconKindToGeometryConverter</c>: ahora devuelve una geometría construida por código (clon
/// figura a figura del parseo del paquete), que el sondeo midió como la única asignable a <c>Path.Data</c>
/// en este host — la geometría parseada directa no lo es (ArgumentException al asignarla). Si algún día el
/// host parsea asignable, este code-behind puede volver a quedarse sin lógica de iconos.</para>
/// </summary>
public sealed partial class NodeCardView : UserControl
{
    /// <summary>
    /// Un socket ha pedido iniciar (o terminar) un cable. El lienzo lo traduce a los comandos del núcleo
    /// (<c>StartConnection</c>/<c>FinishConnection</c>) y computa las anclas; la tarjeta no conoce ni el
    /// lienzo ni el editor — sólo avisa.
    /// </summary>
    public event EventHandler<PortViewModel>? SocketRequested;

    /// <summary>Un socket con cables quiere desconectarse (click derecho sobre el socket).</summary>
    public event EventHandler<PortViewModel>? DisconnectRequested;
    public NodeCardView()
    {
        InitializeComponent();

        // El foco entra a la caja cuando el renombrado la hace visible: el renombrado ocurre donde el
        // usuario escribe, como en el escritorio. WinUI no tiene IsVisibleChanged (WPF): la vía correcta
        // es el callback de la propiedad Visibility, y el foco se pide en el dispatcher (el binding de
        // IsEditingTitle y el pase de layout llegan después del cambio de estado).
        TitleEditBox.RegisterPropertyChangedCallback(
            UIElement.VisibilityProperty,
            (_, _) =>
            {
                if (TitleEditBox.Visibility == Visibility.Visible)
                {
                    _ = DispatcherQueue.TryEnqueue(() => TitleEditBox.Focus(FocusState.Programmatic));
                }
            });
    }

    private void OnTitleEditBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (DataContext is not NodeCardViewModel adapter || e.OriginalSource is not TextBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Enter:
                adapter.Node.CommitTitleRename();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Escape:
                adapter.Node.CancelTitleRename();
                e.Handled = true;
                break;
        }
    }

    private void OnTitleEditBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is NodeCardViewModel adapter && adapter.Node.IsEditingTitle)
        {
            adapter.Node.CommitTitleRename();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Fase 3.3: sockets vivos. La tarjeta NO decide: reporta el puerto y el lienzo habla con el núcleo.
    // ─────────────────────────────────────────────────────────────────────────────

    private void OnSocketPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || e.GetCurrentPoint(element).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        if ((element.DataContext ?? (element.Parent as FrameworkElement)?.DataContext) is PortViewModel port)
        {
            // La fila del puerto se queda con el PUNTERO mientras dura el gesto (hito 278). Sin captura los
            // movimientos con el botón pulsado no llegaban al lienzo —medido con el ratón inyectado: llegaba
            // UNO solo, en la posición de la pulsación— y el cable no seguía al cursor; el soltar tampoco
            // llegaba si el destino caía fuera del lienzo.
            element.CapturePointer(e.Pointer);
            SocketRequested?.Invoke(this, port);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Suelta la captura del puerto. Llega por dos caminos —el soltar del botón y la pérdida de captura—
    /// porque el gesto puede acabar de cualquiera de las dos formas (hito 278): sin esto, una captura
    /// colgada se comería los punteros siguientes.
    /// </summary>
    private void OnSocketReleased(object sender, PointerRoutedEventArgs e)
    {
        (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
    }

    private void OnSocketRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if ((element.DataContext ?? (element.Parent as FrameworkElement)?.DataContext) is PortViewModel port)
        {
            DisconnectRequested?.Invoke(this, port);
            e.Handled = true;
        }
    }
}
