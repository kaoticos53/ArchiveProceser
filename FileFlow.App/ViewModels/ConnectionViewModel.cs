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

