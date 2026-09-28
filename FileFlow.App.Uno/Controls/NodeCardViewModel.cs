using System;
using System.ComponentModel;
using FileFlow.App.Services;
using FileFlow.App.Uno.Platform;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using Material.Icons;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// El adaptador de lectura que la tarjeta del lienzo Uno consume por cada <see cref="NodeViewModel"/> del
/// núcleo: expone el título, la categoría, la descripción y la <see cref="Position"/> —el punto del
/// framework, proyectado con <see cref="UnoPointConverter"/> (la regla del hito 217: ninguna lectura directa
/// de <c>Location.X/.Y</c>, el binding pasa por el conversor)—.
///
/// <para><b>Tarjeta completa</b> (cierra la fase 3.1): añade los miembros presentacionales que el XAML
/// necesita y que no existen en el núcleo porque son del framework — el acento como <c>Color</c> de WinUI
/// (los pinceles no se crean en el núcleo), el radio de esquina de cada socket según su forma, y la
/// visibilidad de los cuerpos. Los comandos y estados siguen siendo del nodo: la tarjeta no duplica
/// lógica.</para>
///
/// <para><b>Modo lectura</b>: suscribe los cambios del nodo y de sus puertos para refrescar, y no escribe
/// nada de vuelta salvo los comandos que el propio nodo expone; la interacción directa (arrastre, cables)
/// llega en las fases 3.2/3.3.</para>
/// </summary>
public sealed class NodeCardViewModel : INotifyPropertyChanged
{
    private readonly NodeViewModel _node;

    public NodeCardViewModel(NodeViewModel node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _node.PropertyChanged += OnNodePropertyChanged;

        // La tarjeta dibuja los puertos: sus cambios de estado (conexión, drag) refrescan el socket.
        _node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(NodeViewModel.InputPorts) or nameof(NodeViewModel.OutputPorts))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            }
        };
    }

    public NodeViewModel Node => _node;

    public string Title => _node.Title;

    public string Category => _node.Category;

    public string Description => _node.Description;

    /// <summary>
    /// La posición de la tarjeta en el lienzo, ya proyectada: el code-behind la aplica con el conversor,
    /// como exige la guardia de geometría (los enlaces de WinUI no traducen puntos por sí solos).
    /// </summary>
    public Windows.Foundation.Point Position => ProjectLocation();

    /// <summary>El ancho medido o de referencia de la tarjeta, para el encuadre.</summary>
    public double Width => _node.Width > 0 ? _node.Width : 220;

    /// <summary>El color de acento del nodo como <see cref="Windows.UI.Color"/> de WinUI.</summary>
    public Windows.UI.Color AccentBrushColor => ParseHex(_node.AccentColor);

    /// <summary>El radio de esquina del socket según su forma (la misma geometría que el escritorio).</summary>
    public CornerRadius SocketRadius => new(3);

    /// <summary>El icono del tipo de nodo (el enum portable del paquete Material.Icons).</summary>
    public MaterialIconKind Icon => _node.Icon;

    // Glifos fijos de la tarjeta (consola de logging, alerta de cuello de botella y telemetría),
    // resueltos una vez y expuestos como propiedades para el binding sin Source del XAML. El conversor
    // devuelve geometría construida por código (clon del parseo del paquete): la única asignable a
    // Path.Data en este host, según midió el sondeo en runtime.
    public Microsoft.UI.Xaml.Media.Geometry ConsoleIconGeometry
        => MaterialIconKindToGeometryConverter.ToGeometry(MaterialIconKind.ConsoleLine);

    public Microsoft.UI.Xaml.Media.Geometry AlertIconGeometry
        => MaterialIconKindToGeometryConverter.ToGeometry(MaterialIconKind.Alert);

    public Microsoft.UI.Xaml.Media.Geometry CounterIconGeometry
        => MaterialIconKindToGeometryConverter.ToGeometry(MaterialIconKind.Counter);

    public Microsoft.UI.Xaml.Media.Geometry PulseIconGeometry
        => MaterialIconKindToGeometryConverter.ToGeometry(MaterialIconKind.Pulse);

    public Microsoft.UI.Xaml.Media.Geometry ExpansionCardIconGeometry
        => MaterialIconKindToGeometryConverter.ToGeometry(MaterialIconKind.ExpansionCard);

    /// <summary>
    /// El glifo del conmutador de parámetros de la cabecera (chevron arriba con el panel desplegado, abajo
    /// con la tarjeta plegada): el estado es del núcleo —la misma <c>IsExpanded</c> que el escritorio
    /// conmutaba— y la tarjeta sólo lo pinta.
    /// </summary>
    public Microsoft.UI.Xaml.Media.Geometry ExpandIconGeometry
        => MaterialIconKindToGeometryConverter.ToGeometry(
            _node.IsExpanded ? MaterialIconKind.ChevronUp : MaterialIconKind.ChevronDown);

    /// <summary>
    /// El rótulo del conmutador de la tarjeta. La CLAVE es la del escritorio —su panel sí son parámetros— y el
    /// TEXTO es el de este host: aquí lo que se despliega son las ACCIONES del nodo, y el diccionario del host
    /// lo dice así en los dos idiomas. La clave se conserva porque es la que audita la guardia de textos
    /// compartidos entre los dos hosts, y el rótulo dice lo que hace ESTE host.
    /// </summary>
    public string ParametersToggleToolTip
        => LocalizationManager.Instance.GetString("ToggleParametersToolTip", "Mostrar/Ocultar las acciones del nodo");

    /// <summary>
    /// La visibilidad de las acciones rápidas del nodo (su <c>Count&gt;0</c>): es también la del
    /// conmutador de la cabecera.
    /// </summary>
    public bool HasCustomActions => _node.CustomActions?.Count > 0;

    /// <summary>
    /// La visibilidad del panel plegable de la tarjeta: desplegado Y con algo dentro.
    ///
    /// <para><b>Qué se despliega</b>: las ACCIONES del nodo, no sus parámetros. El listado de parámetros
    /// que había aquí era una lista muerta —nombres sin editor— y su sitio es la ficha del inspector;
    /// dejar el panel abierto y vacío en un nodo sin acciones sería justo el defecto que se quitó: una
    /// superficie que se abre y no ofrece nada. De ahí la conjunción, que el conmutador comparte por
    /// <see cref="HasCustomActions"/>.</para>
    /// </summary>
    public bool ActionsPanelVisible => HasCustomActions && _node.IsExpanded;

    /// <summary>La visibilidad del panel de telemetría: visible cuando hay algo que contar.</summary>
    public bool FooterVisible => _node.HasTelemetry || _node.IsGpuAccelerated;

    /// <summary>
    /// La proyección del punto del grafo, pasando por <b>el mismo conversor</b> que el XAML cita
    /// ({StaticResource UnoPointConverter}): el aplicador de posiciones y el enlace comparten pieza,
    /// así que la guardia defiende las dos mitades con la misma regla.
    /// </summary>
    private Windows.Foundation.Point ProjectLocation()
    {
        var converter = UnoPointConverter.Instance;
        return (Windows.Foundation.Point)converter.Convert(_node.Location, typeof(Windows.Foundation.Point), null!, "en-US");
    }

    /// <summary>Hex (#RRGGBB / #AARRGGBB) → Color de WinUI; el acento del nodo vive como string en el núcleo.</summary>
    public static Windows.UI.Color ParseHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Microsoft.UI.Colors.Gray;
        }

        string s = hex.TrimStart('#');
        return s.Length switch
        {
            6 => Microsoft.UI.ColorHelper.FromArgb(255,
                Convert.ToByte(s.Substring(0, 2), 16),
                Convert.ToByte(s.Substring(2, 2), 16),
                Convert.ToByte(s.Substring(4, 2), 16)),
            8 => Microsoft.UI.ColorHelper.FromArgb(
                Convert.ToByte(s.Substring(2, 2), 16),
                Convert.ToByte(s.Substring(4, 2), 16),
                Convert.ToByte(s.Substring(6, 2), 16),
                Convert.ToByte(s.Substring(0, 2), 16)),
            _ => Microsoft.UI.Colors.Gray
        };
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeViewModel.Location) or nameof(NodeViewModel.Title)
            or nameof(NodeViewModel.Category) or nameof(NodeViewModel.Description)
            or nameof(NodeViewModel.Width) or nameof(NodeViewModel.AccentColor)
            or nameof(NodeViewModel.ExecutionStatus) or nameof(NodeViewModel.IsSelected)
            // El renombrado (fase 3.2): sin estos dos, F2 cambia el estado en el núcleo y la caja de
            // edición NUNCA aparece en el árbol (el refresco agregado de la tarjeta no se entera).
            or nameof(NodeViewModel.IsEditingTitle) or nameof(NodeViewModel.EditingTitleText)
            // El panel de acciones y su conmutador cuelgan de IsExpanded: sin esto, conmutar cambia el
            // estado en el núcleo y la tarjeta sigue pintando el chevron de antes. La lista de parámetros ya
            // no se pinta aquí —se editan en el inspector—, pero el estado desplegado sigue siendo del
            // núcleo y el refresco tiene que seguirla.
            or nameof(NodeViewModel.IsExpanded))
        {
            // string.Empty refresca todos los bindings de la tarjeta: los estados viajan juntos.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
