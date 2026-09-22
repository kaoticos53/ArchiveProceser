using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFlow.Sdk.Localization;

namespace FileFlow.App.ViewModels;

/// <summary>
/// Un cable que se perdió, con lo que hace falta para arreglarlo sin salir del lienzo: dónde está el nodo cuyo
/// puerto falta, qué puerto vigente se le parece más, y los dos botones —ir allí, reconectar—.
///
/// <para>
/// Existe porque un aviso de pérdida que sólo se lee deja el trabajo entero en manos del usuario: había que
/// buscar el nodo a ojo en el lienzo, averiguar qué puerto era el que faltaba y volver a trazar el cable. Cada
/// fila lleva el puerto que falta <b>nombrado</b>, el nodo <b>señalado</b> y la reconexión a un clic.
/// </para>
///
/// <para>
/// La reconexión sólo se ofrece cuando el puerto que falta tiene <b>sustituto</b> —el más parecido de los que
/// el nodo expone hoy, y libre si es una entrada— y cuando el otro extremo del cable sigue existiendo. Lo que
/// no se puede afirmar no se ofrece: una fila sin botón de reconectar sigue valiendo por el botón que lleva al
/// nodo, que es lo que sí se puede decir de ella.
/// </para>
/// </summary>
public sealed class DroppedConnectionFixViewModel : ObservableObject
{
    private readonly NodeViewModel _node;

    public DroppedConnectionFixViewModel(
        NodeViewModel node,
        string missingPortName,
        string description,
        string? proposedPortName,
        Action<NodeViewModel> goToNode,
        Action<DroppedConnectionFixViewModel> reconnect,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(goToNode);
        ArgumentNullException.ThrowIfNull(reconnect);
        ArgumentNullException.ThrowIfNull(localization);

        _node = node;
        MissingPortName = missingPortName;
        Description = description;
        ProposedPortName = proposedPortName;

        GoToNodeText = localization.GetString("FixGoToNodeBtn", "Ir al nodo");
        ReconnectText = proposedPortName == null
            ? string.Empty
            : localization.GetFormattedString("FixReconnectBtn", "Reconectar a «{0}»", proposedPortName);

        GoToNodeCommand = new RelayCommand(() => goToNode(_node));
        ReconnectCommand = new RelayCommand(() => reconnect(this), () => CanReconnect);
    }

    /// <summary>El nodo cuyo puerto falta: donde hay que mirar para arreglarlo.</summary>
    public NodeViewModel Node => _node;

    /// <summary>Nombre con el que el usuario reconoce al nodo, que es como se le nombra en el aviso.</summary>
    public string NodeTitle => _node.Title;

    /// <summary>El puerto que el cable nombraba y que el nodo ya no expone.</summary>
    public string MissingPortName { get; }

    /// <summary>El cable perdido contado entero —extremos y motivo—, con el mismo texto que la consola.</summary>
    public string Description { get; }

    /// <summary>El puerto vigente más parecido al que falta, o <c>null</c> si ninguno se le parece lo suficiente.</summary>
    public string? ProposedPortName { get; }

    /// <summary>Si la reconexión se puede ofrecer: hay sustituto y hay otro extremo al que conectar.</summary>
    public bool CanReconnect { get; init; }

    /// <summary>
    /// Los dos puertos con los que se vuelve a trazar el cable: el que existe y, en el extremo que perdió el
    /// suyo, el propuesto. <c>null</c> cuando no hay con qué —un extremo sin puerto y sin sustituto—, que es
    /// exactamente cuándo no se ofrece el botón.
    /// </summary>
    public (PortViewModel Output, PortViewModel Input)? LiveEnds { get; init; }

    public string GoToNodeText { get; }

    public string ReconnectText { get; }

    /// <summary>Selecciona y centra el nodo del puerto que falta.</summary>
    public IRelayCommand GoToNodeCommand { get; }

    /// <summary>Vuelve a trazar el cable contra el puerto propuesto.</summary>
    public IRelayCommand ReconnectCommand { get; }
}
