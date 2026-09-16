using CommunityToolkit.Mvvm.ComponentModel;

namespace FileFlow.App.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    private PortViewModel _source;

    [ObservableProperty]
    private PortViewModel _target;

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private bool _hasItems;

    /// <summary>
    /// Hay datos atravesando este cable ahora mismo. Mientras está activo, la plantilla del lienzo dibuja
    /// una capa de energía animada sobre el cable: el trazo base nunca se anima, para no perder legibilidad
    /// cuando el grafo es denso.
    /// </summary>
    [ObservableProperty]
    private bool _isExecuting;

    /// <summary>
    /// Número de pulsos de energía emitidos sobre este cable. Se usa como generación: un pulso antiguo que
    /// llegue tarde no debe apagar la energía de un pulso más reciente.
    /// </summary>
    [ObservableProperty]
    private int _lastDispatchedCount;

    public ConnectionViewModel(PortViewModel source, PortViewModel target)
    {
        _source = source;
        _target = target;
    }

    public void UpdateCount(int count)
    {
        ItemCount = count;
        HasItems = count > 0;
    }
}
