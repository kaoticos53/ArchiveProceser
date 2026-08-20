using CommunityToolkit.Mvvm.ComponentModel;

namespace FileFlow.App.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    private PortViewModel _source;

    [ObservableProperty]
    private PortViewModel _target;

    public ConnectionViewModel(PortViewModel source, PortViewModel target)
    {
        _source = source;
        _target = target;
    }
}
