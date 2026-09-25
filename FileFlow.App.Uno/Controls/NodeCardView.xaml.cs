using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// La tarjeta de nodo del lienzo Uno, en <b>modo lectura</b> (fase 3.1): cabecera con título y categoría,
/// cuerpo con la descripción. Sin puertos vivos aún — los sockets interactivos llegan en la fase 3.3 con el
/// write-back de anclas; su sitio ya está reservado en el cuerpo de la tarjeta.
/// </summary>
public sealed partial class NodeCardView : UserControl
{
    public NodeCardView()
    {
        InitializeComponent();
    }
}
